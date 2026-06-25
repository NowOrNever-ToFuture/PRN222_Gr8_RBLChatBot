namespace PRN222.Models
{
    public class BenchmarkRun
    {
        public Guid Id { get; set; }
        public DateTime RunDate { get; set; }
        public long TotalTimeMs { get; set; }
        public string EmbeddingModel { get; set; }
        public string ChunkingStrategy { get; set; }
        public string Status { get; set; } // "Running", "Completed", "Failed"
        public string ResultSummary { get; set; }

        // Navigation properties
        public ICollection<BenchmarkResult> Results { get; set; } = new List<BenchmarkResult>();
    }
}
