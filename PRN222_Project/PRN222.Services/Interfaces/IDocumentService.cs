using PRN222.Models;
using PRN222.Services.DTOs;

namespace PRN222.Services.Interfaces
{
    public interface IDocumentService
    {
        /// <summary>
        /// Upload a document file with validation and persistence.
        /// Implements Business Rules:
        /// - BR1: Validates file format (.pdf, .docx, .pptx)
        /// - BR2: Validates file size (max 10MB)
        /// - BR4: Checks for duplicate filenames and appends timestamp if needed
        /// </summary>
        /// <param name="dto">Upload document DTO containing CourseId and IFormFile</param>
        /// <param name="uploadBasePath">Physical path to wwwroot/uploads directory</param>
        /// <param name="ownerId">User ID of the uploader</param>
        /// <returns>The created Document entity</returns>
        /// <exception cref="InvalidOperationException">Thrown when validation fails</exception>
        Task<Document> UploadDocumentAsync(UploadDocumentDTO dto, string uploadBasePath, Guid ownerId);

        /// <summary>
        /// Index a document by splitting its content into chunks
        /// </summary>
        /// <param name="documentId">The ID of the document to index</param>
        Task IndexDocumentAsync(Guid documentId, string? chunkingStrategy = null);

        /// <summary>
        /// Re-index every document that is already indexed using the selected model and chunking strategy.
        /// Used before benchmark runs so each strategy is measured against its own chunk set.
        /// </summary>
        Task<int> ReindexIndexedDocumentsAsync(string embeddingModel, string chunkingStrategy);

        /// <summary>
        /// Get documents with role-based filtering
        /// </summary>
        /// <param name="userId">Current user ID</param>
        /// <param name="role">User role (Admin/Student)</param>
        /// <returns>Filtered list of documents</returns>
        Task<List<Document>> GetDocumentsAsync(Guid userId, string role);

        Task<Document?> GetDocumentByIdAsync(Guid id);
        Task<(bool Success, string ErrorMessage)> DeleteDocumentAsync(Guid id, Guid currentUserId, string role);
        
        /// <summary>
        /// Get chunks for a specific document
        /// </summary>
        Task<List<DocumentChunk>> GetDocumentChunksAsync(Guid documentId);

        /// <summary>
        /// Get chunks for a specific document with paging support
        /// </summary>
        Task<(List<DocumentChunk> Chunks, int TotalCount)> GetDocumentChunksPagedAsync(
            Guid documentId, int pageNumber = 1, int pageSize = 10);
    }
}

