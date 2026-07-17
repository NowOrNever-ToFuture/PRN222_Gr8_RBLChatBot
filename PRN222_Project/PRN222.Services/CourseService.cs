using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class CourseService : ICourseService
    {
        private readonly AppDbContext _dbContext;
        private readonly IHubContext<CourseHub> _hubContext;
        private readonly IHubContext<UserHub> _userHubContext;

        public CourseService(AppDbContext dbContext, IHubContext<CourseHub> hubContext, IHubContext<UserHub> userHubContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _userHubContext = userHubContext ?? throw new ArgumentNullException(nameof(userHubContext));
        }

        public async Task<Course> GetCourseByIdAsync(Guid courseId)
        {
            var course = await _dbContext.Courses
                .Include(c => c.Documents)
                .Include(c => c.TestQuestions)
                .Include(c => c.ManagedBy)
                .Include(c => c.CourseLecturers)
                    .ThenInclude(cl => cl.Lecturer)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null)
            {
                throw new InvalidOperationException($"Không tìm thấy môn học với ID {courseId}.");
            }

            return course;
        }

        public async Task<List<Course>> GetAllCoursesAsync()
        {
            return await _dbContext.Courses
                .Include(c => c.Documents)
                .Include(c => c.TestQuestions)
                .Include(c => c.ManagedBy)
                .Include(c => c.CourseLecturers)
                    .ThenInclude(cl => cl.Lecturer)
                .OrderBy(c => c.Code)
                .ToListAsync();
        }

        public async Task<List<Course>> GetCoursesForLecturerAsync(Guid lecturerId)
        {
            return await _dbContext.Courses
                .Include(c => c.ManagedBy)
                .Include(c => c.CourseLecturers)
                    .ThenInclude(cl => cl.Lecturer)
                .Where(c => c.ManagedById == lecturerId || c.CourseLecturers.Any(cl => cl.LecturerId == lecturerId))
                .OrderBy(c => c.Code)
                .ToListAsync();
        }

        public async Task<bool> IsLecturerAssignedToCourseAsync(Guid lecturerId, Guid courseId)
        {
            return await _dbContext.Courses
                .AnyAsync(c => c.Id == courseId && (c.ManagedById == lecturerId || c.CourseLecturers.Any(cl => cl.LecturerId == lecturerId)));
        }

        public async Task<Course> CreateCourseAsync(string name, string code, string description, Guid? managedById = null)
        {
            var existingCourse = await _dbContext.Courses
                .FirstOrDefaultAsync(c => c.Code == code);

            if (existingCourse != null)
            {
                throw new InvalidOperationException($"Mã môn học {code} đã tồn tại.");
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

            await EnsureHeadLecturerAlsoAssignedAsync(course.Id, managedById);

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

            return await GetCourseByIdAsync(course.Id);
        }

        public async Task UpdateCourseAsync(Guid courseId, string name, string code, string description, Guid? managedById = null)
        {
            var course = await _dbContext.Courses
                .Include(c => c.Documents)
                .Include(c => c.ManagedBy)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null)
            {
                throw new InvalidOperationException($"Không tìm thấy môn học với ID {courseId}.");
            }

            var existingCourse = await _dbContext.Courses
                .FirstOrDefaultAsync(c => c.Code == code && c.Id != courseId);

            if (existingCourse != null)
            {
                throw new InvalidOperationException($"Mã môn học {code} đã tồn tại.");
            }

            await ValidateManagedByAssignmentAsync(managedById, courseId);

            course.Name = name;
            course.Code = code;
            course.Description = description;
            course.ManagedById = managedById;

            await _dbContext.SaveChangesAsync();
            await EnsureHeadLecturerAlsoAssignedAsync(course.Id, managedById);

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

            var lecturerIds = await _dbContext.CourseLecturers
                .Where(cl => cl.CourseId == course.Id)
                .Select(cl => cl.LecturerId)
                .ToListAsync();

            foreach (var lecturerId in lecturerIds)
            {
                await _userHubContext.Clients.All.SendAsync("ReceiveUpdatedUser", new { id = lecturerId });
            }
        }

        public async Task DeleteCourseAsync(Guid courseId)
        {
            var course = await _dbContext.Courses
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null)
            {
                throw new InvalidOperationException($"Không tìm thấy môn học với ID {courseId}.");
            }

            if (course.Documents != null && course.Documents.Any())
            {
                throw new InvalidOperationException("Không thể xóa môn học này vì đã có tài liệu học tập bên trong.");
            }

            var lecturerIds = await _dbContext.CourseLecturers
                .Where(cl => cl.CourseId == courseId)
                .Select(cl => cl.LecturerId)
                .ToListAsync();

            _dbContext.Courses.Remove(course);
            await _dbContext.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("ReceiveDeletedCourse", courseId);

            foreach (var lecturerId in lecturerIds)
            {
                await _userHubContext.Clients.All.SendAsync("ReceiveUpdatedUser", new { id = lecturerId });
            }
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

            // Quy tắc: mỗi giảng viên chỉ được làm trưởng bộ môn của MỘT môn học.
            // Phải chuyển giao môn đang quản lý trước khi nhận môn mới.
            var alreadyManagedCourse = await _dbContext.Courses.FirstOrDefaultAsync(c =>
                c.ManagedById == managedById.Value
                && (!currentCourseId.HasValue || c.Id != currentCourseId.Value));
            if (alreadyManagedCourse != null)
            {
                throw new InvalidOperationException(
                    $"Giảng viên này đang là trưởng bộ môn của môn '{alreadyManagedCourse.Name}'. " +
                    "Hãy chuyển giao quyền quản lý môn đó trước khi gán môn mới.");
            }
        }

        private async Task EnsureHeadLecturerAlsoAssignedAsync(Guid courseId, Guid? managedById)
        {
            if (!managedById.HasValue)
            {
                return;
            }

            var exists = await _dbContext.CourseLecturers
                .AnyAsync(cl => cl.CourseId == courseId && cl.LecturerId == managedById.Value);

            if (exists)
            {
                return;
            }

            _dbContext.CourseLecturers.Add(new CourseLecturer
            {
                Id = Guid.NewGuid(),
                CourseId = courseId,
                LecturerId = managedById.Value
            });

            await _dbContext.SaveChangesAsync();
        }
    }

    public class CourseHub : Hub { }
}


