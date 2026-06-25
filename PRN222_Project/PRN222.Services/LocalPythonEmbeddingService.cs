using System.Net.Http.Json;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class LocalPythonEmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;

        public string ProviderName { get; }

        // Ta sẽ truyền tên model cụ thể khi đăng ký DI
        public LocalPythonEmbeddingService(HttpClient httpClient, string modelName)
        {
            _httpClient = httpClient;
            ProviderName = modelName; // "bge-m3", "e5", hoặc "phobert"
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            var requestBody = new { text = text, model_name = ProviderName };
            var response = await _httpClient.PostAsJsonAsync("api/embed", requestBody);
            
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<PythonEmbedResponse>();
            return result?.Vector ?? Array.Empty<float>();
        }

        private class PythonEmbedResponse
        {
            public float[] Vector { get; set; } = Array.Empty<float>();
        }
    }
}
