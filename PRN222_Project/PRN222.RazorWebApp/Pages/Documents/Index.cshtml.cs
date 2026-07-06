using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Mvc.RazorPages;

using PRN222.Services.Interfaces;

using System.Security.Claims;



namespace PRN222.RazorWebApp.Pages.Documents

{

    [Authorize(Roles = "Admin,Lecturer,Student")]

    public class IndexModel : PageModel

    {

        private readonly IDocumentService _documentService;

        private readonly ICourseService _courseService;



        public IndexModel(IDocumentService documentService, ICourseService courseService)

        {

            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));

            _courseService = courseService ?? throw new ArgumentNullException(nameof(courseService));

        }



        public List<PRN222.Models.Document> Documents { get; set; } = new();

        public PRN222.Models.Course? SelectedCourse { get; set; }

        public Guid? CourseId { get; set; }

        public bool HasSelectedCourse => CourseId.HasValue && SelectedCourse != null;

        public bool IsCourseManager { get; set; }



        public async Task<IActionResult> OnGetAsync(Guid? courseId)

        {

            if (!courseId.HasValue)

            {

                return RedirectToPage("/Courses/Index");

            }



            CourseId = courseId;

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            var roleClaim = User.FindFirst(ClaimTypes.Role);

            if (userIdClaim == null || roleClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();



            try

            {

                SelectedCourse = await _courseService.GetCourseByIdAsync(courseId.Value);

                // Lecturers not assigned to the course will see completed documents in read-only mode (handled in DocumentService)



                Documents = await _documentService.GetDocumentsAsync(userId, roleClaim.Value, courseId);

                IsCourseManager = roleClaim.Value == "Lecturer" && SelectedCourse.ManagedById == userId;

            }

            catch

            {

                return RedirectToPage("/Courses/Index");

            }



            return Page();

        }



        public async Task<IActionResult> OnGetRowHtmlAsync(Guid id)

        {

            var doc = await _documentService.GetDocumentWithDetailsAsync(id);

            if (doc == null) return NotFound();

            return Partial("_DocumentRow", doc);

        }



        public async Task<IActionResult> OnPostIndexDocumentAsync(Guid id)

        {

            try

            {

                var document = await _documentService.GetDocumentWithDetailsAsync(id);

                if (document == null) return NotFound();

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                var roleClaim = User.FindFirst(ClaimTypes.Role);

                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();



                if (roleClaim?.Value != "Lecturer" || document.Course?.ManagedById != userId)

                {

                    TempData["ErrorMessage"] = "Bạn không có quyền lập chỉ mục tài liệu này vì bạn không phải là Trưởng bộ môn phụ trách.";

                    return RedirectToPage(new { courseId = document.CourseId });

                }



                await _documentService.IndexDocumentAsync(id, null);

                TempData["SuccessMessage"] = $"Đã bắt đầu lập chỉ mục tài liệu '{document.FileName}'.";

            }

            catch (Exception ex)

            {

                TempData["ErrorMessage"] = $"Lỗi lập chỉ mục: {ex.Message}";

            }

            return RedirectToPage(new { courseId = CourseId });

        }

    }

}

