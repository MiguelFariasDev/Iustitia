using Advocacia.Modules.Legal.Domain.Clientes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cliente implementa IHasTenant e ISoftDeletable — os filtros globais vêm do BaseDbContext,
/// não redeclarar HasQueryFilter aqui.
/// </summary>
public sealed class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("clientes");

        builder.HasKey(cliente => cliente.Id);
        builder.Property(cliente => cliente.Id)
            .HasConversion(id => id.Value, value => ClienteId.From(value))
            .ValueGeneratedNever();

        builder.Property(cliente => cliente.TenantId).IsRequired();

        builder.Property(cliente => cliente.Tipo)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(cliente => cliente.Nome)
            .HasConversion(nome => nome.Value, value => NomeCliente.FromPersistence(value))
            .HasColumnName("nome")
            .HasMaxLength(NomeCliente.MaxLength)
            .IsRequired();

        // Só os dígitos. A máscara é decisão de apresentação e guardá-la aqui tornaria a
        // busca por documento dependente de como o usuário digitou.
        //
        // O tipo é DERIVADO do tamanho (11 = CPF, 14 = CNPJ) e não lido da coluna `tipo`:
        // um value converter do EF Core recebe apenas o valor da própria coluna, sem acesso
        // às outras da linha. Derivar é seguro porque a invariante já foi imposta na criação
        // (Cliente.Create recusa documento cujo tipo divirja do tipo do cliente).
        builder.Property(cliente => cliente.Documento)
            .HasConversion(
                documento => documento.Value,
                value => Documento.FromPersistence(
                    value, value.Length == 14 ? TipoCliente.PessoaJuridica : TipoCliente.PessoaFisica))
            .HasColumnName("documento")
            .HasMaxLength(14)
            .IsRequired();

        builder.Property(cliente => cliente.NomeBusca)
            .HasColumnName("nome_busca")
            .HasMaxLength(NomeCliente.MaxLength)
            .IsRequired();

        builder.Property(cliente => cliente.DocumentoBusca)
            .HasColumnName("documento_busca")
            .HasMaxLength(14)
            .IsRequired();

        builder.OwnsOne(cliente => cliente.Contato, contato =>
        {
            contato.Property(c => c.Email).HasColumnName("email").HasMaxLength(320);
            contato.Property(c => c.Telefone).HasColumnName("telefone").HasMaxLength(11);
        });

        builder.OwnsOne(cliente => cliente.Endereco, endereco =>
        {
            endereco.Property(e => e.Logradouro).HasColumnName("logradouro").HasMaxLength(200);
            endereco.Property(e => e.Numero).HasColumnName("numero").HasMaxLength(20);
            endereco.Property(e => e.Complemento).HasColumnName("complemento").HasMaxLength(100);
            endereco.Property(e => e.Bairro).HasColumnName("bairro").HasMaxLength(100);
            endereco.Property(e => e.Cidade).HasColumnName("cidade").HasMaxLength(100);
            endereco.Property(e => e.Uf).HasColumnName("uf").HasMaxLength(2);
            endereco.Property(e => e.Cep).HasColumnName("cep").HasMaxLength(8);
        });

        builder.Property(cliente => cliente.DataNascimento);
        builder.Property(cliente => cliente.Observacoes).HasMaxLength(2000);
        builder.Property(cliente => cliente.IsActive).IsRequired().HasDefaultValue(true);

        builder.Property(cliente => cliente.CreatedAt).IsRequired();
        builder.Property(cliente => cliente.CreatedBy).HasMaxLength(100);
        builder.Property(cliente => cliente.UpdatedBy).HasMaxLength(100);
        builder.Property(cliente => cliente.DeletedBy).HasMaxLength(100);

        // Um documento por escritório. Filtro de soft delete no índice para que um cliente
        // excluído não impeça o recadastro da mesma pessoa.
        builder.HasIndex(cliente => new { cliente.TenantId, cliente.Documento })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ux_clientes_tenant_documento");

        builder.HasIndex(cliente => new { cliente.TenantId, cliente.IsActive })
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_clientes_tenant_ativo");

        // Índice sobre a coluna DERIVADA (não sobre `nome`): é ela que a busca consulta.
        // O índice trigram para busca parcial/fuzzy fica no SQL do Supabase (ver
        // 012_clientes_processos.sql) — GIN não tem tradução em Fluent API.
        builder.HasIndex(cliente => new { cliente.TenantId, cliente.NomeBusca })
            .HasDatabaseName("ix_clientes_tenant_nome");
    }
}
