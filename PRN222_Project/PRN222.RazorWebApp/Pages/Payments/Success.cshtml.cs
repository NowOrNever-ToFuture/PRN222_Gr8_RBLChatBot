using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.Services.Interfaces;

namespace PRN222.RazorWebApp.Pages.Payments
{
    [Authorize]
    public class SuccessModel : PageModel
    {
        private readonly IPaymentService _paymentService;

        public SuccessModel(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        public bool IsSuccessPayment { get; set; }
        public string? ErrorMsg { get; set; }

        public async Task OnGetAsync()
        {
            var queryString = Request.QueryString.Value;
            if (!string.IsNullOrEmpty(queryString))
            {
                if (queryString.StartsWith("?"))
                {
                    queryString = queryString.Substring(1);
                }

                var result = await _paymentService.ProcessWebhookAsync(queryString);
                if (result.Success)
                {
                    IsSuccessPayment = true;
                    TempData["SuccessMessage"] = "Thanh toán thành công! Gói cước của bạn đã được kích hoạt.";
                }
                else
                {
                    IsSuccessPayment = false;
                    ErrorMsg = result.Message;
                    TempData["ErrorMessage"] = $"Lỗi xác nhận giao dịch: {result.Message}";
                }
            }
            else
            {
                IsSuccessPayment = false;
                ErrorMsg = "Không tìm thấy thông tin giao dịch.";
            }
        }
    }
}
