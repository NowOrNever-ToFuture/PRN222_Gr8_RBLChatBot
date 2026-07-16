using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
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
        private readonly IHubContext<ChatHub> _chatHubContext;

        public IndexModel(
            IChatService chatService,
            ICourseService courseService,
            IPaymentService paymentService,
            ITokenUsageService tokenUsageService,
            ISystemSettingService systemSettingService,
            IHubContext<ChatHub> chatHubContext)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _courseService = courseService ?? throw new ArgumentNullException(nameof(courseService));
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
            _tokenUsageService = tokenUsageService ?? throw new ArgumentNullException(nameof(tokenUsageService));
            _systemSettingService = systemSettingService ?? throw new ArgumentNullException(nameof(systemSettingService));
            _chatHubContext = chatHubContext ?? throw new ArgumentNullException(nameof(chatHubContext));
        }

        public Guid? CourseId { get; set; }
        public string? CourseName { get; set; }
        public UserSubscription? Subscription { get; set; }
        public List<Course> Courses { get; set; } = new();
        public int TodayTokenUsed { get; set; }
        public int DailyTokenLimit { get; set; } = 50000;

        public async Task<IActionResult> OnGetAsync(Guid? courseId)
        {
            CourseId = courseId;
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
                TodayTokenUsed = await _tokenUsageService.GetTodayUsageAsync(userId);
                
                if (Subscription?.PricingPackage != null)
                {
                    DailyTokenLimit = Subscription.PricingPackage.TokenQuota > 0 ? Subscription.PricingPackage.TokenQuota : 50000;
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAskAsync(string query, Guid? courseId, string? clientId = null)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new JsonResult(new { success = false, message = "Vui lòng nhập câu hỏi." });
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();
                var signalRUserId = userIdClaim.Value;

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

                // Đồng bộ đa tab: báo mọi tab của user này biết có câu hỏi mới
                // (tab gửi câu hỏi sẽ tự bỏ qua nhờ clientId).
                await _chatHubContext.Clients.User(signalRUserId)
                    .SendAsync("ReceiveUserMessage", clientId ?? "", query);

                // Generate TRƯỚC rồi mới lưu message: lịch sử hội thoại dùng cho
                // query-rewriting sẽ không lẫn chính câu đang hỏi.
                var ragResponse = await _chatService.GenerateRagResponseAsync(query, userId, courseId);
                await _chatService.SaveMessageAsync(userId, "User", query);
                string citedIds = string.Join(",", ragResponse.Sources.Select(s => s.ChunkId));
                await _chatService.SaveMessageAsync(userId, "Assistant", ragResponse.Answer, citedIds);

                // Stream câu trả lời qua SignalR: đẩy từng đoạn nhỏ để client
                // hiển thị dần (typing effect) trên TẤT CẢ tab của user.
                var streamMessageId = Guid.NewGuid().ToString("N");
                await _chatHubContext.Clients.User(signalRUserId)
                    .SendAsync("ReceiveAnswerStart", streamMessageId);
                foreach (var chunk in SplitIntoStreamChunks(ragResponse.Answer))
                {
                    await _chatHubContext.Clients.User(signalRUserId)
                        .SendAsync("ReceiveAnswerChunk", streamMessageId, chunk);
                    await Task.Delay(30); // nhịp gõ chữ, đủ mượt mà không chậm
                }

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
                var todayTokenUsed = await _tokenUsageService.GetTodayUsageAsync(userId);
                var sessionStartDate = updatedSub?.SessionStartDate?.ToString("o");

                var sourcePayload = ragResponse.Sources.Select(s => new
                {
                    fileName = s.FileName,
                    pageNumber = s.PageNumber,
                    score = s.SimilarityScore,
                    // URL nhảy thẳng đến đúng vector chunk đã dùng làm nguồn
                    chunkUrl = $"/Documents/ViewChunks/{s.DocumentId}?chunkId={s.ChunkId}",
                    // URL mở file gốc + scroll đến đúng trang (PDF hỗ trợ #page=N)
                    fileUrl = $"/uploads/{Uri.EscapeDataString(s.FileName)}#page={s.PageNumber}"
                }).ToList();

                // Kết thúc stream: gửi bản đầy đủ (answer + nguồn + quota) cho mọi tab
                await _chatHubContext.Clients.User(signalRUserId).SendAsync(
                    "ReceiveAnswerComplete",
                    streamMessageId,
                    ragResponse.Answer,
                    sourcePayload,
                    updatedQuota,
                    totalQuota,
                    packageName,
                    todayTokenUsed,
                    sessionStartDate);

                return new JsonResult(new
                {
                    success = true,
                    streamed = true,
                    streamMessageId,
                    response = ragResponse.Answer,
                    sources = sourcePayload,
                    courseId = ragResponse.CourseId,
                    remainingQuota = updatedQuota,
                    totalQuota = totalQuota,
                    packageName = packageName,
                    todayTokenUsed = todayTokenUsed,
                    sessionStartDate = sessionStartDate
                });
            }
            catch (Exception ex) { return new JsonResult(new { success = false, message = $"Lỗi: {ex.Message}" }); }
        }

        /// <summary>
        /// Cắt câu trả lời thành các đoạn nhỏ (~60 ký tự, cắt theo ranh giới từ)
        /// để stream qua SignalR tạo hiệu ứng hiện dần.
        /// </summary>
        private static IEnumerable<string> SplitIntoStreamChunks(string text, int chunkSize = 60)
        {
            for (int index = 0; index < text.Length;)
            {
                int length = Math.Min(chunkSize, text.Length - index);
                // Kéo dài đến hết từ hiện tại để không cắt đôi một từ
                int end = index + length;
                while (end < text.Length && !char.IsWhiteSpace(text[end]))
                    end++;
                yield return text[index..end];
                index = end;
            }
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
