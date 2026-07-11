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
        private readonly IPaymentService _paymentService;

        public IndexModel(
            IDashboardService dashboardService, 
            ISystemSettingService systemSettingService,
            IPaymentService paymentService)
        {
            _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
            _systemSettingService = systemSettingService ?? throw new ArgumentNullException(nameof(systemSettingService));
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        }

        public List<PRN222.Models.BenchmarkRun> BenchmarkRuns { get; set; } = new();
        public PRN222.Services.Interfaces.DashboardStatsDto? Stats { get; set; }
        public List<PRN222.Services.Interfaces.RecentUploadDto> RecentUploads { get; set; } = new();
        public List<PRN222.Models.PricingPackage> PricingPackages { get; set; } = new();
        public string ActiveModel { get; set; } = "Chưa cấu hình";
        public string ActiveChunkingStrategy { get; set; } = "markdown_header";

        public async Task OnGetAsync()
        {
            BenchmarkRuns = await _dashboardService.GetRecentRunsAsync(5);
            Stats = await _dashboardService.GetDashboardStatsAsync();
            RecentUploads = await _dashboardService.GetRecentUploadsAsync(5);
            PricingPackages = await _paymentService.GetAllPackagesAsync();
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

        public async Task<IActionResult> OnPostUpdatePackageAsync(Guid packageId, string name, int tokenQuota, double price, int durationDays, string description)
        {
            if (string.IsNullOrWhiteSpace(name) || tokenQuota <= 0 || price < 0 || durationDays <= 0 || string.IsNullOrWhiteSpace(description))
            {
                return new JsonResult(new { success = false, message = "Thông tin cấu hình gói cước không hợp lệ." });
            }

            var success = await _paymentService.UpdatePricingPackageAsync(packageId, name, tokenQuota, price, durationDays, description);
            if (success)
            {
                return new JsonResult(new { success = true, message = "Cấu hình gói cước đã được cập nhật thành công." });
            }
            return new JsonResult(new { success = false, message = "Không tìm thấy gói cước cần cập nhật." });
        }

        public async Task<IActionResult> OnPostCreatePackageAsync(string name, int tokenQuota, double price, int durationDays, string description)
        {
            if (string.IsNullOrWhiteSpace(name) || tokenQuota <= 0 || price < 0 || durationDays <= 0 || string.IsNullOrWhiteSpace(description))
            {
                return new JsonResult(new { success = false, message = "Thông tin gói cước mới không hợp lệ." });
            }

            try
            {
                await _paymentService.CreatePricingPackageAsync(name, tokenQuota, price, durationDays, description);
                return new JsonResult(new { success = true, message = $"Gói cước '{name}' đã được tạo thành công." });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Lỗi khi tạo gói cước: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnPostDeletePackageAsync(Guid packageId)
        {
            try
            {
                var success = await _paymentService.DeletePricingPackageAsync(packageId);
                if (success)
                {
                    return new JsonResult(new { success = true, message = "Đã xóa gói cước thành công." });
                }
                return new JsonResult(new { success = false, message = "Không tìm thấy gói cước cần xóa." });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Lỗi khi xóa gói cước: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnGetPackagesJsonAsync()
        {
            var packages = await _paymentService.GetAllPackagesAsync();
            return new JsonResult(packages.Select(p => new {
                id = p.Id,
                name = p.Name,
                tokenQuota = p.TokenQuota,
                price = p.Price,
                durationDays = p.DurationDays,
                isActive = p.IsActive,
                description = p.Description
            }));
        }
    }
}
