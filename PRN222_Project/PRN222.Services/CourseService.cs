using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class CourseService : ICourseService
    {
        private readonly AppDbContext _dbContext;

        public CourseService(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<Course> GetCourseByIdAsync(Guid courseId)
        {
            var course = await _dbContext.Courses
                .Include(c => c.Documents)
                .Include(c => c.TestQuestions)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null)
                throw new InvalidOperationException($"Course with ID {courseId} not found.");

            return course;
        }

        public async Task<List<Course>> GetAllCoursesAsync()
        {
            return await _dbContext.Courses
                .Include(c => c.Documents)
                .Include(c => c.TestQuestions)
                .OrderBy(c => c.Code)
                .ToListAsync();
        }

        public async Task<Course> CreateCourseAsync(string name, string code, string description)
        {
            // Check if course code already exists
            var existingCourse = await _dbContext.Courses
                .FirstOrDefaultAsync(c => c.Code == code);

            if (existingCourse != null)
                throw new InvalidOperationException($"Course with code {code} already exists.");

            var course = new Course
            {
                Id = Guid.NewGuid(),
                Name = name,
                Code = code,
                Description = description,
                CreatedDate = DateTime.UtcNow
            };

            _dbContext.Courses.Add(course);
            await _dbContext.SaveChangesAsync();

            return course;
        }

        public async Task UpdateCourseAsync(Guid courseId, string name, string code, string description)
        {
            var course = await _dbContext.Courses.FindAsync(courseId);
            if (course == null)
                throw new InvalidOperationException($"Course with ID {courseId} not found.");

            // Check if new code conflicts with existing courses (excluding current course)
            var existingCourse = await _dbContext.Courses
                .FirstOrDefaultAsync(c => c.Code == code && c.Id != courseId);

            if (existingCourse != null)
                throw new InvalidOperationException($"Course with code {code} already exists.");

            course.Name = name;
            course.Code = code;
            course.Description = description;

            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteCourseAsync(Guid courseId)
        {
            var course = await _dbContext.Courses.FindAsync(courseId);
            if (course == null)
                throw new InvalidOperationException($"Course with ID {courseId} not found.");

            _dbContext.Courses.Remove(course);
            await _dbContext.SaveChangesAsync();
        }
    }
}
