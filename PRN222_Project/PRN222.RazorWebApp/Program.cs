using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services;
using PRN222.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add Razor Pages
builder.Services.AddRazorPages();

// Add DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Authentication (Cookie-based)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
    });

// Add Application Services
builder.Services.AddApplicationServices();

// LLM Service
builder.Services.AddScoped<ILlmService, OpenAiService>();

// Embedding Services
builder.Services.AddScoped<IEmbeddingService, OpenAiService>();

// HttpClient for Python Server
var pythonBaseUrl = builder.Configuration["AIProviders:PythonMicroservice:BaseUrl"];
builder.Services.AddHttpClient("PythonApi").ConfigureHttpClient(c =>
{
    c.BaseAddress = new Uri(pythonBaseUrl ?? "http://localhost:8000");
    c.Timeout = TimeSpan.FromMinutes(30);
});
builder.Services.AddScoped<IEmbeddingService>(sp =>
    new LocalPythonEmbeddingService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("PythonApi"), "bge-m3"));
builder.Services.AddScoped<IEmbeddingService>(sp =>
    new LocalPythonEmbeddingService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("PythonApi"), "e5"));
builder.Services.AddScoped<IEmbeddingService>(sp =>
    new LocalPythonEmbeddingService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("PythonApi"), "phobert"));

// AI Model Factory
builder.Services.AddScoped<AiModelFactory>();

// SignalR
builder.Services.AddSignalR();

var app = builder.Build();

// Create uploads directory
var uploadsPath = Path.Combine(app.Environment.WebRootPath, "uploads");
if (!Directory.Exists(uploadsPath))
    Directory.CreateDirectory(uploadsPath);

// Data Seeding
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!dbContext.Courses.Any())
    {
        dbContext.Courses.Add(new Course
        {
            Id = Guid.NewGuid(),
            Name = "Triết học Mác - Lênin",
            Code = "MLN",
            Description = "Triết học Mác - Lênin: Nền tảng lý thuyết cơ bản",
            CreatedDate = DateTime.UtcNow
        });
        dbContext.SaveChanges();
    }

    if (!dbContext.Users.Any(u => u.Username == "admin"))
    {
        byte[] salt = new byte[16] { 80, 82, 78, 50, 50, 50, 95, 83, 65, 76, 84, 95, 75, 69, 89, 33 };
        using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes("admin123", salt, 10000, System.Security.Cryptography.HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(32);
        byte[] hashWithSalt = new byte[48];
        System.Buffer.BlockCopy(salt, 0, hashWithSalt, 0, 16);
        System.Buffer.BlockCopy(hash, 0, hashWithSalt, 16, 32);
        string passwordHash = Convert.ToBase64String(hashWithSalt);

        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            FullName = "Administrator",
            PasswordHash = passwordHash,
            Role = "Admin"
        });
        dbContext.SaveChanges();
    }

    if (!dbContext.SystemSettings.Any())
    {
        dbContext.SystemSettings.AddRange(
            new SystemSetting { Id = Guid.NewGuid(), SettingKey = "ActiveEmbeddingModel", SettingValue = "bge-m3", Description = "Active embedding model", SettingType = "String", CreatedDate = DateTime.UtcNow },
            new SystemSetting { Id = Guid.NewGuid(), SettingKey = "ActiveLLM", SettingValue = "gpt-4o-mini", Description = "Active language model", SettingType = "String", CreatedDate = DateTime.UtcNow },
            new SystemSetting { Id = Guid.NewGuid(), SettingKey = "ChunkSize", SettingValue = "500", Description = "Document chunk size", SettingType = "Integer", CreatedDate = DateTime.UtcNow },
            new SystemSetting { Id = Guid.NewGuid(), SettingKey = "ActiveChunkingStrategy", SettingValue = "markdown_header", Description = "Active chunking strategy", SettingType = "String", CreatedDate = DateTime.UtcNow }
        );
        dbContext.SaveChanges();
    }
}

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Static files with .md support
var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
provider.Mappings[".md"] = "text/markdown";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = provider });

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// SignalR Hubs
app.MapHub<TestQuestionHub>("/hubs/testquestion");
app.MapHub<BenchmarkHub>("/hubs/benchmark");

app.Run();
