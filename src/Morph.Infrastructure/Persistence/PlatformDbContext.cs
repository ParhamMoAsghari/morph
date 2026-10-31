using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Morph.Identity.Users;
using Morph.Infrastructure.Persistence.Audit;
using Morph.Infrastructure.Persistence.Credentials;
using Morph.Infrastructure.Persistence.EntityEngine;
using Morph.Infrastructure.Persistence.Files;
using Morph.Infrastructure.Persistence.Messaging;
using Morph.Infrastructure.Persistence.Quotas;
using Morph.Infrastructure.Persistence.ProcessEngine;
using Morph.Infrastructure.Persistence.Webhooks;
using Morph.Tenancy.Environments;
using Morph.Tenancy.Memberships;
using Morph.Tenancy.Organizations;
using Morph.Tenancy.Projects;

namespace Morph.Infrastructure.Persistence;

public sealed class PlatformDbContext
    : IdentityDbContext<PlatformUser, IdentityRole<Guid>, Guid>,
      IDataProtectionKeyContext
{
    public PlatformDbContext(DbContextOptions<PlatformDbContext> options)
        : base(options)
    {
    }

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<AppEnvironment> Environments => Set<AppEnvironment>();

    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

    public DbSet<EntityDefinitionRecord> EntityDefinitions => Set<EntityDefinitionRecord>();

    public DbSet<DefinitionVersionRecord> DefinitionVersions => Set<DefinitionVersionRecord>();

    public DbSet<StorageMappingRecord> StorageMappings => Set<StorageMappingRecord>();

    public DbSet<MigrationRunRecord> MigrationRuns => Set<MigrationRunRecord>();

    public DbSet<FileBucketRecord> FileBuckets => Set<FileBucketRecord>();

    public DbSet<StoredFileRecord> StoredFiles => Set<StoredFileRecord>();

    public DbSet<OutboxMessageRecord> OutboxMessages => Set<OutboxMessageRecord>();

    public DbSet<AuditEventRecord> AuditEvents => Set<AuditEventRecord>();

    public DbSet<UsageCounterRecord> UsageCounters => Set<UsageCounterRecord>();

    public DbSet<WebhookSubscriptionRecord> WebhookSubscriptions => Set<WebhookSubscriptionRecord>();

    public DbSet<ApiCredentialRecord> ApiCredentials => Set<ApiCredentialRecord>();

    public DbSet<ProcessDefinitionRecord> ProcessDefinitions => Set<ProcessDefinitionRecord>();

    public DbSet<ProcessVersionRecord> ProcessVersions => Set<ProcessVersionRecord>();

    public DbSet<ProcessInstanceRecord> ProcessInstances => Set<ProcessInstanceRecord>();

    public DbSet<ProcessActivityLogRecord> ProcessActivityLogs => Set<ProcessActivityLogRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("platform");

        builder.UseOpenIddict();

        builder.Entity<PlatformUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(x => x.DisplayName).HasMaxLength(200);
            entity.Property(x => x.CreatedAt).IsRequired();
        });

        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        builder.Entity<DataProtectionKey>(entity =>
        {
            entity.ToTable("data_protection_keys");
        });

        builder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Slug).IsUnique();
        });

        builder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.Slug }).IsUnique();
            entity.HasOne(x => x.Organization)
                .WithMany(x => x.Projects)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AppEnvironment>(entity =>
        {
            entity.ToTable("environments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SchemaName).HasMaxLength(128);
            entity.HasIndex(x => new { x.ProjectId, x.Slug }).IsUnique();
            entity.HasOne(x => x.Project)
                .WithMany(x => x.Environments)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OrganizationMembership>(entity =>
        {
            entity.ToTable("organization_memberships");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Role).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
            entity.HasOne(x => x.Organization)
                .WithMany()
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EntityDefinitionRecord>(entity =>
        {
            entity.ToTable("entity_definitions");
            entity.HasKey(x => x.DefinitionId);
            entity.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Kind).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => new { x.EnvironmentId, x.Slug }).IsUnique();
            entity.HasIndex(x => x.EnvironmentId);
        });

        builder.Entity<DefinitionVersionRecord>(entity =>
        {
            entity.ToTable("definition_versions");
            entity.HasKey(x => x.VersionId);
            entity.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Kind).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.AuthoringJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.EffectiveModelJson).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.DefinitionId, x.VersionNumber }).IsUnique();
            entity.HasIndex(x => x.EnvironmentId);
        });

        builder.Entity<StorageMappingRecord>(entity =>
        {
            entity.ToTable("storage_mappings");
            entity.HasKey(x => x.VersionId);
            entity.Property(x => x.SchemaName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.TableName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Fingerprint).HasMaxLength(128).IsRequired();
            entity.Property(x => x.MappingJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(x => x.DefinitionId);
            entity.HasIndex(x => x.EnvironmentId);
        });

        builder.Entity<MigrationRunRecord>(entity =>
        {
            entity.ToTable("migration_runs");
            entity.HasKey(x => x.RunId);
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.CurrentStage).HasMaxLength(32).IsRequired();
            entity.Property(x => x.PlanJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.FailureMessage).HasMaxLength(4000);
            entity.Property(x => x.FailureStage).HasMaxLength(32);
            entity.HasIndex(x => x.DefinitionId);
            entity.HasIndex(x => x.EnvironmentId);
            entity.HasIndex(x => x.PlanId);
        });

        builder.Entity<FileBucketRecord>(entity =>
        {
            entity.ToTable("file_buckets");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PhysicalBucket).HasMaxLength(200).IsRequired();
            entity.Property(x => x.AllowedContentTypesJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.EnvironmentId, x.Slug }).IsUnique();
            entity.HasIndex(x => x.ProjectId);
        });

        builder.Entity<StoredFileRecord>(entity =>
        {
            entity.ToTable("stored_files");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ObjectKey).HasMaxLength(512).IsRequired();
            entity.Property(x => x.FileName).HasMaxLength(512).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ChecksumSha256).HasMaxLength(128);
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => x.BucketId);
            entity.HasIndex(x => x.ProjectId);
            entity.HasIndex(x => x.EnvironmentId);
            entity.HasIndex(x => x.ObjectKey).IsUnique();
            entity.HasIndex(x => x.DeletedAt);
        });

        builder.Entity<OutboxMessageRecord>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.LastError).HasMaxLength(2000);
            entity.Property(x => x.CorrelationId).HasMaxLength(200);
            entity.Property(x => x.AvailableAt).HasColumnName("available_at");
            entity.HasIndex(x => new { x.Status, x.AvailableAt });
            entity.HasIndex(x => x.Type);
        });

        builder.Entity<AuditEventRecord>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ActorPrincipalId).HasMaxLength(200);
            entity.Property(x => x.ResourceType).HasMaxLength(100);
            entity.Property(x => x.ResourceId).HasMaxLength(200);
            entity.Property(x => x.DetailJson).HasColumnType("jsonb");
            entity.HasIndex(x => x.OccurredAt);
            entity.HasIndex(x => new { x.ProjectId, x.OccurredAt });
            entity.HasIndex(x => x.Action);
        });

        builder.Entity<UsageCounterRecord>(entity =>
        {
            entity.ToTable("usage_counters");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Metric).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.Metric, x.Day }).IsUnique();
        });

        builder.Entity<WebhookSubscriptionRecord>(entity =>
        {
            entity.ToTable("webhook_subscriptions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TargetUrl).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.SigningSecret).HasMaxLength(500).IsRequired();
            entity.Property(x => x.EntitySlug).HasMaxLength(100);
            entity.Property(x => x.EventTypesJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(x => x.ProjectId);
            entity.HasIndex(x => new { x.ProjectId, x.EnvironmentId, x.EntitySlug });
        });

        builder.Entity<ApiCredentialRecord>(entity =>
        {
            entity.ToTable("api_credentials");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.KeyPrefix).HasMaxLength(64).IsRequired();
            entity.Property(x => x.SecretHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.CreatedByPrincipalId).HasMaxLength(200);
            entity.HasIndex(x => x.ProjectId);
            entity.HasIndex(x => x.KeyPrefix);
            entity.HasIndex(x => x.SecretHash);
        });

        builder.Entity<ProcessDefinitionRecord>(entity =>
        {
            entity.ToTable("process_definitions");
            entity.HasKey(x => x.DefinitionId);
            entity.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.EnvironmentId, x.Slug }).IsUnique();
            entity.HasIndex(x => x.ProjectId);
        });

        builder.Entity<ProcessVersionRecord>(entity =>
        {
            entity.ToTable("process_versions");
            entity.HasKey(x => x.VersionId);
            entity.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.AuthoringJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.CompiledJson).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.DefinitionId, x.VersionNumber }).IsUnique();
            entity.HasIndex(x => x.EnvironmentId);
        });

        builder.Entity<ProcessInstanceRecord>(entity =>
        {
            entity.ToTable("process_instances");
            entity.HasKey(x => x.InstanceId);
            entity.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.VariablesJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.CursorJson).HasColumnType("jsonb");
            entity.Property(x => x.FailureMessage).HasMaxLength(4000);
            entity.Property(x => x.PrincipalId).HasMaxLength(200);
            entity.Property(x => x.PrincipalRolesJson).HasColumnType("jsonb");
            entity.HasIndex(x => x.DefinitionId);
            entity.HasIndex(x => x.EnvironmentId);
            entity.HasIndex(x => new { x.Status, x.WaitingUntil });
        });

        builder.Entity<ProcessActivityLogRecord>(entity =>
        {
            entity.ToTable("process_activity_logs");
            entity.HasKey(x => x.LogId);
            entity.Property(x => x.StepId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ActivityType).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.InputJson).HasColumnType("jsonb");
            entity.Property(x => x.OutputJson).HasColumnType("jsonb");
            entity.Property(x => x.ErrorMessage).HasMaxLength(4000);
            entity.HasIndex(x => x.InstanceId);
            entity.HasIndex(x => x.StartedAt);
        });
    }
}

