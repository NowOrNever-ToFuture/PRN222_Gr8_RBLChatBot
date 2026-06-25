using Microsoft.EntityFrameworkCore;
using PRN222.Models;

namespace PRN222.Repositories
{
    public class AppDbContext : DbContext
    {
        // DbSets for Entities
        public DbSet<User> Users { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentChunk> DocumentChunks { get; set; }
        
        // Chat & Conversation
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<Message> Messages { get; set; }
        
        // Benchmark & Testing
        public DbSet<TestQuestion> TestQuestions { get; set; }
        public DbSet<BenchmarkRun> BenchmarkRuns { get; set; }
        public DbSet<BenchmarkResult> BenchmarkResults { get; set; }
        
        // System Configuration
        public DbSet<SystemSetting> SystemSettings { get; set; }

        // Constructor for dependency injection
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Parameterless constructor for migrations
        public AppDbContext()
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ========== Configure Primary Keys with GUID ==========
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

            // ========== Configure Relationships ==========

            // Course - Document relationship (1 to Many)
            modelBuilder.Entity<Document>()
                .HasOne(d => d.Course)
                .WithMany(c => c.Documents)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            // Course - User (ManagedBy) relationship (Many to 1)
            modelBuilder.Entity<Course>()
                .HasOne(c => c.ManagedBy)
                .WithMany()
                .HasForeignKey(c => c.ManagedById)
                .OnDelete(DeleteBehavior.SetNull);

            // User (Owner) - Document relationship (1 to Many)
            modelBuilder.Entity<Document>()
                .HasOne(d => d.Owner)
                .WithMany(u => u.UploadedDocuments)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);

            // User - Course relationship (1 to Many)
            modelBuilder.Entity<User>()
                .HasOne(u => u.Course)
                .WithMany()
                .HasForeignKey(u => u.CourseId)
                .OnDelete(DeleteBehavior.SetNull);

            // Document - DocumentChunk relationship (1 to Many)
            modelBuilder.Entity<DocumentChunk>()
                .HasOne(dc => dc.Document)
                .WithMany(d => d.DocumentChunks)
                .HasForeignKey(dc => dc.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            // User - Conversation relationship (1 to Many)
            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.User)
                .WithMany(u => u.Conversations)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Conversation - Message relationship (1 to Many)
            modelBuilder.Entity<Message>()
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Course - TestQuestion relationship (1 to Many)
            modelBuilder.Entity<TestQuestion>()
                .HasOne(tq => tq.Course)
                .WithMany(c => c.TestQuestions)
                .HasForeignKey(tq => tq.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            // BenchmarkRun - BenchmarkResult relationship (1 to Many)
            modelBuilder.Entity<BenchmarkResult>()
                .HasOne(br => br.BenchmarkRun)
                .WithMany(run => run.Results)
                .HasForeignKey(br => br.BenchmarkRunId)
                .OnDelete(DeleteBehavior.Cascade);

            // TestQuestion - BenchmarkResult relationship (1 to Many)
            modelBuilder.Entity<BenchmarkResult>()
                .HasOne(br => br.TestQuestion)
                .WithMany(tq => tq.BenchmarkResults)
                .HasForeignKey(br => br.TestQuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // SystemSetting - Configure unique SettingKey
            modelBuilder.Entity<SystemSetting>()
                .HasIndex(ss => ss.SettingKey)
                .IsUnique();

            // Configure large text columns (NVARCHAR(MAX))
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
        }
    }
}
