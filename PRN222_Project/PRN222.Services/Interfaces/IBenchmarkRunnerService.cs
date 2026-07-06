using PRN222.Models;

namespace PRN222.Services.Interfaces
{
    public interface IBenchmarkRunnerService
    {
        /// <summary>
        /// Chạy benchmark tự động: loop qua các TestQuestions, gọi RAG + LLM Judge, lưu kết quả.
        /// Bắn tiến độ % qua SignalR mỗi vòng lặp.
        /// </summary>
        Task<BenchmarkRun> RunBenchmarkAsync(string embeddingModel, string chunkingStrategy);
    }
}
