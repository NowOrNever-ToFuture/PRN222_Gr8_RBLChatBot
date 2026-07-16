using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PRN222.Models;

namespace PRN222.Services.Interfaces
{
    public interface IPaymentService
    {
        /// <summary>
        /// Retrieves all pricing packages available in the system.
        /// </summary>
        Task<List<PricingPackage>> GetAllPackagesAsync();

        /// <summary>
        /// Retrieves the current active subscription details for a specific user.
        /// </summary>
        Task<UserSubscription?> GetUserSubscriptionAsync(Guid userId);

        /// <summary>
        /// Creates a payment link using PayOS for a paid service package subscription.
        /// </summary>
        Task<string> CreatePaymentLinkAsync(Guid userId, Guid packageId, string returnUrl, string cancelUrl);

        /// <summary>
        /// Confirms the payment of a transaction and activates/extends the subscription.
        /// </summary>
        Task<(bool Success, string Message)> ConfirmPaymentAsync(string transactionCode);

        /// <summary>
        /// Assigns the default Free package to a user upon registration.
        /// </summary>
        Task AssignFreePackageAsync(Guid userId);

        /// <summary>
        /// Processes and verifies a webhook notification payload from PayOS.
        /// </summary>
        Task<(bool Success, string Message)> ProcessWebhookAsync(string webhookBodyJson);

        /// <summary>
        /// Deducts a specified amount from the user's remaining request quota.
        /// </summary>
        Task DeductQuotaAsync(Guid userId, int amount = 1);

        /// <summary>
        /// Updates an existing pricing package's configuration.
        /// </summary>
        Task<bool> UpdatePricingPackageAsync(Guid packageId, string name, int tokenQuota, double price, int durationDays, string description);

        /// <summary>
        /// Creates a new pricing package.
        /// </summary>
        Task<PricingPackage> CreatePricingPackageAsync(string name, int tokenQuota, double price, int durationDays, string description);

        /// <summary>
        /// Deletes an existing pricing package.
        /// </summary>
        Task<bool> DeletePricingPackageAsync(Guid packageId);

        /// <summary>
        /// Lấy thống kê doanh thu payment theo từng tháng trong một năm (cho biểu đồ).
        /// </summary>
        Task<List<MonthlyPaymentStatDto>> GetMonthlyPaymentStatsAsync(int year);

        /// <summary>
        /// Lấy danh sách các năm có giao dịch thanh toán.
        /// </summary>
        Task<List<int>> GetPaymentAvailableYearsAsync();

        /// <summary>
        /// Lấy tổng tiền đã thanh toán theo từng user (cho bảng chi tiết admin).
        /// </summary>
        Task<List<UserPaymentSummaryDto>> GetUserPaymentSummaryAsync();
    }

    public class MonthlyPaymentStatDto
    {
        public int Month { get; set; }
        public string MonthName { get; set; } = "";
        public double TotalRevenue { get; set; }
        public int TransactionCount { get; set; }
    }

    public class UserPaymentSummaryDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = "";
        public string Username { get; set; } = "";
        public double TotalAmountPaid { get; set; }
        public int SuccessfulTransactions { get; set; }
        public string CurrentPackage { get; set; } = "";
    }
}
