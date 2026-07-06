using PRN222.Models;

namespace PRN222.Services.Interfaces
{
    public interface ICourseService
    {
        Task<Course> GetCourseByIdAsync(Guid courseId);
        Task<List<Course>> GetAllCoursesAsync();
        Task<List<Course>> GetCoursesForLecturerAsync(Guid lecturerId);
        Task<bool> IsLecturerAssignedToCourseAsync(Guid lecturerId, Guid courseId);
        Task<Course> CreateCourseAsync(string name, string code, string description, Guid? managedById = null);
        Task UpdateCourseAsync(Guid courseId, string name, string code, string description, Guid? managedById = null);
        Task DeleteCourseAsync(Guid courseId);
    }
}
