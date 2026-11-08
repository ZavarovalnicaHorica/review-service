using Horica.ReviewService.Domain;
using Microsoft.EntityFrameworkCore;

namespace Horica.ReviewService.Infrastructure.Persistence;

public class ReviewDbContext : DbContext
{
    public ReviewDbContext(DbContextOptions<ReviewDbContext> options)
        : base(options)
    {
    }

    public DbSet<ReviewTask> ReviewTasks => Set<ReviewTask>();

    public DbSet<ReviewHistoryEntry> History => Set<ReviewHistoryEntry>();

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReviewTask>(e =>
        {
            e.ToTable("review_tasks");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.ClaimId).HasColumnName("claim_id").HasMaxLength(100).IsRequired();
            e.Property(x => x.PolicyId).HasColumnName("policy_id").HasMaxLength(100);
            e.Property(x => x.CustomerId).HasColumnName("customer_id").HasMaxLength(100);
            e.Property(x => x.ClaimedAmount).HasColumnName("claimed_amount").HasPrecision(18, 2);
            e.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
            e.Property(x => x.RiskScore).HasColumnName("risk_score");
            e.Property(x => x.RiskReasons).HasColumnName("risk_reasons").HasColumnType("text[]");
            e.Property(x => x.AgentRecommendation).HasColumnName("agent_recommendation").HasMaxLength(100);
            e.Property(x => x.AgentSummary).HasColumnName("agent_summary");

            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.AssignedTo).HasColumnName("assigned_to");
            e.Property(x => x.AssignedToName).HasColumnName("assigned_to_name");
            e.Property(x => x.AssignedAt).HasColumnName("assigned_at");

            e.Property(x => x.Decision).HasColumnName("decision").HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DecisionReason).HasColumnName("decision_reason");
            e.Property(x => x.DecidedBy).HasColumnName("decided_by");
            e.Property(x => x.DecidedByName).HasColumnName("decided_by_name");
            e.Property(x => x.DecidedAt).HasColumnName("decided_at");

            e.Property(x => x.SourceEventId).HasColumnName("source_event_id").HasMaxLength(300).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();

            // A claim can have many tasks over time (e.g. after "needs more info"), but only one open task.
            e.HasIndex(x => x.ClaimId)
                .IsUnique()
                .HasFilter("status IN ('Pending', 'InProgress')")
                .HasDatabaseName("ux_review_tasks_open_claim");

            e.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("ix_review_tasks_status_created");
            e.HasIndex(x => new { x.ClaimId, x.CreatedAt }).HasDatabaseName("ix_review_tasks_claim_created");
        });

        modelBuilder.Entity<ReviewHistoryEntry>(e =>
        {
            e.ToTable("review_history");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.ReviewTaskId).HasColumnName("review_task_id");
            e.Property(x => x.ClaimId).HasColumnName("claim_id").HasMaxLength(100).IsRequired();
            e.Property(x => x.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Actor).HasColumnName("actor");
            e.Property(x => x.ActorName).HasColumnName("actor_name");
            e.Property(x => x.Details).HasColumnName("details");
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at");

            e.HasOne<ReviewTask>()
                .WithMany()
                .HasForeignKey(x => x.ReviewTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.ReviewTaskId, x.OccurredAt }).HasDatabaseName("ix_review_history_task_time");
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_messages");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Topic).HasColumnName("topic").HasMaxLength(200).IsRequired();
            e.Property(x => x.MessageKey).HasColumnName("message_key").HasMaxLength(200).IsRequired();
            e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(200).IsRequired();
            e.Property(x => x.Payload).HasColumnName("payload").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ProcessedAt).HasColumnName("processed_at");
            e.Property(x => x.Attempts).HasColumnName("attempts");
            e.Property(x => x.LastError).HasColumnName("last_error");

            e.HasIndex(x => x.CreatedAt)
                .HasFilter("processed_at IS NULL")
                .HasDatabaseName("ix_outbox_pending");
        });

        modelBuilder.Entity<ProcessedEvent>(e =>
        {
            e.ToTable("processed_events");
            e.HasKey(x => x.EventId);

            e.Property(x => x.EventId).HasColumnName("event_id").HasMaxLength(300);
            e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(200).IsRequired();
            e.Property(x => x.ProcessedAt).HasColumnName("processed_at");
        });
    }
}
