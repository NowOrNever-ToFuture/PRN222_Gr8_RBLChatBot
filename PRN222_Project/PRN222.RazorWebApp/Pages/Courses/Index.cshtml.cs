using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.Services.Interfaces;

namespace PRN222.RazorWebApp.Pages.Courses
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly ICourseService _courseService;
        public IndexModel(ICourseService courseService)
        {
            _courseService = courseService ?? throw new ArgumentNullException(nameof(courseService));
        }
        public List<PRN222.Models.Course> Courses { get; set; } = new();
        public async Task OnGetAsync() => Courses = await _courseService.GetAllCoursesAsync();

        public async Task<IActionResult> OnPostCreateAsync([FromBody] CourseCreateRequest dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Code) || string.IsNullOrWhiteSpace(dto?.Name))
                return new JsonResult(new { success = false, message = "Mã môn học và Tên môn học không được để trống." });
            try
            {
                var course = await _courseService.CreateCourseAsync(dto.Name, dto.Code, dto.Description ?? "");
                return new JsonResult(new { success = true, course = new { id = course.Id, code = course.Code, name = course.Name, description = course.Description } });
            }
            catch (Exception ex) { return new JsonResult(new { success = false, message = ex.Message }); }
        }

        public async Task<IActionResult> OnPostEditAsync([FromBody] CourseEditRequest dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Code) || string.IsNullOrWhiteSpace(dto?.Name))
                return new JsonResult(new { success = false, message = "Mã môn học và Tên môn học không được để trống." });
            try
            {
                await _courseService.UpdateCourseAsync(dto.Id, dto.Name, dto.Code, dto.Description ?? "");
                return new JsonResult(new { success = true });
            }
            catch (Exception ex) { return new JsonResult(new { success = false, message = ex.Message }); }
        }

        public async Task<IActionResult> OnPostDeleteAsync([FromBody] CourseDeleteRequest dto)
        {
            try
            {
                await _courseService.DeleteCourseAsync(dto.Id);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex) { return new JsonResult(new { success = false, message = ex.Message }); }
        }
    }

    public class CourseCreateRequest { public string Code { get; set; } = ""; public string Name { get; set; } = ""; public string? Description { get; set; } }
    public class CourseEditRequest : CourseCreateRequest { public Guid Id { get; set; } }
    public class CourseDeleteRequest { public Guid Id { get; set; } }
}
