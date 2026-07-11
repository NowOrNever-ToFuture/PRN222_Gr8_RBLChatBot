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
}
