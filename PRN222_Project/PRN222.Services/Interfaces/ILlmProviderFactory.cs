namespace PRN222.Services.Interfaces
{
    public interface ILlmProviderFactory
    {
        /// <summary>
        /// Lấy LLM client đi qua FastAPI theo provider: gpt, gemini hoặc qwen.
        /// </summary>
        ILlmService GetService(string provider);
    }
}
