using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly AppDbContext _dbContext;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public DashboardService(AppDbContext dbContext, IServiceScopeFactory serviceScopeFactory)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        }

        public async Task<List<BenchmarkRun>> GetRecentRunsAsync(int count = 10)
        {
            return await _dbContext.BenchmarkRuns
                .OrderByDescending(r => r.RunDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync()
        {
            return new DashboardStatsDto
            {
                TotalCourses = await _dbContext.Courses.CountAsync(),
                TotalDocuments = await _dbContext.Documents.CountAsync(),
                TotalChunks = await _dbContext.DocumentChunks.CountAsync(),
                TotalQuestions = await _dbContext.TestQuestions.CountAsync(),
                TotalBenchmarkRuns = await _dbContext.BenchmarkRuns.CountAsync()
            };
        }

        public async Task<List<RecentUploadDto>> GetRecentUploadsAsync(int count = 5)
        {
            return await _dbContext.Documents
                .Include(d => d.Course)
                .Include(d => d.Owner)
                .OrderByDescending(d => d.UploadDate)
                .Take(count)
                .Select(d => new RecentUploadDto
                {
                    FileName = d.FileName,
                    CourseName = d.Course != null ? d.Course.Name : "N/A",
                    Status = d.Status,
                    UploadDate = d.UploadDate,
                    UploadedBy = d.Owner != null ? d.Owner.FullName : "N/A"
                })
                .ToListAsync();
        }

        public async Task<List<ChartDataDto>> GetChartDataAsync()
        {
            var latestBatchId = await GetLatestCompletedBenchmarkBatchIdAsync();
            if (latestBatchId == Guid.Empty)
                return new List<ChartDataDto>();

            var data = await _dbContext.BenchmarkResults
                .Include(r => r.BenchmarkRun)
                .Where(r => r.BenchmarkRun.Status == "Completed"
                    && r.BenchmarkRun.BenchmarkBatchId == latestBatchId)
                .GroupBy(r => new
                {
                    r.BenchmarkRun.LlmModel,
                    r.BenchmarkRun.EmbeddingModel,
                    r.BenchmarkRun.ChunkingStrategy
                })
                .Select(g => new ChartDataDto
                {
                    Model = g.Key.LlmModel + " (" + g.Key.EmbeddingModel + " / " + g.Key.ChunkingStrategy + ")",
                    AvgFaithfulness = Math.Round(g.Average(r => (double)r.FaithfulnessScore), 3),
                    AvgRelevance = Math.Round(g.Average(r => (double)r.RelevanceScore), 3),
                    AvgLatency = Math.Round(g.Average(r => (double)r.LatencyMs), 0),
                    TotalQuestions = g.Count()
                })
                .ToListAsync();

            if (data.Count == 0)
                return data;

            var positiveLatencies = data
                .Where(item => item.AvgLatency > 0)
                .Select(item => item.AvgLatency)
                .ToList();
            var minimumLatency = positiveLatencies.Count > 0
                ? positiveLatencies.Min()
                : 0;

            foreach (var item in data)
            {
                var performanceScore = item.AvgLatency > 0 && minimumLatency > 0
                    ? minimumLatency / item.AvgLatency
                    : 0;
                item.OverallScore = Math.Round(
                    item.AvgFaithfulness * 0.45
                    + item.AvgRelevance * 0.45
                    + performanceScore * 0.10,
                    3);
            }

            data.OrderByDescending(item => item.OverallScore).First().IsBest = true;

            var orderedData = data
                .OrderBy(item => item.Model.StartsWith("GPT-", StringComparison.OrdinalIgnoreCase) ? 0
                    : item.Model.StartsWith("Gemini-", StringComparison.OrdinalIgnoreCase) ? 1
                    : item.Model.StartsWith("Qwen-", StringComparison.OrdinalIgnoreCase) ? 2
                    : 3)
                .ThenBy(item => item.Model)
                .ToList();
            return orderedData;
        }

        public async Task<List<DifficultyChartDataDto>> GetDifficultyChartDataAsync()
        {
            var latestBatchId = await GetLatestCompletedBenchmarkBatchIdAsync();
            if (latestBatchId == Guid.Empty)
                return new List<DifficultyChartDataDto>();

            return await _dbContext.BenchmarkResults
                .Include(r => r.BenchmarkRun)
                .Include(r => r.TestQuestion)
                .Where(r => r.BenchmarkRun.Status == "Completed"
                    && r.BenchmarkRun.BenchmarkBatchId == latestBatchId)
                .GroupBy(r => new
                {
                    r.BenchmarkRun.LlmModel,
                    r.TestQuestion.Difficulty
                })
                .Select(g => new DifficultyChartDataDto
                {
                    Model = g.Key.LlmModel,
                    Difficulty = g.Key.Difficulty,
                    AvgFaithfulness = Math.Round(g.Average(r => (double)r.FaithfulnessScore), 3),
                    AvgRelevance = Math.Round(g.Average(r => (double)r.RelevanceScore), 3),
                    QualityScore = Math.Round(
                        g.Average(r => ((double)r.FaithfulnessScore + (double)r.RelevanceScore) / 2.0), 3),
                    TotalQuestions = g.Count()
                })
                .OrderBy(d => d.Model)
                .ThenBy(d => d.Difficulty)
                .ToListAsync();
        }

        private async Task<Guid> GetLatestCompletedBenchmarkBatchIdAsync()
        {
            return await _dbContext.BenchmarkRuns
                .Where(run => run.Status == "Completed")
                .OrderByDescending(run => run.RunDate)
                .Select(run => run.BenchmarkBatchId)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Chạy benchmark NGẦM trên background thread.
        /// Tạo scope mới cho DI để tránh DbContext bị dispose.
        /// Controller trả về ngay lập tức, client theo dõi tiến độ qua SignalR.
        /// </summary>
        public Task<Guid> StartBenchmarkAsync(string embeddingModel, string chunkingStrategy)
        {
            // Tạo BenchmarkRun ID trước để trả về cho client
            var runId = Guid.NewGuid();

            // Fire-and-forget: chạy benchmark trên background thread
            _ = Task.Run(async () =>
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var documentService = scope.ServiceProvider.GetRequiredService<IDocumentService>();
                var benchmarkRunner = scope.ServiceProvider.GetRequiredService<IBenchmarkRunnerService>();

                try
                {
                    await documentService.ReindexIndexedDocumentsAsync(embeddingModel, chunkingStrategy);
                    await benchmarkRunner.RunBenchmarkAsync(runId, embeddingModel, chunkingStrategy);
                }
                catch (Exception ex)
                {
                    // Log lỗi nhưng không crash app
                    Console.Error.WriteLine($"[Benchmark Error] {ex.Message}");
                }
            });

            return Task.FromResult(runId);
        }
    }
}
