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
        private readonly IHubContext<CourseHub> _courseHubContext;

        public UserService(AppDbContext dbContext, IAccountService accountService, IHubContext<UserHub> hubContext, IHubContext<CourseHub> courseHubContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _courseHubContext = courseHubContext ?? throw new ArgumentNullException(nameof(courseHubContext));
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _dbContext.Users
                .Include(u => u.TeachingAssignments)
                    .ThenInclude(ta => ta.Course)
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Username)
                .ToListAsync();
        }

        public async Task<User?> GetUserByIdAsync(Guid id)
        {
            return await _dbContext.Users
                .Include(u => u.TeachingAssignments)
                    .ThenInclude(ta => ta.Course)
                .FirstOrDefaultAsync(u => u.Id == id);
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

        public async Task<List<Course>> GetAssignedCoursesAsync(Guid userId)
        {
            return await _dbContext.CourseLecturers
                .Where(cl => cl.LecturerId == userId)
                .Select(cl => cl.Course)
                .OrderBy(c => c.Code)
                .ToListAsync();
        }

        public async Task<(bool Success, string ErrorMessage)> CreateUserAsync(string username, string password, string role, IEnumerable<Guid>? assignedCourseIds = null)
        {
            if (await _dbContext.Users.AnyAsync(u => u.Username == username))
            {
                return (false, "Tên đăng nhập này đã được sử dụng.");
            }

            try
            {
                var normalizedCourseIds = await ValidateAssignedCoursesAsync(role, assignedCourseIds);

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

                await ReplaceTeachingAssignmentsAsync(user.Id, normalizedCourseIds);

                await _hubContext.Clients.All.SendAsync("ReceiveNewUser", new
                {
                    id = user.Id,
                    username = user.Username,
                    fullName = user.FullName,
                    role = user.Role,
                    assignedCourseIds = normalizedCourseIds
                });

                if (normalizedCourseIds != null && normalizedCourseIds.Any())
                {
                    foreach (var courseId in normalizedCourseIds)
                    {
                        await _courseHubContext.Clients.All.SendAsync("ReceiveUpdatedCourse", new { id = courseId });
                    }
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi tạo tài khoản: {ex.Message}");
            }
        }

        public async Task<(bool Success, string ErrorMessage, int ClearedManagedCourseCount)> UpdateUserAsync(Guid id, string username, string role, IEnumerable<Guid>? assignedCourseIds = null)
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
                var normalizedCourseIds = await ValidateAssignedCoursesAsync(role, assignedCourseIds);
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

                var oldCourseIds = await _dbContext.CourseLecturers
                    .Where(cl => cl.LecturerId == id)
                    .Select(cl => cl.CourseId)
                    .ToListAsync();

                user.Username = username;
                user.FullName = username;
                user.Role = role;

                _dbContext.Users.Update(user);
                await _dbContext.SaveChangesAsync();
                await ReplaceTeachingAssignmentsAsync(user.Id, normalizedCourseIds);

                await _hubContext.Clients.All.SendAsync("ReceiveUpdatedUser", new
                {
                    id = user.Id,
                    username = user.Username,
                    fullName = user.FullName,
                    role = user.Role,
                    assignedCourseIds = normalizedCourseIds,
                    clearedManagedCourseCount
                });

                var affectedCourseIds = oldCourseIds.Concat(normalizedCourseIds).Distinct().ToList();
                foreach (var courseId in affectedCourseIds)
                {
                    await _courseHubContext.Clients.All.SendAsync("ReceiveUpdatedCourse", new { id = courseId });
                }

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

            if (Guid.TryParse(currentUserId, out var currentUserGuid) && currentUserGuid == user.Id)
            {
                return (false, "Bạn không thể xóa tài khoản đang đăng nhập.");
            }

            try
            {
                var deletedUserId = user.Id;
                var affectedCourseIds = await _dbContext.CourseLecturers
                    .Where(cl => cl.LecturerId == id)
                    .Select(cl => cl.CourseId)
                    .ToListAsync();

                _dbContext.Users.Remove(user);
                await _dbContext.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("ReceiveDeletedUser", deletedUserId);

                foreach (var courseId in affectedCourseIds)
                {
                    await _courseHubContext.Clients.All.SendAsync("ReceiveUpdatedCourse", new { id = courseId });
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi xóa tài khoản: {ex.Message}");
            }
        }

        private async Task<List<Guid>> ValidateAssignedCoursesAsync(string role, IEnumerable<Guid>? assignedCourseIds)
        {
            if (!string.Equals(role, "Lecturer", StringComparison.OrdinalIgnoreCase))
            {
                return new List<Guid>();
            }

            var normalizedCourseIds = assignedCourseIds?
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList() ?? new List<Guid>();

            if (normalizedCourseIds.Count == 0)
            {
                return normalizedCourseIds;
            }

            var existingCourseIds = await _dbContext.Courses
                .Where(c => normalizedCourseIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync();

            if (existingCourseIds.Count != normalizedCourseIds.Count)
            {
                throw new InvalidOperationException("Danh sách môn học được gán có chứa giá trị không hợp lệ.");
            }

            return normalizedCourseIds;
        }

        private async Task ReplaceTeachingAssignmentsAsync(Guid userId, IEnumerable<Guid> assignedCourseIds)
        {
            var currentAssignments = await _dbContext.CourseLecturers
                .Where(cl => cl.LecturerId == userId)
                .ToListAsync();

            if (currentAssignments.Count > 0)
            {
                _dbContext.CourseLecturers.RemoveRange(currentAssignments);
            }

            foreach (var courseId in assignedCourseIds.Distinct())
            {
                _dbContext.CourseLecturers.Add(new CourseLecturer
                {
                    Id = Guid.NewGuid(),
                    LecturerId = userId,
                    CourseId = courseId
                });
            }

            await _dbContext.SaveChangesAsync();
        }
    }
}
