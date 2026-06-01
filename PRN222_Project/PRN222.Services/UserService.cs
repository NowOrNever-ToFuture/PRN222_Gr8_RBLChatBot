using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _dbContext;
        private readonly IAccountService _accountService;

        public UserService(AppDbContext dbContext, IAccountService accountService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _dbContext.Users.OrderBy(u => u.Role).ThenBy(u => u.Username).ToListAsync();
        }

        public async Task<User?> GetUserByIdAsync(Guid id)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<(bool Success, string ErrorMessage)> CreateUserAsync(string username, string password, string role)
        {
            if (await _dbContext.Users.AnyAsync(u => u.Username == username))
            {
                return (false, "Tên đăng nhập này đã được sử dụng.");
            }

            try
            {
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Username = username,
                    FullName = username,
                    PasswordHash = _accountService.HashPassword(password),
                    Role = role
                };

                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi tạo tài khoản: {ex.Message}");
            }
        }

        public async Task<(bool Success, string ErrorMessage)> UpdateUserAsync(Guid id, string username, string role)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return (false, "Không tìm thấy tài khoản.");
            }

            if (username != user.Username && await _dbContext.Users.AnyAsync(u => u.Username == username))
            {
                return (false, "Tên đăng nhập này đã được sử dụng.");
            }

            try
            {
                user.Username = username;
                user.FullName = username;
                user.Role = role;

                _dbContext.Users.Update(user);
                await _dbContext.SaveChangesAsync();

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi cập nhật tài khoản: {ex.Message}");
            }
        }

        public async Task<(bool Success, string ErrorMessage)> DeleteUserAsync(Guid id, string currentUserId)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return (false, "Không tìm thấy tài khoản.");
            }

            if (currentUserId == user.Id.ToString())
            {
                return (false, "Bạn không thể xóa tài khoản đang đăng nhập.");
            }

            try
            {
                _dbContext.Users.Remove(user);
                await _dbContext.SaveChangesAsync();
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi xóa tài khoản: {ex.Message}");
            }
        }
    }
}
