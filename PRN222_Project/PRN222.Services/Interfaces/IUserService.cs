using PRN222.Models;

namespace PRN222.Services.Interfaces
{
    public interface IUserService
    {
        Task<List<User>> GetAllUsersAsync();
        Task<User?> GetUserByIdAsync(Guid id);
        Task<int> GetManagedCourseCountAsync(Guid userId);
        Task<List<Course>> GetManagedCoursesAsync(Guid userId);
        Task<(bool Success, string ErrorMessage)> CreateUserAsync(string username, string password, string role, Guid? courseId = null);
        Task<(bool Success, string ErrorMessage, int ClearedManagedCourseCount)> UpdateUserAsync(Guid id, string username, string role, Guid? courseId = null);
        Task<(bool Success, string ErrorMessage)> DeleteUserAsync(Guid id, string currentUserId);
    }
}
