using Advocacia.Platform.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// AuditLog é imutável (registro de auditoria, retenção mínima de 5 anos — LGPD/OAB):
/// sem soft delete, sem update depois de criado. Implementa IHasTenant, então recebe o
/// filtro global de isolamento por tenant aplicado centralmente por BaseDbContext.
/// </summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => AuditLogId.From(value))
            .ValueGeneratedNever();

        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.UserId).IsRequired();

        builder.Property(a => a.Action)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.EntityType).HasMaxLength(200).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(100).IsRequired();

        builder.Property(a => a.BeforeJson).HasColumnName("before").HasColumnType("jsonb");
        builder.Property(a => a.AfterJson).HasColumnName("after").HasColumnType("jsonb");

        builder.Property(a => a.Ip).HasMaxLength(45);
        builder.Property(a => a.UserAgent).HasMaxLength(500);

        builder.Property(a => a.CreatedAt).IsRequired();

        builder.HasIndex(a => new { a.TenantId, a.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_audit_logs_tenant_created");

        builder.HasIndex(a => new { a.EntityType, a.EntityId })
            .HasDatabaseName("ix_audit_logs_entity");

        builder.HasIndex(a => new { a.UserId, a.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_audit_logs_user_created");
    }
}
