using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class BenchmarkHub : Hub { }

    public class BenchmarkRunnerService : IBenchmarkRunnerService
    {
        private static readonly (string Provider, string DisplayName)[] BenchmarkModels =
        {
            ("gpt", "GPT-4o-mini"),
            ("gemini", "Gemini-2.5-Flash"),
            ("qwen", "Qwen-2.5-1.5B-LoRA")
        };

        private readonly AppDbContext _dbContext;
        private readonly IChatService _chatService;
        private readonly ILlmProviderFactory _llmProviderFactory;
        private readonly ILlmJudgeService _llmJudgeService;
        private readonly IHubContext<BenchmarkHub> _hubContext;

        public BenchmarkRunnerService(
            AppDbContext dbContext,
            IChatService chatService,
            ILlmProviderFactory llmProviderFactory,
            ILlmJudgeService llmJudgeService,
            IHubContext<BenchmarkHub> hubContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _llmProviderFactory = llmProviderFactory ?? throw new ArgumentNullException(nameof(llmProviderFactory));
            _llmJudgeService = llmJudgeService ?? throw new ArgumentNullException(nameof(llmJudgeService));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public async Task<List<BenchmarkRun>> RunBenchmarkAsync(
            Guid benchmarkBatchId,
            string embeddingModel,
            string chunkingStrategy)
        {
            if (benchmarkBatchId == Guid.Empty)
                throw new ArgumentException("Benchmark batch ID must not be empty.", nameof(benchmarkBatchId));

            var runDate = DateTime.UtcNow;
            var runs = BenchmarkModels.Select(model => new BenchmarkRun
            {
                Id = Guid.NewGuid(),
                BenchmarkBatchId = benchmarkBatchId,
                RunDate = runDate,
                LlmModel = model.DisplayName,
                EmbeddingModel = embeddingModel,
                ChunkingStrategy = chunkingStrategy,
                Status = "Running",
                ResultSummary = string.Empty
            }).ToList();

            _dbContext.BenchmarkRuns.AddRange(runs);
            await _dbContext.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync(
                "ReceiveProgress",
                0,
                "Đang chuẩn bị benchmark GPT, Gemini và Qwen...");

            var testQuestions = await _dbContext.TestQuestions
                .Include(question => question.Course)
                .ToListAsync();

            if (testQuestions.Count == 0)
            {
                foreach (var run in runs)
                {
                    run.Status = "Failed";
                    run.ResultSummary = "Không có câu hỏi nào trong bảng TestQuestions.";
                }

                await _dbContext.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync(
                    "ReceiveProgress",
                    100,
                    "Lỗi: Không có câu hỏi benchmark.");
                return runs;
            }

            var statistics = runs.ToDictionary(run => run.Id, _ => new RunStatistics());
            var totalOperations = testQuestions.Count * BenchmarkModels.Length;
            var completedOperations = 0;

            for (var questionIndex = 0; questionIndex < testQuestions.Count; questionIndex++)
            {
                var question = testQuestions[questionIndex];

                // Retrieval chỉ chạy một lần; cả ba LLM nhận chính xác cùng context.
                var relevantChunks = await _chatService.SearchChunksAsync(question.QuestionText);
                var context = relevantChunks.Count > 0
                    ? string.Join("\n\n", relevantChunks.Select(chunk => chunk.Content))
                    : "Không tìm thấy ngữ cảnh liên quan.";

                var ragPrompt = BuildRagPrompt(question.QuestionText, context);

                for (var modelIndex = 0; modelIndex < BenchmarkModels.Length; modelIndex++)
                {
                    var model = BenchmarkModels[modelIndex];
                    var run = runs[modelIndex];
                    var runStatistics = statistics[run.Id];
                    var stopwatch = Stopwatch.StartNew();
                    string botAnswer;
                    double faithfulness = 0;
                    double relevance = 0;

                    try
                    {
                        var llmService = _llmProviderFactory.GetService(model.Provider);
                        botAnswer = await llmService.GenerateChatResponseAsync(ragPrompt);
                        stopwatch.Stop();

                        var judgeResult = await _llmJudgeService.JudgeAsync(
                            question.QuestionText,
                            context,
                            botAnswer);
                        faithfulness = judgeResult.Faithfulness;
                        relevance = judgeResult.Relevance;

                        runStatistics.TotalFaithfulness += faithfulness;
                        runStatistics.TotalRelevance += relevance;
                        runStatistics.SuccessCount++;
                    }
                    catch (Exception exception)
                    {
                        stopwatch.Stop();
                        botAnswer = $"[ERROR] {exception.Message}";
                        runStatistics.ErrorCount++;
                        Console.Error.WriteLine(
                            $"[Benchmark {model.DisplayName}] {exception.Message}");
                    }

                    runStatistics.TotalLatencyMs += stopwatch.ElapsedMilliseconds;
                    _dbContext.BenchmarkResults.Add(new BenchmarkResult
                    {
                        Id = Guid.NewGuid(),
                        BenchmarkRunId = run.Id,
                        TestQuestionId = question.Id,
                        BotAnswer = botAnswer,
                        FaithfulnessScore = (float)faithfulness,
                        RelevanceScore = (float)relevance,
                        LatencyMs = stopwatch.ElapsedMilliseconds,
                        AnsweredDate = DateTime.UtcNow
                    });

                    completedOperations++;
                    var percentComplete = (int)(completedOperations * 100.0 / totalOperations);
                    await _hubContext.Clients.All.SendAsync(
                        "ReceiveProgress",
                        percentComplete,
                        $"{model.DisplayName}: câu {questionIndex + 1}/{testQuestions.Count}");
                    await _hubContext.Clients.All.SendAsync(
                        "ReceiveLiveResult",
                        model.DisplayName,
                        questionIndex + 1,
                        question.QuestionText,
                        botAnswer,
                        faithfulness,
                        relevance);
                }

                await _dbContext.SaveChangesAsync();
            }

            foreach (var run in runs)
            {
                var runStatistics = statistics[run.Id];
                var divisor = Math.Max(runStatistics.SuccessCount, 1);
                var averageFaithfulness = runStatistics.TotalFaithfulness / divisor;
                var averageRelevance = runStatistics.TotalRelevance / divisor;
                var averageLatency = (double)runStatistics.TotalLatencyMs / testQuestions.Count;

                run.Status = runStatistics.SuccessCount == 0 ? "Failed" : "Completed";
                run.TotalTimeMs = runStatistics.TotalLatencyMs;
                run.ResultSummary =
                    $"Avg Faithfulness: {averageFaithfulness:F2}, " +
                    $"Avg Relevance: {averageRelevance:F2}, " +
                    $"Avg Latency: {averageLatency:F0}ms, " +
                    $"Errors: {runStatistics.ErrorCount}";
            }

            await _dbContext.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync(
                "ReceiveProgress",
                100,
                "✅ Hoàn tất benchmark GPT, Gemini và Qwen.");
            return runs;
        }

        private static string BuildRagPrompt(string question, string context)
        {
            return $$"""
                Dựa duy nhất trên ngữ cảnh sau đây, hãy trả lời câu hỏi bằng tiếng Việt.

                === NGỮ CẢNH ===
                {{context}}

                === CÂU HỎI ===
                {{question}}

                Hãy trả lời chính xác, rõ ràng và không bịa thông tin ngoài ngữ cảnh.
                """;
        }

        private sealed class RunStatistics
        {
            public double TotalFaithfulness { get; set; }
            public double TotalRelevance { get; set; }
            public long TotalLatencyMs { get; set; }
            public int SuccessCount { get; set; }
            public int ErrorCount { get; set; }
        }
    }
}
