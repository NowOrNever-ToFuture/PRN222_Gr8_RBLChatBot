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
        public IndexModel(IDocumentService documentService)
        {
            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        }
        public List<PRN222.Models.Document> Documents { get; set; } = new();
        public async Task OnGetAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            if (userIdClaim == null || roleClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) return;
            Documents = await _documentService.GetDocumentsAsync(userId, roleClaim.Value);
        }
        public async Task<IActionResult> OnGetRowHtmlAsync(Guid id)
        {
            var doc = await _documentService.GetDocumentWithDetailsAsync(id);
            if (doc == null) return NotFound();
            return Partial("_DocumentRow", doc);
        }
        public async Task<IActionResult> OnPostIndexDocumentAsync(Guid id, string? chunkingStrategy)
        {
            try
            {
                var document = await _documentService.GetDocumentByIdAsync(id);
                if (document == null) return NotFound();
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                var roleClaim = User.FindFirst(ClaimTypes.Role);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();
                if (roleClaim?.Value != "Admin" && document.OwnerId != userId) return Forbid();
                await _documentService.IndexDocumentAsync(id, chunkingStrategy);
                TempData["SuccessMessage"] = $"Đã lập chỉ mục tệp '{document.FileName}' thành công. Chunking: {chunkingStrategy ?? "mặc định"}.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi: {ex.Message}";
            }
            return RedirectToPage();
        }
    }
}
