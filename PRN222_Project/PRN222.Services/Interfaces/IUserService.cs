using PRN222.Models;

namespace PRN222.Services.Interfaces
{
    public interface IUserService
    {
        Task<List<User>> GetAllUsersAsync();
        Task<User?> GetUserByIdAsync(Guid id);
        Task<(bool Success, string ErrorMessage)> CreateUserAsync(string username, string password, string role);
        Task<(bool Success, string ErrorMessage)> UpdateUserAsync(Guid id, string username, string role);
        Task<(bool Success, string ErrorMessage)> DeleteUserAsync(Guid id, string currentUserId);
    }
}
