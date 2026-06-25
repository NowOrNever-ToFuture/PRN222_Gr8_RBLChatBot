using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PRN222.Models;

namespace PRN222.Services.Interfaces
{
    public interface ICourseService
    {
        Task<Course> GetCourseByIdAsync(Guid courseId);
        Task<List<Course>> GetAllCoursesAsync();
        Task<Course> CreateCourseAsync(string name, string code, string description, Guid? managedById = null);
        Task UpdateCourseAsync(Guid courseId, string name, string code, string description, Guid? managedById = null);
        Task DeleteCourseAsync(Guid courseId);
    }
}
