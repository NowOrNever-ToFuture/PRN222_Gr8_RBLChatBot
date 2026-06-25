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
        Task<RagResponse> GenerateRagResponseAsync(string query);
        Task<List<DocumentChunk>> SearchChunksAsync(string query);
        string FormatSearchResults(List<DocumentChunk> chunks);
        Task<List<Message>> GetChatHistoryAsync(Guid userId);
        Task SaveMessageAsync(Guid userId, string role, string content, string citedChunkIds = "");
        Task ClearChatHistoryAsync(Guid userId);
    }
}
