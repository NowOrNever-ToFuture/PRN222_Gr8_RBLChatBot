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

        private readonly ICourseService _courseService;



        public ViewChunksModel(IDocumentService documentService, ICourseService courseService)

        {

            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));

            _courseService = courseService ?? throw new ArgumentNullException(nameof(courseService));

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

            if (roleClaim?.Value == "Lecturer")

            {

                var isCourseManager = Document.Course?.ManagedById == userId;

                if (!isCourseManager) return Forbid();

            }

            else if (roleClaim?.Value != "Admin")

            {

                return Forbid();

            }



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


