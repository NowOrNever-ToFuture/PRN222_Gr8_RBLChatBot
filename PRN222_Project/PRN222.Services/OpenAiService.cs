using System;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Microsoft.Extensions.Configuration;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class OpenAiService : IEmbeddingService, ILlmService
    {
        private readonly ChatClient? _chatClient;
        private readonly ChatClient? _fineTunedChatClient;
        private readonly EmbeddingClient? _embeddingClient;

        public string ProviderName => "OpenAI";

        public OpenAiService(IConfiguration config)
        {
            string? apiKey = config["AIProviders:OpenAI:ApiKey"];
            string chatModel = config["AIProviders:OpenAI:ChatModel"] ?? "gpt-4o-mini";
            string? ftModel = config["AIProviders:OpenAI:FineTunedModel"];
            string embedModel = config["AIProviders:OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
            string? baseUrl = config["AIProviders:OpenAI:BaseUrl"];

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
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
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            if (_embeddingClient == null)
            {
                throw new InvalidOperationException("OpenAI EmbeddingClient is not initialized. Please verify that a valid 'ApiKey' is provided under 'AIProviders:OpenAI' in your configuration (appsettings.json or user secrets).");
            }
            var response = await _embeddingClient.GenerateEmbeddingAsync(text);
            return response.Value.Vector.ToArray();
        }

        public async Task<string> GenerateChatResponseAsync(string prompt, bool isFineTuned = false)
        {
            var result = await GenerateChatResponseWithUsageAsync(prompt, isFineTuned);
            return result.Response;
        }

        public async Task<(string Response, int InputTokens, int OutputTokens)> GenerateChatResponseWithUsageAsync(string prompt, bool isFineTuned = false)
        {
            var client = (isFineTuned && _fineTunedChatClient != null) ? _fineTunedChatClient : _chatClient;
            if (client == null)
            {
                throw new InvalidOperationException("OpenAI ChatClient is not initialized. Please verify that a valid 'ApiKey' is provided under 'AIProviders:OpenAI' in your configuration (appsettings.json or user secrets).");
            }
            var response = await client.CompleteChatAsync(prompt);
            
            string content = response.Value.Content[0].Text ?? "";
            int inputTokens = 0;
            int outputTokens = 0;

            var usage = response.Value.Usage;
            if (usage != null)
            {
                try
                {
                    var propInput = usage.GetType().GetProperty("InputTokenCount") 
                                 ?? usage.GetType().GetProperty("InputTokens")
                                 ?? usage.GetType().GetProperty("PromptTokens");
                    var propOutput = usage.GetType().GetProperty("OutputTokenCount") 
                                  ?? usage.GetType().GetProperty("OutputTokens")
                                  ?? usage.GetType().GetProperty("CompletionTokens");
                    
                    if (propInput != null) inputTokens = Convert.ToInt32(propInput.GetValue(usage));
                    if (propOutput != null) outputTokens = Convert.ToInt32(propOutput.GetValue(usage));
                }
                catch { }
            }

            // Fallback estimation
            if (inputTokens <= 0)
            {
                inputTokens = (int)Math.Ceiling(prompt.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length * 1.35);
            }
            if (outputTokens <= 0)
            {
                outputTokens = (int)Math.Ceiling(content.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length * 1.35);
            }

            return (content, inputTokens, outputTokens);
        }
    }
}
