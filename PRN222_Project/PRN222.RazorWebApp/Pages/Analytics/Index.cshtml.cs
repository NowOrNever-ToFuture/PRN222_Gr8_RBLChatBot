using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.Services.Interfaces;

namespace PRN222.RazorWebApp.Pages.Analytics
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly ITokenUsageService _tokenUsageService;
        private readonly IPaymentService _paymentService;

        public IndexModel(ITokenUsageService tokenUsageService, IPaymentService paymentService)
        {
            _tokenUsageService = tokenUsageService ?? throw new ArgumentNullException(nameof(tokenUsageService));
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        }

        public List<int> AvailableTokenYears { get; set; } = new();
        public List<int> AvailablePaymentYears { get; set; } = new();
        public int CurrentYear { get; set; }

        public async Task OnGetAsync()
        {
            CurrentYear = DateTime.UtcNow.Year;
            AvailableTokenYears = await _tokenUsageService.GetAvailableYearsAsync();
            AvailablePaymentYears = await _paymentService.GetPaymentAvailableYearsAsync();
        }

        /// <summary>
        /// Handler lấy dữ liệu biểu đồ Token cho năm chỉ định
        /// </summary>
        public async Task<IActionResult> OnGetTokenChartDataAsync(int year)
        {
            if (year <= 0)
                year = DateTime.UtcNow.Year;

            var stats = await _tokenUsageService.GetMonthlyTokenStatsAsync(year);
            return new JsonResult(new { success = true, data = stats });
        }

        /// <summary>
        /// Handler lấy dữ liệu biểu đồ Payment cho năm chỉ định
        /// </summary>
        public async Task<IActionResult> OnGetPaymentChartDataAsync(int year)
        {
            if (year <= 0)
                year = DateTime.UtcNow.Year;

            var stats = await _paymentService.GetMonthlyPaymentStatsAsync(year);
            return new JsonResult(new { success = true, data = stats });
        }

        /// <summary>
        /// Handler lấy danh sách chi tiết các User sử dụng Token và nạp tiền
        /// </summary>
        public async Task<IActionResult> OnGetUsersSummaryAsync()
        {
            var tokenSummary = await _tokenUsageService.GetUserTokenSummaryAsync();
            return new JsonResult(new { success = true, data = tokenSummary });
        }
    }
}
