using PRN222.Models;

namespace PRN222.Services.Interfaces
{
    public interface IDashboardService
    {
        /// <summary>
        /// Lấy N benchmark runs gần nhất.
        /// </summary>
        Task<List<BenchmarkRun>> GetRecentRunsAsync(int count = 10);

        /// <summary>
        /// Lấy dữ liệu biểu đồ theo LLM, embedding model và chunking strategy.
        /// </summary>
        Task<List<ChartDataDto>> GetChartDataAsync();

        /// <summary>
        /// Lấy điểm chất lượng trung bình của từng LLM theo độ khó câu hỏi (1/2/3).
        /// </summary>
        Task<List<DifficultyChartDataDto>> GetDifficultyChartDataAsync();

        /// <summary>
        /// Chạy benchmark ngầm (fire-and-forget).
        /// Trả về BenchmarkRunId để client theo dõi qua SignalR.
        /// </summary>
        Task<Guid> StartBenchmarkAsync(string embeddingModel, string chunkingStrategy);

        /// <summary>
        /// Lấy thống kê tổng quan cho Dashboard.
        /// </summary>
        Task<DashboardStatsDto> GetDashboardStatsAsync();

        /// <summary>
        /// Lấy danh sách tài liệu mới tải lên gần đây.
        /// </summary>
        Task<List<RecentUploadDto>> GetRecentUploadsAsync(int count = 5);
    }

    public class DashboardStatsDto
    {
        public int TotalCourses { get; set; }
        public int TotalDocuments { get; set; }
        public int TotalChunks { get; set; }
        public int TotalQuestions { get; set; }
        public int TotalBenchmarkRuns { get; set; }
    }

    public class ChartDataDto
    {
        public string Model { get; set; } = "";
        public double AvgFaithfulness { get; set; }
        public double AvgRelevance { get; set; }
        public double AvgLatency { get; set; }
        public double OverallScore { get; set; }
        public bool IsBest { get; set; }
        public int TotalQuestions { get; set; }
    }

    public class DifficultyChartDataDto
    {
        public string Model { get; set; } = "";
        public int Difficulty { get; set; } // 1: Dễ, 2: Trung bình, 3: Khó
        public double AvgFaithfulness { get; set; }
        public double AvgRelevance { get; set; }
        public double QualityScore { get; set; } // Trung bình cộng của 2 điểm trên
        public int TotalQuestions { get; set; }
    }

    public class RecentUploadDto
    {
        public string FileName { get; set; } = "";
        public string CourseName { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime UploadDate { get; set; }
        public string UploadedBy { get; set; } = "";
    }
}
