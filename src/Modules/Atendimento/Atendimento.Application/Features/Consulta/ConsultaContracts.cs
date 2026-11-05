using System.Text;
using System.Text.Json.Serialization;
using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Atendimentos.Application.Features.Consulta;

/// <summary>Começa a verificação de identidade. Só vale com o bot conduzindo e o telefone de um cliente cadastrado.</summary>
public sealed record IniciarConsultaCommand(Guid AtendimentoId) : ICommand<ConsultaEtapaResponse>;

/// <summary>
/// Confere o documento informado pelo cliente. O documento é dado pessoal: o <c>ToString</c> gerado
/// para o record o INCLUIRIA, e qualquer log ou exceção que formatasse o comando o vazaria — por isso
/// ele é sobrescrito para nunca mostrá-lo.
/// </summary>
public sealed record VerificarConsultaCommand(Guid AtendimentoId, string Documento)
    : ICommand<VerificarConsultaResponse>
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("AtendimentoId = ").Append(AtendimentoId).Append(", Documento = ***");
        return true;
    }
}

/// <summary>Detalhe de UM processo da lista que a verificação liberou. <c>Indice</c> começa em 1.</summary>
public sealed record GetConsultaProcessoQuery(Guid AtendimentoId, int Indice) : IQuery<ConsultaProcessoResponse>;

public sealed record ConsultaEtapaResponse(string Etapa);

public sealed record ConsultaProcessoItem(int Indice, string Titulo);

/// <summary><c>Processos</c> só vem quando a etapa é "Verificado" (e some do JSON nas demais).</summary>
public sealed record VerificarConsultaResponse(
    string Etapa,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<ConsultaProcessoItem>? Processos = null);

/// <summary>Só o que o cliente final pode ver. Sem peças, valores, parte contrária nem notas internas.</summary>
public sealed record ConsultaProcessoResponse(string Titulo, string Fase, string UltimoAndamento, DateTimeOffset AtualizadoEm);
