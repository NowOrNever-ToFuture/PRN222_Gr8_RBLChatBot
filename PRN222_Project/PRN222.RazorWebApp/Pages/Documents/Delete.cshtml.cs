using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Mvc.RazorPages;

using PRN222.Services.Interfaces;

using System.Security.Claims;



namespace PRN222.RazorWebApp.Pages.Documents

{

    [Authorize(Roles = "Lecturer")]

    public class DeleteModel : PageModel

    {

        private readonly IDocumentService _documentService;

        private readonly ICourseService _courseService;



        public DeleteModel(IDocumentService documentService, ICourseService courseService)

        {

            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));

            _courseService = courseService ?? throw new ArgumentNullException(nameof(courseService));

        }



        public PRN222.Models.Document? Document { get; set; }



        public async Task<IActionResult> OnGetAsync(Guid id)

        {

            Document = await _documentService.GetDocumentWithDetailsAsync(id);

            if (Document == null) return NotFound();

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            var roleClaim = User.FindFirst(ClaimTypes.Role);

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();

            if (roleClaim?.Value != "Lecturer") return Forbid();



            var isAssigned = await _courseService.IsLecturerAssignedToCourseAsync(userId, Document.CourseId);

            if (!isAssigned || Document.Course?.ManagedById != userId) return Forbid();

            return Page();

        }



        public async Task<IActionResult> OnPostAsync(Guid id)

        {

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            var roleClaim = User.FindFirst(ClaimTypes.Role);

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();

            var (success, errorMessage) = await _documentService.DeleteDocumentAsync(id, userId, roleClaim?.Value ?? "");

            if (!success) { TempData["ErrorMessage"] = errorMessage; return RedirectToPage("/Documents/Index", new { courseId = Document?.CourseId }); }

            TempData["SuccessMessage"] = "Xóa tệp thành công.";
            return RedirectToPage("/Documents/Index", new { courseId = Document?.CourseId });

        }

    }

}

