using PRN222.Models;

namespace PRN222.Services.Interfaces
{
    public interface ITestQuestionService
    {
        Task<List<TestQuestion>> GetAllAsync();
        Task<TestQuestion?> GetByIdAsync(Guid id);
        Task<TestQuestion> CreateAsync(TestQuestion question);
        Task<TestQuestion> UpdateAsync(TestQuestion question);
        Task<bool> DeleteAsync(Guid id); // Hard Delete
    }
}
