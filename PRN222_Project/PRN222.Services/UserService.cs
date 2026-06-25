using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class UserHub : Hub { }

    public class UserService : IUserService
    {
        private readonly AppDbContext _dbContext;
        private readonly IAccountService _accountService;
        private readonly IHubContext<UserHub> _hubContext;

        public UserService(AppDbContext dbContext, IAccountService accountService, IHubContext<UserHub> hubContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _dbContext.Users
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Username)
                .ToListAsync();
        }

        public async Task<User?> GetUserByIdAsync(Guid id)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<int> GetManagedCourseCountAsync(Guid userId)
        {
            return await _dbContext.Courses.CountAsync(c => c.ManagedById == userId);
        }

        public async Task<List<Course>> GetManagedCoursesAsync(Guid userId)
        {
            return await _dbContext.Courses
                .Where(c => c.ManagedById == userId)
                .OrderBy(c => c.Code)
                .ToListAsync();
        }

        public async Task<(bool Success, string ErrorMessage)> CreateUserAsync(string username, string password, string role, Guid? courseId = null)
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
                    Role = role,
                    CourseId = role == "Lecturer" ? courseId : null
                };

                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("ReceiveNewUser", new
                {
                    id = user.Id,
                    username = user.Username,
                    fullName = user.FullName,
                    role = user.Role,
                    courseId = user.CourseId
                });

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi tạo tài khoản: {ex.Message}");
            }
        }

        public async Task<(bool Success, string ErrorMessage, int ClearedManagedCourseCount)> UpdateUserAsync(Guid id, string username, string role, Guid? courseId = null)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return (false, "Không tìm thấy tài khoản.", 0);
            }

            if (username != user.Username && await _dbContext.Users.AnyAsync(u => u.Username == username))
            {
                return (false, "Tên đăng nhập này đã được sử dụng.", 0);
            }

            try
            {
                var clearedManagedCourseCount = 0;
                var isLeavingLecturerRole = string.Equals(user.Role, "Lecturer", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(role, "Lecturer", StringComparison.OrdinalIgnoreCase);

                if (isLeavingLecturerRole)
                {
                    var managedCourses = await _dbContext.Courses
                        .Where(c => c.ManagedById == user.Id)
                        .ToListAsync();

                    clearedManagedCourseCount = managedCourses.Count;
                    foreach (var course in managedCourses)
                    {
                        course.ManagedById = null;
                    }
                }

                user.Username = username;
                user.FullName = username;
                user.Role = role;
                user.CourseId = string.Equals(role, "Lecturer", StringComparison.OrdinalIgnoreCase) ? courseId : null;

                _dbContext.Users.Update(user);
                await _dbContext.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("ReceiveUpdatedUser", new
                {
                    id = user.Id,
                    username = user.Username,
                    fullName = user.FullName,
                    role = user.Role,
                    courseId = user.CourseId,
                    clearedManagedCourseCount
                });

                return (true, string.Empty, clearedManagedCourseCount);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi cập nhật tài khoản: {ex.Message}", 0);
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
                var deletedUserId = user.Id;
                _dbContext.Users.Remove(user);
                await _dbContext.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("ReceiveDeletedUser", deletedUserId);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi xóa tài khoản: {ex.Message}");
            }
        }
    }
}
