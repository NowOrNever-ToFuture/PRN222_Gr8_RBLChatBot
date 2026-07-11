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

## 🏗️ TASK 0 — Foundation Setup (Làm trước, 1 người, ~45 phút)

> **Giao cho:** Nhóm trưởng hoặc người phụ trách Database
> **Nhánh:** Làm trực tiếp trên `dev` hoặc nhánh `feature/foundation`
> **Bàn giao:** Commit + push lên `dev`. Cả nhóm `git pull` rồi mới bắt đầu Phase 1/2/3.

```
Bạn là AI Agent lập trình .NET 8 cho dự án PRN222_Gr8_RBLChatBot.
Workspace: c:\Workspace\Prn222\Asm3\PRN222_Gr8_RBLChatBot

NHIỆM VỤ: Chuẩn bị toàn bộ nền tảng (Entity Models + DB Schema + DI + Hub mapping)
cho 3 Phase mới. Đây là task làm MỘT LẦN DUY NHẤT trước khi cả nhóm bắt đầu.
Sau task này, không ai được sửa AppDbContext.cs, DependencyInjection.cs, Program.cs nữa.

Đọc kỹ AGENTS.md trong workspace trước khi bắt đầu.

━━━ BƯỚC 1: Tạo Entity Models cho Phase 2 (Token Report) ━━━

Tạo file PRN222_Project/PRN222.Models/TokenUsageLog.cs:
  - Guid Id (NEWSEQUENTIALID)
  - Guid UserId
  - int PromptTokens, int CompletionTokens, int TotalTokens
  - string ModelName, string Feature  // "Chat", "LlmJudge", "Benchmark"
  - DateTime CreatedDate
  - Navigation: public User User { get; set; }

━━━ BƯỚC 2: Tạo Entity Models cho Phase 3 (Payments) ━━━

Tạo file PRN222_Project/PRN222.Models/PricingPackage.cs:
  - Guid Id (NEWSEQUENTIALID)
  - string Name, string Description
  - double Price, int TokenQuota, int DurationDays, bool IsActive

Tạo file PRN222_Project/PRN222.Models/UserSubscription.cs:
  - Guid Id (NEWSEQUENTIALID)
  - Guid UserId, Guid PricingPackageId
  - DateTime StartDate, DateTime EndDate
  - int RemainingTokens
  - string Status  // "Active", "Expired", "Suspended"
  - Navigation: public User User { get; set; }
  - Navigation: public PricingPackage PricingPackage { get; set; }

Tạo file PRN222_Project/PRN222.Models/PaymentTransaction.cs:
  - Guid Id (NEWSEQUENTIALID)
  - Guid UserId, Guid PricingPackageId
  - string TransactionCode, double Amount
  - string PaymentMethod  // "PayOS", "SystemFree"
  - string Status  // "Pending", "Success", "Failed"
  - DateTime CreatedDate
  - Navigation: public User User { get; set; }
  - Navigation: public PricingPackage PricingPackage { get; set; }

━━━ BƯỚC 3: Cập nhật AppDbContext.cs ━━━

Mở PRN222_Project/PRN222.Repositories/AppDbContext.cs:

a) Thêm các DbSet sau vào class (sau các DbSet hiện có):
   // Phase 2 - Token Report
   public DbSet<TokenUsageLog> TokenUsageLogs { get; set; }

   // Phase 3 - Payments
   public DbSet<PricingPackage>     PricingPackages     { get; set; }
   public DbSet<UserSubscription>   UserSubscriptions   { get; set; }
   public DbSet<PaymentTransaction> PaymentTransactions { get; set; }

b) Thêm vào cuối phương thức OnModelCreating():

   // ── Phase 1: BenchmarkRun.LlmModel ──────────────────────────────────
   // (Không cần cấu hình thêm — cột string thường, migration sẽ tạo)

   // ── Phase 2: TokenUsageLog ──────────────────────────────────────────
   modelBuilder.Entity<TokenUsageLog>().HasKey(t => t.Id);
   modelBuilder.Entity<TokenUsageLog>()
       .Property(t => t.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
   modelBuilder.Entity<TokenUsageLog>()
       .HasOne(t => t.User).WithMany()
       .HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);

   // ── Phase 3: PricingPackage ─────────────────────────────────────────
   modelBuilder.Entity<PricingPackage>().HasKey(p => p.Id);
   modelBuilder.Entity<PricingPackage>()
       .Property(p => p.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

   // ── Phase 3: UserSubscription ───────────────────────────────────────
   modelBuilder.Entity<UserSubscription>().HasKey(s => s.Id);
   modelBuilder.Entity<UserSubscription>()
       .Property(s => s.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
   modelBuilder.Entity<UserSubscription>()
       .HasOne(s => s.User).WithMany()
       .HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
   modelBuilder.Entity<UserSubscription>()
       .HasOne(s => s.PricingPackage).WithMany()
       .HasForeignKey(s => s.PricingPackageId).OnDelete(DeleteBehavior.Restrict);

   // ── Phase 3: PaymentTransaction ─────────────────────────────────────
   modelBuilder.Entity<PaymentTransaction>().HasKey(t => t.Id);
   modelBuilder.Entity<PaymentTransaction>()
       .Property(t => t.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
   modelBuilder.Entity<PaymentTransaction>()
       .HasOne(t => t.User).WithMany()
       .HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
   modelBuilder.Entity<PaymentTransaction>()
       .HasOne(t => t.PricingPackage).WithMany()
       .HasForeignKey(t => t.PricingPackageId).OnDelete(DeleteBehavior.Restrict);
   modelBuilder.Entity<PaymentTransaction>()
       .Property(t => t.TransactionCode).HasColumnType("nvarchar(max)");

━━━ BƯỚC 4: Cập nhật BenchmarkRun.cs (Phase 1) ━━━

Mở PRN222_Project/PRN222.Models/BenchmarkRun.cs:
- Thêm thuộc tính sau trường ChunkingStrategy:
  public string LlmModel { get; set; } = "GPT";

━━━ BƯỚC 5: Tạo 1 Migration duy nhất gộp tất cả thay đổi ━━━

Chạy từ thư mục PRN222_Project/:
  dotnet ef migrations add AddAllNewFeatures `
    --project PRN222.Repositories `
    --startup-project PRN222.RazorWebApp

  dotnet ef database update `
    --project PRN222.Repositories `
    --startup-project PRN222.RazorWebApp

━━━ BƯỚC 6: Cập nhật DependencyInjection.cs ━━━

Mở PRN222_Project/PRN222.Services/DependencyInjection.cs:
- Thêm vào cuối phương thức AddApplicationServices(), trước return services:

  // Phase 2 - Token Report (service sẽ được thành viên B implement sau)
  services.AddScoped<ITokenUsageService, TokenUsageService>();

  // Phase 3 - Payments (service sẽ được thành viên C implement sau)
  services.AddScoped<IPaymentService, PaymentService>();

LƯU Ý: Tạo stub class rỗng để project build được ngay:
  - PRN222_Project/PRN222.Services/Interfaces/ITokenUsageService.cs (interface rỗng)
  - PRN222_Project/PRN222.Services/TokenUsageService.cs (implement rỗng)
  - PRN222_Project/PRN222.Services/Interfaces/IPaymentService.cs (interface rỗng)
  - PRN222_Project/PRN222.Services/PaymentService.cs (implement rỗng)
  - PRN222_Project/PRN222.Services/TokenUsageService.cs cần khai báo TokenUsageHub rỗng inline
  - PRN222_Project/PRN222.Services/PaymentService.cs cần khai báo PaymentHub rỗng inline

━━━ BƯỚC 7: Cập nhật Program.cs ━━━

Mở PRN222_Project/PRN222.RazorWebApp/Program.cs:
- Thêm 2 dòng vào khối MapHub hiện có:
  app.MapHub<TokenUsageHub>("/hubs/tokenusage");  // Phase 2
  app.MapHub<PaymentHub>("/hubs/payment");         // Phase 3

━━━ BƯỚC 8: Đảm bảo build thành công ━━━

  dotnet build

Sửa tất cả lỗi build trước khi commit.
KHÔNG được để warning mới phát sinh quá số lượng hiện tại.

━━━ BƯỚC 9: Commit và push ━━━

  git add .
  git commit -m "feat: foundation setup - add all entity models, migrations, DI stubs for 3 new features"
  git push origin dev

THÔNG BÁO cho cả nhóm: "Foundation đã xong, mọi người git pull và bắt đầu làm nhánh của mình."

RÀNG BUỘC TUYỆT ĐỐI:
- Sau commit này, KHÔNG AI được sửa AppDbContext.cs, DependencyInjection.cs, Program.cs
- Nếu cần thêm gì vào 3 file trên, báo người làm Task 0 để làm tập trung
- Tuân thủ tuyệt đối file AGENTS.md trong workspace
```

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

---

## 📊 PHASE 2 — Function 1: Report & Statistics (Thống kê Token theo tuần)

---

### 🤖 TASK 2.1 — Database: Tạo entity `TokenUsageLog` và Migration

```
Bạn là AI Agent lập trình .NET 8 cho dự án PRN222_Gr8_RBLChatBot.
Workspace: c:\Workspace\Prn222\Asm3\PRN222_Gr8_RBLChatBot

NHIỆM VỤ: Thêm bảng TokenUsageLogs vào database để ghi nhật ký token API

Các bước thực hiện:

1. Tạo file mới PRN222_Project/PRN222.Models/TokenUsageLog.cs:
   namespace PRN222.Models
   {
       public class TokenUsageLog
       {
           public Guid Id { get; set; }
           public Guid UserId { get; set; }
           public int PromptTokens { get; set; }
           public int CompletionTokens { get; set; }
           public int TotalTokens { get; set; }
           public string ModelName { get; set; }
           public string Feature { get; set; }  // "Chat", "LlmJudge", "Benchmark"
           public DateTime CreatedDate { get; set; }

           // Navigation property
           public User User { get; set; }
       }
   }

2. Mở PRN222_Project/PRN222.Repositories/AppDbContext.cs:
   - Thêm DbSet: public DbSet<TokenUsageLog> TokenUsageLogs { get; set; }
   - Thêm cấu hình trong OnModelCreating():
     modelBuilder.Entity<TokenUsageLog>().HasKey(t => t.Id);
     modelBuilder.Entity<TokenUsageLog>().Property(t => t.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
     modelBuilder.Entity<TokenUsageLog>()
         .HasOne(t => t.User).WithMany()
         .HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
     modelBuilder.Entity<TokenUsageLog>().Property(t => t.ModelName).HasMaxLength(100);
     modelBuilder.Entity<TokenUsageLog>().Property(t => t.Feature).HasMaxLength(50);

3. Tạo Migration từ PRN222_Project/:
   dotnet ef migrations add AddTokenUsageLogs --project PRN222.Repositories --startup-project PRN222.RazorWebApp

4. Apply migration:
   dotnet ef database update --project PRN222.Repositories --startup-project PRN222.RazorWebApp

5. Build kiểm tra: dotnet build

RÀNG BUỘC:
- Không dùng int identity cho PK, dùng Guid với NEWSEQUENTIALID()
- OnDelete phải là Cascade cho FK tới Users
- Tuân thủ tuyệt đối file AGENTS.md trong workspace
```

---

### 🤖 TASK 2.2 — Service: Tạo `ITokenUsageService` và `TokenUsageService`

```
Bạn là AI Agent lập trình .NET 8 cho dự án PRN222_Gr8_RBLChatBot.
Workspace: c:\Workspace\Prn222\Asm3\PRN222_Gr8_RBLChatBot

NHIỆM VỤ: Tạo service ghi nhật ký token và thống kê sử dụng theo tuần

Đọc kỹ AGENTS.md trong workspace trước khi bắt đầu.

Các bước thực hiện:

1. Tạo file PRN222_Project/PRN222.Services/Interfaces/ITokenUsageService.cs:
   namespace PRN222.Services.Interfaces
   {
       public interface ITokenUsageService
       {
           /// <summary>Ghi nhật ký token sau mỗi lần gọi LLM.</summary>
           Task LogAsync(Guid userId, int promptTokens, int completionTokens, string modelName, string feature);

           /// <summary>Lấy thống kê token 7 ngày gần nhất của user, nhóm theo ngày.</summary>
           Task<List<DailyTokenUsageDto>> GetWeeklyUsageAsync(Guid userId);

           /// <summary>Lấy top N user tiêu thụ nhiều token nhất trong 7 ngày (cho Admin).</summary>
           Task<List<UserTokenSummaryDto>> GetTopUsersAsync(int count = 10);

           /// <summary>Lấy tổng token theo từng model trong 7 ngày (cho Admin Pie Chart).</summary>
           Task<List<ModelTokenSummaryDto>> GetModelBreakdownAsync();
       }

       public class DailyTokenUsageDto
       {
           public DateTime Date { get; set; }
           public int TotalTokens { get; set; }
       }

       public class UserTokenSummaryDto
       {
           public string FullName { get; set; }
           public int TotalTokens { get; set; }
       }

       public class ModelTokenSummaryDto
       {
           public string ModelName { get; set; }
           public int TotalTokens { get; set; }
       }
   }

2. Tạo file PRN222_Project/PRN222.Services/TokenUsageService.cs:
   - Inject AppDbContext qua constructor với null guard
   - Implement LogAsync: tạo TokenUsageLog mới, SaveChangesAsync
   - Implement GetWeeklyUsageAsync: lọc 7 ngày gần nhất của userId,
     GroupBy ngày (Date), tính Sum(TotalTokens), OrderBy Date tăng dần
   - Implement GetTopUsersAsync: lọc 7 ngày gần nhất, Join với Users,
     GroupBy UserId + FullName, Sum(TotalTokens), Take(count), OrderByDescending
   - Implement GetModelBreakdownAsync: lọc 7 ngày, GroupBy ModelName,
     Sum(TotalTokens), OrderByDescending

3. Mở PRN222_Project/PRN222.Services/DependencyInjection.cs:
   - Thêm: services.AddScoped<ITokenUsageService, TokenUsageService>();

4. Build kiểm tra: dotnet build

RÀNG BUỘC:
- Không dùng static class, phải qua DI
- Sử dụng DateTime.UtcNow.AddDays(-7) để lọc 7 ngày
- Tuân thủ tuyệt đối file AGENTS.md trong workspace
```

---

### 🤖 TASK 2.3 — Service: Tích hợp ghi token + bắn SignalR vào `ChatService` sau mỗi câu trả lời

```
Bạn là AI Agent lập trình .NET 8 cho dự án PRN222_Gr8_RBLChatBot.
Workspace: c:\Workspace\Prn222\Asm3\PRN222_Gr8_RBLChatBot

NHIỆM VỤ: Ghi nhật ký token vào DB và bắn sự kiện SignalR cập nhật số dư token
sau mỗi lần ChatService nhận được phản hồi từ LLM

Đọc kỹ file sau trước khi bắt đầu:
- PRN222_Project/PRN222.Services/ChatService.cs
- PRN222_Project/PRN222.Services/Interfaces/IChatService.cs
- PRN222_Project/PRN222.Services/BenchmarkRunnerService.cs (tham khảo pattern dùng IHubContext)

Các bước thực hiện:

1. Mở PRN222_Project/PRN222.Services/ChatService.cs:
   - Thêm _tokenUsageService vào constructor (inject ITokenUsageService)
   - Thêm _hubContext vào constructor (inject IHubContext<TokenUsageHub>)
   - Tìm nơi gọi: string answer = await _llmService.GenerateChatResponseAsync(ragPrompt);
   - Sau khi nhận được answer, ước lượng token dựa trên độ dài chuỗi (approximation):
     int promptTokens = (int)Math.Ceiling(ragPrompt.Length / 4.0);
     int completionTokens = (int)Math.Ceiling(answer.Length / 4.0);
   - Ghi log: await _tokenUsageService.LogAsync(userId, promptTokens, completionTokens, "gpt-4o-mini", "Chat");
   - Sau khi log xong, lấy token đã dùng hôm nay của user:
     int todayUsed = await _tokenUsageService.GetTodayUsageAsync(userId);
   - Bắn SignalR để cập nhật widget token trên giao diện chat của user:
     await _hubContext.Clients.User(userId.ToString())
         .SendAsync("ReceiveTokenUpdate", todayUsed);
   - Lấy userId từ tham số truyền vào — kiểm tra xem IChatService có truyền userId không,
     nếu không thì thêm vào signature phương thức chat chính

2. Thêm method GetTodayUsageAsync vào ITokenUsageService và TokenUsageService:
   Task<int> GetTodayUsageAsync(Guid userId);
   // Implement: lọc TokenUsageLogs theo userId và Date == DateTime.UtcNow.Date, Sum(TotalTokens)

3. Build kiểm tra: dotnet build

GHI CHÚ QUAN TRỌNG:
- Dùng ước lượng token (length / 4) thay vì gọi thêm API để tính chính xác
- SignalR bắn theo UserId (Clients.User) để chỉ cập nhật cho đúng user đang chat
- Nếu ghi log thất bại thì không ném lỗi ra ngoài (dùng try-catch bao quanh)
- Tuân thủ tuyệt đối file AGENTS.md trong workspace
```

---

### 🤖 TASK 2.4 — UI: Trang thống kê token cho User và Admin Dashboard

```
Bạn là AI Agent lập trình .NET 8 Razor Pages cho dự án PRN222_Gr8_RBLChatBot.
Workspace: c:\Workspace\Prn222\Asm3\PRN222_Gr8_RBLChatBot

NHIỆM VỤ: Tạo trang thống kê token dùng Chart.js và cập nhật Admin Dashboard

Đọc kỹ các file sau trước khi bắt đầu:
- PRN222_Project/PRN222.RazorWebApp/Pages/Dashboard/Index.cshtml (tham khảo style, chart pattern)
- PRN222_Project/PRN222.RazorWebApp/Pages/ (cấu trúc thư mục hiện có)

Các bước thực hiện:

1. Tạo thư mục PRN222_Project/PRN222.RazorWebApp/Pages/Reports/

2. Tạo file Pages/Reports/TokenUsage.cshtml.cs:
   - Namespace: PRN222.RazorWebApp.Pages.Reports
   - Class: TokenUsageModel kế thừa PageModel
   - [Authorize] ở class level (tất cả role đều xem được usage của mình)
   - Inject ITokenUsageService
   - OnGetAsync(): lấy UserId từ User.FindFirst(ClaimTypes.NameIdentifier)
   - Gọi GetWeeklyUsageAsync(userId) → bind vào property WeeklyUsage

3. Tạo file Pages/Reports/TokenUsage.cshtml:
   - Tiêu đề trang: "Thống kê Token theo tuần"
   - Vẽ Line Chart (Chart.js) hiển thị lượng token tiêu thụ theo từng ngày trong 7 ngày qua
   - Hiển thị tổng token tuần này (Sum của WeeklyUsage)
   - Dùng @section Scripts để inject Chart.js từ CDN

4. Mở Pages/Dashboard/Index.cshtml:
   - Thêm 2 card mới vào section app-grid-2 (bên dưới biểu đồ benchmark):
     - Card 1: Pie Chart — tỉ lệ token theo model (load data từ handler mới GetTokenModelBreakdown)
     - Card 2: Bảng top 5 user dùng nhiều token nhất (load data từ handler mới GetTopTokenUsers)

5. Mở Pages/Dashboard/Index.cshtml.cs:
   - Thêm handler OnGetTokenModelBreakdownAsync() → trả JsonResult từ ITokenUsageService.GetModelBreakdownAsync()
   - Thêm handler OnGetTopTokenUsersAsync() → trả JsonResult từ ITokenUsageService.GetTopUsersAsync(5)

6. Build kiểm tra: dotnet build

RÀNG BUỘC:
- Không dùng ViewBag/ViewData nếu có thể dùng strongly-typed model
- Dùng TempData["ErrorMessage"] nếu cần hiển thị lỗi
- Chart.js load từ CDN (cdn.jsdelivr.net) như hiện tại
- Tuân thủ tuyệt đối file AGENTS.md trong workspace
```

---

### 🤖 TASK 2.5 — [MỚI] SignalR Hub: `TokenUsageHub` — push cập nhật token real-time

> ⚠️ **Chưa tồn tại trong codebase — cần tạo mới hoàn toàn**

```
Bạn là AI Agent lập trình .NET 8 cho dự án PRN222_Gr8_RBLChatBot.
Workspace: c:\Workspace\Prn222\Asm3\PRN222_Gr8_RBLChatBot

NHIỆM VỤ: Tạo TokenUsageHub để server có thể push cập nhật số token
cho đúng user đang chat theo thời gian thực

Đọc kỹ các file sau trước khi bắt đầu:
- PRN222_Project/PRN222.Services/BenchmarkRunnerService.cs (tham khảo pattern Hub rỗng inline)
- PRN222_Project/PRN222.Services/TestQuestionService.cs (tham khảo pattern IHubContext)
- PRN222_Project/PRN222.RazorWebApp/Program.cs (xem cách MapHub)
- AGENTS.md (quy tắc Hub)

Các bước thực hiện:

1. Mở PRN222_Project/PRN222.Services/TokenUsageService.cs:
   - Thêm Hub class rỗng ngay phía trên class TokenUsageService:
     public class TokenUsageHub : Hub { }

2. Cập nhật constructor TokenUsageService:
   - Thêm inject IHubContext<TokenUsageHub>
   - Guard null: _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));

3. Cập nhật phương thức LogAsync trong TokenUsageService:
   - Sau khi SaveChangesAsync(), tính todayUsed:
     var todayUsed = await GetTodayUsageAsync(log.UserId);
   - Bắn SignalR đến đúng user (dùng Clients.User, không phải Clients.All):
     await _hubContext.Clients.User(log.UserId.ToString())
         .SendAsync("ReceiveTokenUpdate", todayUsed);

4. Mở PRN222_Project/PRN222.RazorWebApp/Program.cs:
   - Thêm mapping hub vào danh sách MapHub hiện có:
     app.MapHub<TokenUsageHub>("/hubs/tokenusage");

5. Để SignalR UserID routing hoạt động, đảm bảo trong Program.cs đã có:
   builder.Services.AddAuthentication(...)
   (đã có sẵn — chỉ kiểm tra, không sửa)
   SignalR tự động dùng User.Identity.Name hoặc NameIdentifier để route Clients.User

6. Build kiểm tra: dotnet build

KIẾN TRÚC BẮT BUỘC (theo AGENTS.md):
- Hub class RỖNG — chỉ khai báo: public class TokenUsageHub : Hub { }
- Hub class đặt INLINE trong TokenUsageService.cs (không tạo file riêng)
- Logic bắn event nằm trong TokenUsageService, inject IHubContext<TokenUsageHub>
- Endpoint mapping nằm trong Program.cs
- Tuân thủ tuyệt đối file AGENTS.md trong workspace
```

---

### 🤖 TASK 2.6 — [MỚI] UI Chat: Widget hiển thị token còn lại hôm nay + cập nhật SignalR real-time

> ⚠️ **Chức năng hoàn toàn chưa tồn tại — cần tạo mới**

```
Bạn là AI Agent lập trình .NET 8 Razor Pages cho dự án PRN222_Gr8_RBLChatBot.
Workspace: c:\Workspace\Prn222\Asm3\PRN222_Gr8_RBLChatBot

NHIỆM VỤ: Thêm widget hiển thị số token đã dùng hôm nay và giới hạn hàng ngày
vào trang chat, cập nhật real-time qua SignalR sau mỗi tin nhắn gửi đi

Đọc kỹ các file sau trước khi bắt đầu:
- PRN222_Project/PRN222.RazorWebApp/Pages/Chat/Index.cshtml
- PRN222_Project/PRN222.RazorWebApp/Pages/Chat/Index.cshtml.cs
- PRN222_Project/PRN222.RazorWebApp/Pages/Dashboard/Index.cshtml (tham khảo SignalR pattern)
- AGENTS.md

Các bước thực hiện:

1. Mở Pages/Chat/Index.cshtml.cs:
   - Inject ITokenUsageService vào constructor
   - Thêm property:
     public int TodayTokenUsed { get; set; }
     public int DailyTokenLimit { get; set; } = 50000; // Mặc định 50k token/ngày
   - Trong OnGetAsync(), lấy userId từ User.FindFirst(ClaimTypes.NameIdentifier)
   - Gán: TodayTokenUsed = await _tokenUsageService.GetTodayUsageAsync(userId);

2. Mở Pages/Chat/Index.cshtml:
   a) Thêm widget token bar ngay phía trên khung chat input (hoặc sidebar):
      <div id="token-widget" class="app-card mb-3 p-3">
        <div class="d-flex justify-content-between align-items-center mb-1">
          <span class="small fw-bold">🔋 Token hôm nay</span>
          <span class="small">
            <span id="token-used">@Model.TodayTokenUsed</span>
            / @Model.DailyTokenLimit
          </span>
        </div>
        <div class="progress" style="height: 8px; border-radius: 999px;">
          <div id="token-progress-bar"
               class="progress-bar bg-success"
               role="progressbar"
               style="width: @(Math.Min(100, (int)((double)Model.TodayTokenUsed / Model.DailyTokenLimit * 100)))%">
          </div>
        </div>
        <div id="token-warning" class="small text-warning mt-1 d-none">
          ⚠️ Bạn đã dùng hơn 80% quota hôm nay!
        </div>
      </div>

   b) Thêm SignalR connection vào @section Scripts:
      // Kết nối TokenUsageHub
      const tokenConnection = new signalR.HubConnectionBuilder()
          .withUrl("/hubs/tokenusage")
          .withAutomaticReconnect()
          .build();

      // Nhận cập nhật token sau mỗi tin nhắn
      tokenConnection.on("ReceiveTokenUpdate", (todayUsed) => {
          const limit = @Model.DailyTokenLimit;
          document.getElementById('token-used').textContent = todayUsed;
          const pct = Math.min(100, Math.round(todayUsed / limit * 100));
          const bar = document.getElementById('token-progress-bar');
          bar.style.width = pct + '%';
          // Đổi màu theo mức sử dụng
          bar.className = 'progress-bar ' + (pct >= 90 ? 'bg-danger' : pct >= 70 ? 'bg-warning' : 'bg-success');
          // Hiện cảnh báo khi > 80%
          document.getElementById('token-warning').classList.toggle('d-none', pct < 80);
      });

      tokenConnection.start().catch(err => console.error('TokenUsageHub error:', err));

3. Build kiểm tra: dotnet build

RÀNG BUỘC:
- Widget phải hiển thị ngay khi load trang (server-side render TodayTokenUsed ban đầu)
- SignalR chỉ cập nhật delta (số mới) — không reload toàn trang
- Nếu DailyTokenLimit lấy từ UserSubscription (Phase 3), cần inject IPaymentService
  để lấy limit thực tế thay vì hardcode 50000
- SignalR connection dùng .withAutomaticReconnect() như tất cả hub khác trong project
- Tuân thủ tuyệt đối file AGENTS.md trong workspace
```

---

## 💳 PHASE 3 — Function 2: Package + Payments (Gói dịch vụ & Thanh toán)

---

### 🤖 TASK 3.1 — Database: Tạo 3 bảng `PricingPackages`, `UserSubscriptions`, `PaymentTransactions`

```
Bạn là AI Agent lập trình .NET 8 cho dự án PRN222_Gr8_RBLChatBot.
Workspace: c:\Workspace\Prn222\Asm3\PRN222_Gr8_RBLChatBot

NHIỆM VỤ: Thiết kế database cho hệ thống gói dịch vụ và thanh toán

Các bước thực hiện:

1. Tạo file PRN222_Project/PRN222.Models/PricingPackage.cs:
   - Guid Id (NEWSEQUENTIALID)
   - string Name (ví dụ: "Free", "Standard", "VIP")
   - string Description
   - double Price (VND)
   - int TokenQuota (tổng token được cấp)
   - int DurationDays (hạn dùng tính bằng ngày)
   - bool IsActive
   - Navigation: ICollection<UserSubscription>

2. Tạo file PRN222_Project/PRN222.Models/UserSubscription.cs:
   - Guid Id, Guid UserId, Guid PricingPackageId
   - DateTime StartDate, DateTime EndDate
   - int RemainingTokens
   - string Status ("Active", "Expired", "Suspended")
   - Navigation: User, PricingPackage

3. Tạo file PRN222_Project/PRN222.Models/PaymentTransaction.cs:
   - Guid Id, Guid UserId, Guid PricingPackageId
   - string TransactionCode, double Amount
   - string PaymentMethod ("PayOS", "SystemFree")
   - string Status ("Pending", "Success", "Failed")
   - DateTime CreatedDate
   - Navigation: User, PricingPackage

4. Cập nhật AppDbContext.cs:
   - Thêm 3 DbSet mới
   - Cấu hình trong OnModelCreating():
     * PrimaryKey + NEWSEQUENTIALID cho cả 3 entity
     * FK User→UserSubscriptions: OnDelete Cascade
     * FK PricingPackage→UserSubscriptions: OnDelete Restrict (không xóa gói nếu còn user)
     * FK User→PaymentTransactions: OnDelete Cascade
     * Description và TransactionCode: HasColumnType("nvarchar(max)")

5. Tạo Migration và apply:
   dotnet ef migrations add AddPackagesAndPayments --project PRN222.Repositories --startup-project PRN222.RazorWebApp
   dotnet ef database update --project PRN222.Repositories --startup-project PRN222.RazorWebApp

6. Build kiểm tra: dotnet build

RÀNG BUỘC:
- Tuân thủ tuyệt đối file AGENTS.md trong workspace
```

---

### 🤖 TASK 3.2 — Service: Tạo `IPaymentService` + `PaymentHub` SignalR và tích hợp PayOS SDK

```
Bạn là AI Agent lập trình .NET 8 cho dự án PRN222_Gr8_RBLChatBot.
Workspace: c:\Workspace\Prn222\Asm3\PRN222_Gr8_RBLChatBot

NHIỆM VỤ: Tạo service quản lý gói cước, subscription, khởi tạo thanh toán PayOS
và push cập nhật trạng thái thanh toán real-time qua SignalR

Các bước thực hiện:

1. Thêm PayOS NuGet package vào PRN222.Services.csproj:
   dotnet add PRN222_Project/PRN222.Services/PRN222.Services.csproj package Net.payOS --version 1.0.*

2. Thêm cấu hình PayOS vào appsettings.json:
   "PayOS": {
     "ClientId": "YOUR_CLIENT_ID",
     "ApiKey": "YOUR_API_KEY",
     "ChecksumKey": "YOUR_CHECKSUM_KEY",
     "ReturnUrl": "https://localhost:PORT/Payments/Success",
     "CancelUrl": "https://localhost:PORT/Payments/Cancel"
   }

3. Tạo IPaymentService.cs trong Interfaces/:
   - GetAllPackagesAsync() → List<PricingPackage>
   - GetUserSubscriptionAsync(Guid userId) → UserSubscription?
   - CreatePaymentLinkAsync(Guid userId, Guid packageId) → string (URL thanh toán)
   - ConfirmPaymentAsync(string transactionCode) → (bool Success, string Message)
   - AssignFreePackageAsync(Guid userId) → void (cấp gói Free khi đăng ký)

4. Tạo PaymentService.cs:
   - Thêm Hub rỗng INLINE ngay trên class:
     public class PaymentHub : Hub { }
   - Inject AppDbContext, IConfiguration, IHubContext<PaymentHub> qua constructor
   - CreatePaymentLinkAsync:
     * Tạo PaymentTransaction mới với Status = "Pending"
     * Khởi tạo PayOS client từ config, tạo payment link
     * Trả về payment URL để redirect user
   - ConfirmPaymentAsync (gọi từ Webhook):
     * Tìm PaymentTransaction theo transactionCode
     * Cập nhật Status = "Success"
     * Tạo/cập nhật UserSubscription: cộng TokenQuota, set EndDate = Now + DurationDays
     * Bắn SignalR thông báo cho đúng user:
       await _hubContext.Clients.User(userId.ToString())
           .SendAsync("ReceivePaymentConfirmed", packageName, newRemainingTokens);
   - AssignFreePackageAsync: tạo UserSubscription với gói Free ngay khi register

5. Mở Program.cs, thêm hub mapping:
   app.MapHub<PaymentHub>("/hubs/payment");

6. Đăng ký DI trong DependencyInjection.cs:
   services.AddScoped<IPaymentService, PaymentService>();

7. Build kiểm tra: dotnet build

RÀNG BUỘC:
- Hub class RỖNG — đặt inline trong PaymentService.cs theo quy tắc AGENTS.md
- Bắn SignalR theo Clients.User (đúng user) không phải Clients.All
- Không commit API Key thật vào repo — dùng placeholder "YOUR_..."
- Webhook endpoint phải xác minh checksum signature của PayOS trước khi xử lý
- Tuân thủ tuyệt đối file AGENTS.md trong workspace
```

---

### 🤖 TASK 3.3 — UI: Trang Pricing Cards, Checkout và Webhook endpoint

```
Bạn là AI Agent lập trình .NET 8 Razor Pages cho dự án PRN222_Gr8_RBLChatBot.
Workspace: c:\Workspace\Prn222\Asm3\PRN222_Gr8_RBLChatBot

NHIỆM VỤ: Tạo giao diện mua gói dịch vụ, trang thanh toán và webhook endpoint

Đọc kỹ file AGENTS.md và xem mẫu style tại Pages/Dashboard/Index.cshtml trước khi bắt đầu.

Các bước thực hiện:

1. Tạo thư mục Pages/Payments/

2. Tạo Pages/Payments/Pricing.cshtml + Pricing.cshtml.cs:
   - [Authorize] ở class level
   - OnGetAsync(): load GetAllPackagesAsync(), GetUserSubscriptionAsync(userId)
   - Giao diện Pricing Cards đẹp mắt (3 cột: Free / Standard / VIP)
     * Mỗi card hiển thị: Tên gói, Giá, Hạn mức token, Hạn dùng, nút Mua/Hiện tại
     * Card của gói hiện tại của user thì highlight và hiện "Đang dùng"
     * Gói Free thì nút "Chọn gói miễn phí" (POST handler)
     * Gói trả phí thì nút "Mua ngay" → redirect sang Checkout

3. Tạo Pages/Payments/Checkout.cshtml + Checkout.cshtml.cs:
   - OnGetAsync(Guid packageId): gọi CreatePaymentLinkAsync → lấy paymentUrl
   - Hiển thị thông tin đơn hàng + nút "Tiến hành thanh toán" redirect sang paymentUrl
   - Trang Success và Cancel đơn giản (thêm 2 page nhỏ hoặc dùng TempData)

4. Tạo Webhook API endpoint trong Program.cs (hoặc tạo 1 minimal API endpoint):
   app.MapPost("/api/payment/webhook", async (HttpContext ctx, IPaymentService paymentService) =>
   {
       // Đọc body JSON từ PayOS
       // Xác minh checksum
       // Gọi paymentService.ConfirmPaymentAsync(transactionCode)
       return Results.Ok();
   });

5. Thêm link "Gói dịch vụ" vào navigation (Pages/Shared/_Layout.cshtml hoặc sidebar)

6. Build kiểm tra: dotnet build

RÀNG BUỘC:
- Webhook endpoint không cần [Authorize] và không cần AntiForgery token
- Trang Pricing và Checkout phải có [Authorize] 
- Không viết business logic trong PageModel — chỉ gọi service
- Tuân thủ tuyệt đối file AGENTS.md trong workspace
```

---

## 📌 Bảng phân công tham khảo

| Task | Mô tả ngắn | Phase | SignalR | Độ phức tạp |
|------|-----------|-------|---------|------------|
| **1.1** | DB Migration: cột LlmModel | Benchmark | ❌ | ⭐ Thấp |
| **1.2** | Service: ILlmService + OpenAiService multi-model | Benchmark | ❌ | ⭐⭐ Trung bình |
| **1.3** | Service: BenchmarkRunnerService refactor | Benchmark | ✅ (có sẵn) | ⭐⭐ Trung bình |
| **1.4** | Service: DashboardService 3-model loop + chart | Benchmark | ✅ (có sẵn) | ⭐⭐ Trung bình |
| **1.5** | UI: Dashboard history + label update | Benchmark | ✅ (có sẵn) | ⭐ Thấp |
| **2.1** | DB Migration: TokenUsageLogs | Token Report | ❌ | ⭐ Thấp |
| **2.2** | Service: ITokenUsageService + Impl | Token Report | ❌ | ⭐⭐ Trung bình |
| **2.3** | Service: Ghi log + bắn SignalR trong ChatService | Token Report | ✅ **MỚI** | ⭐⭐ Trung bình |
| **2.4** | UI: Trang thống kê + Admin chart | Token Report | ❌ | ⭐⭐⭐ Cao |
| **2.5** | 🆕 SignalR Hub: TokenUsageHub push token real-time | Token Report | ✅ **MỚI** | ⭐⭐ Trung bình |
| **2.6** | 🆕 UI Chat: Widget token còn lại + SignalR live update | Token Report | ✅ **MỚI** | ⭐⭐⭐ Cao |
| **3.1** | DB Migration: 3 bảng Package/Subscription/Payment | Payments | ❌ | ⭐⭐ Trung bình |
| **3.2** | Service: IPaymentService + PaymentHub + PayOS SDK | Payments | ✅ **MỚI** | ⭐⭐⭐ Cao |
| **3.3** | UI: Pricing Cards + Checkout + Webhook + SignalR | Payments | ✅ **MỚI** | ⭐⭐⭐ Cao |

> ⚠️ **Lưu ý thứ tự phụ thuộc:**
> - Task 1.2 phải hoàn thành trước 1.3, 1.4, 1.5
> - Task 2.1 phải hoàn thành trước 2.2, 2.3
> - Task **2.2 phải hoàn thành trước 2.5** (TokenUsageHub nằm trong TokenUsageService)
> - Task **2.5 phải hoàn thành trước 2.6** (UI Chat cần Hub endpoint sẵn sàng)
> - Task **2.3 phải hoàn thành trước 2.6** (ChatService phải bắn SignalR trước)
> - Task 3.1 phải hoàn thành trước 3.2 và 3.3
> - Task **2.6 nên hoàn thành sau 3.2** nếu muốn lấy DailyTokenLimit từ UserSubscription thật
