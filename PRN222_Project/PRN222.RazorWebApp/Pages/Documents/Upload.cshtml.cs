using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.Services.DTOs;
using PRN222.Services.Interfaces;
using System.Security.Claims;

namespace PRN222.RazorWebApp.Pages.Documents
{
    [Authorize(Roles = "Admin,Lecturer")]
    public class UploadModel : PageModel
    {
        private readonly IDocumentService _documentService;
        private readonly ICourseService _courseService;
        private readonly IWebHostEnvironment _environment;
        public UploadModel(IDocumentService documentService, ICourseService courseService, IWebHostEnvironment environment)
        {
            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
            _courseService = courseService ?? throw new ArgumentNullException(nameof(courseService));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }
        [BindProperty]
        public UploadDocumentDTO Input { get; set; } = new();
        public List<PRN222.Models.Course> Courses { get; set; } = new();
        public async Task OnGetAsync() => Courses = await _courseService.GetAllCoursesAsync();

        /// <summary>
        /// Nhận file qua AJAX (XHR) để client có thể theo dõi tiến độ tải lên thực tế (byte đã gửi)
        /// và nhận log xử lý server-side theo thời gian thực qua SignalR (connectionId).
        /// </summary>
        public async Task<IActionResult> OnPostAsync([FromForm] string? connectionId)
        {
            if (Input?.File == null || Input.File.Length == 0)
            {
                return new JsonResult(new { success = false, message = "Vui lòng chọn tệp để tải lên." });
            }
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return new JsonResult(new { success = false, message = "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại." });

                string uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
                var document = await _documentService.UploadDocumentAsync(Input, uploadsPath, userId, connectionId);
                return new JsonResult(new
                {
                    success = true,
                    message = $"Tải lên tệp '{document.FileName}' thành công! Trạng thái: {document.Status}",
                    redirectUrl = Url.Content("~/Documents/Index")
                });
            }
            catch (InvalidOperationException ex) { return new JsonResult(new { success = false, message = $"Lỗi: {ex.Message}" }); }
            catch (Exception ex) { return new JsonResult(new { success = false, message = $"Lỗi không mong muốn: {ex.Message}" }); }
        }
    }
}
