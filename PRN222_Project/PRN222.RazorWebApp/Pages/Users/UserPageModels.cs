using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RazorWebApp.Models;
using PRN222.Services.Interfaces;
using System.Security.Claims;

namespace PRN222.RazorWebApp.Pages.Users
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly IUserService _userService;
        public IndexModel(IUserService userService) => _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        public List<PRN222.Models.User> Users { get; set; } = new();
        public async Task OnGetAsync() => Users = await _userService.GetAllUsersAsync();
    }

    [Authorize(Roles = "Admin")]
    public class CreateModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly ICourseService _courseService;
        public CreateModel(IUserService userService, ICourseService courseService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _courseService = courseService ?? throw new ArgumentNullException(nameof(courseService));
        }
        [BindProperty] public CreateUserViewModel Input { get; set; } = new();
        public List<PRN222.Models.Course> Courses { get; set; } = new();
        public async Task<IActionResult> OnGetAsync()
        {
            Courses = await _courseService.GetAllCoursesAsync();
            return Page();
        }
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                Courses = await _courseService.GetAllCoursesAsync();
                return Page();
            }
            var (success, errorMessage) = await _userService.CreateUserAsync(Input.Username, Input.Password, Input.Role, Input.CourseId);
            if (!success)
            {
                ModelState.AddModelError("", errorMessage);
                Courses = await _courseService.GetAllCoursesAsync();
                return Page();
            }
            TempData["SuccessMessage"] = $"Tài khoản '{Input.Username}' đã được tạo thành công.";
            return RedirectToPage("/Users/Index");
        }
    }

    [Authorize(Roles = "Admin")]
    public class EditModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly ICourseService _courseService;
        public EditModel(IUserService userService, ICourseService courseService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _courseService = courseService ?? throw new ArgumentNullException(nameof(courseService));
        }
        [BindProperty] public EditUserViewModel Input { get; set; } = new();
        public List<PRN222.Models.Course> Courses { get; set; } = new();
        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null) return NotFound();
            Input = new EditUserViewModel { Id = user.Id, Username = user.Username, Role = user.Role, CourseId = user.CourseId };
            Courses = await _courseService.GetAllCoursesAsync();
            return Page();
        }
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                Courses = await _courseService.GetAllCoursesAsync();
                return Page();
            }
            var (success, errorMessage) = await _userService.UpdateUserAsync(Input.Id, Input.Username, Input.Role, Input.CourseId);
            if (!success)
            {
                ModelState.AddModelError("", errorMessage);
                Courses = await _courseService.GetAllCoursesAsync();
                return Page();
            }
            TempData["SuccessMessage"] = $"Tài khoản '{Input.Username}' đã được cập nhật thành công.";
            return RedirectToPage("/Users/Index");
        }
    }

    [Authorize(Roles = "Admin")]
    public class DeleteModel : PageModel
    {
        private readonly IUserService _userService;
        public DeleteModel(IUserService userService) => _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        public PRN222.Models.User? UserToDelete { get; set; }
        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            UserToDelete = await _userService.GetUserByIdAsync(id);
            if (UserToDelete == null) return NotFound();
            return Page();
        }
        public async Task<IActionResult> OnPostAsync(Guid id)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var (success, errorMessage) = await _userService.DeleteUserAsync(id, currentUserId ?? string.Empty);
            if (!success) { TempData["ErrorMessage"] = errorMessage; return RedirectToPage("/Users/Index"); }
            TempData["SuccessMessage"] = "Tài khoản đã được xóa thành công.";
            return RedirectToPage("/Users/Index");
        }
    }
}
