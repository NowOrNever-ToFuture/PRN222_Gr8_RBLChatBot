using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.Services.Interfaces;
using System.Security.Claims;

namespace PRN222.RazorWebApp.Pages.Documents
{
    [Authorize(Roles = "Admin,Lecturer")]
    public class ViewChunksModel : PageModel
    {
        private readonly IDocumentService _documentService;
        private readonly IUserService _userService;
        public ViewChunksModel(IDocumentService documentService, IUserService userService)
        {
            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }
        public PRN222.Models.Document? Document { get; set; }
        public List<PRN222.Models.DocumentChunk> Chunks { get; set; } = new();
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public Guid DocumentId { get; set; }
        public async Task<IActionResult> OnGetAsync(Guid id, int page = 1)
        {
            Document = await _documentService.GetDocumentWithDetailsAsync(id);
            if (Document == null) return NotFound();
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();
            if (roleClaim?.Value != "Admin" && Document.Course?.ManagedById != userId) return Forbid();
            if (page < 1) page = 1;
            CurrentPage = page;
            DocumentId = id;
            var (chunks, totalCount) = await _documentService.GetDocumentChunksPagedAsync(id, page, PageSize);
            Chunks = chunks;
            TotalCount = totalCount;
            TotalPages = (int)Math.Ceiling((double)totalCount / PageSize);
            return Page();
        }
    }
}
