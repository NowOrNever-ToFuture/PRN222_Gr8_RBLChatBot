namespace PRN222.Services.Interfaces
{
    public interface IEmbeddingService
    {
        string ProviderName { get; } 
        Task<float[]> GenerateEmbeddingAsync(string text);
    }
}
