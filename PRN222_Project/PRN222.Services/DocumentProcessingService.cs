using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Http;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.DTOs;
using PRN222.Services.Interfaces;
using UglyToad.PdfPig;

namespace PRN222.Services
{
    public class DocumentProcessingService : IDocumentProcessingService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppDbContext _dbContext;

        public DocumentProcessingService(IHttpClientFactory httpClientFactory, AppDbContext dbContext)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        // ===================================================================
        // Plan 2 + 3: Gửi file sang Python, nhận kết quả, lưu vào DB
        // ===================================================================

        /// <summary>
        /// Gửi file tài liệu sang Python Microservice qua Multipart form-data,
        /// nhận kết quả parse/chunk/embedding, rồi lưu Document + DocumentChunks vào DB.
        /// </summary>
        public async Task<bool> UploadAndProcessDocumentAsync(IFormFile file, Guid courseId, string modelName = "bge-m3", string chunkStrategy = "markdown_header")
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File không được để trống.", nameof(file));

            // --- Bước 1: Gọi API Python ---
            PythonParseResponseDto parseResult;

            try
            {
                var client = _httpClientFactory.CreateClient("PythonApi");

                using var content = new MultipartFormDataContent();

                // Đính kèm file dưới dạng StreamContent
                var fileStream = file.OpenReadStream();
                var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
                content.Add(streamContent, "file", file.FileName);

                // Thêm 2 trường text
                content.Add(new StringContent(modelName, Encoding.UTF8), "model_name");
                content.Add(new StringContent(chunkStrategy, Encoding.UTF8), "chunk_strategy");

                // POST request
                var response = await client.PostAsync("/api/parse-document", content);
                await EnsurePythonSuccessAsync(response);

                var jsonString = await response.Content.ReadAsStringAsync();
                parseResult = JsonSerializer.Deserialize<PythonParseResponseDto>(jsonString)
                    ?? throw new InvalidOperationException("Python API trả về JSON null hoặc không hợp lệ.");
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    $"Lỗi kết nối tới Python Microservice: {ex.Message}. " +
                    "Hãy kiểm tra Python server đang chạy tại http://localhost:8000.", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new InvalidOperationException(
                    "Python Microservice xử lý quá lâu hoặc không phản hồi. " +
                    "Hãy kiểm tra server Python và thử lại với file nhỏ hơn nếu cần.", ex);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Lỗi deserialize JSON response từ Python: {ex.Message}", ex);
            }

            // --- Bước 2: Lưu file thô vào wwwroot/uploads ---
            try
            {
                // Tạo đường dẫn lưu file: wwwroot/uploads/{courseId}/{timestamp}_{filename}
                var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                var courseFolder = Path.Combine(uploadsRoot, courseId.ToString());
                if (!Directory.Exists(courseFolder))
                    Directory.CreateDirectory(courseFolder);

                var safeFileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Path.GetFileName(file.FileName)}";
                var physicalPath = Path.Combine(courseFolder, safeFileName);

                using (var fs = new FileStream(physicalPath, FileMode.Create))
                {
                    await file.CopyToAsync(fs);
                }

                var relativePath = $"/uploads/{courseId}/{safeFileName}";

                // --- Bước 3: Tạo entity Document ---
                var document = new PRN222.Models.Document
                {
                    Id = Guid.NewGuid(),
                    CourseId = courseId,
                    FileName = parseResult.Filename,
                    FilePath = relativePath,
                    FileSize = file.Length,
                    UploadDate = DateTime.UtcNow,
                    Status = "Indexed",
                    IsIndexed = true
                };

                _dbContext.Documents.Add(document);

                // --- Bước 4: Tạo các entity DocumentChunk ---
                if (parseResult.Chunks != null && parseResult.Chunks.Count > 0)
                {
                    var chunks = parseResult.Chunks.Select(c => new DocumentChunk
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = document.Id,
                        Content = c.Content,
                        ChunkIndex = c.ChunkIndex,
                        PageNumber = 0, // Python không trả page number, set mặc định
                        VectorData = JsonSerializer.Serialize(c.Vector)
                    }).ToList();

                    _dbContext.DocumentChunks.AddRange(chunks);
                }

                // --- Bước 5: Lưu tất cả vào DB ---
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                // Log lỗi DB/File và trả về false
                Console.Error.WriteLine($"[DocumentProcessingService] Lỗi lưu DB: {ex.Message}");
                return false;
            }
        }

        // ===================================================================
        // Các hàm ETL cũ (Extract + Chunking) — giữ nguyên
        // ===================================================================

        /// <summary>
        /// Đọc text thô từ file PDF hoặc DOCX dựa trên extension.
        /// </summary>
        public async Task<string> ExtractTextAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Đường dẫn file không được để trống.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Không tìm thấy file tại: {filePath}");

            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            return extension switch
            {
                ".pdf" => await Task.Run(() => ExtractFromPdf(filePath)),
                ".docx" => await Task.Run(() => ExtractFromDocx(filePath)),
                _ => throw new InvalidOperationException(
                    $"Định dạng file '{extension}' không được hỗ trợ để trích xuất text. Chỉ hỗ trợ: .pdf, .docx")
            };
        }

        /// <summary>
        /// Trích xuất text từ file PDF bằng PdfPig.
        /// Hỗ trợ Unicode/Tiếng Việt.
        /// </summary>
        private string ExtractFromPdf(string filePath)
        {
            try
            {
                var sb = new StringBuilder();

                using (var document = PdfDocument.Open(filePath))
                {
                    foreach (var page in document.GetPages())
                    {
                        string pageText = page.Text;
                        if (!string.IsNullOrWhiteSpace(pageText))
                        {
                            sb.AppendLine(pageText);
                        }
                    }
                }

                string result = sb.ToString().Trim();
                if (string.IsNullOrWhiteSpace(result))
                    throw new InvalidOperationException("File PDF không chứa text nào có thể đọc được (có thể là file scan/ảnh).");

                return result;
            }
            catch (InvalidOperationException)
            {
                throw; // Re-throw our custom exceptions
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Lỗi đọc file PDF: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Trích xuất text từ file DOCX bằng DocumentFormat.OpenXml.
        /// </summary>
        private string ExtractFromDocx(string filePath)
        {
            try
            {
                var sb = new StringBuilder();

                using (var wordDoc = WordprocessingDocument.Open(filePath, false))
                {
                    var body = wordDoc.MainDocumentPart?.Document?.Body;
                    if (body == null)
                        throw new InvalidOperationException("File DOCX không có nội dung.");

                    foreach (var paragraph in body.Elements<Paragraph>())
                    {
                        string paragraphText = paragraph.InnerText;
                        if (!string.IsNullOrWhiteSpace(paragraphText))
                        {
                            sb.AppendLine(paragraphText);
                        }
                    }
                }

                string result = sb.ToString().Trim();
                if (string.IsNullOrWhiteSpace(result))
                    throw new InvalidOperationException("File DOCX không chứa text nào có thể đọc được.");

                return result;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Lỗi đọc file DOCX: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Cắt text theo Fixed-size với Overlap.
        /// Ví dụ: chunkSize=500, overlap=50
        ///   chunk1 = [0..500], chunk2 = [450..950], chunk3 = [900..1400]...
        /// </summary>
        public List<string> SplitByFixedSize(string text, int chunkSize = 500, int overlap = 50)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            if (chunkSize <= 0)
                throw new ArgumentException("chunkSize phải lớn hơn 0.", nameof(chunkSize));

            if (overlap < 0 || overlap >= chunkSize)
                throw new ArgumentException("overlap phải >= 0 và < chunkSize.", nameof(overlap));

            var chunks = new List<string>();
            int step = chunkSize - overlap; // Bước nhảy = chunkSize - overlap
            int textLength = text.Length;

            for (int i = 0; i < textLength; i += step)
            {
                int length = Math.Min(chunkSize, textLength - i);
                string chunk = text.Substring(i, length).Trim();

                if (!string.IsNullOrWhiteSpace(chunk))
                {
                    chunks.Add(chunk);
                }

                // Nếu đã lấy hết text thì dừng
                if (i + length >= textLength)
                    break;
            }

            return chunks;
        }

        /// <summary>
        /// Cắt text theo Sentence (dấu chấm, dấu chấm hỏi, dấu chấm than, xuống dòng).
        /// Gom các câu lại cho đến khi đạt maxChunkSize, rồi bắt đầu chunk mới.
        /// </summary>
        public List<string> SplitBySentence(string text, int maxChunkSize = 500)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            if (maxChunkSize <= 0)
                throw new ArgumentException("maxChunkSize phải lớn hơn 0.", nameof(maxChunkSize));

            var chunks = new List<string>();

            // Tách theo dấu câu: . ! ? và xuống dòng
            var sentences = Regex.Split(text, @"(?<=[.!?])\s+|\r?\n")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();

            var currentChunk = new StringBuilder();

            foreach (var sentence in sentences)
            {
                // Nếu 1 câu đã dài hơn maxChunkSize, cắt cứng câu đó
                if (sentence.Length > maxChunkSize)
                {
                    // Lưu chunk hiện tại trước
                    if (currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString().Trim());
                        currentChunk.Clear();
                    }

                    // Cắt cứng câu dài
                    for (int i = 0; i < sentence.Length; i += maxChunkSize)
                    {
                        int len = Math.Min(maxChunkSize, sentence.Length - i);
                        chunks.Add(sentence.Substring(i, len).Trim());
                    }
                    continue;
                }

                // Nếu thêm câu này vào sẽ vượt maxChunkSize → lưu chunk hiện tại, bắt đầu chunk mới
                if (currentChunk.Length + sentence.Length + 1 > maxChunkSize)
                {
                    if (currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString().Trim());
                        currentChunk.Clear();
                    }
                }

                if (currentChunk.Length > 0)
                    currentChunk.Append(' ');

                currentChunk.Append(sentence);
            }

            // Lưu chunk cuối cùng
            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
            }

            return chunks;
        }

        /// <summary>
        /// Gửi file vật lý sang Python Microservice để parse sang Markdown và chunking có vector embeddings.
        /// </summary>
        public async Task<PythonParseResponseDto> ParseDocumentAsync(string filePath, string modelName = "bge-m3", string chunkStrategy = "markdown_header", int chunkSize = 500)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Đường dẫn file không được để trống.", nameof(filePath));

            if (!System.IO.File.Exists(filePath))
                throw new System.IO.FileNotFoundException($"Không tìm thấy file tại: {filePath}");

            try
            {
                var client = _httpClientFactory.CreateClient("PythonApi");

                using var content = new MultipartFormDataContent();

                // Đọc file thành Stream
                var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                var streamContent = new StreamContent(fileStream);

                // Xác định Content-Type dựa trên đuôi file
                string extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
                string contentType = extension switch
                {
                    ".pdf" => "application/pdf",
                    ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                    _ => "application/octet-stream"
                };

                streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                content.Add(streamContent, "file", System.IO.Path.GetFileName(filePath));

                // Thêm các tham số cấu hình
                content.Add(new StringContent(modelName, Encoding.UTF8), "model_name");
                content.Add(new StringContent(chunkStrategy, Encoding.UTF8), "chunk_strategy");
                content.Add(new StringContent(chunkSize.ToString(), Encoding.UTF8), "chunk_size");

                // POST request sang Python API
                var response = await client.PostAsync("/api/parse-document", content);
                await EnsurePythonSuccessAsync(response);

                var jsonString = await response.Content.ReadAsStringAsync();
                var parseResult = JsonSerializer.Deserialize<PythonParseResponseDto>(jsonString)
                    ?? throw new InvalidOperationException("Python API trả về JSON null hoặc không hợp lệ.");

                return parseResult;
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    $"Lỗi kết nối tới Python Microservice: {ex.Message}. " +
                    "Hãy kiểm tra Python server đang chạy tại http://localhost:8000.", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new InvalidOperationException(
                    "Python Microservice xử lý quá lâu hoặc không phản hồi. " +
                    "Hãy kiểm tra server Python và thử lại với file nhỏ hơn nếu cần.", ex);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Lỗi deserialize JSON response từ Python: {ex.Message}", ex);
            }
        }

        private static async Task EnsurePythonSuccessAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;

            string detail = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(detail))
                detail = response.ReasonPhrase ?? "Không có chi tiết lỗi.";

            throw new InvalidOperationException(
                $"Python Microservice trả về lỗi {(int)response.StatusCode}: {detail}");
        }
    }
}
