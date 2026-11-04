using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Clientes;

namespace Advocacia.Modules.Legal.Application.Features.Clientes;

/// <summary>
/// Cadastro de cliente. O tenant vem SEMPRE do JWT (ICurrentUser), nunca do corpo da
/// requisição — é o que impede um cliente autenticado de cadastrar no escritório alheio.
/// </summary>
public sealed class CreateClienteHandler(
    IClienteRepository clienteRepository,
    ILegalUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<CreateClienteCommand, ClienteResponse>
{
    public async Task<Result<ClienteResponse>> Handle(CreateClienteCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<ClienteResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        if (!Enum.TryParse<TipoCliente>(request.Tipo, ignoreCase: true, out var tipo))
        {
            return Result.Failure<ClienteResponse>(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Tipo)));
        }

        var nome = NomeCliente.Create(request.Nome);
        if (nome.IsFailure)
        {
            return Result.Failure<ClienteResponse>(nome.Error);
        }

        var documento = Documento.Create(request.Documento, tipo);
        if (documento.IsFailure)
        {
            return Result.Failure<ClienteResponse>(documento.Error);
        }

        // Checagem em código é a primeira camada; o índice único (tenant_id, documento) é a
        // definitiva sob concorrência — mesmo desenho da deduplicação de publicações (ADR-056).
        var jaExiste = await clienteRepository.GetByDocumentoAsync(tenantId, documento.Value, cancellationToken);
        if (jaExiste is not null)
        {
            return Result.Failure<ClienteResponse>(ErrorFactory.From(ErrorCode.CLIENT_DOCUMENT_DUPLICATED));
        }

        var contato = Contato.Create(request.Contato?.Email, request.Contato?.Telefone);
        if (contato.IsFailure)
        {
            return Result.Failure<ClienteResponse>(contato.Error);
        }

        var endereco = Endereco.Create(
            request.Endereco?.Logradouro, request.Endereco?.Numero, request.Endereco?.Complemento,
            request.Endereco?.Bairro, request.Endereco?.Cidade, request.Endereco?.Uf, request.Endereco?.Cep);

        var cliente = Cliente.Create(
            tenantId, tipo, nome.Value, documento.Value, contato.Value, endereco.Value,
            request.DataNascimento, request.Observacoes);

        if (cliente.IsFailure)
        {
            return Result.Failure<ClienteResponse>(cliente.Error);
        }

        clienteRepository.Add(cliente.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(cliente.Value.ToResponse());
    }
}

public sealed class UpdateClienteHandler(
    IClienteRepository clienteRepository,
    ILegalUnitOfWork unitOfWork) : ICommandHandler<UpdateClienteCommand>
{
    public async Task<Result> Handle(UpdateClienteCommand request, CancellationToken cancellationToken)
    {
        var cliente = await clienteRepository.GetByIdAsync(ClienteId.From(request.ClienteId), cancellationToken);
        if (cliente is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CLIENT_NOT_FOUND));
        }

        var nome = NomeCliente.Create(request.Nome);
        if (nome.IsFailure)
        {
            return nome;
        }

        var contato = Contato.Create(request.Contato?.Email, request.Contato?.Telefone);
        if (contato.IsFailure)
        {
            return contato;
        }

        var endereco = Endereco.Create(
            request.Endereco?.Logradouro, request.Endereco?.Numero, request.Endereco?.Complemento,
            request.Endereco?.Bairro, request.Endereco?.Cidade, request.Endereco?.Uf, request.Endereco?.Cep);

        var resultado = cliente.Update(
            nome.Value, contato.Value, endereco.Value, request.DataNascimento, request.Observacoes);
        if (resultado.IsFailure)
        {
            return resultado;
        }

        clienteRepository.Update(cliente);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>
/// Desativa o cliente. NÃO exclui: um cliente com processo tem histórico que precisa
/// continuar consultável, e apagá-lo levaria junto a referência de todos os processos dele.
/// </summary>
public sealed class DeactivateClienteHandler(
    IClienteRepository clienteRepository,
    ILegalUnitOfWork unitOfWork) : ICommandHandler<DeactivateClienteCommand>
{
    public async Task<Result> Handle(DeactivateClienteCommand request, CancellationToken cancellationToken)
    {
        var cliente = await clienteRepository.GetByIdAsync(ClienteId.From(request.ClienteId), cancellationToken);
        if (cliente is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CLIENT_NOT_FOUND));
        }

        var resultado = cliente.Deactivate();
        if (resultado.IsFailure)
        {
            return resultado;
        }

        clienteRepository.Update(cliente);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class ReactivateClienteHandler(
    IClienteRepository clienteRepository,
    ILegalUnitOfWork unitOfWork) : ICommandHandler<ReactivateClienteCommand>
{
    public async Task<Result> Handle(ReactivateClienteCommand request, CancellationToken cancellationToken)
    {
        var cliente = await clienteRepository.GetByIdAsync(ClienteId.From(request.ClienteId), cancellationToken);
        if (cliente is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.CLIENT_NOT_FOUND));
        }

        var resultado = cliente.Reactivate();
        if (resultado.IsFailure)
        {
            return resultado;
        }

        clienteRepository.Update(cliente);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>
/// Não filtra por tenant explicitamente: o filtro global do LegalDbContext e o RLS já
/// restringem a consulta ao escritório da requisição, então um cliente de outro escritório
/// simplesmente não é encontrado — 404, não 403, para não confirmar que ele existe.
/// </summary>
public sealed class GetClienteByIdHandler(IClienteRepository clienteRepository)
    : IQueryHandler<GetClienteByIdQuery, ClienteResponse>
{
    public async Task<Result<ClienteResponse>> Handle(GetClienteByIdQuery request, CancellationToken cancellationToken)
    {
        var cliente = await clienteRepository.GetByIdAsync(ClienteId.From(request.ClienteId), cancellationToken);

        return cliente is null
            ? Result.Failure<ClienteResponse>(ErrorFactory.From(ErrorCode.CLIENT_NOT_FOUND))
            : Result.Success(cliente.ToResponse());
    }
}

public sealed class ListClientesHandler(IClienteRepository clienteRepository, ICurrentUser currentUser)
    : IQueryHandler<ListClientesQuery, ListClientesResponse>
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    public async Task<Result<ListClientesResponse>> Handle(ListClientesQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<ListClientesResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        TipoCliente? tipo = null;
        if (!string.IsNullOrWhiteSpace(request.Tipo))
        {
            if (!Enum.TryParse<TipoCliente>(request.Tipo, ignoreCase: true, out var parsed))
            {
                return Result.Failure<ListClientesResponse>(
                    ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Tipo)));
            }

            tipo = parsed;
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : request.PageSize;
        var skip = (page - 1) * pageSize;

        var items = await clienteRepository.ListAsync(
            tenantId, tipo, request.ApenasAtivos, request.Busca, skip, pageSize, cancellationToken);
        var totalCount = await clienteRepository.CountAsync(
            tenantId, tipo, request.ApenasAtivos, request.Busca, cancellationToken);

        return Result.Success(new ListClientesResponse(
            items.Select(cliente => cliente.ToListItem()).ToList(),
            page,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize)));
    }
}
