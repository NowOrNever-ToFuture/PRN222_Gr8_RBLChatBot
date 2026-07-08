using Microsoft.AspNetCore.SignalR;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    // Hub rỗng — logic bắn event nằm trong Service qua IHubContext
    public class PaymentHub : Hub { }

    public class PaymentService : IPaymentService
    {
        private readonly IHubContext<PaymentHub> _hubContext;

        public PaymentService(IHubContext<PaymentHub> hubContext)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }
    }
}
