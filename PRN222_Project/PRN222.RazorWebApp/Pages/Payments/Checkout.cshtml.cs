using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.Interfaces;

namespace PRN222.RazorWebApp.Pages.Payments
{
    [Authorize]
    public class CheckoutModel : PageModel
    {
        private readonly IPaymentService _paymentService;
        private readonly AppDbContext _dbContext;

        public CheckoutModel(IPaymentService paymentService, AppDbContext dbContext)
        {
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public PricingPackage Package { get; set; }
        public string PaymentUrl { get; set; }
        public string ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid packageId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return RedirectToPage("/Account/Login");
            }

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != "Student")
            {
                ErrorMessage = "Chỉ học viên (Student) mới được phép đăng ký mua các gói dịch vụ AI.";
                return Page();
            }

            Package = await _dbContext.PricingPackages.FindAsync(packageId);
            if (Package == null || !Package.IsActive)
            {
                ErrorMessage = "Gói dịch vụ không tồn tại hoặc đã bị ngừng hoạt động.";
                return Page();
            }

            try
            {
                var returnUrl = Url.Page("/Payments/Success", null, null, Request.Scheme);
                var cancelUrl = Url.Page("/Payments/Cancel", null, null, Request.Scheme);
                
                // Request payment link from service layer
                PaymentUrl = await _paymentService.CreatePaymentLinkAsync(userId, packageId, returnUrl, cancelUrl);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Không thể khởi tạo link thanh toán: {ex.Message}";
            }

            return Page();
        }
    }
}
