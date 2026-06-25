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
        public DeleteModel(IDocumentService documentService)
        {
            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        }
        public PRN222.Models.Document? Document { get; set; }
        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            Document = await _documentService.GetDocumentWithDetailsAsync(id);
            if (Document == null) return NotFound();
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();
            if (roleClaim?.Value != "Lecturer" || Document.Course?.ManagedById != userId) return Forbid();
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

