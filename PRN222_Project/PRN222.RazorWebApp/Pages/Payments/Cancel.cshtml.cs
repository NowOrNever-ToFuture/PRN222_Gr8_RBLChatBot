using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PRN222.RazorWebApp.Pages.Payments
{
    [Authorize]
    public class CancelModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
