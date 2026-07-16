using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Mvc.RazorPages;

using PRN222.Services.Interfaces;

using System.Security.Claims;



namespace PRN222.RazorWebApp.Pages.Documents

{

    // Student được xem chunks (read-only) để kiểm chứng trích dẫn nguồn từ Chat.
    [Authorize]

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

        /// <summary>Chunk cần highlight khi đi từ trích dẫn trong Chat.</summary>
        public Guid? HighlightChunkId { get; set; }

        public bool IsReadOnlyViewer { get; set; }



        public async Task<IActionResult> OnGetAsync(Guid id, int page = 1, Guid? chunkId = null)

        {

            Document = await _documentService.GetDocumentWithDetailsAsync(id);

            if (Document == null) return NotFound();

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            var roleClaim = User.FindFirst(ClaimTypes.Role);

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();

            if (roleClaim?.Value == "Lecturer")

            {

                // Lecturer không quản lý môn này vẫn được xem read-only (kiểm chứng nguồn)
                IsReadOnlyViewer = Document.Course?.ManagedById != userId;

            }

            else if (roleClaim?.Value != "Admin")

            {

                // Student: chỉ xem để đối chiếu trích dẫn từ Chat
                IsReadOnlyViewer = true;

            }



            // Deep-link từ trích dẫn chat: tự tính trang chứa chunk cần xem
            if (chunkId.HasValue)

            {

                int position = await _documentService.GetChunkPositionAsync(id, chunkId.Value);

                if (position >= 0)

                {

                    page = position / PageSize + 1;

                    HighlightChunkId = chunkId.Value;

                }

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
