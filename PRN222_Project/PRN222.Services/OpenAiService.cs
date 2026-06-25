using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Microsoft.Extensions.Configuration;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class OpenAiService : IEmbeddingService, ILlmService
    {
        private readonly ChatClient _chatClient;
        private readonly ChatClient? _fineTunedChatClient;
        private readonly EmbeddingClient _embeddingClient;

        public string ProviderName => "OpenAI";

        public OpenAiService(IConfiguration config)
        {
            string apiKey = config["AIProviders:OpenAI:ApiKey"] ?? throw new ArgumentNullException("ApiKey is missing in configuration.");
            string chatModel = config["AIProviders:OpenAI:ChatModel"] ?? "gpt-4o-mini";
            string? ftModel = config["AIProviders:OpenAI:FineTunedModel"];
            string embedModel = config["AIProviders:OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
            string? baseUrl = config["AIProviders:OpenAI:BaseUrl"];

            var options = new OpenAIClientOptions();
            if (!string.IsNullOrEmpty(baseUrl))
            {
                options.Endpoint = new Uri(baseUrl);
            }

            var credential = new System.ClientModel.ApiKeyCredential(apiKey);

            _chatClient = new ChatClient(chatModel, credential, options);
            _embeddingClient = new EmbeddingClient(embedModel, credential, options);
            
            if (!string.IsNullOrEmpty(ftModel))
            {
                _fineTunedChatClient = new ChatClient(ftModel, credential, options);
            }
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            var response = await _embeddingClient.GenerateEmbeddingAsync(text);
            return response.Value.Vector.ToArray();
        }

        public async Task<string> GenerateChatResponseAsync(string prompt, bool isFineTuned = false)
        {
            var client = (isFineTuned && _fineTunedChatClient != null) ? _fineTunedChatClient : _chatClient;
            var response = await client.CompleteChatAsync(prompt);
            return response.Value.Content[0].Text;
        }
    }
}
