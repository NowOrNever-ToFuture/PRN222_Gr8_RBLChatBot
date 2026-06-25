using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class CourseService : ICourseService
    {
        private readonly AppDbContext _dbContext;
        private readonly IHubContext<CourseHub> _hubContext;

        public CourseService(AppDbContext dbContext, IHubContext<CourseHub> hubContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public async Task<Course> GetCourseByIdAsync(Guid courseId)
        {
            var course = await _dbContext.Courses
                .Include(c => c.Documents)
                .Include(c => c.TestQuestions)
                .Include(c => c.ManagedBy)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null)
            {
                throw new InvalidOperationException($"Course with ID {courseId} not found.");
            }

            return course;
        }

        public async Task<List<Course>> GetAllCoursesAsync()
        {
            return await _dbContext.Courses
                .Include(c => c.Documents)
                .Include(c => c.TestQuestions)
                .Include(c => c.ManagedBy)
                .OrderBy(c => c.Code)
                .ToListAsync();
        }

        public async Task<Course> CreateCourseAsync(string name, string code, string description, Guid? managedById = null)
        {
            var existingCourse = await _dbContext.Courses
                .FirstOrDefaultAsync(c => c.Code == code);

            if (existingCourse != null)
            {
                throw new InvalidOperationException($"Course with code {code} already exists.");
            }

            await ValidateManagedByAssignmentAsync(managedById, null);

            var course = new Course
            {
                Id = Guid.NewGuid(),
                Name = name,
                Code = code,
                Description = description,
                CreatedDate = DateTime.UtcNow,
                ManagedById = managedById
            };

            _dbContext.Courses.Add(course);
            await _dbContext.SaveChangesAsync();

            string managerName = string.Empty;
            if (managedById.HasValue)
            {
                var manager = await _dbContext.Users.FindAsync(managedById.Value);
                managerName = manager?.FullName ?? string.Empty;
            }

            await _hubContext.Clients.All.SendAsync("ReceiveNewCourse", new
            {
                id = course.Id,
                code = course.Code,
                name = course.Name,
                description = course.Description,
                managedById = course.ManagedById,
                managerName,
                documentsCount = 0
            });

            return course;
        }

        public async Task UpdateCourseAsync(Guid courseId, string name, string code, string description, Guid? managedById = null)
        {
            var course = await _dbContext.Courses
                .Include(c => c.Documents)
                .Include(c => c.ManagedBy)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null)
            {
                throw new InvalidOperationException($"Course with ID {courseId} not found.");
            }

            var existingCourse = await _dbContext.Courses
                .FirstOrDefaultAsync(c => c.Code == code && c.Id != courseId);

            if (existingCourse != null)
            {
                throw new InvalidOperationException($"Course with code {code} already exists.");
            }

            await ValidateManagedByAssignmentAsync(managedById, courseId);

            course.Name = name;
            course.Code = code;
            course.Description = description;
            course.ManagedById = managedById;

            await _dbContext.SaveChangesAsync();

            string managerName = string.Empty;
            if (managedById.HasValue)
            {
                var manager = await _dbContext.Users.FindAsync(managedById.Value);
                managerName = manager?.FullName ?? string.Empty;
            }

            await _hubContext.Clients.All.SendAsync("ReceiveUpdatedCourse", new
            {
                id = course.Id,
                code = course.Code,
                name = course.Name,
                description = course.Description,
                managedById = course.ManagedById,
                managerName,
                documentsCount = course.Documents?.Count ?? 0
            });
        }

        public async Task DeleteCourseAsync(Guid courseId)
        {
            var course = await _dbContext.Courses
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null)
            {
                throw new InvalidOperationException($"Course with ID {courseId} not found.");
            }

            if (course.Documents != null && course.Documents.Any())
            {
                throw new InvalidOperationException("Không thể xóa môn học này vì đã có tài liệu học tập bên trong.");
            }

            _dbContext.Courses.Remove(course);
            await _dbContext.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("ReceiveDeletedCourse", courseId);
        }

        private async Task ValidateManagedByAssignmentAsync(Guid? managedById, Guid? currentCourseId)
        {
            if (!managedById.HasValue)
            {
                return;
            }

            var manager = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == managedById.Value);
            if (manager == null)
            {
                throw new InvalidOperationException("Không tìm thấy giảng viên được chọn.");
            }

            if (!string.Equals(manager.Role, "Lecturer", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Chỉ tài khoản có role Lecturer mới được gán làm trưởng bộ môn.");
            }

            var alreadyManagedCourse = await _dbContext.Courses
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ManagedById == managedById.Value && c.Id != currentCourseId);

            if (alreadyManagedCourse != null)
            {
                throw new InvalidOperationException(
                    $"Giảng viên này đang là trưởng bộ môn của {alreadyManagedCourse.Code} - {alreadyManagedCourse.Name}. Mỗi giảng viên chỉ được phụ trách tối đa 1 bộ môn.");
            }
        }
    }

    public class CourseHub : Hub { }
}
