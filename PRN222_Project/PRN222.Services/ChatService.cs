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

        // Ngưỡng Cosine Similarity tối thiểu. Dưới ngưỡng này → từ chối trả lời.
        private const double SIMILARITY_THRESHOLD = 0.5;
        private const int TOP_K_CHUNKS = 5;

        // Các trạng thái document hợp lệ để tìm kiếm (có 2 flow indexing khác nhau)
        private static readonly string[] IndexedStatuses = { "Indexed", "Completed" };

        public ChatService(
            AppDbContext dbContext,
            ILlmService llmService,
            AiModelFactory aiModelFactory,
            ISystemSettingService systemSettingService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
            _aiModelFactory = aiModelFactory ?? throw new ArgumentNullException(nameof(aiModelFactory));
            _systemSettingService = systemSettingService ?? throw new ArgumentNullException(nameof(systemSettingService));
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
        public async Task<RagResponse> GenerateRagResponseAsync(string query, Guid userId, Guid? selectedCourseId = null, Guid? conversationId = null)
        {
            // Bước 0a: Phát hiện câu chào hỏi — trả lời tĩnh không qua RAG
            bool isGreeting = IsGreetingQuery(query);
            if (isGreeting)
            {
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

            // Bước 0b: Xử lý ngữ cảnh hội thoại bằng LLM (không so chuỗi keyword).
            // History CHỈ lấy từ conversation hiện tại — không trộn với các hội
            // thoại khác (khác môn) của cùng user.
            var history = conversationId.HasValue
                ? await GetConversationMessagesAsync(conversationId.Value, userId)
                : new List<Message>();
            var (searchQuery, responseInstruction) = await RewriteQueryWithHistoryAsync(history, query);

            // Bước 1: Môn học do người dùng chọn từ dropdown ("Tất cả môn" = null →
            // tìm trên toàn bộ tài liệu, không SmartRoute để tránh thu hẹp sai).
            Guid? courseId = selectedCourseId;

            // Bước 2: Vector Search — tìm theo câu đã chuẩn hóa (tiếng Việt);
            // nếu không có kết quả thì thử lại bằng câu nguyên văn (hữu ích khi
            // tài liệu viết bằng tiếng Anh và học viên cũng hỏi tiếng Anh).
            var scoredChunks = await SearchChunksByVectorAsync(searchQuery, courseId);
            if (scoredChunks.Count == 0 && !string.Equals(searchQuery, query, StringComparison.Ordinal))
            {
                scoredChunks = await SearchChunksByVectorAsync(query, courseId);
            }

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

                // Thu thập trích dẫn nguồn (kèm DocumentId + ChunkId để client
                // dựng link nhảy thẳng đến đúng vector chunk trong tài liệu)
                if (!sources.Any(s => s.FileName == chunk.Document.FileName && s.PageNumber == chunk.PageNumber))
                {
                    sources.Add(new SourceCitation
                    {
                        FileName = chunk.Document.FileName,
                        PageNumber = chunk.PageNumber,
                        ChunkIndex = chunk.ChunkIndex,
                        SimilarityScore = score,
                        DocumentId = chunk.DocumentId,
                        ChunkId = chunk.Id
                    });
                }
            }

            string context = contextBuilder.ToString();

            // Bước 5: Gọi LLM sinh câu trả lời
            // Đính kèm lịch sử gần nhất để bot hiểu ngữ cảnh hội thoại,
            // và yêu cầu bổ sung (ngôn ngữ/format) do bước rewrite tách ra.
            // Khớp ngôn ngữ trả lời với ngôn ngữ câu hỏi (tất định, không dựa LLM):
            // câu hỏi không có ký tự tiếng Việt → ép trả lời đúng ngôn ngữ câu hỏi.
            if (string.IsNullOrWhiteSpace(responseInstruction) && !ContainsVietnameseCharacters(query))
            {
                responseInstruction =
                    "Answer ENTIRELY in the same language as the student's original question below " +
                    "(e.g., an English question must get a fully English answer — do NOT answer in Vietnamese). " +
                    "Verbatim quotes from the document may stay in the document's original language.";
            }

            string historySection = BuildHistorySection(history);
            string instructionSection = !string.IsNullOrWhiteSpace(responseInstruction)
                ? $"\n\n=== YÊU CẦU BẮT BUỘC VỀ CÁCH TRẢ LỜI ===\n{responseInstruction}\n(Phải tuân thủ tuyệt đối: nếu yêu cầu trả lời bằng ngôn ngữ khác thì TOÀN BỘ câu trả lời phải viết bằng ngôn ngữ đó.)"
                : "";
            // Câu đã diễn giải (phục vụ retrieval) chỉ là chú thích phụ — câu hỏi
            // NGUYÊN VĂN mới quyết định ngôn ngữ trả lời.
            string rephrasedSection = !string.Equals(searchQuery, query, StringComparison.Ordinal)
                ? $"\n(Ý chính của câu hỏi, đã diễn giải đầy đủ ngữ cảnh: {searchQuery})"
                : "";

            string ragPrompt = $@"Bạn là một Trợ lý AI Học thuật kiêm Trợ giảng nhiệt tình và thân thiện.
Nhiệm vụ của bạn là giải thích và trả lời câu hỏi của học viên một cách thật TỰ NHIÊN, DỄ HIỂU và MẠCH LẠC.

=== NGUYÊN TẮC PHẢN HỒI ===
1. Hãy trả lời tự nhiên như một con người đang trực tiếp trò chuyện và hướng dẫn học viên (tránh giọng điệu máy móc, khô khan hoặc lặp lại nguyên văn một cách vô cảm).
2. Trình bày lời giải thích rõ ràng, khoa học bằng Markdown đẹp mắt (sử dụng bôi đậm để nhấn mạnh, danh sách gạch đầu dòng, danh sách đánh số cho các bước, ví dụ thực tế hoặc bảng biểu nếu phù hợp).
3. TUYỆT ĐỐI chỉ dùng thông tin trong NGỮ CẢNH TÀI LIỆU bên dưới. KHÔNG được dùng kiến thức bên ngoài, KHÔNG suy diễn hay bịa đặt thông tin không có trong tài liệu — kể cả khi bạn biết câu trả lời từ nguồn khác.
4. Trích dẫn khéo léo và tự nhiên tên tài liệu khi giải thích (ví dụ: ""Theo tài liệu [Tên tài liệu]..."", ""Ở chương này..."").
5. Nếu ngữ cảnh tài liệu chỉ trả lời được MỘT PHẦN câu hỏi, hãy trả lời phần đó và nói rõ phần nào tài liệu chưa đề cập. Nếu hoàn toàn không đủ thông tin, lịch sự nói rõ rằng tài liệu hiện tại chưa có nội dung này — không tự trả lời thay bằng kiến thức ngoài.
6. Nếu học viên yêu cầu trả lời bằng ngôn ngữ khác hoặc định dạng khác, hãy làm đúng yêu cầu đó (nội dung vẫn phải bám tài liệu).
7. NGÔN NGỮ TRẢ LỜI: mặc định phải dùng ĐÚNG ngôn ngữ của câu hỏi nguyên văn bên dưới — học viên hỏi bằng tiếng Anh thì TOÀN BỘ câu trả lời bằng tiếng Anh, hỏi bằng tiếng Việt thì trả lời tiếng Việt (bất kể tài liệu viết bằng ngôn ngữ nào). Riêng phần TRÍCH DẪN NGUYÊN VĂN từ tài liệu thì giữ nguyên ngôn ngữ gốc của tài liệu, có thể kèm giải thích bằng ngôn ngữ trả lời.{historySection}

=== NGỮ CẢNH TÀI LIỆU ===
{context}

=== CÂU HỎI CỦA HỌC VIÊN (NGUYÊN VĂN — trả lời bằng đúng ngôn ngữ này) ===
{query}{rephrasedSection}{instructionSection}

Hãy phản hồi một cách tự nhiên, mạch lạc, dễ hiểu và tràn đầy tinh thần hỗ trợ học thuật.";

            var (answer, inputTokens, outputTokens) = await _llmService.GenerateChatResponseWithUsageAsync(ragPrompt);

            // LƯU Ý: KHÔNG ghi TokenUsageLog ở đây. Việc log do caller (OnPostAsk)
            // đảm nhiệm với tên model thật từ SystemSettings — trước đây log ở cả
            // hai nơi khiến số liệu analytics bị nhân đôi.

            // Bước 6: Đính kèm trích dẫn nguồn cuối câu trả lời
            // (nhãn theo ngôn ngữ câu hỏi để khớp với ngôn ngữ câu trả lời)
            bool vietnameseLabels = ContainsVietnameseCharacters(query);
            var citationText = new StringBuilder();
            citationText.AppendLine();
            citationText.AppendLine("---");
            citationText.AppendLine(vietnameseLabels ? "📚 **Nguồn tham khảo:**" : "📚 **References:**");
            foreach (var source in sources.OrderBy(s => s.FileName).ThenBy(s => s.PageNumber))
            {
                citationText.AppendLine(vietnameseLabels
                    ? $"- 📄 *{source.FileName}*, Trang {source.PageNumber} (Độ liên quan: {source.SimilarityScore:P0})"
                    : $"- 📄 *{source.FileName}*, Page {source.PageNumber} (Relevance: {source.SimilarityScore:P0})");
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
            string normalized = query.Trim().TrimEnd('?', '!', '.', ',', ' ');
            return ExactGreetings.Contains(normalized);
        }

        // ========================================================================
        // Query rewriting theo lịch sử hội thoại (thay cho so chuỗi keyword)
        // ========================================================================

        /// <summary>
        /// Dùng LLM viết lại tin nhắn mới thành câu hỏi học thuật độc lập dựa trên
        /// lịch sử hội thoại, đồng thời tách yêu cầu về cách trả lời (ngôn ngữ/format).
        /// Ví dụ: lịch sử có "Tư bản tài chính là gì?" và tin nhắn mới là
        /// "hãy trả lời cho tôi bằng tiếng anh" → search_query = câu hỏi cũ,
        /// instruction = "trả lời bằng tiếng Anh".
        /// </summary>
        private async Task<(string SearchQuery, string Instruction)> RewriteQueryWithHistoryAsync(
            List<Message> history, string query)
        {
            var recentHistory = history
                .Where(m => !string.IsNullOrWhiteSpace(m.Content))
                .TakeLast(6)
                .Select(m => $"{(m.Role == "User" ? "Học viên" : "Trợ lý")}: {Truncate(m.Content, 400)}")
                .ToList();
            string historyBlock = recentHistory.Count > 0
                ? string.Join("\n", recentHistory)
                : "(chưa có — đây là tin nhắn đầu tiên)";

            string rewritePrompt = $@"Bạn là bộ tiền xử lý câu hỏi cho hệ thống RAG học thuật.

=== LỊCH SỬ HỘI THOẠI GẦN NHẤT ===
{historyBlock}

=== TIN NHẮN MỚI CỦA HỌC VIÊN ===
{query}

=== NHIỆM VỤ ===
1. ""search_query"": viết lại tin nhắn mới thành MỘT câu hỏi học thuật độc lập, đầy đủ ngữ cảnh, BẰNG TIẾNG VIỆT (nếu tin nhắn viết bằng ngôn ngữ khác, hãy DỊCH sang tiếng Việt) để tìm kiếm trong tài liệu.
   - Nếu tin nhắn mới tham chiếu đến nội dung trước đó (""nó"", ""cái đó"", ""giải thích thêm""...), hãy thay bằng nội dung cụ thể từ lịch sử.
   - Nếu tin nhắn mới CHỈ là yêu cầu về cách trả lời (ví dụ: ""trả lời bằng tiếng Anh"", ""ngắn gọn hơn"", ""trình bày dạng bảng"") áp dụng cho câu hỏi trước, hãy dùng lại CÂU HỎI HỌC THUẬT GẦN NHẤT trong lịch sử làm search_query.
2. ""instruction"": tách yêu cầu về ngôn ngữ/độ dài/định dạng trả lời nếu có, viết thành mệnh lệnh rõ ràng (ví dụ: ""Trả lời hoàn toàn bằng tiếng Anh"", ""Trình bày dạng bảng"").
   - QUAN TRỌNG: nếu tin nhắn mới KHÔNG viết bằng tiếng Việt và học viên không yêu cầu ngôn ngữ cụ thể, instruction PHẢI yêu cầu trả lời bằng đúng ngôn ngữ của tin nhắn (ví dụ tin nhắn tiếng Anh → ""Answer entirely in English"").
   - Không có gì để yêu cầu thì để chuỗi rỗng.

Chỉ trả về JSON đúng định dạng: {{""search_query"": ""..."", ""instruction"": ""...""}}";

            try
            {
                string response = await _llmService.GenerateChatResponseAsync(rewritePrompt);
                var jsonMatch = Regex.Match(response, @"\{[\s\S]*\}");
                if (!jsonMatch.Success)
                    return (query, "");

                using var document = JsonDocument.Parse(jsonMatch.Value);
                var root = document.RootElement;
                string searchQuery = root.TryGetProperty("search_query", out var sq)
                    ? sq.GetString() ?? "" : "";
                string instruction = root.TryGetProperty("instruction", out var ins)
                    ? ins.GetString() ?? "" : "";

                return (string.IsNullOrWhiteSpace(searchQuery) ? query : searchQuery.Trim(),
                        instruction.Trim());
            }
            catch
            {
                // LLM lỗi → dùng nguyên văn, flow vẫn chạy tiếp
                return (query, "");
            }
        }

        /// <summary>
        /// Dựng đoạn lịch sử hội thoại gần nhất chèn vào prompt sinh câu trả lời.
        /// </summary>
        private static string BuildHistorySection(List<Message> history)
        {
            if (history.Count == 0) return "";

            var recent = history
                .Where(m => !string.IsNullOrWhiteSpace(m.Content))
                .TakeLast(4)
                .Select(m => $"{(m.Role == "User" ? "Học viên" : "Trợ lý")}: {Truncate(m.Content, 300)}")
                .ToList();

            if (recent.Count == 0) return "";
            return $"\n\n=== LỊCH SỬ HỘI THOẠI GẦN NHẤT (để hiểu ngữ cảnh) ===\n{string.Join("\n", recent)}";
        }

        private static string Truncate(string text, int maxLength)
        {
            text = text.Trim();
            return text.Length <= maxLength ? text : text[..maxLength] + "…";
        }

        /// <summary>
        /// Câu có ký tự đặc trưng tiếng Việt (dấu thanh/chữ đ...) hay không —
        /// dùng để quyết định ngôn ngữ trả lời khớp với ngôn ngữ câu hỏi.
        /// </summary>
        private static bool ContainsVietnameseCharacters(string text)
        {
            const string vietnameseChars =
                "àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ";
            return text.ToLowerInvariant().Any(c => vietnameseChars.Contains(c));
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

        // ========================================================================
        // Quản lý hội thoại: mỗi conversation gắn với MỘT môn học
        // ========================================================================

        public async Task<List<Conversation>> GetConversationsAsync(Guid userId)
        {
            return await _dbContext.Conversations
                .Include(c => c.Course)
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.IsPinned) // hội thoại ghim luôn nằm trên
                .ThenByDescending(c => c.LastModifiedDate ?? c.CreatedDate)
                .ToListAsync();
        }

        public async Task<Conversation?> GetConversationAsync(Guid conversationId, Guid userId)
        {
            // Luôn kiểm tra quyền sở hữu — không cho đọc hội thoại của người khác
            return await _dbContext.Conversations
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);
        }

        public async Task<Conversation> CreateConversationAsync(Guid userId, Guid? courseId, string title)
        {
            title = string.IsNullOrWhiteSpace(title) ? "Hội thoại mới" : title.Trim();
            if (title.Length > 60) title = title[..60] + "…";

            var conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CourseId = courseId,
                Title = title,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };
            _dbContext.Conversations.Add(conversation);
            await _dbContext.SaveChangesAsync();
            return conversation;
        }

        public async Task<bool> DeleteConversationAsync(Guid conversationId, Guid userId)
        {
            var conversation = await _dbContext.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);
            if (conversation == null) return false;

            _dbContext.Conversations.Remove(conversation); // messages xóa theo (cascade)
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RenameConversationAsync(Guid conversationId, Guid userId, string newTitle)
        {
            if (string.IsNullOrWhiteSpace(newTitle)) return false;
            var conversation = await _dbContext.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);
            if (conversation == null) return false;

            newTitle = newTitle.Trim();
            if (newTitle.Length > 60) newTitle = newTitle[..60] + "…";
            conversation.Title = newTitle;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool?> TogglePinConversationAsync(Guid conversationId, Guid userId)
        {
            var conversation = await _dbContext.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);
            if (conversation == null) return null;

            conversation.IsPinned = !conversation.IsPinned;
            await _dbContext.SaveChangesAsync();
            return conversation.IsPinned;
        }

        public async Task<List<Message>> GetConversationMessagesAsync(Guid conversationId, Guid userId)
        {
            return await _dbContext.Messages
                .Where(m => m.ConversationId == conversationId
                    && m.Conversation.UserId == userId)
                .OrderBy(m => m.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<DocumentChunk>> GetChunksByIdsAsync(IEnumerable<Guid> chunkIds)
        {
            var ids = chunkIds.Distinct().ToList();
            if (ids.Count == 0) return new List<DocumentChunk>();

            return await _dbContext.DocumentChunks
                .Include(c => c.Document)
                .Where(c => ids.Contains(c.Id))
                .ToListAsync();
        }

        public async Task SaveMessageAsync(Guid conversationId, string role, string content, string citedChunkIds = "")
        {
            var message = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                Role = role,
                Content = content,
                CitedChunkIds = citedChunkIds,
                CreatedDate = DateTime.UtcNow
            };
            _dbContext.Messages.Add(message);

            var conversation = await _dbContext.Conversations.FindAsync(conversationId);
            if (conversation != null)
                conversation.LastModifiedDate = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
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
        public Guid DocumentId { get; set; }
        public Guid ChunkId { get; set; }
    }
}
