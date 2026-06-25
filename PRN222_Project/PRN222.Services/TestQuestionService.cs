using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class TestQuestionService : ITestQuestionService
    {
        private readonly AppDbContext _dbContext;
        private readonly IHubContext<TestQuestionHub> _hubContext;

        public TestQuestionService(AppDbContext dbContext, IHubContext<TestQuestionHub> hubContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public async Task<List<TestQuestion>> GetAllAsync()
        {
            return await _dbContext.TestQuestions
                .Include(tq => tq.Course)
                .OrderByDescending(tq => tq.CreatedDate)
                .ToListAsync();
        }

        public async Task<TestQuestion?> GetByIdAsync(Guid id)
        {
            return await _dbContext.TestQuestions
                .Include(tq => tq.Course)
                .FirstOrDefaultAsync(tq => tq.Id == id);
        }

        public async Task<TestQuestion> CreateAsync(TestQuestion question)
        {
            question.Id = Guid.NewGuid();
            question.CreatedDate = DateTime.UtcNow;

            _dbContext.TestQuestions.Add(question);
            await _dbContext.SaveChangesAsync();

            // Bắn sự kiện SignalR đến tất cả Clients
            await _hubContext.Clients.All.SendAsync("ReceiveNewQuestion", question);

            return question;
        }

        public async Task<TestQuestion> UpdateAsync(TestQuestion question)
        {
            var existing = await _dbContext.TestQuestions.FindAsync(question.Id);
            if (existing == null)
                throw new InvalidOperationException($"Không tìm thấy câu hỏi với ID {question.Id}.");

            existing.QuestionText = question.QuestionText;
            existing.AnswerOptions = question.AnswerOptions;
            existing.GroundTruth = question.GroundTruth;
            existing.Explanation = question.Explanation;
            existing.Difficulty = question.Difficulty;
            existing.CourseId = question.CourseId;

            _dbContext.TestQuestions.Update(existing);
            await _dbContext.SaveChangesAsync();

            // Bắn sự kiện SignalR
            await _hubContext.Clients.All.SendAsync("ReceiveUpdatedQuestion", existing);

            return existing;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var question = await _dbContext.TestQuestions.FindAsync(id);
            if (question == null)
                return false;

            _dbContext.TestQuestions.Remove(question);
            await _dbContext.SaveChangesAsync();

            // Bắn sự kiện SignalR
            await _hubContext.Clients.All.SendAsync("ReceiveDeletedQuestion", id);

            return true;
        }
    }

    // Hub rỗng — chỉ cần khai báo, logic bắn event nằm trong Service qua IHubContext
    public class TestQuestionHub : Hub { }
}
