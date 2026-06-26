using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    // Hub rỗng — logic bắn event nằm trong BenchmarkRunnerService qua IHubContext
    public class BenchmarkHub : Hub { }

    public class BenchmarkRunnerService : IBenchmarkRunnerService
    {
        private readonly AppDbContext _dbContext;
        private readonly IChatService _chatService;
        private readonly ILlmService _llmService;
        private readonly ILlmJudgeService _llmJudgeService;
        private readonly IHubContext<BenchmarkHub> _hubContext;

        public BenchmarkRunnerService(
            AppDbContext dbContext,
            IChatService chatService,
            ILlmService llmService,
            ILlmJudgeService llmJudgeService,
            IHubContext<BenchmarkHub> hubContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
            _llmJudgeService = llmJudgeService ?? throw new ArgumentNullException(nameof(llmJudgeService));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public async Task<BenchmarkRun> RunBenchmarkAsync(string embeddingModel, string chunkingStrategy)
        {
            var overallStopwatch = Stopwatch.StartNew();

            // Bước 1: Tạo BenchmarkRun mới
            var benchmarkRun = new BenchmarkRun
            {
                Id = Guid.NewGuid(),
                RunDate = DateTime.UtcNow,
                EmbeddingModel = embeddingModel,
                ChunkingStrategy = chunkingStrategy,
                Status = "Running",
                ResultSummary = ""
            };

            _dbContext.BenchmarkRuns.Add(benchmarkRun);
            await _dbContext.SaveChangesAsync();

            // Bắn trạng thái bắt đầu
            await _hubContext.Clients.All.SendAsync("ReceiveProgress", 0, "Đang bắt đầu benchmark...");

            try
            {
                // Bước 2: Lấy danh sách TestQuestions
                var testQuestions = await _dbContext.TestQuestions
                    .Include(tq => tq.Course)
                    .ToListAsync();

                if (testQuestions.Count == 0)
                {
                    benchmarkRun.Status = "Failed";
                    benchmarkRun.ResultSummary = "Không có câu hỏi nào trong bảng TestQuestions.";
                    await _dbContext.SaveChangesAsync();
                    await _hubContext.Clients.All.SendAsync("ReceiveProgress", 100, "Lỗi: Không có câu hỏi.");
                    return benchmarkRun;
                }

                int totalQuestions = testQuestions.Count;
                double totalFaithfulness = 0;
                double totalRelevance = 0;
                long totalLatency = 0;

                // Bước 3: Loop qua từng câu hỏi
                for (int i = 0; i < totalQuestions; i++)
                {
                    var question = testQuestions[i];
                    var questionStopwatch = Stopwatch.StartNew();

                    string botAnswer = "";
                    double faithfulness = 0;
                    double relevance = 0;

                    try
                    {
                        // 3a. Tìm chunks liên quan (RAG retrieval) bằng keyword search
                        var relevantChunks = await _chatService.SearchChunksAsync(question.QuestionText);
                        string context = relevantChunks.Count > 0
                            ? string.Join("\n\n", relevantChunks.Select(c => c.Content))
                            : "Không tìm thấy ngữ cảnh liên quan.";

                        // 3b. Gọi LLM để lấy câu trả lời
                        string ragPrompt = $@"Dựa trên ngữ cảnh sau đây, hãy trả lời câu hỏi.

=== NGỮ CẢNH ===
{context}

=== CÂU HỎI ===
{question.QuestionText}

Hãy trả lời chính xác và chi tiết.";

                        botAnswer = await _llmService.GenerateChatResponseAsync(ragPrompt);

                        questionStopwatch.Stop();
                        long latencyMs = questionStopwatch.ElapsedMilliseconds;

                        // 3c. Gọi LLM Judge để chấm điểm
                        var judgeResult = await _llmJudgeService.JudgeAsync(
                            question.QuestionText, context, botAnswer);
                        
                        faithfulness = judgeResult.Faithfulness;
                        relevance = judgeResult.Relevance;

                        // 3d. Lưu BenchmarkResult
                        var result = new BenchmarkResult
                        {
                            Id = Guid.NewGuid(),
                            BenchmarkRunId = benchmarkRun.Id,
                            TestQuestionId = question.Id,
                            BotAnswer = botAnswer,
                            FaithfulnessScore = (float)faithfulness,
                            RelevanceScore = (float)relevance,
                            LatencyMs = latencyMs,
                            AnsweredDate = DateTime.UtcNow
                        };

                        _dbContext.BenchmarkResults.Add(result);
                        await _dbContext.SaveChangesAsync();

                        totalFaithfulness += faithfulness;
                        totalRelevance += relevance;
                        totalLatency += latencyMs;
                    }
                    catch (Exception ex)
                    {
                        botAnswer = $"[ERROR] {ex.Message}";
                        
                        // Nếu 1 câu lỗi, vẫn tiếp tục chạy các câu còn lại
                        var errorResult = new BenchmarkResult
                        {
                            Id = Guid.NewGuid(),
                            BenchmarkRunId = benchmarkRun.Id,
                            TestQuestionId = question.Id,
                            BotAnswer = botAnswer,
                            FaithfulnessScore = 0,
                            RelevanceScore = 0,
                            LatencyMs = questionStopwatch.ElapsedMilliseconds,
                            AnsweredDate = DateTime.UtcNow
                        };

                        _dbContext.BenchmarkResults.Add(errorResult);
                        await _dbContext.SaveChangesAsync();
                    }

                    // 3e. Bắn tiến độ % qua SignalR
                    int percentComplete = (int)((i + 1) * 100.0 / totalQuestions);
                    string progressMsg = $"Đã xử lý {i + 1}/{totalQuestions} câu hỏi...";
                    await _hubContext.Clients.All.SendAsync("ReceiveProgress", percentComplete, progressMsg);

                    // 3f. Bắn kết quả Live qua SignalR
                    await _hubContext.Clients.All.SendAsync("ReceiveLiveResult", 
                        i + 1, 
                        question.QuestionText, 
                        botAnswer, 
                        faithfulness, 
                        relevance);
                }

                // Bước 4: Cập nhật BenchmarkRun kết thúc
                overallStopwatch.Stop();
                double avgFaithfulness = totalQuestions > 0 ? totalFaithfulness / totalQuestions : 0;
                double avgRelevance = totalQuestions > 0 ? totalRelevance / totalQuestions : 0;
                double avgLatency = totalQuestions > 0 ? (double)totalLatency / totalQuestions : 0;

                benchmarkRun.Status = "Completed";
                benchmarkRun.TotalTimeMs = overallStopwatch.ElapsedMilliseconds;
                benchmarkRun.ResultSummary = $"Avg Faithfulness: {avgFaithfulness:F2}, Avg Relevance: {avgRelevance:F2}, Avg Latency: {avgLatency:F0}ms";

                await _dbContext.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("ReceiveProgress", 100,
                    $"✅ Hoàn tất! Faithfulness: {avgFaithfulness:F2}, Relevance: {avgRelevance:F2}, Latency: {avgLatency:F0}ms");

                return benchmarkRun;
            }
            catch (Exception ex)
            {
                overallStopwatch.Stop();
                benchmarkRun.Status = "Failed";
                benchmarkRun.TotalTimeMs = overallStopwatch.ElapsedMilliseconds;
                benchmarkRun.ResultSummary = $"Lỗi: {ex.Message}";
                await _dbContext.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("ReceiveProgress", 100, $"❌ Lỗi: {ex.Message}");

                return benchmarkRun;
            }
        }
    }
}
