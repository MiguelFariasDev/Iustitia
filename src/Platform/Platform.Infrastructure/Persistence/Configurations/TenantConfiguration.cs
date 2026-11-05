using Advocacia.Platform.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// Os filtros globais de soft delete (IsDeleted == false) são aplicados centralmente
/// por BaseDbContext a partir de ISoftDeletable — não redeclarar HasQueryFilter aqui.
/// Tenant não implementa IHasTenant (ele *é* o tenant), então não recebe o filtro de
/// isolamento por tenant.
/// </summary>
public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .ValueGeneratedNever();

        builder.Property(t => t.Name)
            .HasMaxLength(200)
            .IsRequired();

        // CNPJ.Create valida os dígitos verificadores em toda escrita; a leitura confia
        // que o dado já foi validado (.Value lança se um dado corrompido chegar ao banco).
        // Nulo em conta Solo. Índice único em coluna anulável: o Postgres aceita vários NULL,
        // então só os valores preenchidos precisam ser únicos.
        builder.Property(t => t.Cnpj)
            .HasConversion(cnpj => cnpj!.Value, value => CNPJ.Create(value).Value)
            .HasColumnName("cnpj")
            .HasMaxLength(14);

        builder.Property(t => t.Cpf)
            .HasConversion(cpf => cpf!.Value, value => CPF.Create(value).Value)
            .HasColumnName("cpf")
            .HasMaxLength(11);

        // O padrão 'Firm' é o que preenche os tenants já existentes na migration (ADR-068).
        builder.Property(t => t.AccountType)
            .HasConversion<string>()
            .HasColumnName("account_type")
            .HasMaxLength(10)
            .IsRequired()
            .HasDefaultValue(AccountType.Firm);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.LogoUrl).HasMaxLength(500);

        builder.Property(t => t.AddressJson)
            .HasColumnName("address")
            .HasColumnType("jsonb");

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.CreatedBy).HasMaxLength(100);
        builder.Property(t => t.UpdatedBy).HasMaxLength(100);
        builder.Property(t => t.DeletedBy).HasMaxLength(100);

        builder.HasIndex(t => t.Cnpj).IsUnique().HasDatabaseName("ux_tenants_cnpj");
        builder.HasIndex(t => t.Cpf).IsUnique().HasDatabaseName("ux_tenants_cpf");
        builder.HasIndex(t => t.Status).HasDatabaseName("ix_tenants_status");
    }
}
