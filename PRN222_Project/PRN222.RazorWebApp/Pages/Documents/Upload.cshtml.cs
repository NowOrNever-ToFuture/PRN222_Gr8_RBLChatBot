using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.Services.DTOs;
using PRN222.Services.Interfaces;
using System.Security.Claims;

namespace PRN222.RazorWebApp.Pages.Documents
{
    [Authorize]
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
        public async Task<IActionResult> OnPostAsync()
        {
            Courses = await _courseService.GetAllCoursesAsync();
            if (Input?.File == null || Input.File.Length == 0)
            {
                ModelState.AddModelError("", "Vui lòng chọn tệp để tải lên.");
                return Page();
            }
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();
                string uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
                var document = await _documentService.UploadDocumentAsync(Input, uploadsPath, userId);
                TempData["SuccessMessage"] = $"Tải lên tệp '{document.FileName}' thành công! Trạng thái: {document.Status}";
                return RedirectToPage("/Documents/Index");
            }
            catch (InvalidOperationException ex) { ModelState.AddModelError("", $"Lỗi: {ex.Message}"); return Page(); }
            catch (Exception ex) { ModelState.AddModelError("", $"Lỗi không mong muốn: {ex.Message}"); return Page(); }
        }
    }
}
