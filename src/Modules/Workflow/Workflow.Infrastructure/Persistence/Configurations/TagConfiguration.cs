using Advocacia.Modules.Workflow.Domain.Tags;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("tags");

        builder.HasKey(tag => tag.Id);
        builder.Property(tag => tag.Id)
            .HasConversion(id => id.Value, value => TagId.From(value))
            .ValueGeneratedNever();

        builder.Property(tag => tag.TenantId).IsRequired();
        builder.Property(tag => tag.Name).HasMaxLength(Tag.NameMaxLength).IsRequired();
        builder.Property(tag => tag.Color).HasMaxLength(7).IsRequired();
        builder.Property(tag => tag.CreatedAt).IsRequired();
        builder.Property(tag => tag.CreatedBy).HasMaxLength(100);
        builder.Property(tag => tag.UpdatedBy).HasMaxLength(100);
        builder.Property(tag => tag.DeletedBy).HasMaxLength(100);

        builder.HasIndex(tag => new { tag.TenantId, tag.Name }).HasDatabaseName("ix_tags_tenant_name");
    }
}

public sealed class TagAssignmentConfiguration : IEntityTypeConfiguration<TagAssignment>
{
    public void Configure(EntityTypeBuilder<TagAssignment> builder)
    {
        builder.ToTable("tag_assignments");

        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Id).ValueGeneratedNever();
        builder.Property(assignment => assignment.TenantId).IsRequired();
        builder.Property(assignment => assignment.TagId)
            .HasConversion(id => id.Value, value => TagId.From(value))
            .IsRequired();
        builder.Property(assignment => assignment.Target).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(assignment => assignment.TargetId).IsRequired();

        // Uma tag só pode estar uma vez em cada alvo.
        builder.HasIndex(assignment => new { assignment.TagId, assignment.Target, assignment.TargetId })
            .IsUnique()
            .HasDatabaseName("ux_tag_assignments_tag_target");

        builder.HasIndex(assignment => new { assignment.TenantId, assignment.Target, assignment.TargetId })
            .HasDatabaseName("ix_tag_assignments_tenant_target");
    }
}
