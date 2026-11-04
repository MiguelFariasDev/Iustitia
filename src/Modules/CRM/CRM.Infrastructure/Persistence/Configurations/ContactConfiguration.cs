using Advocacia.Modules.CRM.Domain.Contacts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.CRM.Infrastructure.Persistence.Configurations;

/// <summary>Contact implementa IHasTenant e ISoftDeletable — filtros globais vêm do BaseDbContext.</summary>
public sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("contacts");

        builder.HasKey(contact => contact.Id);
        builder.Property(contact => contact.Id)
            .HasConversion(id => id.Value, value => ContactId.From(value))
            .ValueGeneratedNever();

        builder.Property(contact => contact.TenantId).IsRequired();
        builder.Property(contact => contact.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(contact => contact.Name).HasMaxLength(Contact.NameMaxLength).IsRequired();
        builder.Property(contact => contact.NameSearch).HasMaxLength(Contact.NameMaxLength).IsRequired();
        builder.Property(contact => contact.Document).HasMaxLength(14);
        builder.Property(contact => contact.Email).HasMaxLength(320);
        builder.Property(contact => contact.Phone).HasMaxLength(11);
        builder.Property(contact => contact.Notes).HasMaxLength(2000);
        builder.Property(contact => contact.IsActive).IsRequired().HasDefaultValue(true);

        builder.Property(contact => contact.CreatedAt).IsRequired();
        builder.Property(contact => contact.CreatedBy).HasMaxLength(100);
        builder.Property(contact => contact.UpdatedBy).HasMaxLength(100);
        builder.Property(contact => contact.DeletedBy).HasMaxLength(100);

        builder.HasIndex(contact => new { contact.TenantId, contact.Type })
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_contacts_tenant_type");

        builder.HasIndex(contact => new { contact.TenantId, contact.NameSearch })
            .HasDatabaseName("ix_contacts_tenant_name");
    }
}
