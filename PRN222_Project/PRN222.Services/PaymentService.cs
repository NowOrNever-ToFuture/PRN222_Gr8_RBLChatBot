using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class PaymentHub : Hub { }

    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<PaymentHub> _hubContext;

        public PaymentService(
            AppDbContext dbContext,
            IConfiguration configuration,
            IHubContext<PaymentHub> hubContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public async Task<List<PricingPackage>> GetAllPackagesAsync()
        {
            return await _dbContext.PricingPackages
                .Where(p => p.IsActive)
                .OrderBy(p => p.Price)
                .ToListAsync();
        }

        public async Task<UserSubscription?> GetUserSubscriptionAsync(Guid userId)
        {
            var userExists = await _dbContext.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                return null;
            }

            var subscription = await _dbContext.UserSubscriptions
                .Include(us => us.PricingPackage)
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == "Active");

            if (subscription == null)
            {
                // Automatically assign Free package if none exists
                await AssignFreePackageAsync(userId);
                subscription = await _dbContext.UserSubscriptions
                    .Include(us => us.PricingPackage)
                    .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == "Active");
            }
            else if (subscription.EndDate < DateTime.UtcNow)
            {
                subscription.Status = "Expired";
                await _dbContext.SaveChangesAsync();

                // Automatically fallback to Free package
                await AssignFreePackageAsync(userId);

                // Retrieve the newly assigned Free subscription
                subscription = await _dbContext.UserSubscriptions
                    .Include(us => us.PricingPackage)
                    .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == "Active");
            }

            // Apply session reset logic if session has expired
            if (subscription != null && subscription.PricingPackage != null)
            {
                await CheckAndResetSessionQuotaAsync(subscription);
            }
 
            return subscription;
        }

        private async Task CheckAndResetSessionQuotaAsync(UserSubscription subscription)
        {
            if (subscription.SessionStartDate.HasValue && DateTime.UtcNow >= subscription.SessionStartDate.Value.AddHours(5))
            {
                int quotaLimit = subscription.PricingPackage.TokenQuota;
                
                // If user remaining tokens are below standard quota, reset to quota.
                // If they are still above due to accumulation (e.g. napping VIP), we preserve it.
                if (subscription.RemainingTokens < quotaLimit)
                {
                    subscription.RemainingTokens = quotaLimit;
                }
                
                // Reset session start date so the next message begins a new 5-hour session
                subscription.SessionStartDate = null;
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task<string> CreatePaymentLinkAsync(Guid userId, Guid packageId, string returnUrl, string cancelUrl)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found.");
            }

            var package = await _dbContext.PricingPackages.FindAsync(packageId);
            if (package == null)
            {
                throw new InvalidOperationException("Package not found.");
            }

            // Generate a unique order code
            long orderCode = DateTime.UtcNow.Ticks % 9007199254740991;

            var transaction = new PaymentTransaction
            {
                UserId = userId,
                PricingPackageId = packageId,
                TransactionCode = orderCode.ToString(),
                Amount = package.Price,
                PaymentMethod = "VNPay",
                Status = "Pending",
                CreatedDate = DateTime.UtcNow
            };

            _dbContext.PaymentTransactions.Add(transaction);
            await _dbContext.SaveChangesAsync();

            var tmnCode = _configuration["VNPay:TmnCode"];
            var hashSecret = _configuration["VNPay:HashSecret"];
            var baseUrl = _configuration["VNPay:BaseUrl"];

            // VNPay Parameters
            var payParams = new SortedList<string, string>(new VnPayCompare());
            payParams.Add("vnp_Version", "2.1.0");
            payParams.Add("vnp_Command", "pay");
            payParams.Add("vnp_TmnCode", tmnCode);
            payParams.Add("vnp_Amount", ((long)(package.Price * 100)).ToString()); // VNPay expects amount in cents
            payParams.Add("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            payParams.Add("vnp_CurrCode", "VND");
            payParams.Add("vnp_IpAddr", "127.0.0.1");
            payParams.Add("vnp_Locale", "vn");
            payParams.Add("vnp_OrderInfo", $"Thanh toan goi {package.Name}");
            payParams.Add("vnp_OrderType", "other");
            payParams.Add("vnp_ReturnUrl", returnUrl);
            payParams.Add("vnp_TxnRef", orderCode.ToString());

            // Build raw data to hash
            var rawData = new System.Text.StringBuilder();
            var paymentUrl = new System.Text.StringBuilder(baseUrl);
            paymentUrl.Append("?");

            foreach (var kv in payParams)
            {
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    rawData.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
                    paymentUrl.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
                }
            }

            // Remove trailing '&' from rawData
            if (rawData.Length > 0)
            {
                rawData.Length--;
            }

            // Calculate HMAC-SHA512 signature
            string secureHash = "";
            byte[] keyByte = System.Text.Encoding.UTF8.GetBytes(hashSecret);
            byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(rawData.ToString());
            using (var hmac = new System.Security.Cryptography.HMACSHA512(keyByte))
            {
                byte[] hashmessage = hmac.ComputeHash(messageBytes);
                secureHash = BitConverter.ToString(hashmessage).Replace("-", "");
            }

            paymentUrl.Append("vnp_SecureHash=" + secureHash);
            return paymentUrl.ToString();
        }

        public async Task<(bool Success, string Message)> ConfirmPaymentAsync(string transactionCode)
        {
            var transaction = await _dbContext.PaymentTransactions
                .FirstOrDefaultAsync(pt => pt.TransactionCode == transactionCode);

            if (transaction == null)
            {
                return (false, "Transaction not found.");
            }

            if (transaction.Status == "Success")
            {
                return (true, "Transaction already processed successfully.");
            }

            transaction.Status = "Success";

            var package = await _dbContext.PricingPackages.FindAsync(transaction.PricingPackageId);
            if (package == null)
            {
                return (false, "Pricing package not found.");
            }

            // Find current active subscription
            var subscription = await _dbContext.UserSubscriptions
                .FirstOrDefaultAsync(us => us.UserId == transaction.UserId && us.Status == "Active");

            if (subscription != null)
            {
                subscription.RemainingTokens += package.TokenQuota;
                subscription.EndDate = DateTime.UtcNow.AddDays(package.DurationDays);
                subscription.PricingPackageId = package.Id;
            }
            else
            {
                subscription = new UserSubscription
                {
                    UserId = transaction.UserId,
                    PricingPackageId = package.Id,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(package.DurationDays),
                    RemainingTokens = package.TokenQuota,
                    Status = "Active"
                };
                _dbContext.UserSubscriptions.Add(subscription);
            }

            await _dbContext.SaveChangesAsync();

            // Send SignalR update to the user
            await _hubContext.Clients.User(transaction.UserId.ToString())
                .SendAsync("ReceivePaymentConfirmed", package.Name, subscription.RemainingTokens);

            return (true, "Payment confirmed and subscription updated.");
        }

        public async Task AssignFreePackageAsync(Guid userId)
        {
            var userExists = await _dbContext.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                return;
            }

            var freePackage = await _dbContext.PricingPackages
                .FirstOrDefaultAsync(p => p.Price == 0 && p.IsActive);

            if (freePackage == null)
            {
                freePackage = new PricingPackage
                {
                    Name = "Free",
                    Description = "Default Free Tier",
                    Price = 0,
                    TokenQuota = 5000,
                    DurationDays = 36500,
                    IsActive = true
                };
                _dbContext.PricingPackages.Add(freePackage);
                await _dbContext.SaveChangesAsync();
            }

            var transaction = new PaymentTransaction
            {
                UserId = userId,
                PricingPackageId = freePackage.Id,
                TransactionCode = $"FREE_{Guid.NewGuid()}",
                Amount = 0,
                PaymentMethod = "SystemFree",
                Status = "Success",
                CreatedDate = DateTime.UtcNow
            };
            _dbContext.PaymentTransactions.Add(transaction);

            var subscription = new UserSubscription
            {
                UserId = userId,
                PricingPackageId = freePackage.Id,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(freePackage.DurationDays),
                RemainingTokens = freePackage.TokenQuota,
                Status = "Active"
            };
            _dbContext.UserSubscriptions.Add(subscription);

            await _dbContext.SaveChangesAsync();
        }

        public async Task<(bool Success, string Message)> ProcessWebhookAsync(string webhookBodyJson)
        {
            try
            {
                var dict = new Dictionary<string, string>();
                if (webhookBodyJson.StartsWith("{"))
                {
                    dict = JsonSerializer.Deserialize<Dictionary<string, string>>(webhookBodyJson);
                }
                else
                {
                    // Parse query string
                    var query = System.Web.HttpUtility.ParseQueryString(webhookBodyJson);
                    foreach (string key in query.AllKeys)
                    {
                        if (key != null)
                        {
                            dict[key] = query[key];
                        }
                    }
                }

                if (dict == null || !dict.ContainsKey("vnp_SecureHash"))
                {
                    return (false, "Missing vnp_SecureHash");
                }

                var hashSecret = _configuration["VNPay:HashSecret"];
                var vnp_SecureHash = dict["vnp_SecureHash"];

                // Build raw data to verify signature
                var payParams = new SortedList<string, string>(new VnPayCompare());
                foreach (var kv in dict)
                {
                    if (kv.Key.StartsWith("vnp_") && kv.Key != "vnp_SecureHash" && kv.Key != "vnp_SecureHashType")
                    {
                        payParams.Add(kv.Key, kv.Value);
                    }
                }

                var rawData = new System.Text.StringBuilder();
                foreach (var kv in payParams)
                {
                    if (!string.IsNullOrEmpty(kv.Value))
                    {
                        rawData.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
                    }
                }
                if (rawData.Length > 0)
                {
                    rawData.Length--;
                }

                string secureHash = "";
                byte[] keyByte = System.Text.Encoding.UTF8.GetBytes(hashSecret);
                byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(rawData.ToString());
                using (var hmac = new System.Security.Cryptography.HMACSHA512(keyByte))
                {
                    byte[] hashmessage = hmac.ComputeHash(messageBytes);
                    secureHash = BitConverter.ToString(hashmessage).Replace("-", "");
                }

                if (!secureHash.Equals(vnp_SecureHash, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "Invalid signature");
                }

                // Check transaction status
                var responseCode = dict.GetValueOrDefault("vnp_ResponseCode");
                var transactionStatus = dict.GetValueOrDefault("vnp_TransactionStatus");

                if (responseCode == "00" && transactionStatus == "00")
                {
                    var orderRef = dict.GetValueOrDefault("vnp_TxnRef");
                    if (!string.IsNullOrEmpty(orderRef))
                    {
                        return await ConfirmPaymentAsync(orderRef);
                    }
                }

                return (false, $"Payment failed with ResponseCode {responseCode}");
            }
            catch (Exception ex)
            {
                return (false, $"Error processing VNPay callback: {ex.Message}");
            }
        }

        public async Task DeductQuotaAsync(Guid userId, int amount = 1)
        {
            var subscription = await GetUserSubscriptionAsync(userId);
            if (subscription != null)
            {
                // 1. Perform session reset checks if session has expired
                await CheckAndResetSessionQuotaAsync(subscription);

                // 2. Initialize session start time if this is the first message in the session
                if (!subscription.SessionStartDate.HasValue)
                {
                    subscription.SessionStartDate = DateTime.UtcNow;
                }

                // 3. Deduct the tokens
                subscription.RemainingTokens = Math.Max(0, subscription.RemainingTokens - amount);
                await _dbContext.SaveChangesAsync();

                // Broadcast quota update real-time via SignalR
                await _hubContext.Clients.User(userId.ToString())
                    .SendAsync("ReceiveQuotaUpdate", subscription.RemainingTokens, subscription.PricingPackage.TokenQuota, subscription.PricingPackage.Name);
            }
        }

        public async Task<bool> UpdatePricingPackageAsync(Guid packageId, string name, int tokenQuota, double price, int durationDays, string description)
        {
            var package = await _dbContext.PricingPackages.FindAsync(packageId);
            if (package == null) return false;

            package.Name = name;
            package.TokenQuota = tokenQuota;
            package.Price = price;
            package.DurationDays = durationDays;
            package.Description = description;

            await _dbContext.SaveChangesAsync();

            // Broadcast package updates to all clients
            await _hubContext.Clients.All.SendAsync("ReceivePackagesUpdated");

            return true;
        }

        public async Task<PricingPackage> CreatePricingPackageAsync(string name, int tokenQuota, double price, int durationDays, string description)
        {
            var package = new PricingPackage
            {
                Id = Guid.NewGuid(),
                Name = name,
                TokenQuota = tokenQuota,
                Price = price,
                DurationDays = durationDays,
                IsActive = true,
                Description = description
            };

            _dbContext.PricingPackages.Add(package);
            await _dbContext.SaveChangesAsync();

            // Broadcast package updates to all clients
            await _hubContext.Clients.All.SendAsync("ReceivePackagesUpdated");

            return package;
        }

        public async Task<bool> DeletePricingPackageAsync(Guid packageId)
        {
            var package = await _dbContext.PricingPackages.FindAsync(packageId);
            if (package == null) return false;

            _dbContext.PricingPackages.Remove(package);
            await _dbContext.SaveChangesAsync();

            // Broadcast package updates to all clients
            await _hubContext.Clients.All.SendAsync("ReceivePackagesUpdated");

            return true;
        }

        private static readonly string[] MonthNames = {
            "Th.1", "Th.2", "Th.3", "Th.4", "Th.5", "Th.6",
            "Th.7", "Th.8", "Th.9", "Th.10", "Th.11", "Th.12"
        };

        public async Task<List<MonthlyPaymentStatDto>> GetMonthlyPaymentStatsAsync(int year)
        {
            var rawData = await _dbContext.PaymentTransactions
                .Where(p => p.Status == "Success" && p.Amount > 0 && p.CreatedDate.Year == year)
                .GroupBy(p => p.CreatedDate.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    TotalRevenue = g.Sum(p => p.Amount),
                    TransactionCount = g.Count()
                })
                .ToListAsync();

            // Trả về đủ 12 tháng (tháng không có data = 0)
            return Enumerable.Range(1, 12).Select(m =>
            {
                var found = rawData.FirstOrDefault(r => r.Month == m);
                return new MonthlyPaymentStatDto
                {
                    Month = m,
                    MonthName = MonthNames[m - 1],
                    TotalRevenue = found?.TotalRevenue ?? 0,
                    TransactionCount = found?.TransactionCount ?? 0
                };
            }).ToList();
        }

        public async Task<List<int>> GetPaymentAvailableYearsAsync()
        {
            var years = await _dbContext.PaymentTransactions
                .Select(p => p.CreatedDate.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            if (!years.Any())
                years.Add(DateTime.UtcNow.Year);

            return years;
        }

        public async Task<List<UserPaymentSummaryDto>> GetUserPaymentSummaryAsync()
        {
            var paymentByUser = await _dbContext.PaymentTransactions
                .Where(p => p.Status == "Success" && p.Amount > 0)
                .GroupBy(p => p.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    TotalPaid = g.Sum(p => p.Amount),
                    TxCount = g.Count()
                })
                .ToListAsync();

            var subscriptions = await _dbContext.UserSubscriptions
                .Include(us => us.PricingPackage)
                .Where(us => us.Status == "Active")
                .ToListAsync();

            var users = await _dbContext.Users
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return users.Select(u =>
            {
                var pay = paymentByUser.FirstOrDefault(p => p.UserId == u.Id);
                var sub = subscriptions.FirstOrDefault(s => s.UserId == u.Id);
                return new UserPaymentSummaryDto
                {
                    UserId = u.Id,
                    FullName = u.FullName ?? u.Username,
                    Username = u.Username,
                    TotalAmountPaid = pay?.TotalPaid ?? 0,
                    SuccessfulTransactions = pay?.TxCount ?? 0,
                    CurrentPackage = sub?.PricingPackage?.Name ?? "Free"
                };
            }).ToList();
        }
    }
}
