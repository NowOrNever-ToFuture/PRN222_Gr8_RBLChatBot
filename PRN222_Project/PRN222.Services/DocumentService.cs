using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.DTOs;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class DocumentUploadHub : Hub { }

    public class DocumentService : IDocumentService
    {
        private readonly AppDbContext _dbContext;
        private readonly IDocumentProcessingService _docProcessing;
        private readonly AiModelFactory _aiModelFactory;
        private readonly ISystemSettingService _systemSettingService;
        private readonly IHubContext<DocumentUploadHub> _uploadHubContext;
        private static readonly string[] AllowedExtensions = { ".pdf", ".docx", ".pptx" };
        private const long MaxFileSizeInBytes = 10 * 1024 * 1024;

        public DocumentService(AppDbContext dbContext, IDocumentProcessingService docProcessing, AiModelFactory aiModelFactory, ISystemSettingService systemSettingService, IHubContext<DocumentUploadHub> uploadHubContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _docProcessing = docProcessing ?? throw new ArgumentNullException(nameof(docProcessing));
            _aiModelFactory = aiModelFactory ?? throw new ArgumentNullException(nameof(aiModelFactory));
            _systemSettingService = systemSettingService ?? throw new ArgumentNullException(nameof(systemSettingService));
            _uploadHubContext = uploadHubContext ?? throw new ArgumentNullException(nameof(uploadHubContext));
        }

        private async Task ReportUploadProgressAsync(string? connectionId, int percent, string message)
        {
            Console.WriteLine($"[DocumentService:Upload] [{percent}%] {message}");
            if (string.IsNullOrEmpty(connectionId)) return;
            await _uploadHubContext.Clients.Client(connectionId).SendAsync("ReceiveUploadProgress", percent, message);
        }

        public async Task<Document> UploadDocumentAsync(UploadDocumentDTO dto, string uploadBasePath, Guid ownerId, string? connectionId = null)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (dto.File == null || dto.File.Length == 0) throw new InvalidOperationException("File không được để trống.");

            await ReportUploadProgressAsync(connectionId, 72, $"Đã nhận tệp '{dto.File.FileName}' trên server. Bắt đầu kiểm tra...");
            await ReportUploadProgressAsync(connectionId, 76, "Đang kiểm tra định dạng tệp (.pdf, .docx, .pptx)...");
            ValidateFileFormat(dto.File.FileName);
            await ReportUploadProgressAsync(connectionId, 80, "Đang kiểm tra dung lượng tệp (tối đa 10MB)...");
            ValidateFileSize(dto.File.Length);

            await ReportUploadProgressAsync(connectionId, 84, "Đang kiểm tra môn học áp dụng...");
            var course = await _dbContext.Courses.FindAsync(dto.CourseId);
            if (course == null) throw new InvalidOperationException($"Không tìm thấy môn học với ID {dto.CourseId}.");

            await ReportUploadProgressAsync(connectionId, 87, "Đang xác thực người dùng tải lên...");
            var owner = await _dbContext.Users.FindAsync(ownerId);
            if (owner == null) throw new InvalidOperationException("Phiên đăng nhập không hợp lệ. Vui lòng đăng xuất rồi đăng nhập lại.");

            if (owner.Role == "Lecturer")
            {
                if (course.ManagedById != ownerId)
                {
                    throw new InvalidOperationException("Bạn không phải trưởng bộ môn của môn học này nên không được phép tải lên tài liệu.");
                }
            }
            else if (owner.Role == "Admin")
            {
                throw new InvalidOperationException("Quản trị viên không được phép tải lên tài liệu học tập.");
            }
            else
            {
                throw new InvalidOperationException("Bạn không có quyền tải lên tài liệu học tập.");
            }

            await ReportUploadProgressAsync(connectionId, 88, "Đang kiểm tra trùng lặp nội dung tệp...");
            string fileHash;
            using (var stream = dto.File.OpenReadStream())
            {
                fileHash = CalculateSHA256(stream);
            }

            var duplicateDoc = await _dbContext.Documents.FirstOrDefaultAsync(d => d.FileHash == fileHash);
            if (duplicateDoc != null)
            {
                throw new InvalidOperationException($"Tài liệu này đã tồn tại trên hệ thống (tên tệp: '{duplicateDoc.FileName}').");
            }

            await ReportUploadProgressAsync(connectionId, 90, "Đang kiểm tra tên tệp trùng lặp...");
            string uniqueFileName = await GenerateUniqueFileNameAsync(dto.CourseId, dto.File.FileName);

            if (!Directory.Exists(uploadBasePath)) Directory.CreateDirectory(uploadBasePath);

            await ReportUploadProgressAsync(connectionId, 94, $"Đang lưu tệp '{uniqueFileName}' vào hệ thống...");
            string filePath = Path.Combine(uploadBasePath, uniqueFileName);
            await using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await dto.File.CopyToAsync(fileStream);
            }

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
                courseId = document.CourseId,
                ownerName = owner.FullName,
                ownerId = document.OwnerId
            });

            return document;
        }

        private void ValidateFileFormat(string fileName)
        {
            string fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(fileExtension))
            {
                throw new InvalidOperationException($"Định dạng file không hợp lệ. Chỉ hỗ trợ: {string.Join(", ", AllowedExtensions)}. File được upload: {fileExtension}");
            }
        }

        private void ValidateFileSize(long fileSize)
        {
            if (fileSize > MaxFileSizeInBytes)
            {
                throw new InvalidOperationException($"Dung lượng file vượt quá giới hạn. Tối đa: 10MB, kích thước hiện tại: {Math.Round(fileSize / (1024.0 * 1024.0), 2)}MB");
            }
        }

        private async Task<string> GenerateUniqueFileNameAsync(Guid courseId, string originalFileName)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
            string fileExtension = Path.GetExtension(originalFileName);
            bool fileExists = await _dbContext.Documents.AnyAsync(d => d.CourseId == courseId && d.FileName == originalFileName);
            if (!fileExists) return originalFileName;
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            return $"{fileNameWithoutExtension}_{timestamp}{fileExtension}";
        }

        public async Task IndexDocumentAsync(Guid documentId, string? chunkingStrategy = null)
        {
            var document = await _dbContext.Documents.FindAsync(documentId);
            if (document == null) throw new InvalidOperationException($"Không tìm thấy tài liệu với ID {documentId}.");
            if (!System.IO.File.Exists(document.FilePath)) throw new InvalidOperationException($"Không tìm thấy file tại {document.FilePath}");

            document.Status = "Processing";
            await _dbContext.SaveChangesAsync();
            await _uploadHubContext.Clients.All.SendAsync("DocumentUpdated", new { id = document.Id, status = document.Status, isIndexed = document.IsIndexed, courseId = document.CourseId });

            try
            {
                string activeModelName = await _systemSettingService.GetSettingValueAsync("ActiveEmbeddingModel");
                string activeChunkingStrategy = string.IsNullOrWhiteSpace(chunkingStrategy) ? await _systemSettingService.GetSettingValueAsync("ActiveChunkingStrategy") : chunkingStrategy;
                if (string.IsNullOrWhiteSpace(activeChunkingStrategy)) activeChunkingStrategy = "markdown_header";
                string chunkSizeStr = await _systemSettingService.GetSettingValueAsync("ChunkSize");
                int chunkSize = int.TryParse(chunkSizeStr, out int size) ? size : 500;

                var parseResult = await _docProcessing.ParseDocumentAsync(document.FilePath, activeModelName, activeChunkingStrategy, chunkSize);
                string mdFilePath = System.IO.Path.ChangeExtension(document.FilePath, ".md");
                await System.IO.File.WriteAllTextAsync(mdFilePath, parseResult.Markdown, System.Text.Encoding.UTF8);

                var existingChunks = await _dbContext.DocumentChunks.Where(c => c.DocumentId == documentId).ToListAsync();
                if (existingChunks.Any()) _dbContext.DocumentChunks.RemoveRange(existingChunks);

                if (parseResult.Chunks != null && parseResult.Chunks.Count > 0)
                {
                    var documentChunks = parseResult.Chunks.Select(c => new DocumentChunk
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = documentId,
                        Content = c.Content,
                        ChunkIndex = c.ChunkIndex,
                        PageNumber = (c.ChunkIndex / 4) + 1,
                        VectorData = JsonSerializer.Serialize(c.Vector)
                    }).ToList();
                    _dbContext.DocumentChunks.AddRange(documentChunks);
                }

                document.Status = "Completed";
                document.IsIndexed = true;
                await _dbContext.SaveChangesAsync();
                await _uploadHubContext.Clients.All.SendAsync("DocumentUpdated", new { id = document.Id, status = document.Status, isIndexed = document.IsIndexed, courseId = document.CourseId });
            }
            catch (Exception ex)
            {
                document.Status = "Failed";
                await _dbContext.SaveChangesAsync();
                await _uploadHubContext.Clients.All.SendAsync("DocumentUpdated", new { id = document.Id, status = document.Status, isIndexed = document.IsIndexed, courseId = document.CourseId });
                throw new InvalidOperationException($"Lỗi lập chỉ mục tài liệu: {ex.Message}", ex);
            }
        }

        public async Task<int> ReindexIndexedDocumentsAsync(string embeddingModel, string chunkingStrategy)
        {
            if (string.IsNullOrWhiteSpace(embeddingModel)) embeddingModel = await _systemSettingService.GetSettingValueAsync("ActiveEmbeddingModel");
            if (string.IsNullOrWhiteSpace(chunkingStrategy)) chunkingStrategy = "markdown_header";

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

        public async Task<List<Document>> GetDocumentsAsync(Guid userId, string role, Guid? courseId = null)
        {
            IQueryable<Document> query = _dbContext.Documents.Include(d => d.Course).Include(d => d.Owner);
            if (!courseId.HasValue) return new List<Document>();

            query = query.Where(d => d.CourseId == courseId.Value);
            if (role == "Admin") return await query.OrderByDescending(d => d.UploadDate).ToListAsync();
            if (role == "Student") return await query.Where(d => d.Status == "Completed").OrderByDescending(d => d.UploadDate).ToListAsync();

            var isAssigned = await _dbContext.Courses.AnyAsync(c => c.Id == courseId.Value && (c.ManagedById == userId || c.CourseLecturers.Any(cl => cl.LecturerId == userId)));
            if (!isAssigned) return await query.Where(d => d.Status == "Completed").OrderByDescending(d => d.UploadDate).ToListAsync();

            return await query.OrderByDescending(d => d.UploadDate).ToListAsync();
        }

        public async Task<Document?> GetDocumentByIdAsync(Guid id) => await _dbContext.Documents.FindAsync(id);

        public async Task<Document?> GetDocumentWithDetailsAsync(Guid id)
        {
            return await _dbContext.Documents.Include(d => d.Course).Include(d => d.Owner).FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<(bool Success, string ErrorMessage)> DeleteDocumentAsync(Guid id, Guid currentUserId, string role)
        {
            var document = await _dbContext.Documents.Include(d => d.Course).FirstOrDefaultAsync(d => d.Id == id);
            if (document == null) return (false, "Không tìm thấy tài liệu.");

            if (role == "Lecturer")
            {
                if (document.Course?.ManagedById != currentUserId)
                {
                    return (false, "Bạn không phải trưởng bộ môn của môn học này nên không được phép xóa tài liệu.");
                }
            }
            else
            {
                return (false, "Bạn không có quyền xóa tài liệu học tập.");
            }

            try
            {
                if (File.Exists(document.FilePath)) File.Delete(document.FilePath);
                string mdFilePath = Path.ChangeExtension(document.FilePath, ".md");
                if (File.Exists(mdFilePath)) File.Delete(mdFilePath);

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
            return await _dbContext.DocumentChunks.Where(c => c.DocumentId == documentId).OrderBy(c => c.PageNumber).ThenBy(c => c.ChunkIndex).ToListAsync();
        }

        public async Task<(List<DocumentChunk> Chunks, int TotalCount)> GetDocumentChunksPagedAsync(Guid documentId, int pageNumber = 1, int pageSize = 10)
        {
            var query = _dbContext.DocumentChunks.Where(c => c.DocumentId == documentId).OrderBy(c => c.PageNumber).ThenBy(c => c.ChunkIndex);
            int totalCount = await query.CountAsync();
            var chunks = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
            return (chunks, totalCount);
        }

        public async Task<int> GetChunkPositionAsync(Guid documentId, Guid chunkId)
        {
            var target = await _dbContext.DocumentChunks
                .Where(c => c.DocumentId == documentId && c.Id == chunkId)
                .Select(c => new { c.PageNumber, c.ChunkIndex })
                .FirstOrDefaultAsync();
            if (target == null) return -1;

            // Đếm số chunk đứng trước theo đúng thứ tự hiển thị (PageNumber, ChunkIndex)
            return await _dbContext.DocumentChunks
                .Where(c => c.DocumentId == documentId &&
                    (c.PageNumber < target.PageNumber ||
                     (c.PageNumber == target.PageNumber && c.ChunkIndex < target.ChunkIndex)))
                .CountAsync();
        }

        private string CalculateSHA256(Stream stream)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
