using Microsoft.AspNetCore.SignalR;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    // Hub rỗng — logic bắn event nằm trong Service qua IHubContext
    public class TokenUsageHub : Hub { }

    public class TokenUsageService : ITokenUsageService
    {
        private readonly IHubContext<TokenUsageHub> _hubContext;

        public TokenUsageService(IHubContext<TokenUsageHub> hubContext)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }
    }
}
