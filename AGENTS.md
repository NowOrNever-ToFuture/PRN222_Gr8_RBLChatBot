# AGENTS.md — PRN222 Group 8 Assignment 2

Tài liệu này dành cho AI Agent (Copilot, Cursor, Kiro, v.v.) khi làm việc với codebase này.
Đọc kỹ trước khi sinh code, refactor, hoặc thêm tính năng mới.

---

## 1. Tổng quan kiến trúc

```
PRN222_Gr8_Asm2/
├── PRN222_Project/          # .NET 8 ASP.NET Core MVC
│   ├── PRN222.Models/       # Entity models — không có business logic
│   ├── PRN222.Repositories/ # EF Core DbContext + Migrations
│   ├── PRN222.Services/     # Business logic, Interfaces, DTOs
│   └── PRN222.WebApp/       # Controllers, Views, ViewModels, Program.cs
└── Python_RAG_Server/       # FastAPI microservice (embedding + document parsing)
    └── api_server.py
```

**Dependency flow (một chiều, không được đảo ngược):**
```
WebApp → Services → Repositories → Models
```

- `WebApp` KHÔNG được tham chiếu trực tiếp `Repositories` hay `Models` ngoài DI setup.
- `Services` KHÔNG được tham chiếu `WebApp`.
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
| PRN222.WebApp (Controllers) | `PRN222.WebApp.Controllers` |
| PRN222.WebApp (ViewModels) | `PRN222.WebApp.Models` |

### 3.2 Classes & Files
- **Entity models:** PascalCase, tên số ít — `User`, `Document`, `DocumentChunk`, `BenchmarkRun`
- **Interfaces:** prefix `I` — `IDocumentService`, `IChatService`, `IEmbeddingService`
- **Service implementations:** tên class = tên interface bỏ `I` — `DocumentService`, `ChatService`
- **Controllers:** suffix `Controller` — `DocumentsController`, `ChatController`
  - Tên controller dùng số nhiều nếu là CRUD resource: `Documents`, `Courses`, `Users`
  - Tên controller dùng số ít nếu là single-page feature: `Chat`, `Dashboard`, `Account`
- **DTOs:** suffix `DTO` hoặc `Dto` — `UploadDocumentDTO`, `PythonParseResponseDto`
- **ViewModels:** suffix `ViewModel` — `LoginViewModel`, `CreateUserViewModel`, `EditUserViewModel`
- **Hubs (SignalR):** suffix `Hub` — `TestQuestionHub`, `BenchmarkHub`

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

### 4.2 Controllers
- Luôn dùng `[Authorize]` ở class level, thêm `[AllowAnonymous]` cho action cụ thể nếu cần.
- Không viết business logic trong controller — chỉ gọi service, xử lý kết quả, trả về View/Redirect.
- Dùng `TempData["SuccessMessage"]` và `TempData["ErrorMessage"]` để truyền thông báo sau redirect.
- Validate user identity qua `User.FindFirst(ClaimTypes.NameIdentifier)` và `User.FindFirst(ClaimTypes.Role)`.
- Dùng `[ValidateAntiForgeryToken]` cho tất cả POST actions.

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
- Không bắt `Exception` chung chung trong service — để controller xử lý và hiển thị lỗi.
- Log lỗi ra `Console.Error.WriteLine` tạm thời (chưa có logging framework).

### 4.6 File & Path
- Không hardcode absolute path (`C:\`, `D:\`, v.v.).
- Dùng `Path.Combine()` cho tất cả path operations.
- Dùng `app.Environment.WebRootPath` hoặc `Directory.GetCurrentDirectory()` làm gốc.
- Upload files lưu tại `wwwroot/uploads/{courseId}/{timestamp}_{filename}`.
- Relative URL trả về client: `/uploads/{courseId}/{filename}`.

### 4.7 Authentication & Authorization
- Cookie-based authentication, scheme: `CookieAuthenticationDefaults.AuthenticationScheme`.
- Hai roles: `"Admin"` và `"Student"` (string literal, không dùng enum).
- Login path: `/Account/Login`, Logout path: `/Account/Logout`.
- Session expire: 24 giờ.

---

## 5. Python RAG Server

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

## 6. Cấu hình môi trường

- **Connection string:** `appsettings.json` → `ConnectionStrings:DefaultConnection`
  - Default: `Server=.\SQLEXPRESS;Database=PRN222_ChatbotDB;User Id=sa;Password=12345;TrustServerCertificate=True;`
  - Mỗi máy dev có thể override bằng `appsettings.Development.json` hoặc user secrets.
- **OpenAI API Key:** `appsettings.json` → `AIProviders:OpenAI:ApiKey`
  - Không commit key thật lên git public — dùng user secrets hoặc environment variable.
- **Python server URL:** `appsettings.json` → `AIProviders:PythonMicroservice:BaseUrl` (default: `http://localhost:8000/`)

---

## 7. Migrations

- Migration files nằm tại `PRN222.Repositories/Migrations/`.
- Tạo migration mới: chạy từ thư mục `PRN222_Project/`
  ```
  dotnet ef migrations add <TênMigration> --project PRN222.Repositories --startup-project PRN222.WebApp
  ```
- Apply migration:
  ```
  dotnet ef database update --project PRN222.Repositories --startup-project PRN222.WebApp
  ```
- Không sửa tay file migration đã tạo.

---

## 8. Build & Run

```bash
# Restore & build
cd PRN222_Project
dotnet restore
dotnet build

# Run web app
dotnet run --project PRN222.WebApp

# Run Python server (cần Python 3.10+, venv đã setup)
cd Python_RAG_Server
uvicorn api_server:app --reload --port 8000
```

- Build phải **0 errors**. Warnings CS8618 (nullable) hiện tại được chấp nhận nhưng không được tăng thêm.
- Không dùng `dotnet run` với `--no-build` khi chưa build lần đầu.

---

## 9. Những điều KHÔNG được làm

- Không thêm project reference ngược chiều (ví dụ: `Models` tham chiếu `Services`).
- Không dùng `static` class cho services — phải qua DI.
- Không dùng `ViewData` thay `ViewBag` hoặc strongly-typed model nếu có thể dùng model.
- Không dùng `Thread.Sleep` — dùng `await Task.Delay` nếu cần delay.
- Không commit `appsettings.json` chứa API key thật lên repository public.
- Không thay đổi schema DB mà không tạo migration mới.
- Không xóa hoặc sửa migration đã được apply vào DB.
