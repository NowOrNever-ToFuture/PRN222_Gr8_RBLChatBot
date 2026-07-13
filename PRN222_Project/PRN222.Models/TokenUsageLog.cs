namespace PRN222.Models
{
    public class TokenUsageLog
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public string ModelName { get; set; }
        public string Feature { get; set; } // "Chat", "LlmJudge", "Benchmark"
        public DateTime CreatedDate { get; set; }

        // Navigation property
        public User User { get; set; }
    }
}
