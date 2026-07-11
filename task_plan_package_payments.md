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
