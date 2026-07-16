using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.Models;
using PRN222.Services;
using PRN222.Services.Interfaces;
using System.Security.Claims;

namespace PRN222.RazorWebApp.Pages.Chat
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly IChatService _chatService;
        private readonly ICourseService _courseService;
        private readonly IPaymentService _paymentService;
        private readonly ITokenUsageService _tokenUsageService;
        private readonly ISystemSettingService _systemSettingService;

        public IndexModel(
            IChatService chatService,
            ICourseService courseService,
            IPaymentService paymentService,
            ITokenUsageService tokenUsageService,
            ISystemSettingService systemSettingService)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _courseService = courseService ?? throw new ArgumentNullException(nameof(courseService));
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
            _tokenUsageService = tokenUsageService ?? throw new ArgumentNullException(nameof(tokenUsageService));
            _systemSettingService = systemSettingService ?? throw new ArgumentNullException(nameof(systemSettingService));
        }

        public Guid? CourseId { get; set; }
        public string? CourseName { get; set; }
        public UserSubscription? Subscription { get; set; }
        public List<Course> Courses { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(Guid? courseId)
        {
            CourseId = courseId;
            // Tải toàn bộ danh sách môn học để hiển thị Course Picker
            Courses = await _courseService.GetAllCoursesAsync();

            if (courseId.HasValue)
            {
                var course = Courses.FirstOrDefault(c => c.Id == courseId.Value);
                if (course != null)
                    CourseName = $"{course.Name} ({course.Code})";
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
                var ragResponse = await _chatService.GenerateRagResponseAsync(query, courseId);
                string citedIds = string.Join(",", ragResponse.Sources.Select(s => s.ChunkIndex));
                await _chatService.SaveMessageAsync(userId, "Assistant", ragResponse.Answer, citedIds);

                // Ghi TokenUsageLog cho analytics
                if (ragResponse.InputTokens > 0 || ragResponse.OutputTokens > 0)
                {
                    var activeModel = await _systemSettingService.GetSettingValueAsync("ActiveLLM");
                    try
                    {
                        await _tokenUsageService.LogUsageAsync(
                            userId,
                            ragResponse.InputTokens,
                            ragResponse.OutputTokens,
                            activeModel ?? "gpt-4o-mini",
                            "Chat");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[TokenUsageLog] Lỗi ghi log: {ex.Message}");
                    }
                }

                // Deduct actual tokens (Input + Output) consumed
                int totalTokensUsed = Math.Max(1, ragResponse.InputTokens + ragResponse.OutputTokens);
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
                    sources = ragResponse.Sources.Select(s => new
                    {
                        fileName = s.FileName,
                        pageNumber = s.PageNumber,
                        score = s.SimilarityScore,
                        // URL để mở trực tiếp tài liệu + scroll đến đúng trang (PDF hỗ trợ #page=N)
                        fileUrl = $"/uploads/{Uri.EscapeDataString(s.FileName)}"
                    }),
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
