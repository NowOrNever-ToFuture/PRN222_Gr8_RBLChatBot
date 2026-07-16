using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PRN222.Services.Interfaces
{
    public interface ITokenUsageService
    {
        /// <summary>Ghi nhật ký token sau mỗi lần gọi LLM.</summary>
        Task LogAsync(Guid userId, int promptTokens, int completionTokens, string modelName, string feature);

        /// <summary>Lấy thống kê token 7 ngày gần nhất của user, nhóm theo ngày.</summary>
        Task<List<DailyTokenUsageDto>> GetWeeklyUsageAsync(Guid userId);

        /// <summary>Lấy top N user tiêu thụ nhiều token nhất trong 7 ngày (cho Admin).</summary>
        Task<List<UserTokenSummaryDto>> GetTopUsersAsync(int count = 10);

        /// <summary>Lấy tổng token theo từng model trong 7 ngày (cho Admin Pie Chart).</summary>
        Task<List<ModelTokenSummaryDto>> GetModelBreakdownAsync();

        /// <summary>Lấy tổng số token user đã dùng trong ngày hôm nay.</summary>
        Task<int> GetTodayUsageAsync(Guid userId);

        /// <summary>
        /// Ghi log mỗi lần sử dụng token (sau mỗi lần gọi LLM) - Alias gọi sang LogAsync.
        /// </summary>
        Task LogUsageAsync(Guid userId, int promptTokens, int completionTokens, string modelName, string feature);

        /// <summary>
        /// Lấy thống kê token theo từng tháng trong một năm (cho biểu đồ).
        /// </summary>
        Task<List<MonthlyTokenStatDto>> GetMonthlyTokenStatsAsync(int year);

        /// <summary>
        /// Lấy danh sách các năm có dữ liệu token log.
        /// </summary>
        Task<List<int>> GetAvailableYearsAsync();

        /// <summary>
        /// Lấy tổng token đã dùng theo từng user (cho bảng chi tiết admin).
        /// </summary>
        Task<List<AdminUserTokenSummaryDto>> GetUserTokenSummaryAsync();
    }

    public class DailyTokenUsageDto
    {
        public DateTime Date { get; set; }
        public int TotalTokens { get; set; }
    }

    public class UserTokenSummaryDto
    {
        public string FullName { get; set; } = string.Empty;
        public int TotalTokens { get; set; }
    }

    public class ModelTokenSummaryDto
    {
        public string ModelName { get; set; } = string.Empty;
        public int TotalTokens { get; set; }
    }

    public class MonthlyTokenStatDto
    {
        public int Month { get; set; }
        public string MonthName { get; set; } = "";
        public long TotalPromptTokens { get; set; }
        public long TotalCompletionTokens { get; set; }
        public long TotalTokens { get; set; }
        public int RequestCount { get; set; }
    }

    public class AdminUserTokenSummaryDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = "";
        public string Username { get; set; } = "";
        public long TotalTokensUsed { get; set; }
        public int TotalRequests { get; set; }
        public double TotalAmountPaid { get; set; }
        public string CurrentPackage { get; set; } = "";
        public int RemainingTokens { get; set; }
    }
}
