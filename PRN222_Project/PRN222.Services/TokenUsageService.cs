using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    // Hub rỗng — logic bắn event nằm trong Service qua IHubContext
    public class TokenUsageHub : Hub { }

    public class TokenUsageService : ITokenUsageService
    {
        private readonly AppDbContext _dbContext;
        private readonly IHubContext<TokenUsageHub> _hubContext;

        private static readonly string[] MonthNames = {
            "Th.1", "Th.2", "Th.3", "Th.4", "Th.5", "Th.6",
            "Th.7", "Th.8", "Th.9", "Th.10", "Th.11", "Th.12"
        };

        public TokenUsageService(AppDbContext dbContext, IHubContext<TokenUsageHub> hubContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        /// <inheritdoc/>
        public async Task LogUsageAsync(Guid userId, int promptTokens, int completionTokens, string modelName, string feature)
        {
            var log = new TokenUsageLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = promptTokens + completionTokens,
                ModelName = modelName ?? "unknown",
                Feature = feature ?? "Chat",
                CreatedDate = DateTime.UtcNow
            };
            _dbContext.TokenUsageLogs.Add(log);
            await _dbContext.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<List<MonthlyTokenStatDto>> GetMonthlyTokenStatsAsync(int year)
        {
            var rawData = await _dbContext.TokenUsageLogs
                .Where(t => t.CreatedDate.Year == year)
                .GroupBy(t => t.CreatedDate.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    TotalPrompt = (long)g.Sum(t => t.PromptTokens),
                    TotalCompletion = (long)g.Sum(t => t.CompletionTokens),
                    TotalTokens = (long)g.Sum(t => t.TotalTokens),
                    RequestCount = g.Count()
                })
                .ToListAsync();

            // Đảm bảo trả về đủ 12 tháng (tháng không có data = 0)
            var result = Enumerable.Range(1, 12).Select(m =>
            {
                var found = rawData.FirstOrDefault(r => r.Month == m);
                return new MonthlyTokenStatDto
                {
                    Month = m,
                    MonthName = MonthNames[m - 1],
                    TotalPromptTokens = found?.TotalPrompt ?? 0,
                    TotalCompletionTokens = found?.TotalCompletion ?? 0,
                    TotalTokens = found?.TotalTokens ?? 0,
                    RequestCount = found?.RequestCount ?? 0
                };
            }).ToList();

            return result;
        }

        /// <inheritdoc/>
        public async Task<List<int>> GetAvailableYearsAsync()
        {
            var years = await _dbContext.TokenUsageLogs
                .Select(t => t.CreatedDate.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            // Nếu chưa có data, trả về năm hiện tại
            if (!years.Any())
                years.Add(DateTime.UtcNow.Year);

            return years;
        }

        /// <inheritdoc/>
        public async Task<List<UserTokenSummaryDto>> GetUserTokenSummaryAsync()
        {
            // Query token usage per user
            var tokenByUser = await _dbContext.TokenUsageLogs
                .GroupBy(t => t.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    TotalTokens = (long)g.Sum(t => t.TotalTokens),
                    TotalRequests = g.Count()
                })
                .ToListAsync();

            // Query payment per user (chỉ giao dịch thành công, bỏ Free)
            var paymentByUser = await _dbContext.PaymentTransactions
                .Where(p => p.Status == "Success" && p.Amount > 0)
                .GroupBy(p => p.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    TotalPaid = g.Sum(p => p.Amount)
                })
                .ToListAsync();

            // Query active subscription per user
            var subscriptions = await _dbContext.UserSubscriptions
                .Include(us => us.PricingPackage)
                .Where(us => us.Status == "Active")
                .ToListAsync();

            // Query all users
            var users = await _dbContext.Users
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var result = users.Select(u =>
            {
                var tokenStat = tokenByUser.FirstOrDefault(t => t.UserId == u.Id);
                var paymentStat = paymentByUser.FirstOrDefault(p => p.UserId == u.Id);
                var sub = subscriptions.FirstOrDefault(s => s.UserId == u.Id);

                return new UserTokenSummaryDto
                {
                    UserId = u.Id,
                    FullName = u.FullName ?? u.Username,
                    Username = u.Username,
                    TotalTokensUsed = tokenStat?.TotalTokens ?? 0,
                    TotalRequests = tokenStat?.TotalRequests ?? 0,
                    TotalAmountPaid = paymentStat?.TotalPaid ?? 0,
                    CurrentPackage = sub?.PricingPackage?.Name ?? "Chưa có",
                    RemainingTokens = sub?.RemainingTokens ?? 0
                };
            }).ToList();

            return result;
        }
    }
}
