using Advocacia.Modules.Workflow.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkTaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> builder)
    {
        builder.ToTable("work_tasks");

        builder.HasKey(task => task.Id);
        builder.Property(task => task.Id)
            .HasConversion(id => id.Value, value => WorkTaskId.From(value))
            .ValueGeneratedNever();

        builder.Property(task => task.TenantId).IsRequired();
        builder.Property(task => task.Title).HasMaxLength(WorkTask.TitleMaxLength).IsRequired();
        builder.Property(task => task.Description).HasMaxLength(4000);
        builder.Property(task => task.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(task => task.CreatedAt).IsRequired();
        builder.Property(task => task.CreatedBy).HasMaxLength(100);
        builder.Property(task => task.UpdatedBy).HasMaxLength(100);
        builder.Property(task => task.DeletedBy).HasMaxLength(100);

        builder.HasIndex(task => new { task.TenantId, task.ProcessId })
            .HasFilter("is_deleted = false AND process_id IS NOT NULL")
            .HasDatabaseName("ix_work_tasks_tenant_process");

        builder.HasIndex(task => new { task.TenantId, task.AssignedUserId })
            .HasFilter("is_deleted = false AND assigned_user_id IS NOT NULL")
            .HasDatabaseName("ix_work_tasks_tenant_assignee");
    }
}
