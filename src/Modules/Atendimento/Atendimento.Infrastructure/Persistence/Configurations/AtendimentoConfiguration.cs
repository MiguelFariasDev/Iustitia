using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Atendimentos.Infrastructure.Persistence.Configurations;

/// <summary>Atendimento implementa IHasTenant — o filtro global vem do BaseDbContext, não redeclarar HasQueryFilter.</summary>
public sealed class AtendimentoConfiguration : IEntityTypeConfiguration<Atendimento>
{
    public void Configure(EntityTypeBuilder<Atendimento> builder)
    {
        builder.ToTable("atendimentos");

        builder.HasKey(atendimento => atendimento.Id);
        builder.Property(atendimento => atendimento.Id)
            .HasConversion(id => id.Value, value => AtendimentoId.From(value))
            .ValueGeneratedNever();

        builder.Property(atendimento => atendimento.TenantId).IsRequired();
        builder.Property(atendimento => atendimento.ClientId);

        builder.Property(atendimento => atendimento.PhoneNumber).HasMaxLength(TelefoneWhatsApp.MaxDigits).IsRequired();
        builder.Property(atendimento => atendimento.ContactName).HasMaxLength(Atendimento.MaxContactNameLength);

        builder.Property(atendimento => atendimento.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(atendimento => atendimento.AssignedUserId);
        builder.Property(atendimento => atendimento.LastMessageAt).IsRequired();

        builder.Property(atendimento => atendimento.CreatedAt).IsRequired();
        builder.Property(atendimento => atendimento.CreatedBy).HasMaxLength(100);
        builder.Property(atendimento => atendimento.UpdatedBy).HasMaxLength(100);

        builder.Ignore(atendimento => atendimento.IsOpen);

        // No máximo UM atendimento aberto por telefone e escritório. É a garantia definitiva
        // sob concorrência (dois webhooks simultâneos do mesmo número): a checagem no handler
        // é só a primeira camada.
        builder.HasIndex(atendimento => new { atendimento.TenantId, atendimento.PhoneNumber })
            .HasDatabaseName("ux_atendimentos_open_phone")
            .IsUnique()
            .HasFilter("status <> 'Closed'");

        // Fila do Inbox: filtra por status e ordena pela atividade mais recente.
        builder.HasIndex(atendimento => new { atendimento.TenantId, atendimento.Status, atendimento.LastMessageAt })
            .HasDatabaseName("ix_atendimentos_tenant_status_last_message")
            .IsDescending(false, false, true);

        builder.HasIndex(atendimento => new { atendimento.TenantId, atendimento.AssignedUserId })
            .HasDatabaseName("ix_atendimentos_tenant_assigned")
            .HasFilter("assigned_user_id IS NOT NULL");

        builder.HasIndex(atendimento => new { atendimento.TenantId, atendimento.ClientId })
            .HasDatabaseName("ix_atendimentos_tenant_client")
            .HasFilter("client_id IS NOT NULL");
    }
}

public sealed class AtendimentoMensagemConfiguration : IEntityTypeConfiguration<AtendimentoMensagem>
{
    public void Configure(EntityTypeBuilder<AtendimentoMensagem> builder)
    {
        builder.ToTable("atendimento_mensagens");

        builder.HasKey(mensagem => mensagem.Id);
        builder.Property(mensagem => mensagem.Id)
            .HasConversion(id => id.Value, value => AtendimentoMensagemId.From(value))
            .ValueGeneratedNever();

        builder.Property(mensagem => mensagem.AtendimentoId)
            .HasConversion(id => id.Value, value => AtendimentoId.From(value))
            .IsRequired();

        // O histórico é prova de atendimento e fica retido pelo prazo da política: nunca se
        // apaga em cascata — apagar um atendimento exige decisão explícita (LGPD).
        builder.HasOne<Atendimento>()
            .WithMany()
            .HasForeignKey(mensagem => mensagem.AtendimentoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(mensagem => mensagem.TenantId).IsRequired();
        builder.Property(mensagem => mensagem.Direction).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(mensagem => mensagem.Sender).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(mensagem => mensagem.Body).HasColumnType("text").IsRequired();
        builder.Property(mensagem => mensagem.ExternalId).HasMaxLength(255);
        builder.Property(mensagem => mensagem.Delivery).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(mensagem => mensagem.CreatedAt).IsRequired();

        builder.HasIndex(mensagem => new { mensagem.AtendimentoId, mensagem.CreatedAt })
            .HasDatabaseName("ix_atendimento_mensagens_conversa");

        // Idempotência do webhook: o wamid da Meta é único por escritório.
        builder.HasIndex(mensagem => new { mensagem.TenantId, mensagem.ExternalId })
            .HasDatabaseName("ux_atendimento_mensagens_external_id")
            .IsUnique()
            .HasFilter("external_id IS NOT NULL");
    }
}

public sealed class AtendimentoAuditoriaConfiguration : IEntityTypeConfiguration<AtendimentoAuditoria>
{
    public void Configure(EntityTypeBuilder<AtendimentoAuditoria> builder)
    {
        builder.ToTable("atendimento_auditoria");

        builder.HasKey(auditoria => auditoria.Id);
        builder.Property(auditoria => auditoria.Id).ValueGeneratedNever();

        builder.Property(auditoria => auditoria.AtendimentoId)
            .HasConversion(id => id.Value, value => AtendimentoId.From(value))
            .IsRequired();

        builder.HasOne<Atendimento>()
            .WithMany()
            .HasForeignKey(auditoria => auditoria.AtendimentoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(auditoria => auditoria.TenantId).IsRequired();
        builder.Property(auditoria => auditoria.Action).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(auditoria => auditoria.ActorType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(auditoria => auditoria.ActorId);
        builder.Property(auditoria => auditoria.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(auditoria => auditoria.ToStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(auditoria => auditoria.OccurredAt).IsRequired();

        builder.HasIndex(auditoria => new { auditoria.AtendimentoId, auditoria.OccurredAt })
            .HasDatabaseName("ix_atendimento_auditoria_atendimento");
    }
}

public sealed class BotApiKeyConfiguration : IEntityTypeConfiguration<BotApiKey>
{
    public void Configure(EntityTypeBuilder<BotApiKey> builder)
    {
        builder.ToTable("bot_api_keys");

        builder.HasKey(key => key.Id);
        builder.Property(key => key.Id).ValueGeneratedNever();

        builder.Property(key => key.TenantId).IsRequired();
        builder.Property(key => key.Name).HasMaxLength(100).IsRequired();
        builder.Property(key => key.Prefix).HasMaxLength(32).IsRequired();
        builder.Property(key => key.KeyHash).HasMaxLength(64).IsRequired();
        builder.Property(key => key.CreatedByUserId);
        builder.Property(key => key.CreatedAt).IsRequired();
        builder.Property(key => key.LastUsedAt);
        builder.Property(key => key.RevokedAt);

        builder.Ignore(key => key.IsActive);

        builder.HasIndex(key => key.KeyHash).HasDatabaseName("ux_bot_api_keys_hash").IsUnique();
        builder.HasIndex(key => key.TenantId).HasDatabaseName("ix_bot_api_keys_tenant");
    }
}
