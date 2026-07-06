using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.Services.Interfaces;
using System.Security.Claims;

namespace PRN222.RazorWebApp.Pages.Chat
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly IChatService _chatService;
        private readonly ICourseService _courseService;

        public IndexModel(IChatService chatService, ICourseService courseService)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _courseService = courseService ?? throw new ArgumentNullException(nameof(courseService));
        }

        public Guid? CourseId { get; set; }
        public string? CourseName { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? courseId)
        {
            CourseId = courseId;
            if (courseId.HasValue)
            {
                try
                {
                    var course = await _courseService.GetCourseByIdAsync(courseId.Value);
                    CourseName = $"{course.Name} ({course.Code})";
                }
                catch { }
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAskAsync(string query, Guid? courseId)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new JsonResult(new { success = false, message = "Vui lòng nhập câu hỏi." });
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();
                await _chatService.SaveMessageAsync(userId, "User", query);
                var ragResponse = await _chatService.GenerateRagResponseAsync(query, courseId);
                string citedIds = string.Join(",", ragResponse.Sources.Select(s => s.ChunkIndex));
                await _chatService.SaveMessageAsync(userId, "Assistant", ragResponse.Answer, citedIds);
                return new JsonResult(new
                {
                    success = true,
                    response = ragResponse.Answer,
                    sources = ragResponse.Sources.Select(s => new { fileName = s.FileName, pageNumber = s.PageNumber, score = s.SimilarityScore }),
                    courseId = ragResponse.CourseId
                });
            }
            catch (Exception ex) { return new JsonResult(new { success = false, message = $"Lỗi: {ex.Message}" }); }
        }

        public async Task<IActionResult> OnGetHistoryAsync()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();
                var history = await _chatService.GetChatHistoryAsync(userId);
                var formattedHistory = history.Select(m => new { role = m.Role, content = m.Content, createdDate = m.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss") });
                return new JsonResult(new { success = true, history = formattedHistory });
            }
            catch (Exception ex) { return new JsonResult(new { success = false, message = $"Lỗi: {ex.Message}" }); }
        }

        public async Task<IActionResult> OnPostClearHistoryAsync()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();
                await _chatService.ClearChatHistoryAsync(userId);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex) { return new JsonResult(new { success = false, message = $"Lỗi: {ex.Message}" }); }
        }

        public async Task<IActionResult> OnPostSearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new JsonResult(new { success = false, message = "Vui lòng nhập câu hỏi." });
            try
            {
                var chunks = await _chatService.SearchChunksAsync(query);
                string response = _chatService.FormatSearchResults(chunks);
                return new JsonResult(new { success = true, response });
            }
            catch (Exception ex) { return new JsonResult(new { success = false, message = $"Lỗi: {ex.Message}" }); }
        }
    }
}
