using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PRN222.RazorWebApp.Pages
{
    public class IndexModel : PageModel
    {
        public IActionResult OnGet()
        {
            if (!(User.Identity?.IsAuthenticated ?? false))
                return RedirectToPage("/Account/Login");

            if (User.IsInRole("Admin"))
                return RedirectToPage("/Dashboard/Index");

            if (User.IsInRole("Student"))
                return RedirectToPage("/Chat/Index");

            return Page();
        }
    }
}
