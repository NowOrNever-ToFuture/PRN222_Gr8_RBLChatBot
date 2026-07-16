using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class FastApiLlmService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly string _provider;

        public FastApiLlmService(HttpClient httpClient, string provider)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _provider = string.IsNullOrWhiteSpace(provider)
                ? throw new ArgumentException("Provider must not be empty.", nameof(provider))
                : provider.Trim().ToLowerInvariant();
        }

        public async Task<string> GenerateChatResponseAsync(string prompt, bool isFineTuned = false)
        {
            var result = await GenerateChatResponseWithUsageAsync(prompt, isFineTuned);
            return result.Response;
        }

        public async Task<(string Response, int InputTokens, int OutputTokens)> GenerateChatResponseWithUsageAsync(string prompt, bool isFineTuned = false)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt must not be empty.", nameof(prompt));

            using var response = await _httpClient.PostAsJsonAsync(
                $"api/chat/{Uri.EscapeDataString(_provider)}",
                new
                {
                    message = prompt,
                    provider = _provider,
                    max_new_tokens = 256,
                    temperature = 0.0
                });

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"FastAPI {_provider} returned {(int)response.StatusCode}: {ExtractError(errorBody)}");
            }

            var result = await response.Content.ReadFromJsonAsync<GatewayChatResponse>();
            if (string.IsNullOrWhiteSpace(result?.Answer))
                throw new InvalidOperationException($"FastAPI {_provider} returned an empty answer.");

            return (result.Answer, result.InputTokens, result.OutputTokens);
        }

        private static string ExtractError(string responseBody)
        {
            try
            {
                using var document = JsonDocument.Parse(responseBody);
                return document.RootElement.TryGetProperty("detail", out var detail)
                    ? detail.ToString()
                    : responseBody;
            }
            catch (JsonException)
            {
                return responseBody;
            }
        }

        private sealed class GatewayChatResponse
        {
            public string Answer { get; set; } = string.Empty;

            [JsonPropertyName("input_tokens")]
            public int InputTokens { get; set; }

            [JsonPropertyName("output_tokens")]
            public int OutputTokens { get; set; }
        }
    }
}
