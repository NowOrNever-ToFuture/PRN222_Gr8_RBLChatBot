using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services;
using PRN222.Services.DTOs;
using PRN222.Services.Interfaces;

var runner = new AutomatedCheckRunner();
await runner.RunAsync();

internal sealed class AutomatedCheckRunner
{
    private readonly List<(string Name, Func<Task> Test)> _tests;

    public AutomatedCheckRunner()
    {
        _tests =
        [
            ("Update role clears ManagedById assignments", TestRoleChangeClearsManagedByAsync),
            ("CourseService rejects lecturer heading multiple subjects", TestHeadLecturerSingleDepartmentRuleAsync),
            ("DocumentService rejects upload by non-head lecturer", TestNonHeadLecturerCannotUploadAsync),
            ("DocumentService rejects admin upload", TestAdminCannotUploadAsync),
            ("DocumentService allows head lecturer upload and stores audit info", TestHeadLecturerUploadStoresHistoryAsync),
            ("DocumentService returns correct visibility by role", TestDocumentVisibilityRulesAsync)
        ];
    }

    public async Task RunAsync()
    {
        var passed = 0;
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("PRN222 automated checks");
        Console.WriteLine(new string('=', 60));

        foreach (var (name, test) in _tests)
        {
            try
            {
                await test();
                passed++;
                Console.WriteLine($"PASS  {name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL  {name}");
                Console.WriteLine($"      {ex.Message}");
            }
        }

        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"Summary: {passed}/{_tests.Count} checks passed.");
        Environment.ExitCode = passed == _tests.Count ? 0 : 1;
    }

    private async Task TestRoleChangeClearsManagedByAsync()
    {
        await using var db = CreateDbContext();
        var lecturer = new User { Id = Guid.NewGuid(), Username = "lect1", FullName = "Lecturer 1", PasswordHash = "hash", Role = "Lecturer" };
        var course1 = new Course { Id = Guid.NewGuid(), Code = "MLN", Name = "Mac Lenin", Description = "desc", CreatedDate = DateTime.UtcNow, ManagedById = lecturer.Id };
        var course2 = new Course { Id = Guid.NewGuid(), Code = "VRN", Name = "Lich Su Dang", Description = "desc", CreatedDate = DateTime.UtcNow, ManagedById = lecturer.Id };
        db.Users.Add(lecturer);
        db.Courses.AddRange(course1, course2);
        await db.SaveChangesAsync();

        var service = new UserService(db, new FakeAccountService(), new FakeHubContext<UserHub>());
        var result = await service.UpdateUserAsync(lecturer.Id, lecturer.Username, "Student", null);

        AssertTrue(result.Success, "Expected user update success.");
        AssertEqual(2, result.ClearedManagedCourseCount, "Expected both managed courses to be cleared.");
        AssertTrue((await db.Courses.Where(c => c.ManagedById == lecturer.Id).CountAsync()) == 0, "ManagedById should be cleared from all courses.");
    }

    private async Task TestHeadLecturerSingleDepartmentRuleAsync()
    {
        await using var db = CreateDbContext();
        var lecturer = new User { Id = Guid.NewGuid(), Username = "lect1", FullName = "Lecturer 1", PasswordHash = "hash", Role = "Lecturer" };
        db.Users.Add(lecturer);
        db.Courses.Add(new Course { Id = Guid.NewGuid(), Code = "MLN", Name = "Mac Lenin", Description = "desc", CreatedDate = DateTime.UtcNow, ManagedById = lecturer.Id });
        await db.SaveChangesAsync();

        var service = new CourseService(db, new FakeHubContext<CourseHub>());

        try
        {
            await service.CreateCourseAsync("Lich Su Dang", "VRN", "desc", lecturer.Id);
            throw new Exception("Expected exception was not thrown.");
        }
        catch (InvalidOperationException ex)
        {
            AssertTrue(ex.Message.Contains("tối đa 1 bộ môn", StringComparison.OrdinalIgnoreCase), "Expected single-head-lecturer validation message.");
        }
    }

    private async Task TestNonHeadLecturerCannotUploadAsync()
    {
        await using var db = CreateDbContext();
        var head = new User { Id = Guid.NewGuid(), Username = "head", FullName = "Head", PasswordHash = "hash", Role = "Lecturer" };
        var other = new User { Id = Guid.NewGuid(), Username = "other", FullName = "Other", PasswordHash = "hash", Role = "Lecturer" };
        var course = new Course { Id = Guid.NewGuid(), Code = "MLN", Name = "Mac Lenin", Description = "desc", CreatedDate = DateTime.UtcNow, ManagedById = head.Id };
        db.Users.AddRange(head, other);
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var service = CreateDocumentService(db);
        var dto = new UploadDocumentDTO { CourseId = course.Id, File = CreateFormFile("test.pdf", "sample content") };

        try
        {
            await service.UploadDocumentAsync(dto, CreateTempUploadsPath(), other.Id, null);
            throw new Exception("Expected upload rejection was not thrown.");
        }
        catch (InvalidOperationException ex)
        {
            AssertTrue(ex.Message.Contains("không phải Trưởng bộ môn", StringComparison.OrdinalIgnoreCase), "Expected non-head lecturer rejection.");
        }
    }

    private async Task TestAdminCannotUploadAsync()
    {
        await using var db = CreateDbContext();
        var admin = new User { Id = Guid.NewGuid(), Username = "admin", FullName = "Admin", PasswordHash = "hash", Role = "Admin" };
        var course = new Course { Id = Guid.NewGuid(), Code = "MLN", Name = "Mac Lenin", Description = "desc", CreatedDate = DateTime.UtcNow };
        db.Users.Add(admin);
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var service = CreateDocumentService(db);
        var dto = new UploadDocumentDTO { CourseId = course.Id, File = CreateFormFile("test.pdf", "sample content") };

        try
        {
            await service.UploadDocumentAsync(dto, CreateTempUploadsPath(), admin.Id, null);
            throw new Exception("Expected upload rejection was not thrown.");
        }
        catch (InvalidOperationException ex)
        {
            AssertTrue(ex.Message.Contains("Admin", StringComparison.OrdinalIgnoreCase), "Expected admin upload rejection.");
        }
    }

    private async Task TestHeadLecturerUploadStoresHistoryAsync()
    {
        await using var db = CreateDbContext();
        var head = new User { Id = Guid.NewGuid(), Username = "head", FullName = "Head Lecturer", PasswordHash = "hash", Role = "Lecturer" };
        var course = new Course { Id = Guid.NewGuid(), Code = "MLN", Name = "Mac Lenin", Description = "desc", CreatedDate = DateTime.UtcNow, ManagedById = head.Id };
        db.Users.Add(head);
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var service = CreateDocumentService(db);
        var dto = new UploadDocumentDTO { CourseId = course.Id, File = CreateFormFile("test.pdf", "sample content") };
        var uploadsPath = CreateTempUploadsPath();

        var document = await service.UploadDocumentAsync(dto, uploadsPath, head.Id, null);

        AssertEqual(head.Id, document.OwnerId, "Uploaded document should store owner id.");
        AssertEqual(course.Id, document.CourseId, "Uploaded document should keep course id.");
        AssertTrue(document.UploadDate > DateTime.UtcNow.AddMinutes(-1), "UploadDate should be set to current time.");
        AssertTrue(File.Exists(document.FilePath), "Uploaded file should exist on disk.");
    }

    private async Task TestDocumentVisibilityRulesAsync()
    {
        await using var db = CreateDbContext();
        var head = new User { Id = Guid.NewGuid(), Username = "head", FullName = "Head Lecturer", PasswordHash = "hash", Role = "Lecturer" };
        var lecturer = new User { Id = Guid.NewGuid(), Username = "lect", FullName = "Lecturer", PasswordHash = "hash", Role = "Lecturer" };
        var student = new User { Id = Guid.NewGuid(), Username = "student", FullName = "Student", PasswordHash = "hash", Role = "Student" };
        var course = new Course { Id = Guid.NewGuid(), Code = "MLN", Name = "Mac Lenin", Description = "desc", CreatedDate = DateTime.UtcNow, ManagedById = head.Id };
        var completed = new Document { Id = Guid.NewGuid(), FileName = "done.pdf", FilePath = "done.pdf", FileSize = 1, UploadDate = DateTime.UtcNow, CourseId = course.Id, OwnerId = head.Id, Status = "Completed" };
        var pending = new Document { Id = Guid.NewGuid(), FileName = "pending.pdf", FilePath = "pending.pdf", FileSize = 1, UploadDate = DateTime.UtcNow, CourseId = course.Id, OwnerId = head.Id, Status = "Pending" };
        db.Users.AddRange(head, lecturer, student);
        db.Courses.Add(course);
        db.Documents.AddRange(completed, pending);
        await db.SaveChangesAsync();

        var service = CreateDocumentService(db);
        var studentDocs = await service.GetDocumentsAsync(student.Id, "Student", course.Id);
        var lecturerDocs = await service.GetDocumentsAsync(lecturer.Id, "Lecturer", course.Id);

        AssertEqual(1, studentDocs.Count, "Student should only see completed documents.");
        AssertEqual("Completed", studentDocs[0].Status, "Student should only receive completed docs.");
        AssertEqual(2, lecturerDocs.Count, "Lecturer viewer should see all documents in selected course.");
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static DocumentService CreateDocumentService(AppDbContext db)
    {
        return new DocumentService(
            db,
            new FakeDocumentProcessingService(),
            new AiModelFactory(Array.Empty<IEmbeddingService>()),
            new FakeSystemSettingService(),
            new FakeHubContext<DocumentUploadHub>());
    }

    private static IFormFile CreateFormFile(string fileName, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }

    private static string CreateTempUploadsPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "prn222-automated-checks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"{message} Expected: {expected}. Actual: {actual}.");
        }
    }
}

internal sealed class FakeAccountService : IAccountService
{
    public Task<User?> LoginAsync(string username, string password) => Task.FromResult<User?>(null);
    public Task<(bool Success, string ErrorMessage)> RegisterAsync(string username, string password) => Task.FromResult((true, string.Empty));
    public string HashPassword(string password) => $"hashed::{password}";
    public bool VerifyPassword(string passwordPlain, string passwordHash) => passwordHash == HashPassword(passwordPlain);
}

internal sealed class FakeDocumentProcessingService : IDocumentProcessingService
{
    public Task<string> ExtractTextAsync(string filePath) => Task.FromResult(string.Empty);
    public List<string> SplitByFixedSize(string text, int chunkSize = 500, int overlap = 50) => new();
    public List<string> SplitBySentence(string text, int maxChunkSize = 500) => new();
    public Task<bool> UploadAndProcessDocumentAsync(IFormFile file, Guid courseId, string modelName = "bge-m3", string chunkStrategy = "markdown_header") => Task.FromResult(true);
    public Task<PythonParseResponseDto> ParseDocumentAsync(string filePath, string modelName = "bge-m3", string chunkStrategy = "markdown_header") => Task.FromResult(new PythonParseResponseDto());
}

internal sealed class FakeSystemSettingService : ISystemSettingService
{
    private readonly Dictionary<string, string> _settings = new();
    public Task<string> GetSettingValueAsync(string settingKey) => Task.FromResult(_settings.TryGetValue(settingKey, out var value) ? value : string.Empty);
    public Task<SystemSetting> GetSettingAsync(string settingKey) => Task.FromResult(new SystemSetting { Id = Guid.NewGuid(), SettingKey = settingKey, SettingValue = _settings.GetValueOrDefault(settingKey, string.Empty), Description = string.Empty, SettingType = "String", CreatedDate = DateTime.UtcNow });
    public Task<List<SystemSetting>> GetAllSettingsAsync() => Task.FromResult(_settings.Select(kv => new SystemSetting { Id = Guid.NewGuid(), SettingKey = kv.Key, SettingValue = kv.Value, Description = string.Empty, SettingType = "String", CreatedDate = DateTime.UtcNow }).ToList());
    public Task SetSettingAsync(string settingKey, string settingValue, string description = "", string settingType = "String") { _settings[settingKey] = settingValue; return Task.CompletedTask; }
    public Task DeleteSettingAsync(string settingKey) { _settings.Remove(settingKey); return Task.CompletedTask; }
    public Task<bool> SettingExistsAsync(string settingKey) => Task.FromResult(_settings.ContainsKey(settingKey));
}

internal sealed class FakeHubContext<THub> : IHubContext<THub> where THub : Hub
{
    public IHubClients Clients { get; } = new FakeHubClients();
    public IGroupManager Groups { get; } = new FakeGroupManager();
}

internal sealed class FakeHubClients : IHubClients
{
    private readonly IClientProxy _proxy = new FakeClientProxy();
    public IClientProxy All => _proxy;
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => _proxy;
    public IClientProxy Client(string connectionId) => _proxy;
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => _proxy;
    public IClientProxy Group(string groupName) => _proxy;
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _proxy;
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => _proxy;
    public IClientProxy User(string userId) => _proxy;
    public IClientProxy Users(IReadOnlyList<string> userIds) => _proxy;
}

internal sealed class FakeClientProxy : IClientProxy
{
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeGroupManager : IGroupManager
{
    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
