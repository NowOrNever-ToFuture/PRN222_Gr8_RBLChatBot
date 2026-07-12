using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.Models;
using PRN222.Services.Interfaces;

namespace PRN222.RazorWebApp.Pages.Payments
{
    [Authorize]
    public class PricingModel : PageModel
    {
        private readonly IPaymentService _paymentService;

        public PricingModel(IPaymentService paymentService)
        {
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        }

        public List<PricingPackage> Packages { get; set; } = new List<PricingPackage>();
        public UserSubscription? CurrentSubscription { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return RedirectToPage("/Account/Login");
            }

            Packages = await _paymentService.GetAllPackagesAsync();
            CurrentSubscription = await _paymentService.GetUserSubscriptionAsync(userId);

            return Page();
        }
    }
}
