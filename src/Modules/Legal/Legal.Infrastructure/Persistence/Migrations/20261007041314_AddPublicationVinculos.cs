using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicationVinculos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cliente_id",
                table: "publications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cnj_digits",
                table: "publications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "published_on",
                table: "publications",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<Guid>(
                name: "responsavel_user_id",
                table: "publications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tribunal",
                table: "publications",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            // Backfill das colunas derivadas para publicações capturadas antes desta migration.
            // cnj_digits e published_on saem direto das colunas de origem; cliente e responsável
            // vêm do processo vinculado; o tribunal repete a regra de TribunalResolver (Res. CNJ
            // 65/2008) para o segmento estadual (8) e os tribunais de segmento único. Os demais
            // segmentos ficam com tribunal nulo — passam a ser preenchidos pelas novas capturas.
            migrationBuilder.Sql(@"
                UPDATE publications
                   SET cnj_digits = regexp_replace(cnj_number, '\D', '', 'g'),
                       published_on = (published_at AT TIME ZONE 'UTC')::date;

                UPDATE publications p
                   SET cliente_id = pr.cliente_id,
                       responsavel_user_id = pr.responsavel_user_id
                  FROM processos pr
                 WHERE p.process_id = pr.id AND p.tenant_id = pr.tenant_id;

                UPDATE publications
                   SET tribunal = CASE substr(cnj_digits, 14, 1)
                       WHEN '1' THEN 'STF'
                       WHEN '2' THEN 'CNJ'
                       WHEN '3' THEN 'STJ'
                       WHEN '4' THEN CASE WHEN substr(cnj_digits, 15, 2)::int BETWEEN 1 AND 6
                                          THEN 'TRF' || substr(cnj_digits, 15, 2)::int END
                       WHEN '5' THEN CASE WHEN substr(cnj_digits, 15, 2)::int BETWEEN 1 AND 24
                                          THEN 'TRT' || substr(cnj_digits, 15, 2)::int END
                       WHEN '7' THEN 'STM'
                       WHEN '8' THEN CASE WHEN substr(cnj_digits, 15, 2)::int BETWEEN 1 AND 27
                                          THEN 'TJ' || (ARRAY['AC','AL','AP','AM','BA','CE','DFT','ES','GO','MA','MT','MS','MG',
                                                              'PA','PB','PR','PE','PI','RJ','RN','RS','RO','RR','SC','SE','SP','TO']
                                                        )[substr(cnj_digits, 15, 2)::int] END
                       ELSE NULL END
                 WHERE length(cnj_digits) = 20;
            ");

            migrationBuilder.CreateIndex(
                name: "ix_publications_tenant_cliente",
                table: "publications",
                columns: new[] { "tenant_id", "cliente_id" },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_publications_tenant_published_on",
                table: "publications",
                columns: new[] { "tenant_id", "published_on" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_publications_tenant_responsavel",
                table: "publications",
                columns: new[] { "tenant_id", "responsavel_user_id" },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_publications_tenant_tribunal_published_on",
                table: "publications",
                columns: new[] { "tenant_id", "tribunal", "published_on" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_publications_tenant_cliente",
                table: "publications");

            migrationBuilder.DropIndex(
                name: "ix_publications_tenant_published_on",
                table: "publications");

            migrationBuilder.DropIndex(
                name: "ix_publications_tenant_responsavel",
                table: "publications");

            migrationBuilder.DropIndex(
                name: "ix_publications_tenant_tribunal_published_on",
                table: "publications");

            migrationBuilder.DropColumn(
                name: "cliente_id",
                table: "publications");

            migrationBuilder.DropColumn(
                name: "cnj_digits",
                table: "publications");

            migrationBuilder.DropColumn(
                name: "published_on",
                table: "publications");

            migrationBuilder.DropColumn(
                name: "responsavel_user_id",
                table: "publications");

            migrationBuilder.DropColumn(
                name: "tribunal",
                table: "publications");
        }
    }
}
