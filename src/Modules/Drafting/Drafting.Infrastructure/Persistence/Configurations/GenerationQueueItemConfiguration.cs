using Advocacia.Modules.Drafting.Domain.Fila;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence.Configurations;

public sealed class GenerationQueueItemConfiguration : IEntityTypeConfiguration<GenerationQueueItem>
{
    public void Configure(EntityTypeBuilder<GenerationQueueItem> builder)
    {
        builder.ToTable("generation_queue");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();

        builder.Property(item => item.TenantId).IsRequired();
        builder.Property(item => item.Tipo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.ReferenciaId).IsRequired();
        builder.Property(item => item.Tentativas).IsRequired();
        builder.Property(item => item.ReservadoPor).HasMaxLength(GenerationQueueItem.WorkerMaxLength);
        builder.Property(item => item.UltimoErro).HasMaxLength(GenerationQueueItem.ErroMaxLength);

        // A varredura do worker: o que está elegível, do mais antigo para o mais novo.
        builder.HasIndex(item => new { item.Status, item.DisponivelEm, item.CriadoEm })
            .HasDatabaseName("ix_generation_queue_status_disponivel");
        builder.HasIndex(item => item.ReferenciaId).HasDatabaseName("ix_generation_queue_referencia");
    }
}
