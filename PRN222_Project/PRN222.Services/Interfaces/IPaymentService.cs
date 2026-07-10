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
    }
}
