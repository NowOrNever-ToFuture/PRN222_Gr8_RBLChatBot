using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.DTOs;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    // Hub rỗng — logic bắn event nằm trong DocumentService qua IHubContext
    public class DocumentUploadHub : Hub { }

    public class DocumentService : IDocumentService
    {
        private readonly AppDbContext _dbContext;

        private readonly IDocumentProcessingService _docProcessing;
        private readonly AiModelFactory _aiModelFactory;
        private readonly ISystemSettingService _systemSettingService;
        private readonly IHubContext<DocumentUploadHub> _uploadHubContext;
        private static readonly string[] AllowedExtensions = { ".pdf", ".docx", ".pptx" };
        private const long MaxFileSizeInBytes = 10 * 1024 * 1024; // 10MB

        public DocumentService(
            AppDbContext dbContext,
            IDocumentProcessingService docProcessing,
            AiModelFactory aiModelFactory,
            ISystemSettingService systemSettingService,
            IHubContext<DocumentUploadHub> uploadHubContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _docProcessing = docProcessing ?? throw new ArgumentNullException(nameof(docProcessing));
            _aiModelFactory = aiModelFactory ?? throw new ArgumentNullException(nameof(aiModelFactory));
            _systemSettingService = systemSettingService ?? throw new ArgumentNullException(nameof(systemSettingService));
            _uploadHubContext = uploadHubContext ?? throw new ArgumentNullException(nameof(uploadHubContext));
        }

        /// <summary>
        /// Gửi tiến độ (%) và thông điệp xử lý hiện tại tới client đang theo dõi qua SignalR.
        /// </summary>
        private async Task ReportUploadProgressAsync(string? connectionId, int percent, string message)
        {
            Console.WriteLine($"[DocumentService:Upload] [{percent}%] {message}");

            if (string.IsNullOrEmpty(connectionId))
                return;

            await _uploadHubContext.Clients.Client(connectionId)
                .SendAsync("ReceiveUploadProgress", percent, message);
        }

        public async Task<Document> UploadDocumentAsync(UploadDocumentDTO dto, string uploadBasePath, Guid ownerId, string? connectionId = null)
        {
            // Validation: Check if DTO is valid
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (dto.File == null || dto.File.Length == 0)
                throw new InvalidOperationException("File không được để trống.");

            await ReportUploadProgressAsync(connectionId, 72, $"Đã nhận tệp '{dto.File.FileName}' trên server. Bắt đầu kiểm tra...");

            // BR1: Validate file format
            await ReportUploadProgressAsync(connectionId, 76, "Đang kiểm tra định dạng tệp (.pdf, .docx, .pptx)...");
            ValidateFileFormat(dto.File.FileName);

            // BR2: Validate file size
            await ReportUploadProgressAsync(connectionId, 80, "Đang kiểm tra dung lượng tệp (tối đa 10MB)...");
            ValidateFileSize(dto.File.Length);

            // Verify Course exists
            await ReportUploadProgressAsync(connectionId, 84, "Đang kiểm tra môn học áp dụng...");
            var course = await _dbContext.Courses.FindAsync(dto.CourseId);
            if (course == null)
                throw new InvalidOperationException($"Course với ID {dto.CourseId} không tồn tại.");

            // Verify owner user exists (after DB reset, cookie may have stale user ID)
            await ReportUploadProgressAsync(connectionId, 87, "Đang xác thực người dùng tải lên...");
            var owner = await _dbContext.Users.FindAsync(ownerId);
            if (owner == null)
                throw new InvalidOperationException(
                    "Phiên đăng nhập không hợp lệ (User ID không tồn tại trong hệ thống). " +
                    "Vui lòng đăng xuất rồi đăng nhập lại.");

            // Check for duplicate content (SHA-256 hash check)
            await ReportUploadProgressAsync(connectionId, 88, "Đang kiểm tra trùng lặp nội dung tệp...");
            string fileHash;
            using (var stream = dto.File.OpenReadStream())
            {
                fileHash = CalculateSHA256(stream);
            }

            var duplicateDoc = await _dbContext.Documents
                .FirstOrDefaultAsync(d => d.FileHash == fileHash);
            if (duplicateDoc != null)
            {
                throw new InvalidOperationException($"Tài liệu này đã tồn tại trên hệ thống (Tên tệp: '{duplicateDoc.FileName}').");
            }

            // BR4: Check for duplicate filenames and generate unique name if needed
            await ReportUploadProgressAsync(connectionId, 90, "Đang kiểm tra tên tệp trùng lặp...");
            string uniqueFileName = await GenerateUniqueFileNameAsync(dto.CourseId, dto.File.FileName);

            // Ensure upload directory exists
            if (!Directory.Exists(uploadBasePath))
                Directory.CreateDirectory(uploadBasePath);

            // Save file physically
            await ReportUploadProgressAsync(connectionId, 94, $"Đang lưu tệp '{uniqueFileName}' vào hệ thống...");
            string filePath = Path.Combine(uploadBasePath, uniqueFileName);
            await using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await dto.File.CopyToAsync(fileStream);
            }

            // Create Document record in database
            await ReportUploadProgressAsync(connectionId, 98, "Đang tạo bản ghi tài liệu trong cơ sở dữ liệu...");
            var document = new Document
            {
                Id = Guid.NewGuid(),
                FileName = uniqueFileName,
                FilePath = filePath,
                FileSize = dto.File.Length,
                UploadDate = DateTime.UtcNow,
                CourseId = dto.CourseId,
                OwnerId = ownerId,
                Status = "Pending",
                FileHash = fileHash
            };

            // Save to database
            _dbContext.Documents.Add(document);
            await _dbContext.SaveChangesAsync();

            await ReportUploadProgressAsync(connectionId, 100, $"Hoàn tất! Tệp '{document.FileName}' đã được tải lên thành công.");

            await _uploadHubContext.Clients.All.SendAsync("DocumentAdded", new
            {
                id = document.Id,
                fileName = document.FileName,
                fileSize = document.FileSize,
                uploadDate = document.UploadDate,
                status = document.Status,
                courseCode = course.Code,
                courseName = course.Name,
                ownerName = owner.FullName,
                ownerId = document.OwnerId
            });

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

            document.Status = "Processing";
            await _dbContext.SaveChangesAsync();
            await _uploadHubContext.Clients.All.SendAsync("DocumentUpdated", new
            {
                id = document.Id,
                status = document.Status,
                isIndexed = document.IsIndexed
            });

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

                await _uploadHubContext.Clients.All.SendAsync("DocumentUpdated", new
                {
                    id = document.Id,
                    status = document.Status,
                    isIndexed = document.IsIndexed
                });

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

                await _uploadHubContext.Clients.All.SendAsync("DocumentUpdated", new
                {
                    id = document.Id,
                    status = document.Status,
                    isIndexed = document.IsIndexed
                });

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

            // Student: see all completed documents
            if (role == "Student")
            {
                return await query
                    .Where(d => d.Status == "Completed")
                    .OrderByDescending(d => d.UploadDate)
                    .ToListAsync();
            }

            // Lecturer: see all documents of their assigned course, including documents
            // retained after a previous lecturer account was deleted.
            Guid? courseId = await _dbContext.Users
                .Where(u => u.Id == userId)
                .Select(u => u.CourseId)
                .FirstOrDefaultAsync();

            if (!courseId.HasValue)
            {
                return new List<Document>();
            }

            return await query
                .Where(d => d.CourseId == courseId.Value)
                .OrderByDescending(d => d.UploadDate)
                .ToListAsync();
        }

        public async Task<Document?> GetDocumentByIdAsync(Guid id)
        {
            return await _dbContext.Documents.FindAsync(id);
        }

        public async Task<Document?> GetDocumentWithDetailsAsync(Guid id)
        {
            return await _dbContext.Documents
                .Include(d => d.Course)
                .Include(d => d.Owner)
                .FirstOrDefaultAsync(d => d.Id == id);
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

                string mdFilePath = Path.ChangeExtension(document.FilePath, ".md");
                if (File.Exists(mdFilePath))
                {
                    File.Delete(mdFilePath);
                }

                _dbContext.Documents.Remove(document);
                await _dbContext.SaveChangesAsync();

                await _uploadHubContext.Clients.All.SendAsync("DocumentDeleted", id);

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

        private string CalculateSHA256(Stream stream)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
