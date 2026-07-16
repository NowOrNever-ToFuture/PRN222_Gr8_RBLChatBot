using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services;
using PRN222.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
    });

builder.Services.AddApplicationServices();
builder.Services.AddScoped<IEmbeddingService, OpenAiService>();

var pythonBaseUrl = builder.Configuration["AIProviders:PythonMicroservice:BaseUrl"];
builder.Services.AddHttpClient("PythonApi").ConfigureHttpClient(c =>
{
    c.BaseAddress = new Uri(pythonBaseUrl ?? "http://localhost:8000");
    c.Timeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddScoped<ILlmService>(sp =>
    sp.GetRequiredService<ILlmProviderFactory>().GetService("gpt"));

builder.Services.AddScoped<IEmbeddingService>(sp =>
    new LocalPythonEmbeddingService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("PythonApi"), "bge-m3"));
builder.Services.AddScoped<IEmbeddingService>(sp =>
    new LocalPythonEmbeddingService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("PythonApi"), "e5"));
builder.Services.AddScoped<IEmbeddingService>(sp =>
    new LocalPythonEmbeddingService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("PythonApi"), "phobert"));

builder.Services.AddScoped<AiModelFactory>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();

var app = builder.Build();

var uploadsPath = Path.Combine(app.Environment.WebRootPath, "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

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
            Description = "Triết học Mác - Lênin lí thuyết cơ bản",
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
            FullName = "Admin FPT",
            PasswordHash = passwordHash,
            Role = "Admin"
        });
        dbContext.SaveChanges();
    }

    if (!dbContext.Users.Any(u => u.Username == "student"))
    {
        byte[] salt = new byte[16] { 80, 82, 78, 50, 50, 50, 95, 83, 65, 76, 84, 95, 75, 69, 89, 33 };
        using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes("student123", salt, 10000, System.Security.Cryptography.HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(32);
        byte[] hashWithSalt = new byte[48];
        System.Buffer.BlockCopy(salt, 0, hashWithSalt, 0, 16);
        System.Buffer.BlockCopy(hash, 0, hashWithSalt, 16, 32);
        string passwordHash = Convert.ToBase64String(hashWithSalt);

        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "student",
            FullName = "Nguyễn Hoàng Nam",
            PasswordHash = passwordHash,
            Role = "Student"
        });
        dbContext.SaveChanges();
    }

    // Helper function to create password hash
    string GenerateHash(string password)
    {
        byte[] salt = new byte[16] { 80, 82, 78, 50, 50, 50, 95, 83, 65, 76, 84, 95, 75, 69, 89, 33 };
        using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(password, salt, 10000, System.Security.Cryptography.HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(32);
        byte[] hashWithSalt = new byte[48];
        System.Buffer.BlockCopy(salt, 0, hashWithSalt, 0, 16);
        System.Buffer.BlockCopy(hash, 0, hashWithSalt, 16, 32);
        return Convert.ToBase64String(hashWithSalt);
    }

    // Seed "VRN Lecturer" (Password: 123456)
    if (!dbContext.Users.Any(u => u.Username == "VRN Lecturer"))
    {
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "VRN Lecturer",
            FullName = "VRN Lecturer",
            PasswordHash = GenerateHash("123456"),
            Role = "Lecturer"
        });
    }

    // Seed "MLN Lecturer" (Password: 123456)
    if (!dbContext.Users.Any(u => u.Username == "MLN Lecturer"))
    {
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "MLN Lecturer",
            FullName = "MLN Lecturer",
            PasswordHash = GenerateHash("123456"),
            Role = "Lecturer"
        });
    }

    // Seed "SWD391" (Password: SWD391)
    if (!dbContext.Users.Any(u => u.Username == "SWD391"))
    {
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "SWD391",
            FullName = "SWD391 Lecturer",
            PasswordHash = GenerateHash("SWD391"),
            Role = "Lecturer"
        });
    }

    dbContext.SaveChanges();

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

    // Synchronize package quotas and descriptions to reflect request-based limits
    var existingPackages = dbContext.PricingPackages.ToList();
    bool databaseUpdated = false;
    
    var dbFree = existingPackages.FirstOrDefault(p => p.Name == "Free");
    if (dbFree != null && (dbFree.TokenQuota != 5000 || dbFree.Price != 0 || dbFree.DurationDays != 36500))
    {
        dbFree.TokenQuota = 5000;
        dbFree.Price = 0;
        dbFree.DurationDays = 36500;
        dbFree.Description = "Gói dùng thử miễn phí mặc định, tự động reset 5,000 Tokens sau mỗi 5 tiếng.";
        databaseUpdated = true;
    }
    
    // Deactivate the Standard package
    var dbStandard = existingPackages.FirstOrDefault(p => p.Name == "Standard");
    if (dbStandard != null && dbStandard.IsActive)
    {
        dbStandard.IsActive = false;
        databaseUpdated = true;
    }
    
    var dbVip = existingPackages.FirstOrDefault(p => p.Name == "VIP");
    if (dbVip != null && (dbVip.TokenQuota != 50000 || dbVip.Price != 50000 || dbVip.DurationDays != 30))
    {
        dbVip.TokenQuota = 50000;
        dbVip.Price = 50000;
        dbVip.DurationDays = 30;
        dbVip.Description = "Nạp thêm 50,000 Tokens chất lượng cao để học tập và ôn luyện.";
        databaseUpdated = true;
    }
    
    // Reset existing active Free subscriptions that are outdated to the new 5,000 quota
    if (dbFree != null)
    {
        var outdatedFreeSubs = dbContext.UserSubscriptions
            .Where(us => us.PricingPackageId == dbFree.Id && us.Status == "Active" && us.RemainingTokens < 5000)
            .ToList();
        if (outdatedFreeSubs.Any())
        {
            foreach (var sub in outdatedFreeSubs)
            {
                sub.RemainingTokens = 5000;
            }
            databaseUpdated = true;
        }
    }

    if (databaseUpdated)
    {
        dbContext.SaveChanges();
    }

    if (!dbContext.PricingPackages.Any())
    {
        dbContext.PricingPackages.AddRange(
            new PricingPackage
            {
                Id = Guid.NewGuid(),
                Name = "Free",
                Description = "Gói dùng thử miễn phí mặc định, tự động reset 5,000 Tokens sau mỗi 5 tiếng.",
                Price = 0,
                TokenQuota = 5000,
                DurationDays = 36500,
                IsActive = true
            },
            new PricingPackage
            {
                Id = Guid.NewGuid(),
                Name = "VIP",
                Description = "Nạp thêm 50,000 Tokens chất lượng cao để học tập và ôn luyện.",
                Price = 50000,
                TokenQuota = 50000,
                DurationDays = 30,
                IsActive = true
            }
        );
        dbContext.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
provider.Mappings[".md"] = "text/markdown";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = provider });

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapHub<TestQuestionHub>("/hubs/testquestion");
app.MapHub<BenchmarkHub>("/hubs/benchmark");
app.MapHub<DocumentUploadHub>("/hubs/documentupload");
app.MapHub<CourseHub>("/hubs/course");
app.MapHub<UserHub>("/hubs/user");
app.MapHub<SystemSettingsHub>("/hubs/systemsettings");
app.MapHub<TokenUsageHub>("/hubs/tokenusage");  // Phase 2
app.MapHub<PaymentHub>("/hubs/payment");         // Phase 3


app.MapMethods("/api/payment/webhook", new[] { "GET", "POST" }, async (HttpContext ctx, IPaymentService paymentService) =>
{
    try
    {
        string data = "";
        if (ctx.Request.Method == "POST")
        {
            using var reader = new System.IO.StreamReader(ctx.Request.Body);
            data = await reader.ReadToEndAsync();
        }
        else
        {
            data = ctx.Request.QueryString.Value ?? "";
            if (data.StartsWith("?"))
            {
                data = data.Substring(1);
            }
        }

        var result = await paymentService.ProcessWebhookAsync(data);
        if (result.Success)
        {
            return Results.Json(new { RspCode = "00", Message = "Confirm Success" });
        }
        else
        {
            Console.Error.WriteLine($"Webhook processing error: {result.Message}");
            return Results.Json(new { RspCode = "99", Message = result.Message });
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Webhook endpoint error: {ex.Message}");
        return Results.Json(new { RspCode = "99", Message = ex.Message });
    }
});

app.Run();

public class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }
}
