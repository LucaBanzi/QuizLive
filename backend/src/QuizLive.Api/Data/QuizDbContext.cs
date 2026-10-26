using Microsoft.EntityFrameworkCore;
using QuizLive.Api.Domain;

namespace QuizLive.Api.Data;

public class QuizDbContext(DbContextOptions<QuizDbContext> options) : DbContext(options)
{
    public DbSet<QuizSession> Sessions => Set<QuizSession>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Option> QuestionOptions => Set<Option>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<ScoreAdjustment> Adjustments => Set<ScoreAdjustment>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<QuizSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Phase).HasConversion<string>().HasMaxLength(30);
            // una sola sessione attiva
            e.HasIndex(x => x.IsActive).IsUnique().HasFilter("\"IsActive\" = true");
            e.HasOne<Question>().WithMany().HasForeignKey(x => x.CurrentQuestionId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Question>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(500);
            e.Property(x => x.MediaType).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.MediaUrl).HasMaxLength(300);
            e.Property(x => x.Explanation).HasMaxLength(500);
            e.HasOne<QuizSession>().WithMany().HasForeignKey(x => x.QuizSessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.QuizSessionId, x.OrderIndex }).IsUnique();
            e.HasMany(x => x.Options).WithOne().HasForeignKey(o => o.QuestionId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Option>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(200);
            e.HasIndex(x => new { x.QuestionId, x.Label }).IsUnique();
        });

        b.Entity<Team>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Nickname).HasMaxLength(24);
            e.Property(x => x.NicknameNormalized).HasMaxLength(24);
            e.Property(x => x.SpokespersonName).HasMaxLength(80);
            e.HasOne<QuizSession>().WithMany().HasForeignKey(x => x.QuizSessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.QuizSessionId, x.NicknameNormalized }).IsUnique();
            e.HasIndex(x => new { x.QuizSessionId, x.TableNumber }).IsUnique().HasFilter("\"IsActive\" = true");
            e.HasIndex(x => x.SessionToken);
        });

        b.Entity<Answer>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Team).WithMany(t => t.Answers).HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
            // barriera definitiva contro il doppio invio
            e.HasIndex(x => new { x.TeamId, x.QuestionId }).IsUnique();
        });

        b.Entity<ScoreAdjustment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Reason).HasMaxLength(200);
            e.Property(x => x.CreatedBy).HasMaxLength(50);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10);
            e.HasOne(x => x.Team).WithMany(t => t.Adjustments).HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AdminUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(50);
            e.HasIndex(x => x.Username).IsUnique();
        });
    }
}
