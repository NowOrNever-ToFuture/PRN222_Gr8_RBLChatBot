using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.Services.Interfaces;
using PRN222.Models;
using System.Security.Claims;

namespace PRN222.RazorWebApp.Pages.Chat
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly IChatService _chatService;
        private readonly ICourseService _courseService;
        private readonly IPaymentService _paymentService;

        public IndexModel(IChatService chatService, ICourseService courseService, IPaymentService paymentService)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _courseService = courseService ?? throw new ArgumentNullException(nameof(courseService));
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        }

        public Guid? CourseId { get; set; }
        public string? CourseName { get; set; }
        public UserSubscription? Subscription { get; set; }

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

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                Subscription = await _paymentService.GetUserSubscriptionAsync(userId);
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

                // Check quota limit
                var subscription = await _paymentService.GetUserSubscriptionAsync(userId);
                if (subscription == null || subscription.RemainingTokens <= 0)
                {
                    var isFreePkg = subscription?.PricingPackage?.Name == "Free";
                    var errMsg = isFreePkg
                        ? "Bạn đã hết hạn mức Token miễn phí trong ngày. Vui lòng đợi đến ngày hôm sau để hệ thống tự động reset lại 100 Tokens, hoặc nâng cấp lên gói VIP để tiếp tục trò chuyện ngay lập tức."
                        : "Tài khoản của bạn đã hết Token. Vui lòng nạp thêm gói cước để tiếp tục trò chuyện.";
                    return new JsonResult(new { success = false, message = errMsg });
                }

                await _chatService.SaveMessageAsync(userId, "User", query);
                var ragResponse = await _chatService.GenerateRagResponseAsync(query, userId, courseId);
                string citedIds = string.Join(",", ragResponse.Sources.Select(s => s.ChunkIndex));
                await _chatService.SaveMessageAsync(userId, "Assistant", ragResponse.Answer, citedIds);

                // Deduct actual tokens (Input + Output) consumed
                int totalTokensUsed = ragResponse.InputTokens + ragResponse.OutputTokens;
                await _paymentService.DeductQuotaAsync(userId, totalTokensUsed);

                // Retrieve updated subscription for client display
                var updatedSub = await _paymentService.GetUserSubscriptionAsync(userId);
                var updatedQuota = updatedSub?.RemainingTokens ?? 0;
                var totalQuota = updatedSub?.PricingPackage?.TokenQuota ?? 100;
                var packageName = updatedSub?.PricingPackage?.Name ?? "Free";

                return new JsonResult(new
                {
                    success = true,
                    response = ragResponse.Answer,
                    sources = ragResponse.Sources.Select(s => new { fileName = s.FileName, pageNumber = s.PageNumber, score = s.SimilarityScore }),
                    courseId = ragResponse.CourseId,
                    remainingQuota = updatedQuota,
                    totalQuota = totalQuota,
                    packageName = packageName
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
