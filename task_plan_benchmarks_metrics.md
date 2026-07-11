# 📋 Task Plan cho AI Agent — PRN222 Group 8 Assignment 3

> **Hướng dẫn sử dụng:**
> Mỗi thành viên copy đúng phần task được phân công bên dưới, dán vào AI Agent của mình và ra lệnh thực thi.
> AI Agent sẽ tự đọc codebase, hiểu kiến trúc và triển khai theo đúng quy tắc trong file `AGENTS.md`.
>
> **Workspace:** `c:\Workspace\Prn222\Asm3\PRN222_Gr8_RBLChatBot`
> **Solution:** `PRN222_Project/PRN222_Project.sln`

---

> [!IMPORTANT]
> **Kết quả kiểm tra codebase (2026-07-09):**
> Chưa có **bất kỳ chức năng nào** liên quan đến token limit, quota, hay hiển thị số token còn lại cho User trong toàn bộ codebase.
> Các keyword `quota`, `RemainingToken`, `TokenLimit`, `TokenUsage`, `DailyLimit` đều trả về **0 kết quả**.
> → Cần triển khai đầy đủ từ đầu theo Task Plan bên dưới.

> [!NOTE]
> **Chính sách SignalR trong project này:**
> Mọi chức năng có dữ liệu cập nhật liên tục (token tiêu thụ, trạng thái thanh toán, tiến độ benchmark)
> **BẮT BUỘC phải sử dụng SignalR** để push real-time về client thay vì polling.
> Hub class đặt **rỗng** inline trong file service tương ứng. Logic bắn event nằm trong service.

---

> [!CAUTION]
> **QUY TẮC LÀM VIỆC NHÓM — BẮT BUỘC ĐỌC TRƯỚC:**
> **TASK 0 phải hoàn thành và merge vào `dev` TRƯỚC KHI bất kỳ thành viên nào bắt đầu Phase 1/2/3.**
> Sau khi Task 0 merge, các thành viên `git pull` và bắt đầu làm trên nhánh feature riêng.
> **Tuyệt đối không được sửa** `AppDbContext.cs`, `DependencyInjection.cs`, `Program.cs` sau Task 0.

---

## ⚡ PHASE 1 — Function 3: Multi-LLM Benchmark (GPT vs Gemini vs DeepSeek)

> ⚠️ **Yêu cầu:** Task 0 phải hoàn thành và `git pull` trước khi bắt đầu Phase này.
> **Nhánh:** `feature/benchmark-multimodel`

---

### 🤖 TASK 1.1 — ~~Database: Thêm cột `LlmModel` vào `BenchmarkRun`~~ ✅ Đã xử lý trong Task 0

```
Bạn là AI Agent lập trình .NET 8 cho dự án PRN222_Gr8_RBLChatBot.
Workspace: c:\Workspace\Prn222\Asm3\PRN222_Gr8_RBLChatBot

NHIỆM VỤ: Cập nhật Model và tạo Migration cho cột LlmModel

Các bước thực hiện:

1. Mở file PRN222_Project/PRN222.Models/BenchmarkRun.cs
   - Thêm thuộc tính mới sau trường ChunkingStrategy:
     public string LlmModel { get; set; } = "GPT";
   - Không được thêm bất kỳ annotation nào ([Required], [MaxLength]...) lên thuộc tính này.

2. Mở file PRN222_Project/PRN222.Repositories/AppDbContext.cs
   - Trong phương thức OnModelCreating(), tìm khối cấu hình entity BenchmarkRun
   - Thêm cấu hình cột LlmModel nếu cần (không bắt buộc nếu string thường)
   - Đảm bảo ResultSummary được cấu hình HasColumnType("nvarchar(max)") nếu chưa có

3. Tạo EF Core Migration từ thư mục PRN222_Project/:
   dotnet ef migrations add AddLlmModelToBenchmarkRun --project PRN222.Repositories --startup-project PRN222.RazorWebApp

4. Apply migration vào database:
   dotnet ef database update --project PRN222.Repositories --startup-project PRN222.RazorWebApp

5. Đảm bảo build thành công: dotnet build

RÀNG BUỘC:
- Target framework: net8.0, không dùng net9 hay net10
- Không sửa migration đã tồn tại trước đó
- Primary key dùng Guid với NEWSEQUENTIALID()
- Tuân thủ tuyệt đối file AGENTS.md trong workspace
```

---

### 🤖 TASK 1.2 — Service: Cập nhật `ILlmService` và `OpenAiService` để gọi LLM động

```
Bạn là AI Agent lập trình .NET 8 cho dự án PRN222_Gr8_RBLChatBot.
Workspace: c:\Workspace\Prn222\Asm3\PRN222_Gr8_RBLChatBot

NHIỆM VỤ: Cho phép gọi bất kỳ model nào (GPT/Gemini/DeepSeek) từ một phương thức duy nhất

Các bước thực hiện:

1. Mở file PRN222_Project/PRN222.Services/Interfaces/ILlmService.cs
   - Thêm phương thức mới với XML doc comment đầy đủ:
     /// <summary>
     /// Gọi LLM với model name cụ thể, dùng cho Multi-Model Benchmark.
     /// modelName: "GPT" | "Gemini" | "DeepSeek"
     /// </summary>
     Task<string> GenerateChatResponseWithModelAsync(string prompt, string modelName);

2. Mở file PRN222_Project/PRN222.Services/OpenAiService.cs
   - Lưu credential và options thành private fields trong class:
     private readonly System.ClientModel.ApiKeyCredential _credential;
     private readonly OpenAIClientOptions _options;
   - Cập nhật constructor để gán _credential và _options thay vì tạo inline
   - Implement phương thức GenerateChatResponseWithModelAsync:
     - Map modelName thành tên model thực tế:
       * "GPT"      → config["AIProviders:OpenAI:ChatModel"] (mặc định "gpt-4o-mini")
       * "Gemini"   → "gemini-1.5-flash"  
       * "DeepSeek" → "DeepSeek-V3"
     - Tạo ChatClient động: new ChatClient(resolvedModelName, _credential, _options)
     - Gọi CompleteChatAsync(prompt) và trả về Content[0].Text
     - Bọc toàn bộ trong try-catch, ném InvalidOperationException nếu model không hợp lệ

3. Đảm bảo build: dotnet build từ PRN222_Project/

RÀNG BUỘC:
- Giữ nguyên phương thức GenerateChatResponseAsync() hiện tại, không được xóa hay sửa
- Không thay đổi chữ ký (signature) của phương thức cũ
- GitHub Models (Azure Inference) đang được dùng làm BaseUrl — cả 3 model dùng chung API Key
- Tuân thủ tuyệt đối file AGENTS.md trong workspace
```

---

### 🤖 TASK 1.3 — Service: Refactor `BenchmarkRunnerService` chạy tuần tự 3 Model

```
Bạn là AI Agent lập trình .NET 8 cho dự án PRN222_Gr8_RBLChatBot.
Workspace: c:\Workspace\Prn222\Asm3\PRN222_Gr8_RBLChatBot

NHIỆM VỤ: Cập nhật BenchmarkRunnerService để chạy benchmark cho 3 LLM (GPT, Gemini, DeepSeek)

Đọc kỹ các file sau trước khi bắt đầu:
- PRN222_Project/PRN222.Services/BenchmarkRunnerService.cs
- PRN222_Project/PRN222.Services/Interfaces/IBenchmarkRunnerService.cs
- PRN222_Project/PRN222.Models/BenchmarkRun.cs

Các bước thực hiện:

1. Mở file PRN222_Project/PRN222.Services/Interfaces/IBenchmarkRunnerService.cs
   - Cập nhật chữ ký phương thức RunBenchmarkAsync, thêm tham số llmModelName:
     Task<BenchmarkRun> RunBenchmarkAsync(string embeddingModel, string chunkingStrategy, string llmModelName);

2. Mở file PRN222_Project/PRN222.Services/BenchmarkRunnerService.cs
   - Cập nhật chữ ký RunBenchmarkAsync để nhận thêm string llmModelName
   - Khi khởi tạo BenchmarkRun mới, gán: LlmModel = llmModelName
   - Trong vòng lặp xử lý câu hỏi, thay thế:
     botAnswer = await _llmService.GenerateChatResponseAsync(ragPrompt);
     bằng:
     botAnswer = await _llmService.GenerateChatResponseWithModelAsync(ragPrompt, llmModelName);
   - Cập nhật thông báo SignalR để bao gồm tên model đang chạy:
     Ví dụ: $"[{llmModelName}] Đã xử lý {i+1}/{total} câu hỏi..."

3. Đảm bảo build: dotnet build từ PRN222_Project/

RÀNG BUỘC:
- Không xóa bất kỳ logic SignalR nào hiện có (ReceiveProgress, ReceiveLiveResult)
- Không xóa error handling (try-catch per question) hiện tại
- Tuân thủ tuyệt đối file AGENTS.md trong workspace
```

---

### 🤖 TASK 1.4 — Service: Refactor `DashboardService` chạy 3 Model liên tiếp & cập nhật Chart data

```
Bạn là AI Agent lập trình .NET 8 cho dự án PRN222_Gr8_RBLChatBot.
Workspace: c:\Workspace\Prn222\Asm3\PRN222_Gr8_RBLChatBot

NHIỆM VỤ: Cập nhật DashboardService để khởi động benchmark cho 3 model và cập nhật chart data

Đọc kỹ các file sau trước khi bắt đầu:
- PRN222_Project/PRN222.Services/DashboardService.cs
- PRN222_Project/PRN222.Services/Interfaces/IDashboardService.cs

Các bước thực hiện:

1. Mở file PRN222_Project/PRN222.Services/Interfaces/IDashboardService.cs
   - Kiểm tra DTO ChartDataDto — đảm bảo field Model (string) đã tồn tại

2. Mở file PRN222_Project/PRN222.Services/DashboardService.cs
   - Tìm phương thức StartBenchmarkAsync(string embeddingModel, string chunkingStrategy)
   - Cập nhật luồng chạy ngầm trong Task.Run để chạy benchmark cho 3 model liên tiếp:

     // Re-index chỉ 1 lần
     await documentService.ReindexIndexedDocumentsAsync(embeddingModel, chunkingStrategy);

     // Chạy lần lượt 3 model
     var llmModels = new[] { "GPT", "Gemini", "DeepSeek" };
     foreach (var llmModel in llmModels)
     {
         await benchmarkRunner.RunBenchmarkAsync(embeddingModel, chunkingStrategy, llmModel);
     }

   - Tìm phương thức GetChartDataAsync()
   - Cập nhật GroupBy để nhóm thêm theo LlmModel:
     .GroupBy(r => new { r.BenchmarkRun.LlmModel, r.BenchmarkRun.EmbeddingModel, r.BenchmarkRun.ChunkingStrategy })
   - Cập nhật field Model trong Select:
     Model = g.Key.LlmModel + " (" + g.Key.EmbeddingModel + " / " + g.Key.ChunkingStrategy + ")",

3. Đảm bảo build: dotnet build từ PRN222_Project/

RÀNG BUỘC:
- Không thay đổi interface IDashboardService nếu không cần thiết
- Giữ nguyên try-catch bao quanh toàn bộ Task.Run để tránh crash app
- Tuân thủ tuyệt đối file AGENTS.md trong workspace
```

---

### 🤖 TASK 1.5 — UI: Cập nhật Dashboard để hiển thị tên LLM Model trong lịch sử & biểu đồ

```
Bạn là AI Agent lập trình .NET 8 Razor Pages cho dự án PRN222_Gr8_RBLChatBot.
Workspace: c:\Workspace\Prn222\Asm3\PRN222_Gr8_RBLChatBot

NHIỆM VỤ: Cập nhật Admin Dashboard để hiển thị đầy đủ thông tin LlmModel

Đọc kỹ file sau trước khi bắt đầu:
- PRN222_Project/PRN222.RazorWebApp/Pages/Dashboard/Index.cshtml
- PRN222_Project/PRN222.RazorWebApp/Pages/Dashboard/Index.cshtml.cs

Các bước thực hiện:

1. Mở Index.cshtml.cs — không cần thay đổi gì vì chart data đã được cập nhật từ DashboardService.

2. Mở Index.cshtml:
   - Tìm phần hiển thị lịch sử benchmark (vòng lặp foreach BenchmarkRuns):
     Sửa dòng hiển thị tên model từ:
       <div class="fw-bold">@run.EmbeddingModel</div>
     thành:
       <div class="fw-bold">@run.LlmModel</div>
       <div class="small text-muted">@run.EmbeddingModel / @run.ChunkingStrategy</div>

   - Cập nhật nhãn KPI card "Lần benchmark" để thêm phụ đề rõ hơn:
     <div class="app-kpi-meta">Mỗi lần chạy = 3 model (GPT · Gemini · DeepSeek)</div>

   - Cập nhật tiêu đề card benchmark form:
     Từ: "Chạy benchmark theo chiến lược chunking"
     Thành: "So sánh 3 Model: GPT · Gemini · DeepSeek"

   - Cập nhật đoạn mô tả (app-muted) phía dưới tiêu đề để nêu rõ mỗi lần chạy
     sẽ đánh giá cả 3 dòng model tự động.

   - KHÔNG thay đổi bất kỳ logic JavaScript hay Chart.js nào — chúng đã
     hoạt động đúng với nhãn động từ API.

3. Kiểm tra build: dotnet build từ PRN222_Project/

RÀNG BUỘC:
- Không thêm form mới hay input mới vào dashboard
- Giữ nguyên toàn bộ SignalR connection code trong phần Scripts
- Tuân thủ tuyệt đối file AGENTS.md trong workspace
```
