using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RazorWebApp.Models;
using PRN222.Services.Interfaces;

namespace PRN222.RazorWebApp.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly IAccountService _accountService;

        public RegisterModel(IAccountService accountService)
        {
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
        }

        [BindProperty]
        public RegisterViewModel Input { get; set; } = new();

        public IActionResult OnGet() => Page();

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            if (Input.Password != Input.ConfirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu xác nhận không khớp.");
                return Page();
            }

            var (success, errorMessage) = await _accountService.RegisterAsync(Input.Username, Input.Password);
            if (!success)
            {
                ModelState.AddModelError("", errorMessage);
                return Page();
            }

            TempData["SuccessMessage"] = "Đăng ký thành công! Vui lòng đăng nhập.";
            return RedirectToPage("/Account/Login");
        }
    }
}
