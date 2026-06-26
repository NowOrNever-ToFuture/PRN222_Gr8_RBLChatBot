using System.Text.Json;
using System.Text.RegularExpressions;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class LlmJudgeService : ILlmJudgeService
    {
        private readonly ILlmService _llmService;

        public LlmJudgeService(ILlmService llmService)
        {
            _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        }

        public async Task<(double Faithfulness, double Relevance)> JudgeAsync(
            string question, string context, string botAnswer)
        {
            // System Prompt đóng vai giám khảo AI
            string prompt = $@"Bạn là một giám khảo AI chuyên nghiệp. Nhiệm vụ của bạn là đánh giá câu trả lời của một Chatbot dựa trên 2 tiêu chí.

=== CÂU HỎI ===
{question}

=== NGỮ CẢNH (Context từ tài liệu) ===
{context}

=== CÂU TRẢ LỜI CỦA BOT ===
{botAnswer}

=== TIÊU CHÍ ĐÁNH GIÁ ===
1. **Faithfulness** (Độ trung thực): Câu trả lời có trung thực, chính xác so với Context không? Có bịa đặt thông tin không?
   - 1.0 = Hoàn toàn trung thực, mọi thông tin đều có trong Context
   - 0.0 = Hoàn toàn bịa đặt, không liên quan đến Context

2. **Relevance** (Độ liên quan): Câu trả lời có trả lời đúng câu hỏi không? Có đi lạc đề không?
   - 1.0 = Trả lời chính xác, đầy đủ câu hỏi
   - 0.0 = Hoàn toàn không liên quan đến câu hỏi

=== YÊU CẦU ===
Trả về KẾT QUẢ dưới dạng JSON duy nhất, không giải thích thêm:
{{""faithfulness"": 0.85, ""relevance"": 0.90}}";

            try
            {
                string response = await _llmService.GenerateChatResponseAsync(prompt);
                return ParseJudgeResponse(response);
            }
            catch (Exception)
            {
                // Nếu LLM lỗi, trả về điểm mặc định 0
                return (0.0, 0.0);
            }
        }

        /// <summary>
        /// Parse kết quả JSON trả về từ LLM thành (double, double).
        /// Hỗ trợ cả trường hợp LLM trả về text kèm JSON.
        /// </summary>
        private (double Faithfulness, double Relevance) ParseJudgeResponse(string response)
        {
            try
            {
                // Tìm JSON object trong response (LLM có thể trả về text kèm JSON)
                var jsonMatch = Regex.Match(response, @"\{[^}]*""faithfulness""[^}]*\}", RegexOptions.IgnoreCase);
                if (!jsonMatch.Success)
                {
                    jsonMatch = Regex.Match(response, @"\{[^}]*""Faithfulness""[^}]*\}", RegexOptions.IgnoreCase);
                }

                if (jsonMatch.Success)
                {
                    string jsonStr = jsonMatch.Value;
                    using var doc = JsonDocument.Parse(jsonStr);
                    var root = doc.RootElement;

                    double faithfulness = 0.0;
                    double relevance = 0.0;

                    // Thử nhiều biến thể key (case-insensitive)
                    foreach (var prop in root.EnumerateObject())
                    {
                        string key = prop.Name.ToLowerInvariant();
                        if (key == "faithfulness")
                            faithfulness = prop.Value.GetDouble();
                        else if (key == "relevance")
                            relevance = prop.Value.GetDouble();
                    }

                    // Clamp giá trị trong khoảng [0.0, 1.0]
                    faithfulness = Math.Max(0.0, Math.Min(1.0, faithfulness));
                    relevance = Math.Max(0.0, Math.Min(1.0, relevance));

                    return (faithfulness, relevance);
                }

                return (0.0, 0.0);
            }
            catch
            {
                return (0.0, 0.0);
            }
        }
    }
}
