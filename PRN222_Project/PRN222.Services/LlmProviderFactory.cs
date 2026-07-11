using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class LlmProviderFactory : ILlmProviderFactory
    {
        private static readonly HashSet<string> SupportedProviders = new(
            new[] { "gpt", "gemini", "qwen" },
            StringComparer.OrdinalIgnoreCase);

        private readonly IHttpClientFactory _httpClientFactory;

        public LlmProviderFactory(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory
                ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public ILlmService GetService(string provider)
        {
            if (!SupportedProviders.Contains(provider))
                throw new ArgumentException($"Unsupported LLM provider: {provider}", nameof(provider));

            return new FastApiLlmService(
                _httpClientFactory.CreateClient("PythonApi"),
                provider);
        }
    }
}
