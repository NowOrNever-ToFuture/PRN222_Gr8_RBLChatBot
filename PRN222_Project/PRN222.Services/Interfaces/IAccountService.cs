using PRN222.Models;

namespace PRN222.Services.Interfaces
{
    public interface IAccountService
    {
        Task<User?> LoginAsync(string username, string password);
        Task<(bool Success, string ErrorMessage)> RegisterAsync(string username, string password);
        string HashPassword(string password);
        bool VerifyPassword(string passwordPlain, string passwordHash);
    }
}
