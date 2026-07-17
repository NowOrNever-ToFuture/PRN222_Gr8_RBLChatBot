using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PRN222.Models;

namespace PRN222.Services.Interfaces
{
    public interface IChatService
    {
        Task<Guid?> SmartRouteAsync(string query);
        Task<List<(DocumentChunk Chunk, double Score)>> SearchChunksByVectorAsync(string query, Guid? courseId = null);
        Task<RagResponse> GenerateRagResponseAsync(string query, Guid userId, Guid? selectedCourseId = null, Guid? conversationId = null);
        Task<List<DocumentChunk>> SearchChunksAsync(string query);
        string FormatSearchResults(List<DocumentChunk> chunks);

        // ===== Quản lý hội thoại: mỗi conversation gắn 1 môn học =====
        Task<List<Conversation>> GetConversationsAsync(Guid userId);
        Task<Conversation?> GetConversationAsync(Guid conversationId, Guid userId);
        Task<Conversation> CreateConversationAsync(Guid userId, Guid? courseId, string title);
        Task<bool> DeleteConversationAsync(Guid conversationId, Guid userId);
        Task<List<Message>> GetConversationMessagesAsync(Guid conversationId, Guid userId);
        Task SaveMessageAsync(Guid conversationId, string role, string content, string citedChunkIds = "");

        /// <summary>Lấy chunks (kèm Document) theo danh sách id — dựng lại trích dẫn cho history.</summary>
        Task<List<DocumentChunk>> GetChunksByIdsAsync(IEnumerable<Guid> chunkIds);
    }
}
