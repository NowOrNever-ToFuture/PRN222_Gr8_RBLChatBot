using Microsoft.AspNetCore.Http;
using PRN222.Services.DTOs;

namespace PRN222.Services.Interfaces
{
    public interface IDocumentProcessingService
    {
        /// <summary>
        /// Đọc text thô từ file PDF hoặc DOCX.
        /// Throw exception nếu file hỏng hoặc sai định dạng.
        /// </summary>
        Task<string> ExtractTextAsync(string filePath);

        /// <summary>
        /// Cắt theo Fixed-size (mặc định 500 ký tự mỗi chunk, overlap 50 ký tự).
        /// Overlap giúp không mất ngữ nghĩa ở ranh giới giữa 2 chunk.
        /// </summary>
        List<string> SplitByFixedSize(string text, int chunkSize = 500, int overlap = 50);

        /// <summary>
        /// Cắt theo Sentence (dấu chấm / xuống dòng).
        /// Gom các câu lại cho đến khi đạt maxChunkSize, rồi bắt đầu chunk mới.
        /// </summary>
        List<string> SplitBySentence(string text, int maxChunkSize = 500);

        /// <summary>
        /// Gửi file tài liệu sang Python Microservice để parse, chunk, và nhúng vector.
        /// Sau đó lưu kết quả (Document + DocumentChunks) vào database.
        /// </summary>
        /// <param name="file">File tài liệu (.pdf, .docx) từ người dùng upload</param>
        /// <param name="courseId">ID của môn học liên kết</param>
        /// <param name="modelName">Tên model embedding (mặc định: bge-m3)</param>
        /// <returns>true nếu toàn bộ pipeline thành công</returns>
        Task<bool> UploadAndProcessDocumentAsync(IFormFile file, Guid courseId, string modelName = "bge-m3", string chunkStrategy = "markdown_header");

        /// <summary>
        /// Gửi file vật lý sang Python Microservice để parse sang Markdown và chunking có vector embeddings.
        Task<PythonParseResponseDto> ParseDocumentAsync(string filePath, string modelName = "bge-m3", string chunkStrategy = "markdown_header", int chunkSize = 500);
    }
}
