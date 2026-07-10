using Microsoft.AspNetCore.Authentication.Cookies;
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
builder.Services.AddScoped<ILlmService, OpenAiService>();
builder.Services.AddScoped<IEmbeddingService, OpenAiService>();

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

builder.Services.AddScoped<AiModelFactory>();
builder.Services.AddSignalR();

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

    if (!dbContext.Users.Any(u => u.Username == "lecturer"))
    {
        byte[] salt = new byte[16] { 80, 82, 78, 50, 50, 50, 95, 83, 65, 76, 84, 95, 75, 69, 89, 33 };
        using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes("lecturer123", salt, 10000, System.Security.Cryptography.HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(32);
        byte[] hashWithSalt = new byte[48];
        System.Buffer.BlockCopy(salt, 0, hashWithSalt, 0, 16);
        System.Buffer.BlockCopy(hash, 0, hashWithSalt, 16, 32);
        string passwordHash = Convert.ToBase64String(hashWithSalt);

        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Username = "lecturer01",
            FullName = "Nguyễn Văn A",
            PasswordHash = passwordHash,
            Role = "Lecturer"
        };

        dbContext.Users.Add(lecturer);
        dbContext.SaveChanges();

        var firstCourse = dbContext.Courses.FirstOrDefault();
        if (firstCourse != null && !dbContext.CourseLecturers.Any(cl => cl.CourseId == firstCourse.Id && cl.LecturerId == lecturer.Id))
        {
            dbContext.CourseLecturers.Add(new CourseLecturer
            {
                Id = Guid.NewGuid(),
                CourseId = firstCourse.Id,
                LecturerId = lecturer.Id
            });
            dbContext.SaveChanges();
        }
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

    if (!dbContext.PricingPackages.Any())
    {
        dbContext.PricingPackages.AddRange(
            new PricingPackage
            {
                Id = Guid.NewGuid(),
                Name = "Free",
                Description = "Gói dùng thử miễn phí cho mọi tài khoản mới.",
                Price = 0,
                TokenQuota = 10000,
                DurationDays = 30,
                IsActive = true
            },
            new PricingPackage
            {
                Id = Guid.NewGuid(),
                Name = "Standard",
                Description = "Phù hợp cho học sinh học tập cơ bản hàng ngày.",
                Price = 50000,
                TokenQuota = 100000,
                DurationDays = 30,
                IsActive = true
            },
            new PricingPackage
            {
                Id = Guid.NewGuid(),
                Name = "VIP",
                Description = "Hạn mức lớn dành cho học tập chuyên sâu và tài liệu dài.",
                Price = 100000,
                TokenQuota = 250000,
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

app.MapHub<PaymentHub>("/hubs/payment");

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
