namespace PRN222.Services.Interfaces
{
    public interface ILlmService
    {
        // Cờ isFineTuned giúp hàm biết nên dùng model gốc hay model đã train
        Task<string> GenerateChatResponseAsync(string prompt, bool isFineTuned = false);
    }
}
