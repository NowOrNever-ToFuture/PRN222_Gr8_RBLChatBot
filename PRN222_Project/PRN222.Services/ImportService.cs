using ClosedXML.Excel;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class ImportService : IImportService
    {
        private readonly AppDbContext _dbContext;
        private readonly IHubContext<TestQuestionHub> _hubContext;

        public ImportService(AppDbContext dbContext, IHubContext<TestQuestionHub> hubContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public async Task<(int Inserted, int Updated, int Errors)> ImportTestQuestionsFromExcelAsync(
            Stream fileStream, Guid courseId)
        {
            int inserted = 0, updated = 0, errors = 0;

            try
            {
                using var workbook = new XLWorkbook(fileStream);
                var worksheet = workbook.Worksheets.First();

                // Bắt đầu từ dòng 2 (dòng 1 là header)
                int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

                for (int row = 2; row <= lastRow; row++)
                {
                    try
                    {
                        string questionText = worksheet.Cell(row, 1).GetString().Trim();
                        string answerOptions = worksheet.Cell(row, 2).GetString().Trim();
                        string groundTruth = worksheet.Cell(row, 3).GetString().Trim();
                        string explanation = worksheet.Cell(row, 4).GetString().Trim();
                        int difficulty = worksheet.Cell(row, 5).TryGetValue<int>(out var diff) ? diff : 1;

                        if (string.IsNullOrWhiteSpace(questionText))
                        {
                            errors++;
                            continue;
                        }

                        // Upsert: tìm theo QuestionText + CourseId
                        var existing = await _dbContext.TestQuestions
                            .FirstOrDefaultAsync(tq => tq.QuestionText == questionText && tq.CourseId == courseId);

                        if (existing != null)
                        {
                            // Update
                            existing.AnswerOptions = answerOptions;
                            existing.GroundTruth = groundTruth;
                            existing.Explanation = explanation;
                            existing.Difficulty = difficulty;

                            _dbContext.TestQuestions.Update(existing);
                            updated++;

                            await _hubContext.Clients.All.SendAsync("ReceiveUpdatedQuestion", existing);
                        }
                        else
                        {
                            // Insert
                            var newQuestion = new TestQuestion
                            {
                                Id = Guid.NewGuid(),
                                CourseId = courseId,
                                QuestionText = questionText,
                                AnswerOptions = answerOptions,
                                GroundTruth = groundTruth,
                                Explanation = explanation,
                                Difficulty = difficulty,
                                CreatedDate = DateTime.UtcNow
                            };

                            _dbContext.TestQuestions.Add(newQuestion);
                            inserted++;

                            await _hubContext.Clients.All.SendAsync("ReceiveNewQuestion", newQuestion);
                        }
                    }
                    catch
                    {
                        errors++;
                    }
                }

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Lỗi đọc file Excel: {ex.Message}", ex);
            }

            return (inserted, updated, errors);
        }
    }
}
