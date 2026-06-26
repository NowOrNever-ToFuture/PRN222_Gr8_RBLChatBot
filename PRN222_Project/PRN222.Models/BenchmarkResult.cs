namespace PRN222.Models
{
    public class BenchmarkResult
    {
        public Guid Id { get; set; }
        public Guid BenchmarkRunId { get; set; }
        public Guid TestQuestionId { get; set; }
        public string BotAnswer { get; set; }
        public float FaithfulnessScore { get; set; }
        public float RelevanceScore { get; set; }
        public long LatencyMs { get; set; }
        public DateTime AnsweredDate { get; set; }

        // Navigation properties
        public BenchmarkRun BenchmarkRun { get; set; }
        public TestQuestion TestQuestion { get; set; }
    }
}
