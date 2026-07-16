using Microsoft.EntityFrameworkCore;
using PRN222.Models;

namespace PRN222.Repositories
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<CourseLecturer> CourseLecturers { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentChunk> DocumentChunks { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<TestQuestion> TestQuestions { get; set; }
        public DbSet<BenchmarkRun> BenchmarkRuns { get; set; }
        public DbSet<BenchmarkResult> BenchmarkResults { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<PricingPackage> PricingPackages { get; set; }
        public DbSet<UserSubscription> UserSubscriptions { get; set; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }

        // Phase 2 - Token Report
        public DbSet<TokenUsageLog> TokenUsageLogs { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public AppDbContext()
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasKey(u => u.Id);
            modelBuilder.Entity<User>()
                .Property(u => u.Id)
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<Course>()
                .HasKey(c => c.Id);
            modelBuilder.Entity<Course>()
                .Property(c => c.Id)
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<CourseLecturer>()
                .HasKey(cl => cl.Id);
            modelBuilder.Entity<CourseLecturer>()
                .Property(cl => cl.Id)
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<Document>()
                .HasKey(d => d.Id);
            modelBuilder.Entity<Document>()
                .Property(d => d.Id)
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<DocumentChunk>()
                .HasKey(dc => dc.Id);
            modelBuilder.Entity<DocumentChunk>()
                .Property(dc => dc.Id)
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<Conversation>()
                .HasKey(c => c.Id);
            modelBuilder.Entity<Conversation>()
                .Property(c => c.Id)
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<Message>()
                .HasKey(m => m.Id);
            modelBuilder.Entity<Message>()
                .Property(m => m.Id)
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<TestQuestion>()
                .HasKey(tq => tq.Id);
            modelBuilder.Entity<TestQuestion>()
                .Property(tq => tq.Id)
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<BenchmarkRun>()
                .HasKey(br => br.Id);
            modelBuilder.Entity<BenchmarkRun>()
                .Property(br => br.Id)
                .HasDefaultValueSql("NEWSEQUENTIALID()");
            modelBuilder.Entity<BenchmarkRun>()
                .HasIndex(br => br.BenchmarkBatchId);

            modelBuilder.Entity<BenchmarkResult>()
                .HasKey(br => br.Id);
            modelBuilder.Entity<BenchmarkResult>()
                .Property(br => br.Id)
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<SystemSetting>()
                .HasKey(ss => ss.Id);
            modelBuilder.Entity<SystemSetting>()
                .Property(ss => ss.Id)
                .HasDefaultValueSql("NEWSEQUENTIALID()");

            modelBuilder.Entity<Document>()
                .HasOne(d => d.Course)
                .WithMany(c => c.Documents)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Course>()
                .HasOne(c => c.ManagedBy)
                .WithMany()
                .HasForeignKey(c => c.ManagedById)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CourseLecturer>()
                .HasOne(cl => cl.Course)
                .WithMany(c => c.CourseLecturers)
                .HasForeignKey(cl => cl.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseLecturer>()
                .HasOne(cl => cl.Lecturer)
                .WithMany(u => u.TeachingAssignments)
                .HasForeignKey(cl => cl.LecturerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Document>()
                .HasOne(d => d.Owner)
                .WithMany(u => u.UploadedDocuments)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<DocumentChunk>()
                .HasOne(dc => dc.Document)
                .WithMany(d => d.DocumentChunks)
                .HasForeignKey(dc => dc.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.User)
                .WithMany(u => u.Conversations)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TestQuestion>()
                .HasOne(tq => tq.Course)
                .WithMany(c => c.TestQuestions)
                .HasForeignKey(tq => tq.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BenchmarkResult>()
                .HasOne(br => br.BenchmarkRun)
                .WithMany(run => run.Results)
                .HasForeignKey(br => br.BenchmarkRunId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BenchmarkResult>()
                .HasOne(br => br.TestQuestion)
                .WithMany(tq => tq.BenchmarkResults)
                .HasForeignKey(br => br.TestQuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SystemSetting>()
                .HasIndex(ss => ss.SettingKey)
                .IsUnique();

            modelBuilder.Entity<CourseLecturer>()
                .HasIndex(cl => new { cl.CourseId, cl.LecturerId })
                .IsUnique();

            modelBuilder.Entity<DocumentChunk>()
                .Property(dc => dc.VectorData)
                .HasColumnType("nvarchar(max)");

            modelBuilder.Entity<DocumentChunk>()
                .Property(dc => dc.Content)
                .HasColumnType("nvarchar(max)");

            modelBuilder.Entity<TestQuestion>()
                .Property(tq => tq.GroundTruth)
                .HasColumnType("nvarchar(max)");

            modelBuilder.Entity<BenchmarkResult>()
                .Property(br => br.BotAnswer)
                .HasColumnType("nvarchar(max)");

            // PricingPackage
            modelBuilder.Entity<PricingPackage>()
                .HasKey(p => p.Id);
            modelBuilder.Entity<PricingPackage>()
                .Property(p => p.Id)
                .HasDefaultValueSql("NEWSEQUENTIALID()");
            modelBuilder.Entity<PricingPackage>()
                .Property(p => p.Description)
                .HasColumnType("nvarchar(max)");

            // UserSubscription
            modelBuilder.Entity<UserSubscription>()
                .HasKey(us => us.Id);
            modelBuilder.Entity<UserSubscription>()
                .Property(us => us.Id)
                .HasDefaultValueSql("NEWSEQUENTIALID()");
            modelBuilder.Entity<UserSubscription>()
                .HasOne(us => us.User)
                .WithMany(u => u.UserSubscriptions)
                .HasForeignKey(us => us.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<UserSubscription>()
                .HasOne(us => us.PricingPackage)
                .WithMany(p => p.UserSubscriptions)
                .HasForeignKey(us => us.PricingPackageId)
                .OnDelete(DeleteBehavior.Restrict);

            // PaymentTransaction
            modelBuilder.Entity<PaymentTransaction>()
                .HasKey(pt => pt.Id);
            modelBuilder.Entity<PaymentTransaction>()
                .Property(pt => pt.Id)
                .HasDefaultValueSql("NEWSEQUENTIALID()");
            modelBuilder.Entity<PaymentTransaction>()
                .Property(pt => pt.TransactionCode)
                .HasColumnType("nvarchar(max)");
            modelBuilder.Entity<PaymentTransaction>()
                .HasOne(pt => pt.User)
                .WithMany(u => u.PaymentTransactions)
                .HasForeignKey(pt => pt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<PaymentTransaction>()
                .HasOne(pt => pt.PricingPackage)
                .WithMany()
                .HasForeignKey(pt => pt.PricingPackageId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
