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

           /// <summary>Lấy top N user tiêuhtu nhiều token nhất trong 7 ngày (cho Admin).</summary>
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
