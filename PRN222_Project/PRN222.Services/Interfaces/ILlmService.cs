using System.Threading.Tasks;

namespace PRN222.Services.Interfaces
{
    public interface ILlmService
    {
        // Cờ isFineTuned giúp hàm biết nên dùng model gốc hay model đã train
        Task<string> GenerateChatResponseAsync(string prompt, bool isFineTuned = false);

        Task<(string Response, int InputTokens, int OutputTokens)> GenerateChatResponseWithUsageAsync(string prompt, bool isFineTuned = false);
    }
}
