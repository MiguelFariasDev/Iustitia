using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Domain.Events;

namespace Advocacia.Modules.Legal.Domain.Clientes;

/// <summary>
/// Pessoa física ou jurídica atendida pelo escritório. Raiz de agregado: o processo aponta
/// para o cliente por id, nunca o contém.
///
/// Nunca é excluído de verdade — só desativado (<see cref="ISoftDeletable"/> mais
/// <see cref="IsActive"/>). Um cliente com processo tem histórico que precisa continuar
/// consultável para prestação de contas e prazo prescricional; apagá-lo levaria junto a
/// referência de todo processo dele.
/// </summary>
public sealed class Cliente : AggregateRoot<ClienteId>, IAuditable, ISoftDeletable, IHasTenant
{
    public Guid TenantId { get; private set; }

    public TipoCliente Tipo { get; private set; }

    public NomeCliente Nome { get; private set; } = null!;

    /// <summary>
    /// Nome em minúsculas, DERIVADO de <see cref="Nome"/> e mantido em sincronia por este
    /// agregado. Existe porque <see cref="Nome"/> é um value object mapeado por HasConversion,
    /// e o EF Core não consegue traduzir uma busca "contém" sobre um valor convertido: ele
    /// aplica o conversor ao PARÂMETRO e tenta transformar o termo digitado em NomeCliente,
    /// falhando em runtime com InvalidCastException.
    ///
    /// É denormalização deliberada, com o custo de uma coluna e o ganho de uma busca
    /// indexável (pg_trgm — ver 012_clientes_processos.sql).
    /// </summary>
    public string NomeBusca { get; private set; } = string.Empty;

    /// <summary>
    /// Os dígitos do documento, DERIVADOS de <see cref="Documento"/>. Mesmo motivo de
    /// <see cref="NomeBusca"/>: value object convertido não é pesquisável por prefixo.
    /// </summary>
    public string DocumentoBusca { get; private set; } = string.Empty;

    /// <summary>CPF ou CNPJ, conforme o <see cref="Tipo"/>. Único por escritório.</summary>
    public Documento Documento { get; private set; } = null!;

    public Contato Contato { get; private set; } = Contato.Vazio;

    public Endereco Endereco { get; private set; } = Endereco.Vazio;

    /// <summary>
    /// Data de nascimento. Só faz sentido para pessoa física — para pessoa jurídica é sempre
    /// nula, e informá-la é rejeitado em vez de silenciosamente ignorada: um campo que a tela
    /// aceita e o sistema descarta é pior que um campo recusado.
    /// </summary>
    public DateOnly? DataNascimento { get; private set; }

    /// <summary>Anotações livres do escritório sobre o cliente.</summary>
    public string? Observacoes { get; private set; }

    /// <summary>
    /// Cliente inativo não some das listagens nem dos processos — só deixa de aparecer nas
    /// buscas de cadastro novo e não é mais alvo de captura de publicações.
    /// </summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    private Cliente()
    {
        // EF Core.
    }

    private Cliente(ClienteId id, Guid tenantId, TipoCliente tipo, NomeCliente nome, Documento documento)
        : base(id)
    {
        TenantId = tenantId;
        Tipo = tipo;
        Nome = nome;
        NomeBusca = nome.Value.ToLowerInvariant();
        Documento = documento;
        DocumentoBusca = documento.Value;
        IsActive = true;
    }

    public static Result<Cliente> Create(
        Guid tenantId,
        TipoCliente tipo,
        NomeCliente nome,
        Documento documento,
        Contato? contato = null,
        Endereco? endereco = null,
        DateOnly? dataNascimento = null,
        string? observacoes = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<Cliente>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(TenantId)));
        }

        if (nome is null)
        {
            return Result.Failure<Cliente>(ErrorFactory.From(ErrorCode.CLIENT_NAME_REQUIRED));
        }

        if (documento is null)
        {
            return Result.Failure<Cliente>(ErrorFactory.From(ErrorCode.CLIENT_DOCUMENT_INVALID));
        }

        // O documento carrega o tipo com que foi validado. Se divergir do tipo do cliente,
        // um CPF teria sido aceito para uma empresa — erro que só apareceria na hora de
        // emitir um documento oficial.
        if (documento.Tipo != tipo)
        {
            return Result.Failure<Cliente>(ErrorFactory.From(ErrorCode.CLIENT_DOCUMENT_INVALID));
        }

        var dataNascimentoResult = ValidarDataNascimento(tipo, dataNascimento);
        if (dataNascimentoResult.IsFailure)
        {
            return Result.Failure<Cliente>(dataNascimentoResult.Error);
        }

        var cliente = new Cliente(ClienteId.New(), tenantId, tipo, nome, documento)
        {
            Contato = contato ?? Contato.Vazio,
            Endereco = endereco ?? Endereco.Vazio,
            DataNascimento = dataNascimento,
            Observacoes = Normalizar(observacoes),
        };

        cliente.RaiseDomainEvent(new ClienteCriadoEvent(cliente.Id.Value, tenantId, tipo));

        return Result.Success(cliente);
    }

    /// <summary>
    /// Atualiza os dados cadastrais. Tipo e documento NÃO são alteráveis: trocar o CPF de um
    /// cliente é cadastrar outro cliente, e permitir isso silenciosamente reescreveria o
    /// vínculo de todos os processos já atribuídos a ele.
    /// </summary>
    public Result Update(
        NomeCliente nome, Contato contato, Endereco endereco, DateOnly? dataNascimento, string? observacoes)
    {
        if (nome is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CLIENT_NAME_REQUIRED));
        }

        var dataNascimentoResult = ValidarDataNascimento(Tipo, dataNascimento);
        if (dataNascimentoResult.IsFailure)
        {
            return dataNascimentoResult;
        }

        Nome = nome;
        NomeBusca = nome.Value.ToLowerInvariant();
        Contato = contato ?? Contato.Vazio;
        Endereco = endereco ?? Endereco.Vazio;
        DataNascimento = dataNascimento;
        Observacoes = Normalizar(observacoes);

        RaiseDomainEvent(new ClienteAtualizadoEvent(Id.Value, TenantId));

        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CLIENT_ALREADY_INACTIVE));
        }

        IsActive = false;
        RaiseDomainEvent(new ClienteDesativadoEvent(Id.Value, TenantId));

        return Result.Success();
    }

    public Result Reactivate()
    {
        if (IsActive)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CLIENT_ALREADY_ACTIVE));
        }

        IsActive = true;
        RaiseDomainEvent(new ClienteReativadoEvent(Id.Value, TenantId));

        return Result.Success();
    }

    /// <summary>
    /// Data de nascimento só existe para pessoa física e nunca no futuro.
    ///
    /// A data é comparada com <see cref="DateTime.UtcNow"/> em vez de vir de um
    /// IDateTimeProvider injetado: aqui não há regra sensível a fuso nem a relógio de
    /// terceiro — só se recusa uma data que ainda não aconteceu, e uma diferença de
    /// algumas horas no limite não muda nada. Onde o relógio importa de verdade (prazo,
    /// captura), o provider é injetado.
    /// </summary>
    private static Result ValidarDataNascimento(TipoCliente tipo, DateOnly? dataNascimento)
    {
        if (dataNascimento is not { } data)
        {
            return Result.Success();
        }

        if (tipo == TipoCliente.PessoaJuridica)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CLIENT_BIRTHDATE_INVALID));
        }

        return data > DateOnly.FromDateTime(DateTime.UtcNow)
            ? Result.Failure(ErrorFactory.From(ErrorCode.CLIENT_BIRTHDATE_INVALID))
            : Result.Success();
    }

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
