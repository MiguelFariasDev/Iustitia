namespace Advocacia.Modules.Atendimentos.Domain.Consulta;

/// <summary>
/// Em que ponto está a verificação de identidade para a consulta de andamento. <see cref="Negado"/>
/// é o resultado de UMA tentativa (documento não confere, ou telefone sem cadastro) — nunca é um
/// estado guardado: depois dele a sessão continua aguardando o documento.
/// </summary>
public enum ConsultaEtapa
{
    AguardandoDocumento,
    Verificado,
    Negado,
    Bloqueado,
    Expirada,
}
