namespace PRN222.Services.Interfaces
{
    public interface ILlmJudgeService
    {
        /// <summary>
        /// Sử dụng LLM (GPT-4o-mini) đóng vai giám khảo để chấm điểm câu trả lời của Bot.
        /// </summary>
        /// <param name="question">Câu hỏi gốc</param>
        /// <param name="context">Ngữ cảnh (các chunks tài liệu liên quan)</param>
        /// <param name="botAnswer">Câu trả lời của Bot</param>
        /// <returns>Faithfulness (0.0-1.0) và Relevance (0.0-1.0)</returns>
        Task<(double Faithfulness, double Relevance)> JudgeAsync(
            string question, string context, string botAnswer);
    }
}
