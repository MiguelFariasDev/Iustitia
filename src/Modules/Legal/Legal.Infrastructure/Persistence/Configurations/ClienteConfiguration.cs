using Advocacia.BuildingBlocks.Infrastructure.Security;
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
    // "enc:v1:{keyId}:" + Base64(12 + 16 + n). Folgado para o maior valor (telefone/documento).
    private const int EncryptedColumnLength = 255;

    // E-mail chega a 320 caracteres: 4/3 * (28 + 320) + prefixo ≈ 480.
    private const int EncryptedEmailColumnLength = 600;

    private const int BlindIndexLength = 64;

    private static Documento FromStoredDocumento(string stored)
    {
        var digits = FieldEncryption.Decrypt(stored, EncryptionContexts.ClienteDocumento);
        return Documento.FromPersistence(digits, digits.Length == 14 ? TipoCliente.PessoaJuridica : TipoCliente.PessoaFisica);
    }

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

        // CIFRADO em repouso (AES-256-GCM, ver ADR-068). Só os dígitos são cifrados: a máscara é
        // decisão de apresentação.
        //
        // O tipo é DERIVADO do tamanho (11 = CPF, 14 = CNPJ) e não lido da coluna `tipo`:
        // um value converter do EF Core recebe apenas o valor da própria coluna, sem acesso
        // às outras da linha. Derivar é seguro porque a invariante já foi imposta na criação
        // (Cliente.Create recusa documento cujo tipo divirja do tipo do cliente).
        //
        // A cifra é não-determinística (nonce aleatório): esta coluna NÃO serve para igualdade,
        // unicidade nem busca — para isso existe `documento_busca` (índice cego, abaixo).
        builder.Property(cliente => cliente.Documento)
            .HasConversion(
                documento => FieldEncryption.Encrypt(documento.Value, EncryptionContexts.ClienteDocumento),
                stored => FromStoredDocumento(stored))
            .HasColumnName("documento")
            .HasMaxLength(EncryptedColumnLength)
            .IsRequired();

        builder.Property(cliente => cliente.NomeBusca)
            .HasColumnName("nome_busca")
            .HasMaxLength(NomeCliente.MaxLength)
            .IsRequired();

        // ÍNDICE CEGO do documento: HMAC-SHA256 (64 hex) dos dígitos. É o que permite achar um
        // cliente pelo CPF/CNPJ e impor "um documento por escritório" sem guardar o documento
        // em claro. Não é reversível; a busca é por igualdade com o documento COMPLETO — busca
        // por prefixo deixou de existir de propósito (ver ADR-068).
        builder.Property(cliente => cliente.DocumentoBusca)
            .HasConversion(new BlindIndexConverter(EncryptionContexts.ClienteDocumentoBlindIndex))
            .HasColumnName("documento_busca")
            .HasMaxLength(BlindIndexLength)
            .IsRequired();

        builder.OwnsOne(cliente => cliente.Contato, contato =>
        {
            contato.Property(c => c.Email)
                .HasConversion(new EncryptedStringConverter(EncryptionContexts.ClienteEmail))
                .HasColumnName("email")
                .HasMaxLength(EncryptedEmailColumnLength);
            contato.Property(c => c.Telefone)
                .HasConversion(new EncryptedStringConverter(EncryptionContexts.ClienteTelefone))
                .HasColumnName("telefone")
                .HasMaxLength(EncryptedColumnLength);
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
        // Sobre o índice cego, não sobre `documento` (cifrado com nonce aleatório: dois valores
        // iguais teriam textos cifrados diferentes e a unicidade jamais dispararia).
        builder.HasIndex(cliente => new { cliente.TenantId, cliente.DocumentoBusca })
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
