using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.DTOs;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly AppDbContext _dbContext;

        private readonly IDocumentProcessingService _docProcessing;
        private readonly AiModelFactory _aiModelFactory;
        private readonly ISystemSettingService _systemSettingService;
        private static readonly string[] AllowedExtensions = { ".pdf", ".docx", ".pptx" };
        private const long MaxFileSizeInBytes = 10 * 1024 * 1024; // 10MB

        public DocumentService(
            AppDbContext dbContext,
            IDocumentProcessingService docProcessing,
            AiModelFactory aiModelFactory,
            ISystemSettingService systemSettingService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _docProcessing = docProcessing ?? throw new ArgumentNullException(nameof(docProcessing));
            _aiModelFactory = aiModelFactory ?? throw new ArgumentNullException(nameof(aiModelFactory));
            _systemSettingService = systemSettingService ?? throw new ArgumentNullException(nameof(systemSettingService));
        }

        public async Task<Document> UploadDocumentAsync(UploadDocumentDTO dto, string uploadBasePath, Guid ownerId)
        {
            // Validation: Check if DTO is valid
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (dto.File == null || dto.File.Length == 0)
                throw new InvalidOperationException("File không được để trống.");

            // BR1: Validate file format
            ValidateFileFormat(dto.File.FileName);

            // BR2: Validate file size
            ValidateFileSize(dto.File.Length);

            // Verify Course exists
            var course = await _dbContext.Courses.FindAsync(dto.CourseId);
            if (course == null)
                throw new InvalidOperationException($"Course với ID {dto.CourseId} không tồn tại.");

            // Verify owner user exists (after DB reset, cookie may have stale user ID)
            var owner = await _dbContext.Users.FindAsync(ownerId);
            if (owner == null)
                throw new InvalidOperationException(
                    "Phiên đăng nhập không hợp lệ (User ID không tồn tại trong hệ thống). " +
                    "Vui lòng đăng xuất rồi đăng nhập lại.");

            // BR4: Check for duplicate filenames and generate unique name if needed
            string uniqueFileName = await GenerateUniqueFileNameAsync(dto.CourseId, dto.File.FileName);

            // Ensure upload directory exists
            if (!Directory.Exists(uploadBasePath))
                Directory.CreateDirectory(uploadBasePath);

            // Save file physically
            string filePath = Path.Combine(uploadBasePath, uniqueFileName);
            await using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await dto.File.CopyToAsync(fileStream);
            }

            // Create Document record in database
            var document = new Document
            {
                Id = Guid.NewGuid(),
                FileName = uniqueFileName,
                FilePath = filePath,
                FileSize = dto.File.Length,
                UploadDate = DateTime.UtcNow,
                CourseId = dto.CourseId,
                OwnerId = ownerId,
                Status = "Pending"
            };

            // Save to database
            _dbContext.Documents.Add(document);
            await _dbContext.SaveChangesAsync();

            return document;
        }

        /// <summary>
        /// BR1: Validates file format (.pdf, .docx, .pptx only)
        /// </summary>
        private void ValidateFileFormat(string fileName)
        {
            string fileExtension = Path.GetExtension(fileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(fileExtension))
            {
                throw new InvalidOperationException(
                    $"Định dạng file không hợp lệ. Chỉ hỗ trợ: {string.Join(", ", AllowedExtensions)}. " +
                    $"File được upload: {fileExtension}");
            }
        }

        /// <summary>
        /// BR2: Validates file size (max 10MB)
        /// </summary>
        private void ValidateFileSize(long fileSize)
        {
            if (fileSize > MaxFileSizeInBytes)
            {
                throw new InvalidOperationException(
                    $"Dung lượng file vượt quá giới hạn. Max: 10MB, File size: {Math.Round(fileSize / (1024.0 * 1024.0), 2)}MB");
            }
        }

        /// <summary>
        /// BR4: Generates unique filename by checking for duplicates and appending timestamp if needed
        /// </summary>
        private async Task<string> GenerateUniqueFileNameAsync(Guid courseId, string originalFileName)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
            string fileExtension = Path.GetExtension(originalFileName);

            // Check if filename already exists for this course
            bool fileExists = await _dbContext.Documents
                .AnyAsync(d => d.CourseId == courseId && d.FileName == originalFileName);

            if (!fileExists)
                return originalFileName;

            // Generate unique name with timestamp
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            string uniqueFileName = $"{fileNameWithoutExtension}_{timestamp}{fileExtension}";

            return uniqueFileName;
        }

        /// <summary>
        /// Index document: Gửi sang Python để parse sang Markdown → Ghi tệp .md → Lưu chunks và embeddings vào DB
        /// </summary>
        public async Task IndexDocumentAsync(Guid documentId, string? chunkingStrategy = null)
        {
            var document = await _dbContext.Documents.FindAsync(documentId);
            if (document == null)
                throw new InvalidOperationException($"Không tìm thấy document với ID {documentId}.");

            if (!System.IO.File.Exists(document.FilePath))
                throw new InvalidOperationException($"Không tìm thấy file tại {document.FilePath}");

            Console.WriteLine();
            Console.WriteLine("=================================================================================");
            Console.WriteLine($"[DocumentService] 🚀 BẮT ĐẦU LẬP CHỈ MỤC TÀI LIỆU: {document.FileName}");
            Console.WriteLine($"[DocumentService] 📌 ID: {document.Id}");
            Console.WriteLine($"[DocumentService] 📁 Path: {document.FilePath}");
            Console.WriteLine("=================================================================================");

            try
            {
                // Bước 1: Đọc cấu hình ActiveEmbeddingModel từ SystemSettings
                string activeModelName = await _systemSettingService.GetSettingValueAsync("ActiveEmbeddingModel");
                string activeChunkingStrategy = string.IsNullOrWhiteSpace(chunkingStrategy)
                    ? await _systemSettingService.GetSettingValueAsync("ActiveChunkingStrategy")
                    : chunkingStrategy;

                if (string.IsNullOrWhiteSpace(activeChunkingStrategy))
                    activeChunkingStrategy = "markdown_header";

                Console.WriteLine($"[DocumentService] ⚙️ Cấu hình: Model = {activeModelName} | Strategy = {activeChunkingStrategy}");

                // Bước 2: Gọi dịch vụ xử lý tài liệu thông qua Python Microservice
                // Thao tác này trả về trích xuất Markdown đầy đủ cùng các chunks và vector embeddings
                Console.WriteLine($"[DocumentService] 🕒 Đang gửi tệp sang Python Microservice để parse và tạo embedding...");
                var parseResult = await _docProcessing.ParseDocumentAsync(document.FilePath, activeModelName, activeChunkingStrategy);
                Console.WriteLine($"[DocumentService] ✅ Python Microservice xử lý thành công! Nhận về {parseResult.Chunks?.Count ?? 0} chunks.");

                // Bước 3: Ghi toàn bộ nội dung Markdown (.md) xuống đĩa bên cạnh tệp tài liệu gốc
                string mdFilePath = System.IO.Path.ChangeExtension(document.FilePath, ".md");
                Console.WriteLine($"[DocumentService] 📝 Đang ghi nội dung Markdown ra file local: {mdFilePath}");
                await System.IO.File.WriteAllTextAsync(mdFilePath, parseResult.Markdown, System.Text.Encoding.UTF8);

                // Bước 4: Xóa chunks cũ nếu document đã được index trước đó (re-index)
                var existingChunks = await _dbContext.DocumentChunks
                    .Where(c => c.DocumentId == documentId)
                    .ToListAsync();
                if (existingChunks.Any())
                {
                    Console.WriteLine($"[DocumentService] 🧹 Phát hiện {existingChunks.Count} chunks cũ. Tiến hành xóa...");
                    _dbContext.DocumentChunks.RemoveRange(existingChunks);
                }

                // Bước 5: Tạo các DocumentChunk mới từ dữ liệu Python trả về
                var documentChunks = new List<DocumentChunk>();
                if (parseResult.Chunks != null && parseResult.Chunks.Count > 0)
                {
                    Console.WriteLine($"[DocumentService] 💾 Đang chuẩn bị lưu các chunks và vector vào Database...");
                    foreach (var c in parseResult.Chunks)
                    {
                        var chunk = new DocumentChunk
                        {
                            Id = Guid.NewGuid(),
                            DocumentId = documentId,
                            Content = c.Content,
                            ChunkIndex = c.ChunkIndex,
                            PageNumber = (c.ChunkIndex / 4) + 1, // Ước tính trang dựa theo số lượng chunks
                            VectorData = JsonSerializer.Serialize(c.Vector)
                        };
                        documentChunks.Add(chunk);
                    }
                    _dbContext.DocumentChunks.AddRange(documentChunks);
                }

                // Bước 6: Cập nhật trạng thái tài liệu
                document.Status = "Completed";
                document.IsIndexed = true;

                Console.WriteLine($"[DocumentService] 💾 Đang cập nhật trạng thái Document sang 'Completed' và lưu DbContext...");
                await _dbContext.SaveChangesAsync();

                Console.WriteLine("=================================================================================");
                Console.WriteLine($"[DocumentService] 🎉 HOÀN TẤT LẬP CHỈ MỤC TÀI LIỆU THÀNH CÔNG: {document.FileName}!");
                Console.WriteLine("=================================================================================");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("=================================================================================");
                Console.WriteLine($"[DocumentService] ❌ THẤT BẠI: Lỗi lập chỉ mục tài liệu {document.FileName}: {ex.Message}");
                Console.WriteLine("=================================================================================");
                Console.WriteLine();

                document.Status = "Failed";
                await _dbContext.SaveChangesAsync();
                throw new InvalidOperationException($"Lỗi lập chỉ mục tài liệu: {ex.Message}", ex);
            }
        }

        public async Task<int> ReindexIndexedDocumentsAsync(string embeddingModel, string chunkingStrategy)
        {
            if (string.IsNullOrWhiteSpace(embeddingModel))
                embeddingModel = await _systemSettingService.GetSettingValueAsync("ActiveEmbeddingModel");

            if (string.IsNullOrWhiteSpace(chunkingStrategy))
                chunkingStrategy = "markdown_header";

            await _systemSettingService.SetSettingAsync("ActiveEmbeddingModel", embeddingModel);
            await _systemSettingService.SetSettingAsync("ActiveChunkingStrategy", chunkingStrategy);

            var documentIds = await _dbContext.Documents
                .Where(d => d.IsIndexed || d.Status == "Completed" || d.Status == "Indexed")
                .OrderBy(d => d.UploadDate)
                .Select(d => d.Id)
                .ToListAsync();

            foreach (var documentId in documentIds)
            {
                await IndexDocumentAsync(documentId, chunkingStrategy);
            }

            return documentIds.Count;
        }

        /// <summary>
        /// Get documents with role-based access control
        /// </summary>
        public async Task<List<Document>> GetDocumentsAsync(Guid userId, string role)
        {
            IQueryable<Document> query = _dbContext.Documents.Include(d => d.Course).Include(d => d.Owner);

            // Admin: see all documents
            if (role == "Admin")
            {
                return await query.OrderByDescending(d => d.UploadDate).ToListAsync();
            }

            // Student: see only their own documents
            return await query
                .Where(d => d.OwnerId == userId)
                .OrderByDescending(d => d.UploadDate)
                .ToListAsync();
        }

        public async Task<Document?> GetDocumentByIdAsync(Guid id)
        {
            return await _dbContext.Documents.FindAsync(id);
        }

        public async Task<(bool Success, string ErrorMessage)> DeleteDocumentAsync(Guid id, Guid currentUserId, string role)
        {
            var document = await _dbContext.Documents.FindAsync(id);
            if (document == null)
            {
                return (false, "Không tìm thấy tài liệu.");
            }

            if (role != "Admin" && document.OwnerId != currentUserId)
            {
                return (false, "Bạn không có quyền xóa tài liệu này.");
            }

            try
            {
                if (File.Exists(document.FilePath))
                {
                    File.Delete(document.FilePath);
                }

                _dbContext.Documents.Remove(document);
                await _dbContext.SaveChangesAsync();

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi xóa tài liệu: {ex.Message}");
            }
        }

        public async Task<List<DocumentChunk>> GetDocumentChunksAsync(Guid documentId)
        {
            return await _dbContext.DocumentChunks
                .Where(c => c.DocumentId == documentId)
                .OrderBy(c => c.PageNumber)
                .ThenBy(c => c.ChunkIndex)
                .ToListAsync();
        }

        /// <summary>
        /// Lấy danh sách chunks có phân trang (paging).
        /// </summary>
        public async Task<(List<DocumentChunk> Chunks, int TotalCount)> GetDocumentChunksPagedAsync(
            Guid documentId, int pageNumber = 1, int pageSize = 10)
        {
            var query = _dbContext.DocumentChunks
                .Where(c => c.DocumentId == documentId)
                .OrderBy(c => c.PageNumber)
                .ThenBy(c => c.ChunkIndex);

            int totalCount = await query.CountAsync();

            var chunks = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (chunks, totalCount);
        }
    }
}
