using Microsoft.Extensions.DependencyInjection;
using PRN222.Repositories;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public static class DependencyInjection
    {
        /// <summary>
        /// Registers all services and repositories for dependency injection.
        /// Call this method in Program.cs: builder.Services.AddApplicationServices();
        /// </summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Register AppDbContext
            services.AddScoped<AppDbContext>();

            // Register Services
            services.AddScoped<IDocumentService, DocumentService>();
            services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
            services.AddScoped<ICourseService, CourseService>();
            services.AddScoped<ISystemSettingService, SystemSettingService>();
            services.AddScoped<IAccountService, AccountService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ITestQuestionService, TestQuestionService>();
            services.AddScoped<IImportService, ImportService>();
            services.AddScoped<ILlmProviderFactory, LlmProviderFactory>();
            // Judge dùng GPT (qua ILlmService => provider "gpt"): tránh Gemini tự chấm
            // chính nó và không đốt quota Gemini free-tier (chỉ ~20 request/ngày).
            services.AddScoped<ILlmJudgeService, LlmJudgeService>();
            services.AddScoped<IBenchmarkRunnerService, BenchmarkRunnerService>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IChatService, ChatService>();

            // Phase 2 - Token Report (service sẽ được thành viên B implement sau)
            services.AddScoped<ITokenUsageService, TokenUsageService>();

            // Phase 3 - Payments (service sẽ được thành viên C implement sau)
            services.AddScoped<IPaymentService, PaymentService>();

            return services;
        }
    }
}

