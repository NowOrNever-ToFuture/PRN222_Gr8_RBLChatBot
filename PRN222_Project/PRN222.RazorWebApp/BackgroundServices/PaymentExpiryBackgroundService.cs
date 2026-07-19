using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PRN222.Repositories;

namespace PRN222.RazorWebApp.BackgroundServices
{
    public class PaymentExpiryBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);
        private readonly TimeSpan _expiryThreshold = TimeSpan.FromHours(24);

        public PaymentExpiryBackgroundService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var cutoffTime = DateTime.UtcNow.Subtract(_expiryThreshold);

                        var expiredTransactions = await dbContext.PaymentTransactions
                            .Where(t => t.Status == "Pending" && t.CreatedDate < cutoffTime)
                            .ToListAsync(stoppingToken);

                        if (expiredTransactions.Any())
                        {
                            foreach (var tx in expiredTransactions)
                            {
                                tx.Status = "Expired";
                            }
                            await dbContext.SaveChangesAsync(stoppingToken);
                            Console.WriteLine($"[PaymentExpiryBackgroundService] Expired {expiredTransactions.Count} pending transactions.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[PaymentExpiryBackgroundService] Error expiring payments: {ex.Message}");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
    }
}
