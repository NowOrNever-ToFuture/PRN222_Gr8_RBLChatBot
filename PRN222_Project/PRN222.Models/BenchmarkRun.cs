namespace PRN222.Models
{
    public class BenchmarkRun
    {
        public Guid Id { get; set; }
        public Guid BenchmarkBatchId { get; set; }
        public DateTime RunDate { get; set; }
        public long TotalTimeMs { get; set; }
        public string LlmModel { get; set; } = string.Empty;
        public string EmbeddingModel { get; set; } = string.Empty;
        public string ChunkingStrategy { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "Running", "Completed", "Failed"
        public string ResultSummary { get; set; } = string.Empty;

        // Navigation properties
        public ICollection<BenchmarkResult> Results { get; set; } = new List<BenchmarkResult>();
    }
}
