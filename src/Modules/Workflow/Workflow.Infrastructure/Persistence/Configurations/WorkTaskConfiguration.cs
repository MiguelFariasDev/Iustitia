using Advocacia.Modules.Workflow.Domain.WorkTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Workflow.Infrastructure.Persistence.Configurations;

/// <summary>Filtro global de tenant aplicado por BaseDbContext. RLS em infra/supabase/migrations/015_workflow.sql.</summary>
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
        builder.Property(task => task.AssigneeUserId).IsRequired();
        builder.Property(task => task.Title).HasMaxLength(WorkTask.TitleMaxLength).IsRequired();
        builder.Property(task => task.Description).HasMaxLength(WorkTask.DescriptionMaxLength);

        // `date`, não timestamp: prazo é um dia (ver WorkTask).
        builder.Property(task => task.DueDate).HasColumnType("date").IsRequired();
        builder.Property(task => task.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(task => task.CreatedAt).IsRequired();

        // O acesso é sempre "as tarefas deste usuário, por status e vencimento".
        builder.HasIndex(task => new { task.TenantId, task.AssigneeUserId, task.Status, task.DueDate })
            .HasDatabaseName("ix_work_tasks_tenant_assignee_status_due");

        builder.HasIndex(task => task.ProcessoId).HasDatabaseName("ix_work_tasks_processo");
    }
}
