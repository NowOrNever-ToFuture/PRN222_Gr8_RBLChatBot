using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.Models;
using PRN222.Services.Interfaces;

namespace PRN222.RazorWebApp.Pages.Courses
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ICourseService _courseService;
        private readonly IUserService _userService;

        public IndexModel(ICourseService courseService, IUserService userService)
        {
            _courseService = courseService ?? throw new ArgumentNullException(nameof(courseService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        public List<PRN222.Models.Course> Courses { get; set; } = new();
        public List<User> Lecturers { get; set; } = new();

        public async Task OnGetAsync()
        {
            Courses = await _courseService.GetAllCoursesAsync();
            if (User.IsInRole("Admin"))
            {
                var users = await _userService.GetAllUsersAsync();
                Lecturers = users.Where(u => u.Role == "Lecturer").ToList();
            }
        }

        public async Task<IActionResult> OnPostCreateAsync([FromBody] CourseCreateRequest dto)
        {
            if (!User.IsInRole("Admin"))
                return new JsonResult(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });

            if (string.IsNullOrWhiteSpace(dto?.Code) || string.IsNullOrWhiteSpace(dto?.Name))
                return new JsonResult(new { success = false, message = "Mã môn học và Tên môn học không được để trống." });
            try
            {
                var course = await _courseService.CreateCourseAsync(dto.Name, dto.Code, dto.Description ?? "", dto.ManagedById);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex) { return new JsonResult(new { success = false, message = ex.Message }); }
        }

        public async Task<IActionResult> OnPostEditAsync([FromBody] CourseEditRequest dto)
        {
            if (!User.IsInRole("Admin"))
                return new JsonResult(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });

            if (string.IsNullOrWhiteSpace(dto?.Code) || string.IsNullOrWhiteSpace(dto?.Name))
                return new JsonResult(new { success = false, message = "Mã môn học và Tên môn học không được để trống." });
            try
            {
                await _courseService.UpdateCourseAsync(dto.Id, dto.Name, dto.Code, dto.Description ?? "", dto.ManagedById);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex) { return new JsonResult(new { success = false, message = ex.Message }); }
        }

        public async Task<IActionResult> OnPostDeleteAsync([FromBody] CourseDeleteRequest dto)
        {
            if (!User.IsInRole("Admin"))
                return new JsonResult(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });

            try
            {
                await _courseService.DeleteCourseAsync(dto.Id);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex) { return new JsonResult(new { success = false, message = ex.Message }); }
        }

        public async Task<IActionResult> OnGetRowHtmlAsync(Guid id)
        {
            var course = await _courseService.GetCourseByIdAsync(id);
            if (course == null) return NotFound();
            return Partial("_CourseRow", course);
        }

        public async Task<IActionResult> OnGetCardHtmlAsync(Guid id)
        {
            var course = await _courseService.GetCourseByIdAsync(id);
            if (course == null) return NotFound();
            return Partial("_CourseCard", course);
        }
    }

    public class CourseCreateRequest 
    { 
        public string Code { get; set; } = ""; 
        public string Name { get; set; } = ""; 
        public string? Description { get; set; } 
        public Guid? ManagedById { get; set; }
    }
    public class CourseEditRequest : CourseCreateRequest { public Guid Id { get; set; } }
    public class CourseDeleteRequest { public Guid Id { get; set; } }
}
