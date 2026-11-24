using Advocacia.Modules.Drafting.Domain.Gpu;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence.Configurations;

public sealed class GpuInstanceLogConfiguration : IEntityTypeConfiguration<GpuInstanceLog>
{
    public void Configure(EntityTypeBuilder<GpuInstanceLog> builder)
    {
        builder.ToTable("gpu_instance_logs");

        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).ValueGeneratedNever();

        builder.Property(log => log.VastInstanceId).HasMaxLength(40);
        builder.Property(log => log.Acao).HasMaxLength(40).IsRequired();
        builder.Property(log => log.StatusAnterior).HasMaxLength(40);
        builder.Property(log => log.StatusNovo).HasMaxLength(40);
        builder.Property(log => log.Motivo).HasMaxLength(300).IsRequired();
        builder.Property(log => log.Erro).HasMaxLength(500);

        builder.HasIndex(log => log.CriadoEm).HasDatabaseName("ix_gpu_instance_logs_criado");
    }
}
