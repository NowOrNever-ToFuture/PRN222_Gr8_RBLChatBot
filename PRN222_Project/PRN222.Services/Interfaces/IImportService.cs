namespace PRN222.Services.Interfaces
{
    public interface IImportService
    {
        /// <summary>
        /// Import câu hỏi từ file Excel (.xlsx) vào bảng TestQuestions.
        /// Logic Upsert: Update nếu đã tồn tại (trùng QuestionText + CourseId), Insert nếu chưa có.
        /// </summary>
        Task<(int Inserted, int Updated, int Errors)> ImportTestQuestionsFromExcelAsync(
            Stream fileStream, Guid courseId);
    }
}
