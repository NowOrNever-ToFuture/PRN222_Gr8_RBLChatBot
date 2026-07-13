using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.Services.Interfaces;
using System.Security.Claims;

namespace PRN222.RazorWebApp.Pages.Reports
{
    [Authorize]
    public class TokenUsageModel : PageModel
    {
        private readonly ITokenUsageService _tokenUsageService;

        public TokenUsageModel(ITokenUsageService tokenUsageService)
        {
            _tokenUsageService = tokenUsageService ?? throw new ArgumentNullException(nameof(tokenUsageService));
        }

        public List<DailyTokenUsageDto> WeeklyUsage { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized();
            }

            WeeklyUsage = await _tokenUsageService.GetWeeklyUsageAsync(userId);
            
            return Page();
        }
    }
}
