using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class TokenUsageHub : Hub
    {
    }

    public class TokenUsageService : ITokenUsageService
    {
        private readonly AppDbContext _dbContext;

        public TokenUsageService(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task LogAsync(Guid userId, int promptTokens, int completionTokens, string modelName, string feature)
        {
            var log = new TokenUsageLog
            {
                UserId = userId,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = promptTokens + completionTokens,
                ModelName = modelName,
                Feature = feature,
                CreatedDate = DateTime.UtcNow
            };

            _dbContext.TokenUsageLogs.Add(log);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<List<DailyTokenUsageDto>> GetWeeklyUsageAsync(Guid userId)
        {
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7).Date;

            var logs = await _dbContext.TokenUsageLogs
                .Where(t => t.UserId == userId && t.CreatedDate >= sevenDaysAgo)
                .ToListAsync();

            var result = logs
                .GroupBy(t => t.CreatedDate.Date)
                .Select(g => new DailyTokenUsageDto
                {
                    Date = g.Key,
                    TotalTokens = g.Sum(t => t.TotalTokens)
                })
                .OrderBy(d => d.Date)
                .ToList();

            return result;
        }

        public async Task<List<UserTokenSummaryDto>> GetTopUsersAsync(int count = 10)
        {
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7).Date;

            var result = await _dbContext.TokenUsageLogs
                .Include(t => t.User)
                .Where(t => t.CreatedDate >= sevenDaysAgo)
                .GroupBy(t => new { t.UserId, t.User.FullName })
                .Select(g => new UserTokenSummaryDto
                {
                    FullName = g.Key.FullName,
                    TotalTokens = g.Sum(t => t.TotalTokens)
                })
                .OrderByDescending(u => u.TotalTokens)
                .Take(count)
                .ToListAsync();

            return result;
        }

        public async Task<List<ModelTokenSummaryDto>> GetModelBreakdownAsync()
        {
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7).Date;

            var result = await _dbContext.TokenUsageLogs
                .Where(t => t.CreatedDate >= sevenDaysAgo)
                .GroupBy(t => t.ModelName)
                .Select(g => new ModelTokenSummaryDto
                {
                    ModelName = g.Key,
                    TotalTokens = g.Sum(t => t.TotalTokens)
                })
                .OrderByDescending(m => m.TotalTokens)
                .ToListAsync();

            return result;
        }

        public async Task<int> GetTodayUsageAsync(Guid userId)
        {
            var today = DateTime.UtcNow.Date;
            
            var totalTokens = await _dbContext.TokenUsageLogs
                .Where(t => t.UserId == userId && t.CreatedDate >= today)
                .SumAsync(t => t.TotalTokens);

            return totalTokens;
        }
    }
}
