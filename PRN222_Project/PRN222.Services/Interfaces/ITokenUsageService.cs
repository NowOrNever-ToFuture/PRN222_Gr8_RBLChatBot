using PRN222.Models;

namespace PRN222.Services.Interfaces
{
    public interface ITokenUsageService
    {
        /// <summary>
        /// Ghi log mỗi lần sử dụng token (sau mỗi lần gọi LLM).
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
        Task<List<UserTokenSummaryDto>> GetUserTokenSummaryAsync();
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

    public class UserTokenSummaryDto
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
