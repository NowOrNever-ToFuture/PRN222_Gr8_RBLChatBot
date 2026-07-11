using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.Services.Interfaces;

namespace PRN222.RazorWebApp.Pages.Dashboard
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly IDashboardService _dashboardService;
        private readonly ISystemSettingService _systemSettingService;

        public IndexModel(IDashboardService dashboardService, ISystemSettingService systemSettingService)
        {
            _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
            _systemSettingService = systemSettingService ?? throw new ArgumentNullException(nameof(systemSettingService));
        }

        public List<PRN222.Models.BenchmarkRun> BenchmarkRuns { get; set; } = new();
        public PRN222.Services.Interfaces.DashboardStatsDto? Stats { get; set; }
        public List<PRN222.Services.Interfaces.RecentUploadDto> RecentUploads { get; set; } = new();
        public string ActiveModel { get; set; } = "Chưa cấu hình";
        public string ActiveChunkingStrategy { get; set; } = "markdown_header";

        public async Task OnGetAsync()
        {
            BenchmarkRuns = await _dashboardService.GetRecentRunsAsync(5);
            Stats = await _dashboardService.GetDashboardStatsAsync();
            RecentUploads = await _dashboardService.GetRecentUploadsAsync(5);
            var activeModel = await _systemSettingService.GetSettingValueAsync("ActiveEmbeddingModel");
            var activeChunking = await _systemSettingService.GetSettingValueAsync("ActiveChunkingStrategy");
            ActiveModel = string.IsNullOrEmpty(activeModel) ? "Chưa cấu hình" : activeModel;
            ActiveChunkingStrategy = string.IsNullOrEmpty(activeChunking) ? "markdown_header" : activeChunking;
        }

        public async Task<IActionResult> OnGetChartDataAsync()
        {
            var data = await _dashboardService.GetChartDataAsync();
            return new JsonResult(data);
        }

        public async Task<IActionResult> OnGetDifficultyChartDataAsync()
        {
            var data = await _dashboardService.GetDifficultyChartDataAsync();
            return new JsonResult(data);
        }

        public async Task<IActionResult> OnPostRunBenchmarkAsync(string embeddingModel, string chunkingStrategy)
        {
            try
            {
                var runId = await _dashboardService.StartBenchmarkAsync(
                    embeddingModel ?? "bge-m3",
                    chunkingStrategy ?? "fixed-size");
                return new JsonResult(new { success = true, runId, message = "Benchmark đã bắt đầu chạy ngầm. Theo dõi tiến độ qua thanh Progress." });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }
    }
}
