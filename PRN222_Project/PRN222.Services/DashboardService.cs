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
            return await _dbContext.BenchmarkResults
                .Include(r => r.BenchmarkRun)
                .Where(r => r.BenchmarkRun.Status == "Completed")
                .GroupBy(r => new { r.BenchmarkRun.EmbeddingModel, r.BenchmarkRun.ChunkingStrategy })
                .Select(g => new ChartDataDto
                {
                    Model = g.Key.EmbeddingModel + " / " + g.Key.ChunkingStrategy,
                    AvgFaithfulness = Math.Round(g.Average(r => (double)r.FaithfulnessScore), 3),
                    AvgRelevance = Math.Round(g.Average(r => (double)r.RelevanceScore), 3),
                    AvgLatency = Math.Round(g.Average(r => (double)r.LatencyMs), 0),
                    TotalQuestions = g.Count()
                })
                .ToListAsync();
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
                    await benchmarkRunner.RunBenchmarkAsync(embeddingModel, chunkingStrategy);
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
