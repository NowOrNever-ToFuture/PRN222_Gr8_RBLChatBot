using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
namespace PRN222.Services
{
    public class ChatService : IChatService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILlmService _llmService;
        private readonly AiModelFactory _aiModelFactory;
        private readonly ISystemSettingService _systemSettingService;
        private readonly ITokenUsageService _tokenUsageService;
        private readonly IHubContext<TokenUsageHub> _hubContext;

        // Ngưỡng Cosine Similarity tối thiểu. Dưới ngưỡng này → từ chối trả lời.
        private const double SIMILARITY_THRESHOLD = 0.5;
        private const int TOP_K_CHUNKS = 5;

        // Các trạng thái document hợp lệ để tìm kiếm (có 2 flow indexing khác nhau)
        private static readonly string[] IndexedStatuses = { "Indexed", "Completed" };

        public ChatService(
            AppDbContext dbContext,
            ILlmService llmService,
            AiModelFactory aiModelFactory,
            ISystemSettingService systemSettingService,
            ITokenUsageService tokenUsageService,
            IHubContext<TokenUsageHub> hubContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
            _aiModelFactory = aiModelFactory ?? throw new ArgumentNullException(nameof(aiModelFactory));
            _systemSettingService = systemSettingService ?? throw new ArgumentNullException(nameof(systemSettingService));
            _tokenUsageService = tokenUsageService ?? throw new ArgumentNullException(nameof(tokenUsageService));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        // ========================================================================
        // BƯỚC 1: SMART ROUTING — Tự động xác định môn học bằng LLM
        // ========================================================================

        /// <summary>
        /// Gọi LLM để phân loại câu hỏi thuộc môn học nào.
        /// Trả về CourseId nếu tìm thấy, null nếu không xác định được.
        /// </summary>
        public async Task<Guid?> SmartRouteAsync(string query)
        {
            // Lấy danh sách tất cả môn học để LLM biết các lựa chọn
            var courses = await _dbContext.Courses.ToListAsync();
            if (courses.Count == 0) return null;

            string courseList = string.Join("\n", courses.Select(c => $"- Mã: \"{c.Code}\", Tên: \"{c.Name}\""));

            string classifyPrompt = $@"Bạn là hệ thống phân loại câu hỏi. Dựa vào danh sách môn học bên dưới, hãy xác định câu hỏi sau thuộc môn nào.

=== DANH SÁCH MÔN HỌC ===
{courseList}

=== CÂU HỎI ===
{query}

=== YÊU CẦU ===
Chỉ trả về MÃ MÔN HỌC duy nhất (ví dụ: PRN222), không giải thích thêm. Nếu không xác định được, trả về ""UNKNOWN"".";

            try
            {
                string courseCode = (await _llmService.GenerateChatResponseAsync(classifyPrompt)).Trim();

                // Loại bỏ dấu ngoặc kép nếu LLM trả về có quotes
                courseCode = courseCode.Trim('"', '\'', ' ', '\n', '\r');

                if (string.IsNullOrEmpty(courseCode) || courseCode.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase))
                    return null;

                // Tìm Course theo Code (case-insensitive)
                var course = courses.FirstOrDefault(c =>
                    c.Code.Equals(courseCode, StringComparison.OrdinalIgnoreCase));

                return course?.Id;
            }
            catch
            {
                return null; // Nếu LLM lỗi, không filter theo môn
            }
        }

        // ========================================================================
        // BƯỚC 2: VECTOR SEARCH — Cosine Similarity + CourseId Filter + Threshold
        // ========================================================================

        /// <summary>
        /// Tìm kiếm chunks liên quan bằng Cosine Similarity trên vector embeddings.
        /// Có lọc theo CourseId và ngưỡng threshold để chống Hallucination.
        /// </summary>
        public async Task<List<(DocumentChunk Chunk, double Score)>> SearchChunksByVectorAsync(
            string query, Guid? courseId = null)
        {
            // Lấy embedding model đang active
            string activeModelName = await _systemSettingService.GetSettingValueAsync("ActiveEmbeddingModel");
            var embeddingService = _aiModelFactory.GetEmbeddingService(activeModelName);

            // Embedding câu hỏi thành vector
            float[] queryVector = await embeddingService.GenerateEmbeddingAsync(query);

            // Lấy tất cả chunks từ documents đã indexed
            var chunksQuery = _dbContext.DocumentChunks
                .Where(c => IndexedStatuses.Contains(c.Document.Status))
                .Include(c => c.Document);

            // *** BẮT BUỘC: Lọc theo CourseId nếu có ***
            if (courseId.HasValue)
            {
                chunksQuery = chunksQuery
                    .Where(c => c.Document.CourseId == courseId.Value)
                    .Include(c => c.Document);
            }

            var allChunks = await chunksQuery.ToListAsync();

            // Tính Cosine Similarity cho từng chunk
            var scoredChunks = new List<(DocumentChunk Chunk, double Score)>();

            foreach (var chunk in allChunks)
            {
                if (string.IsNullOrEmpty(chunk.VectorData))
                    continue;

                try
                {
                    // Deserialize JSON vector từ DB
                    float[]? chunkVector = JsonSerializer.Deserialize<float[]>(chunk.VectorData);
                    if (chunkVector == null || chunkVector.Length == 0)
                        continue;

                    double similarity = CosineSimilarity(queryVector, chunkVector);

                    // *** CHỐNG HALLUCINATION: Chỉ giữ chunks trên ngưỡng ***
                    if (similarity >= SIMILARITY_THRESHOLD)
                    {
                        scoredChunks.Add((chunk, similarity));
                    }
                }
                catch
                {
                    // Skip chunk nếu vector data bị lỗi
                    continue;
                }
            }

            // Sắp xếp theo điểm giảm dần, lấy top K
            return scoredChunks
                .OrderByDescending(x => x.Score)
                .Take(TOP_K_CHUNKS)
                .ToList();
        }

        // ========================================================================
        // BƯỚC 3: RAG RESPONSE — Gọi LLM sinh câu trả lời + Trích dẫn nguồn
        // ========================================================================

        /// <summary>
        /// Luồng RAG hoàn chỉnh: Smart Route → Vector Search → LLM Generate → Trích dẫn nguồn.
        /// </summary>
        public async Task<RagResponse> GenerateRagResponseAsync(string query, Guid userId, Guid? selectedCourseId = null)
        {
            // Bước 0: Phát hiện câu chào hỏi / hỏi ngoài phạm vi trước — trả lời ngay không qua RAG
            bool isGreeting = IsGreetingQuery(query);
            if (isGreeting)
            {
                // Trả lời chào hỏi tĩnh — KHÔNG gọi LLM (tránh lỗi 401 khi chưa có API key)
                string greetingAnswer = "👋 Xin chào! Tôi là **Trợ lý AI Học thuật RBL**.\n\n" +
                    "Tôi có thể giúp bạn tra cứu và trả lời các câu hỏi liên quan đến **nội dung tài liệu học tập** " +
                    "đã được tải lên hệ thống.\n\n" +
                    "💡 Hãy đặt câu hỏi cụ thể về nội dung môn học, ví dụ:\n" +
                    "- _\"Phép biện chứng duy vật gồm mấy quy luật?\"_\n" +
                    "- _\"Giải thích quy luật thống nhất và đấu tranh giữa các mặt đối lập\"_";

                return new RagResponse
                {
                    Answer = greetingAnswer,
                    Sources = new List<SourceCitation>(),
                    CourseId = null
                };
            }

            // Bước 1: Smart Routing — xác định môn học (nếu có selectedCourseId thì bỏ qua Smart Routing)
            Guid? courseId = selectedCourseId ?? await SmartRouteAsync(query);

            // Bước 2: Vector Search — tìm chunks liên quan
            var scoredChunks = await SearchChunksByVectorAsync(query, courseId);

            // Bước 3: Nếu không tìm thấy chunks → gọi LLM để từ chối lịch sự thay vì trả về message cứng
            if (scoredChunks.Count == 0)
            {
                // Kiểm tra DB có tài liệu chưa
                bool hasDocuments = await _dbContext.DocumentChunks.AnyAsync(c => IndexedStatuses.Contains(c.Document.Status));

                string refusalAnswer;
                if (!hasDocuments)
                {
                    refusalAnswer = "📚 Hệ thống hiện chưa có tài liệu nào được tải lên và lập chỉ mục. " +
                                    "Vui lòng liên hệ Admin để upload tài liệu học tập trước khi đặt câu hỏi.";
                }
                else
                {
                    // Trả lời từ chối tĩnh — KHÔNG gọi LLM (tránh lỗi 401 khi chưa có API key)
                    refusalAnswer = "⚠️ Xin lỗi, tôi không tìm thấy thông tin liên quan đến câu hỏi này " +
                        "trong tài liệu hiện có.\n\n" +
                        "Câu hỏi của bạn có thể nằm ngoài phạm vi nội dung đã được tải lên, " +
                        "hoặc tài liệu chưa được lập chỉ mục đầy đủ.\n\n" +
                        "💡 **Gợi ý:** Hãy thử đặt câu hỏi cụ thể hơn về nội dung môn học trong tài liệu đã upload.";
                }

                return new RagResponse
                {
                    Answer = refusalAnswer,
                    Sources = new List<SourceCitation>(),
                    CourseId = courseId
                };
            }

            // Bước 4: Ghép context từ các chunks đã tìm
            var contextBuilder = new StringBuilder();
            var sources = new List<SourceCitation>();

            foreach (var (chunk, score) in scoredChunks)
            {
                contextBuilder.AppendLine($"[Tài liệu: {chunk.Document.FileName}, Trang {chunk.PageNumber}]");
                contextBuilder.AppendLine(chunk.Content);
                contextBuilder.AppendLine();

                // Thu thập trích dẫn nguồn
                if (!sources.Any(s => s.FileName == chunk.Document.FileName && s.PageNumber == chunk.PageNumber))
                {
                    sources.Add(new SourceCitation
                    {
                        FileName = chunk.Document.FileName,
                        PageNumber = chunk.PageNumber,
                        ChunkIndex = chunk.ChunkIndex,
                        SimilarityScore = score
                    });
                }
            }

            string context = contextBuilder.ToString();

            // Bước 5: Gọi LLM sinh câu trả lời
            string ragPrompt = $@"Bạn là một Trợ lý AI Học thuật kiêm Trợ giảng nhiệt tình và thân thiện.
Nhiệm vụ của bạn là giải thích và trả lời câu hỏi của học viên một cách thật TỰ NHIÊN, DỄ HIỂU và MẠCH LẠC.

=== NGUYÊN TẮC PHẢN HỒI ===
1. Hãy trả lời tự nhiên như một con người đang trực tiếp trò chuyện và hướng dẫn học viên (tránh giọng điệu máy móc, khô khan hoặc lặp lại nguyên văn một cách vô cảm).
2. Trình bày lời giải thích rõ ràng, khoa học bằng Markdown đẹp mắt (sử dụng bôi đậm để nhấn mạnh, danh sách gạch đầu dòng, danh sách đánh số cho các bước, ví dụ thực tế hoặc bảng biểu nếu phù hợp).
3. Câu trả lời của bạn phải dựa HOÀN TOÀN vào ngữ cảnh tài liệu được cung cấp bên dưới. Không được tự bịa đặt thông tin nằm ngoài ngữ cảnh tài liệu.
4. Trích dẫn khéo léo và tự nhiên tên tài liệu khi giải thích (ví dụ: ""Theo tài liệu [Tên tài liệu]..."", ""Ở chương này..."").
5. Nếu ngữ cảnh tài liệu không chứa đủ thông tin để trả lời câu hỏi, hãy khéo léo và lịch sự xin lỗi học viên, nói rõ rằng cơ sở tài liệu hiện tại chưa hỗ trợ nội dung này, và có thể gợi ý họ đặt câu hỏi gần hơn với tài liệu học tập đã upload.

=== NGỮ CẢNH TÀI LIỆU ===
{context}

=== CÂU HỎI CỦA HỌC VIÊN ===
{query}

Hãy phản hồi bằng tiếng Việt một cách tự nhiên, mạch lạc, dễ hiểu và tràn đầy tinh thần hỗ trợ học thuật.";

            var (answer, inputTokens, outputTokens) = await _llmService.GenerateChatResponseWithUsageAsync(ragPrompt);

            try
            {
                int promptTokens = (int)Math.Ceiling(ragPrompt.Length / 4.0);
                int completionTokens = (int)Math.Ceiling(answer.Length / 4.0);
                
                await _tokenUsageService.LogAsync(userId, promptTokens, completionTokens, "gpt-4o-mini", "Chat");
                
                int todayUsed = await _tokenUsageService.GetTodayUsageAsync(userId);
                await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveTokenUpdate", todayUsed);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error logging tokens: {ex.Message}");
            }

            // Bước 6: Đính kèm trích dẫn nguồn cuối câu trả lời
            var citationText = new StringBuilder();
            citationText.AppendLine();
            citationText.AppendLine("---");
            citationText.AppendLine("📚 **Nguồn tham khảo:**");
            foreach (var source in sources.OrderBy(s => s.FileName).ThenBy(s => s.PageNumber))
            {
                citationText.AppendLine($"- 📄 *{source.FileName}*, Trang {source.PageNumber} (Độ liên quan: {source.SimilarityScore:P0})");
            }

            return new RagResponse
            {
                Answer = answer + citationText.ToString(),
                Sources = sources,
                CourseId = courseId,
                InputTokens = inputTokens,
                OutputTokens = outputTokens
            };
        }

        // ========================================================================
        // UTILS: Cosine Similarity
        // ========================================================================

        /// <summary>
        /// Tính Cosine Similarity giữa 2 vector.
        /// Kết quả: -1.0 (ngược chiều) → 0.0 (vuông góc) → 1.0 (cùng chiều).
        /// </summary>
        public static double CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length)
            {
                // Nếu chiều dài khác nhau, lấy min length
                int minLen = Math.Min(vectorA.Length, vectorB.Length);
                vectorA = vectorA[..minLen];
                vectorB = vectorB[..minLen];
            }

            double dotProduct = 0.0;
            double normA = 0.0;
            double normB = 0.0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                normA += vectorA[i] * vectorA[i];
                normB += vectorB[i] * vectorB[i];
            }

            double denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
            if (denominator == 0) return 0.0;

            return dotProduct / denominator;
        }

        // ========================================================================
        // Phát hiện câu chào hỏi (greeting detection)
        // ========================================================================

        /// <summary>
        /// Danh sách các câu chào hỏi exact match (toàn bộ câu hỏi phải trùng khớp).
        /// </summary>
        private static readonly HashSet<string> ExactGreetings = new(StringComparer.OrdinalIgnoreCase)
        {
            "xin chào", "chào", "hello", "hi", "hey", "helo", "chào bạn",
            "chào buổi sáng", "chào buổi chiều", "chào buổi tối",
            "good morning", "good afternoon", "good evening",
            "bạn là ai", "bạn tên gì", "bạn là ai vậy", "bạn tên là gì",
            "bạn có thể làm gì", "giúp tôi", "hỗ trợ",
            "giới thiệu về bản thân bạn", "giới thiệu bản thân",
            "bạn có tiêu thụ token nhiều không",
            "xin chào bạn", "hello bot", "hi bot"
        };

        /// <summary>
        /// Phát hiện câu hỏi là chào hỏi/giao tiếp xã giao, không phải truy vấn học thuật.
        /// Chỉ khớp khi toàn bộ câu hỏi (sau khi loại bỏ dấu câu cuối) trùng exact match.
        /// </summary>
        private static bool IsGreetingQuery(string query)
        {
            // Chuẩn hóa: trim + bỏ dấu câu cuối (? ! . ,)
            string normalized = query.Trim().TrimEnd('?', '!', '.', ',', ' ');

            // Câu quá ngắn (dưới 4 ký tự, ví dụ: "hi", "hey") → kiểm tra exact match
            if (normalized.Length <= 4)
            {
                return ExactGreetings.Contains(normalized);
            }

            // Exact match toàn bộ câu hỏi
            return ExactGreetings.Contains(normalized);
        }

        // ========================================================================
        // Backward compatibility: giữ hàm cũ cho BenchmarkRunnerService
        // ========================================================================

        /// <summary>
        /// Tìm chunks bằng keyword (backward compatible cho BenchmarkRunnerService).
        /// </summary>
        public async Task<List<DocumentChunk>> SearchChunksAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<DocumentChunk>();

            // Thử dùng Vector Search trước
            try
            {
                var vectorResults = await SearchChunksByVectorAsync(query);
                if (vectorResults.Count > 0)
                    return vectorResults.Select(x => x.Chunk).ToList();
            }
            catch
            {
                // Fallback sang keyword search nếu vector search lỗi
            }

            // Fallback: keyword search
            var keywords = ExtractKeywords(query);
            if (keywords.Count == 0)
                return new List<DocumentChunk>();

            var chunks = await _dbContext.DocumentChunks
                .Where(c => IndexedStatuses.Contains(c.Document.Status))
                .Include(c => c.Document)
                .ToListAsync();

            return chunks
                .Where(c => keywords.Any(kw =>
                    c.Content.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(c => CalculateRelevance(c.Content, keywords))
                .Take(TOP_K_CHUNKS)
                .ToList();
        }

        public string FormatSearchResults(List<DocumentChunk> chunks)
        {
            if (chunks.Count == 0)
                return "Không tìm thấy thông tin liên quan.";

            var response = new StringBuilder();
            response.AppendLine("📄 Kết quả tìm kiếm:\n");

            foreach (var chunk in chunks)
            {
                response.AppendLine($"<strong>Từ tài liệu: {chunk.Document.FileName} (Trang {chunk.PageNumber})</strong>");

                string content = chunk.Content.Length > 300
                    ? chunk.Content.Substring(0, 300) + "..."
                    : chunk.Content;

                response.AppendLine($"<em>{content}</em>");
                response.AppendLine();
            }

            return response.ToString();
        }

        // ========================================================================
        // Private helpers
        // ========================================================================

        private List<string> ExtractKeywords(string query)
        {
            var commonWords = new[] { "là", "và", "của", "có", "để", "được", "từ", "với", "như", "này",
                "cái", "gì", "nào", "những", "khi", "mà", "thì", "hay", "hoặc", "cơ", "dạo", "không", "a", "an", "the" };

            return Regex.Split(query.ToLower(), @"\W+")
                .Where(w => !string.IsNullOrWhiteSpace(w) && w.Length > 2)
                .Where(w => !commonWords.Contains(w))
                .Distinct()
                .ToList();
        }

        private int CalculateRelevance(string content, List<string> keywords)
        {
            int score = 0;
            foreach (var keyword in keywords)
            {
                var matches = Regex.Matches(content, Regex.Escape(keyword), RegexOptions.IgnoreCase);
                score += matches.Count;
            }
            return score;
        }

        public async Task<List<Message>> GetChatHistoryAsync(Guid userId)
        {
            var conversation = await _dbContext.Conversations
                .Include(c => c.Messages)
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedDate)
                .FirstOrDefaultAsync();

            if (conversation == null)
            {
                return new List<Message>();
            }

            return conversation.Messages.OrderBy(m => m.CreatedDate).ToList();
        }

        public async Task SaveMessageAsync(Guid userId, string role, string content, string citedChunkIds = "")
        {
            var conversation = await _dbContext.Conversations
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedDate)
                .FirstOrDefaultAsync();

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = "Hội thoại học tập",
                    CreatedDate = DateTime.UtcNow,
                    LastModifiedDate = DateTime.UtcNow
                };
                _dbContext.Conversations.Add(conversation);
                await _dbContext.SaveChangesAsync();
            }

            var message = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                Role = role,
                Content = content,
                CitedChunkIds = citedChunkIds,
                CreatedDate = DateTime.UtcNow
            };
            _dbContext.Messages.Add(message);
            conversation.LastModifiedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }

        public async Task ClearChatHistoryAsync(Guid userId)
        {
            var conversations = await _dbContext.Conversations
                .Where(c => c.UserId == userId)
                .ToListAsync();

            if (conversations.Any())
            {
                _dbContext.Conversations.RemoveRange(conversations);
                await _dbContext.SaveChangesAsync();
            }
        }
    }

    // ========================================================================
    // DTOs cho RAG Response
    // ========================================================================

    public class RagResponse
    {
        public string Answer { get; set; } = "";
        public List<SourceCitation> Sources { get; set; } = new();
        public Guid? CourseId { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
    }

    public class SourceCitation
    {
        public string FileName { get; set; } = "";
        public int PageNumber { get; set; }
        public int ChunkIndex { get; set; }
        public double SimilarityScore { get; set; }
    }
}
