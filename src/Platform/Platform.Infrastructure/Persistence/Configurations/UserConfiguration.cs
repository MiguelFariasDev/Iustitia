using Advocacia.Platform.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// Filtros globais de soft delete e de isolamento por tenant são aplicados centralmente
/// por BaseDbContext (User implementa ISoftDeletable e IHasTenant) — não redeclarar
/// HasQueryFilter aqui.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasConversion(id => id.Value, value => UserId.From(value))
            .ValueGeneratedNever();

        builder.Property(u => u.TenantId).IsRequired();

        builder.Property(u => u.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasConversion(email => email.Value, value => Email.Create(value).Value)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(u => u.OabNumber)
            .HasConversion(
                oab => oab == null ? null : oab.Number + "/" + oab.StateCode,
                value => value == null ? null : ParseOabNumber(value))
            .HasColumnName("oab_number")
            .HasMaxLength(30);

        builder.Property(u => u.Specialty).HasMaxLength(100);

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(u => u.AvatarUrl).HasMaxLength(500);
        builder.Property(u => u.Phone).HasMaxLength(20);

        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.CreatedBy).HasMaxLength(100);
        builder.Property(u => u.UpdatedBy).HasMaxLength(100);
        builder.Property(u => u.DeletedBy).HasMaxLength(100);

        builder.HasIndex(u => new { u.TenantId, u.Email })
            .IsUnique()
            .HasDatabaseName("ux_users_tenant_email");

        builder.HasIndex(u => new { u.TenantId, u.Role })
            .HasDatabaseName("ix_users_tenant_role");

        builder.HasIndex(u => new { u.TenantId, u.IsActive })
            .HasDatabaseName("ix_users_tenant_active");

        builder.HasIndex(u => new { u.TenantId, u.Specialty })
            .HasDatabaseName("ix_users_tenant_specialty");

        builder.HasIndex(u => u.TenantId)
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_users_tenant_active_only");
    }

    private static OABNumber? ParseOabNumber(string value)
    {
        var parts = value.Split('/', 2);
        return OABNumber.Create(parts[0], parts.Length > 1 ? parts[1] : null).Value;
    }
}
