# AGENTS.md — PRN222 Group 8 Assignment 3

Tài liệu này dành cho AI Agent (Copilot, Cursor, Kiro, v.v.) khi làm việc với codebase này.
Đọc kỹ trước khi sinh code, refactor, hoặc thêm tính năng mới.

---

## 1. Tổng quan kiến trúc

```
PRN222_Gr8_RBLChatBot/
├── PRN222_Project/          # .NET 8 ASP.NET Core Razor Pages
│   ├── PRN222.Models/       # Entity models — không có business logic
│   ├── PRN222.Repositories/ # EF Core DbContext + Migrations
│   ├── PRN222.Services/     # Business logic, Interfaces, DTOs, SignalR Hubs
│   └── PRN222.RazorWebApp/  # Pages, Models (ViewModels), Program.cs
└── Python_RAG_Server/       # FastAPI microservice (embedding + document parsing)
    └── api_server.py
```

**Dependency flow (một chiều, không được đảo ngược):**
```
RazorWebApp → Services → Repositories → Models
```

- `RazorWebApp` KHÔNG được tham chiếu trực tiếp `Repositories` hay `Models` ngoài DI setup.
- `Services` KHÔNG được tham chiếu `RazorWebApp`.
- `Models` KHÔNG được tham chiếu bất kỳ project nào khác.

---

## 2. Target Framework & Runtime

- **Bắt buộc:** `net8.0` cho tất cả các project .NET.
- **Không dùng** `net9.0`, `net10.0`, hoặc bất kỳ preview framework nào.
- **NuGet packages** phải tương thích với .NET 8. Ưu tiên version `8.0.x`.
  - Ví dụ đúng: `Microsoft.EntityFrameworkCore` `8.0.0`, `Microsoft.Extensions.DependencyInjection` `8.0.1`
  - Ví dụ sai: version `10.x`, `9.x`, hoặc preview `-beta`
- **Ngoại lệ được phép:** `OpenAI` `2.0.0-beta.11` (SDK chính thức chưa có stable cho .NET 8), `UglyToad.PdfPig` `1.7.0-custom-5`.

---

## 3. Naming Conventions

### 3.1 Namespaces
| Project | Namespace gốc |
|---|---|
| PRN222.Models | `PRN222.Models` |
| PRN222.Repositories | `PRN222.Repositories` |
| PRN222.Services | `PRN222.Services` |
| PRN222.Services (Interfaces) | `PRN222.Services.Interfaces` |
| PRN222.Services (DTOs) | `PRN222.Services.DTOs` |
| PRN222.RazorWebApp (Pages) | `PRN222.RazorWebApp.Pages.<Feature>` |
| PRN222.RazorWebApp (ViewModels) | `PRN222.RazorWebApp.Models` |

### 3.2 Classes & Files
- **Entity models:** PascalCase, tên số ít — `User`, `Document`, `DocumentChunk`, `BenchmarkRun`
- **Interfaces:** prefix `I` — `IDocumentService`, `IChatService`, `IEmbeddingService`
- **Service implementations:** tên class = tên interface bỏ `I` — `DocumentService`, `ChatService`
- **Razor Pages (PageModel):** suffix `Model`, kế thừa `PageModel`
  - File naming: `<Feature>/Index.cshtml` + `<Feature>/Index.cshtml.cs`
  - Class naming: `IndexModel`, `LoginModel`, `CreateModel`, `UploadModel`
  - Namespace: `PRN222.RazorWebApp.Pages.<Feature>`
- **DTOs:** suffix `DTO` hoặc `Dto` — `UploadDocumentDTO`, `PythonParseResponseDto`
- **ViewModels:** suffix `ViewModel` — `LoginViewModel`, `CreateUserViewModel`, `EditUserViewModel`
- **Hubs (SignalR):** suffix `Hub` — `TestQuestionHub`, `BenchmarkHub`, `CourseHub`
  - Hub classes rỗng, đặt inline trong file service tương ứng (ví dụ `TestQuestionHub` trong `TestQuestionService.cs`)
  - Inject `IHubContext<THub>` vào service constructor để bắn event

### 3.3 Members
- **Private fields:** prefix `_` camelCase — `_documentService`, `_dbContext`, `_httpClientFactory`
- **Properties:** PascalCase — `Id`, `FileName`, `CourseId`
- **Methods:** PascalCase, async methods suffix `Async` — `GetDocumentsAsync`, `DeleteDocumentAsync`
- **Local variables & parameters:** camelCase — `userId`, `courseId`, `uploadsPath`
- **Constants:** PascalCase hoặc UPPER_SNAKE_CASE — `pageSize`, `"Admin"`

### 3.4 Database / EF Core
- **Primary key:** `Guid Id` với `NEWSEQUENTIALID()` default — không dùng `int` identity.
- **Foreign key:** `Guid {EntityName}Id` — `CourseId`, `OwnerId`, `DocumentId`
- **Navigation properties:** tên entity (số ít cho 1-side, `ICollection<T>` cho many-side)
  ```csharp
  public Course Course { get; set; }
  public ICollection<DocumentChunk> DocumentChunks { get; set; } = new List<DocumentChunk>();
  ```
- **Cascade delete:** dùng `OnDelete(DeleteBehavior.Cascade)` cho tất cả FK relationships.
- **Large text columns:** khai báo `HasColumnType("nvarchar(max)")` trong `OnModelCreating`.

---

## 4. Quy tắc lập trình

### 4.1 Dependency Injection
- Tất cả services đăng ký trong `PRN222.Services/DependencyInjection.cs` qua extension method `AddApplicationServices()`.
- Lifetime mặc định: `AddScoped<>` cho services và repositories.
- Constructor injection bắt buộc — không dùng service locator hay `HttpContext.RequestServices` trực tiếp.
- Guard null trong constructor:
  ```csharp
  _service = service ?? throw new ArgumentNullException(nameof(service));
  ```

### 4.2 Pages (Razor Pages)
- File naming: `<Feature>/Index.cshtml` + `<Feature>/Index.cshtml.cs` (code-behind là PageModel)
- Class naming: suffix `Model` — `IndexModel`, `LoginModel`, `UploadModel`, `CreateModel`
- Namespace: `PRN222.RazorWebApp.Pages.<Feature>`
- Luôn dùng `[Authorize]` ở class level PageModel, thêm `[AllowAnonymous]` cho page cụ thể nếu cần.
- Không viết business logic trong PageModel — chỉ gọi service, xử lý kết quả, trả về Page/Redirect.
- Dùng `[BindProperty]` cho form binding (thay vì manual `Request.Form`).
- Handler methods: `OnGetAsync()`, `OnPostAsync()`, `OnPostDeleteAsync()`, `OnPostXxxAsync()`.
- `[ValidateAntiForgeryToken]` tự động cho POST trong Razor Pages — không cần thêm attribute thủ công.
- Partial views: prefix `_` — ví dụ `_CourseCard.cshtml`, `_DocumentRow.cshtml`, `_UserRow.cshtml`
- Dùng `TempData["SuccessMessage"]` và `TempData["ErrorMessage"]` để truyền thông báo sau redirect.
- Validate user identity qua `User.FindFirst(ClaimTypes.NameIdentifier)` và `User.FindFirst(ClaimTypes.Role)`.

### 4.3 Services & Interfaces
- Mỗi service phải có interface tương ứng trong `Interfaces/`.
- Interface phải có XML doc comment (`/// <summary>`) cho mỗi method.
- Service không được gọi trực tiếp `HttpContext` — nhận dữ liệu qua parameters.
- Trả về tuple `(bool Success, string ErrorMessage)` cho các operation có thể thất bại thay vì throw exception tùy tiện.
- Dùng `async/await` cho tất cả I/O operations (DB, HTTP, file).

### 4.4 Entity Models
- Chỉ chứa properties và navigation properties — không có methods, không có business logic.
- Không dùng `[Required]`, `[MaxLength]` annotation trên entity — cấu hình trong `OnModelCreating`.
- Navigation properties nullable (`?`) nếu optional, non-nullable nếu required.

### 4.5 Error Handling
- Ném `InvalidOperationException` cho lỗi business logic (validation, not found, forbidden).
- Ném `ArgumentException` / `ArgumentNullException` cho lỗi input.
- Không bắt `Exception` chung chung trong service — để PageModel xử lý và hiển thị lỗi.
- Log lỗi ra `Console.Error.WriteLine` tạm thời (chưa có logging framework).

### 4.6 File & Path
- Không hardcode absolute path (`C:\`, `D:\`, v.v.).
- Dùng `Path.Combine()` cho tất cả path operations.
- Dùng `app.Environment.WebRootPath` hoặc `Directory.GetCurrentDirectory()` làm gốc.
- Upload files lưu tại `wwwroot/uploads/{courseId}/{timestamp}_{filename}`.
- Relative URL trả về client: `/uploads/{courseId}/{filename}`.

### 4.7 Authentication & Authorization
- Cookie-based authentication, scheme: `CookieAuthenticationDefaults.AuthenticationScheme`.
- Ba roles: `"Admin"`, `"Student"`, và `"Lecturer"` (string literal, không dùng enum).
- Login path: `/Account/Login`, Logout path: `/Account/Logout`.
- Session expire: 24 giờ.
- **Lecturer** được gán vào Course qua entity `CourseLecturer` (many-to-many join table).
  - Lecturer chỉ upload/xóa document cho môn mà mình quản lý (`ManagedById`) hoặc được assign.
- **Student** chỉ xem document đã indexed.
- **Admin** quản lý tất cả (users, courses, settings) nhưng KHÔNG upload document học tập.

---

## 5. SignalR Hubs

Hệ thống sử dụng **SignalR** để cập nhật real-time cho client.

| Hub Class | Endpoint | Defined In |
|---|---|---|
| `TestQuestionHub` | `/hubs/testquestion` | `TestQuestionService.cs` |
| `BenchmarkHub` | `/hubs/benchmark` | `BenchmarkRunnerService.cs` |
| `DocumentUploadHub` | `/hubs/documentupload` | `DocumentService.cs` |
| `CourseHub` | `/hubs/course` | `CourseService.cs` |
| `UserHub` | `/hubs/user` | `UserService.cs` |
| `SystemSettingsHub` | `/hubs/systemsettings` | `SystemSettingService.cs` |

### Quy tắc:
- Hub classes **rỗng** — chỉ khai báo `public class XxxHub : Hub { }`, đặt inline trong file service tương ứng.
- Logic bắn event nằm trong Service, inject `IHubContext<THub>` qua constructor.
- Endpoint mapping nằm trong `Program.cs`: `app.MapHub<TestQuestionHub>("/hubs/testquestion");`
- Hub definitions thuộc project `PRN222.Services`, KHÔNG thuộc WebApp.

---

## 6. Python RAG Server

- Framework: **FastAPI** + **uvicorn**, port `8000`.
- Chạy từ thư mục `Python_RAG_Server/`: `uvicorn api_server:app --reload`
- Temp files lưu tại `temp_files/` (relative, tự tạo, tự xóa sau mỗi request).
- Supported embedding models: `bge-m3`, `e5`, `phobert`.
- Supported chunking strategies: `markdown_header`, `fixed_size`, `fixed_size_overlap`, `sentence`, `paragraph`.
- API endpoints:
  - `GET /api/health` — health check
  - `POST /api/embed` — generate embedding cho một đoạn text
  - `POST /api/parse-document` — parse file + chunk + embed, trả JSON

---

## 7. Cấu hình môi trường

- **Connection string:** `appsettings.json` → `ConnectionStrings:DefaultConnection`
  - Default: `Server=.\SQLEXPRESS;Database=PRN222_ChatbotDB;User Id=sa;Password=12345;TrustServerCertificate=True;`
  - Mỗi máy dev có thể override bằng `appsettings.Development.json` hoặc user secrets.
- **OpenAI API Key:** `appsettings.json` → `AIProviders:OpenAI:ApiKey`
  - Không commit key thật lên git public — dùng user secrets hoặc environment variable.
- **Python server URL:** `appsettings.json` → `AIProviders:PythonMicroservice:BaseUrl` (default: `http://localhost:8000/`)

---

## 8. Migrations

- Migration files nằm tại `PRN222.Repositories/Migrations/`.
- Tạo migration mới: chạy từ thư mục `PRN222_Project/`
  ```
  dotnet ef migrations add <TênMigration> --project PRN222.Repositories --startup-project PRN222.RazorWebApp
  ```
- Apply migration:
  ```
  dotnet ef database update --project PRN222.Repositories --startup-project PRN222.RazorWebApp
  ```
- Không sửa tay file migration đã tạo.

---

## 9. Build & Run

```bash
# Restore & build
cd PRN222_Project
dotnet restore
dotnet build

# Run web app
dotnet run --project PRN222.RazorWebApp

# Run Python server (cần Python 3.10+, venv đã setup)
cd Python_RAG_Server
uvicorn api_server:app --reload --port 8000
```

- Build phải **0 errors**. Warnings CS8618 (nullable) hiện tại được chấp nhận nhưng không được tăng thêm.
- Không dùng `dotnet run` với `--no-build` khi chưa build lần đầu.

---

## 10. Những điều KHÔNG được làm

- Không thêm project reference ngược chiều (ví dụ: `Models` tham chiếu `Services`).
- Không dùng `static` class cho services — phải qua DI.
- Không dùng `ViewData` thay `ViewBag` hoặc strongly-typed model nếu có thể dùng model.
- Không dùng `Thread.Sleep` — dùng `await Task.Delay` nếu cần delay.
- Không commit `appsettings.json` chứa API key thật lên repository public.
- Không thay đổi schema DB mà không tạo migration mới.
- Không xóa hoặc sửa migration đã được apply vào DB.
