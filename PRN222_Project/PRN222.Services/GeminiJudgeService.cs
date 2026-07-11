using System.Text.Json;
using System.Text.RegularExpressions;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class GeminiJudgeService : ILlmJudgeService
    {
        private readonly ILlmService _geminiService;

        public GeminiJudgeService(ILlmProviderFactory llmProviderFactory)
        {
            ArgumentNullException.ThrowIfNull(llmProviderFactory);
            _geminiService = llmProviderFactory.GetService("gemini");
        }

        public async Task<(double Faithfulness, double Relevance)> JudgeAsync(
            string question,
            string context,
            string botAnswer)
        {
            if (string.IsNullOrWhiteSpace(question))
                throw new ArgumentException("Question must not be empty.", nameof(question));
            if (string.IsNullOrWhiteSpace(context))
                throw new ArgumentException("Context must not be empty.", nameof(context));
            if (string.IsNullOrWhiteSpace(botAnswer))
                throw new ArgumentException("Bot answer must not be empty.", nameof(botAnswer));

            var prompt = $$"""
                Bạn là giám khảo độc lập cho hệ thống RAG. Hãy chấm câu trả lời dựa duy nhất trên câu hỏi và context.

                CÂU HỎI:
                {{question}}

                CONTEXT:
                {{context}}

                CÂU TRẢ LỜI CỦA MODEL:
                {{botAnswer}}

                Chấm hai tiêu chí trong khoảng 0.0 đến 1.0:
                - faithfulness: mọi khẳng định có được context hỗ trợ hay không.
                - relevance: câu trả lời có trực tiếp và đầy đủ cho câu hỏi hay không.

                Chỉ trả về JSON: {"faithfulness": 0.0, "relevance": 0.0}
                """;

            var response = await _geminiService.GenerateChatResponseAsync(prompt);
            return ParseJudgeResponse(response);
        }

        private static (double Faithfulness, double Relevance) ParseJudgeResponse(string response)
        {
            var jsonMatch = Regex.Match(
                response,
                @"\{[^}]*""faithfulness""[^}]*\}",
                RegexOptions.IgnoreCase);
            if (!jsonMatch.Success)
                throw new InvalidOperationException("Gemini Judge did not return the expected JSON.");

            try
            {
                using var document = JsonDocument.Parse(jsonMatch.Value);
                var root = document.RootElement;
                double faithfulness = 0;
                double relevance = 0;

                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name.Equals("faithfulness", StringComparison.OrdinalIgnoreCase))
                        faithfulness = property.Value.GetDouble();
                    else if (property.Name.Equals("relevance", StringComparison.OrdinalIgnoreCase))
                        relevance = property.Value.GetDouble();
                }

                return (
                    Math.Clamp(faithfulness, 0.0, 1.0),
                    Math.Clamp(relevance, 0.0, 1.0));
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("Gemini Judge returned invalid JSON.", exception);
            }
        }
    }
}
