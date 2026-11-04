using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Suggestions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Configurations;

/// <summary>
/// Tabela <c>publication_suggestions</c>: a sugestão de IA (tipo do ato e prazo) de uma
/// publicação — ver ADR-067. PublicationSuggestion implementa IHasTenant, então o filtro
/// global de tenant vem do BaseDbContext; o RLS está em
/// infra/supabase/migrations/014_publication_suggestions.sql.
/// </summary>
public sealed class PublicationSuggestionConfiguration : IEntityTypeConfiguration<PublicationSuggestion>
{
    public void Configure(EntityTypeBuilder<PublicationSuggestion> builder)
    {
        builder.ToTable("publication_suggestions");

        builder.HasKey(suggestion => suggestion.Id);
        builder.Property(suggestion => suggestion.Id)
            .HasConversion(id => id.Value, value => PublicationSuggestionId.From(value))
            .ValueGeneratedNever();

        builder.Property(suggestion => suggestion.TenantId).IsRequired();

        builder.Property(suggestion => suggestion.PublicationId)
            .HasConversion(id => id.Value, value => PublicationId.From(value))
            .IsRequired();

        builder.Property(suggestion => suggestion.ActType).HasMaxLength(50).IsRequired();

        builder.Property(suggestion => suggestion.SuggestedDeadlineDays);

        // business/calendar em minúsculas, como no contrato do prompt (HasConversion<string>
        // gravaria "Business"). O DeadlineType? nulo não passa pelo conversor.
        builder.Property(suggestion => suggestion.DeadlineType)
            .HasConversion(
                type => type!.Value.ToValue(),
                value => ToDeadlineType(value))
            .HasMaxLength(20);

        builder.Property(suggestion => suggestion.LegalBasis).HasColumnType("text");

        builder.Property(suggestion => suggestion.Confidence).IsRequired();
        builder.Property(suggestion => suggestion.RequiresManualReview).IsRequired();
        builder.Property(suggestion => suggestion.ContentTruncated).IsRequired();

        builder.Property(suggestion => suggestion.AiProvider).HasMaxLength(30).IsRequired();
        builder.Property(suggestion => suggestion.AiModel).HasMaxLength(100).IsRequired();
        builder.Property(suggestion => suggestion.PromptVersion).HasMaxLength(20).IsRequired();

        builder.Property(suggestion => suggestion.RawResponse).HasColumnType("jsonb").IsRequired();

        builder.Property(suggestion => suggestion.CreatedAt).IsRequired();

        // Sugestão não sobrevive à publicação que a originou. Publicação é soft delete na
        // prática; o cascade só vale para a exclusão física (direito ao esquecimento — LGPD).
        builder.HasOne<Publication>()
            .WithMany()
            .HasForeignKey(suggestion => suggestion.PublicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Uma sugestão por publicação: reprocessar não duplica e a corrida entre duas
        // instâncias do Worker termina em violação de unicidade (tratada no job), não em
        // duas sugestões divergentes para o advogado revisar.
        builder.HasIndex(suggestion => suggestion.PublicationId)
            .IsUnique()
            .HasDatabaseName("ux_publication_suggestions_publication");

        builder.HasIndex(suggestion => suggestion.CreatedAt)
            .HasDatabaseName("ix_publication_suggestions_created_at");
    }

    private static DeadlineType? ToDeadlineType(string value) =>
        DeadlineTypeExtensions.TryParse(value, out var type) ? type : null;
}
