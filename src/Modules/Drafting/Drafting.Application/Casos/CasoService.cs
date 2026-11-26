using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Casos;

namespace Advocacia.Modules.Drafting.Application.Casos;

/// <summary>
/// Autorização de caso, num lugar só. Vê o caso: quem é responsável, quem foi autorizado nele e a gestão do
/// escritório (Owner/Partner). O tenant vem do token e é reforçado por filtro global e RLS.
/// </summary>
public sealed class CasoAcessoService(ICasoRepository casos, ICurrentUser currentUser, ISegurancaIaService seguranca) : ICasoAcessoService
{
    public async Task<Result<AcessoCaso>> AutorizarAsync(Guid casoId, bool escrita, CancellationToken cancellationToken = default)
    {
        if (currentUser.TenantId is not { } tenantId || currentUser.UserId is not { } usuarioId)
        {
            return Result.Failure<AcessoCaso>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var caso = await casos.ObterAsync(CasoId.From(casoId), cancellationToken);
        var gestao = Gestao(currentUser);
        if (caso is null || caso.TenantId != tenantId || !(gestao || caso.PodeLer(usuarioId)))
        {
            if (caso is not null)
            {
                await seguranca.RegistrarAsync(tenantId, usuarioId, "AcessoNegadoAoCaso", casoId.ToString(), null, cancellationToken);
            }

            return Result.Failure<AcessoCaso>(ErrorFactory.From(ErrorCode.DRAFTING_CASE_NOT_FOUND));
        }

        var podeEscrever = gestao || caso.PodeEscrever(usuarioId);
        if (escrita && !podeEscrever)
        {
            return Result.Failure<AcessoCaso>(ErrorFactory.From(ErrorCode.DRAFTING_CASE_FORBIDDEN));
        }

        if (escrita && caso.Status == CasoStatus.Encerrado)
        {
            return Result.Failure<AcessoCaso>(ErrorFactory.From(ErrorCode.DRAFTING_CASE_CLOSED));
        }

        return Result.Success(new AcessoCaso(caso, usuarioId, podeEscrever, VeRestritos: caso.UsuarioIdResponsavel == usuarioId));
    }

    internal static bool Gestao(ICurrentUser user) => user.IsInRole("Owner") || user.IsInRole("Partner");
}

public sealed class CasoService(
    ICasoRepository casos,
    ICasoAcessoService acesso,
    IDraftingUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICasoService
{
    public async Task<Result<CasoResponse>> CriarAsync(CriarCasoRequest request, CancellationToken cancellationToken = default)
    {
        if (currentUser.TenantId is not { } tenantId || currentUser.UserId is not { } usuarioId)
        {
            return Result.Failure<CasoResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var campos = Validar(request.Titulo, request.Area, request.NumeroProcesso, request.Uf);
        if (campos.IsFailure)
        {
            return Result.Failure<CasoResponse>(campos.Error);
        }

        var (titulo, area, numero, uf) = campos.Value;
        var nomeCliente = InputSanitizer.Clean(request.ClienteNome);
        if (nomeCliente.Length > Caso.ClienteNomeMaxLength)
        {
            return Result.Failure<CasoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_TOO_LONG, "clienteNome", Caso.ClienteNomeMaxLength));
        }

        var criado = Caso.Criar(
            tenantId, usuarioId, request.ClienteId, titulo, area, nomeCliente, request.ProcessoId, numero, uf, clock.UtcNow);
        if (criado.IsFailure)
        {
            return Result.Failure<CasoResponse>(criado.Error);
        }

        casos.Add(criado.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToResponse(criado.Value, podeEscrever: true));
    }

    public async Task<Result<CasoResponse>> ObterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var autorizado = await acesso.AutorizarAsync(id, escrita: false, cancellationToken);

        return autorizado.IsFailure
            ? Result.Failure<CasoResponse>(autorizado.Error)
            : Result.Success(ToResponse(autorizado.Value.Caso, autorizado.Value.PodeEscrever));
    }

    public async Task<Result<IReadOnlyList<CasoResponse>>> ListarAsync(
        Guid? clienteId, int pagina, int tamanho, CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } usuarioId)
        {
            return Result.Failure<IReadOnlyList<CasoResponse>>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var gestao = CasoAcessoService.Gestao(currentUser);
        var take = Math.Clamp(tamanho, 1, 100);
        var itens = await casos.ListarAsync(gestao ? null : usuarioId, clienteId, Math.Max(0, pagina - 1) * take, take, cancellationToken);

        return Result.Success<IReadOnlyList<CasoResponse>>(
            itens.Select(c => ToResponse(c, gestao || c.PodeEscrever(usuarioId))).ToList());
    }

    public async Task<Result<CasoResponse>> AtualizarAsync(
        Guid id, AtualizarCasoRequest request, CancellationToken cancellationToken = default)
    {
        // Reabrir é escrita num caso encerrado: autoriza como leitura e confere a permissão aqui.
        var autorizado = await acesso.AutorizarAsync(id, escrita: request.Encerrado != false, cancellationToken);
        if (autorizado.IsFailure)
        {
            return Result.Failure<CasoResponse>(autorizado.Error);
        }

        if (!autorizado.Value.PodeEscrever)
        {
            return Result.Failure<CasoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_CASE_FORBIDDEN));
        }

        var campos = Validar(request.Titulo, request.Area, request.NumeroProcesso, request.Uf);
        if (campos.IsFailure)
        {
            return Result.Failure<CasoResponse>(campos.Error);
        }

        var caso = autorizado.Value.Caso;
        var (titulo, area, numero, uf) = campos.Value;
        caso.Atualizar(titulo, area, numero, uf);
        if (request.Encerrado == true)
        {
            caso.Encerrar(clock.UtcNow);
        }
        else if (request.Encerrado == false)
        {
            caso.Reabrir();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToResponse(caso, podeEscrever: true));
    }

    public async Task<Result<CasoResponse>> ConcederAcessoAsync(
        Guid id, ConcederAcessoRequest request, CancellationToken cancellationToken = default)
    {
        var autorizado = await AutorizarGestaoDoCasoAsync(id, cancellationToken);
        if (autorizado.IsFailure)
        {
            return Result.Failure<CasoResponse>(autorizado.Error);
        }

        if (request.UsuarioId == Guid.Empty)
        {
            return Result.Failure<CasoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "usuarioId"));
        }

        if (!Enum.TryParse<CasoPermissao>(request.Permissao, ignoreCase: true, out var permissao) || !Enum.IsDefined(permissao))
        {
            return Result.Failure<CasoResponse>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "permissao"));
        }

        var caso = autorizado.Value.Caso;
        if (request.UsuarioId != caso.UsuarioIdResponsavel
            && caso.Conceder(request.UsuarioId, permissao, autorizado.Value.UsuarioId, clock.UtcNow) is { } novo)
        {
            casos.AddMembro(novo);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToResponse(caso, podeEscrever: true));
    }

    public async Task<Result<CasoResponse>> RevogarAcessoAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var autorizado = await AutorizarGestaoDoCasoAsync(id, cancellationToken);
        if (autorizado.IsFailure)
        {
            return Result.Failure<CasoResponse>(autorizado.Error);
        }

        autorizado.Value.Caso.Revogar(usuarioId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToResponse(autorizado.Value.Caso, podeEscrever: true));
    }

    /// <summary>Quem decide quem entra no caso: o responsável ou a gestão. Membro com escrita não convida outros.</summary>
    private async Task<Result<AcessoCaso>> AutorizarGestaoDoCasoAsync(Guid id, CancellationToken ct)
    {
        var autorizado = await acesso.AutorizarAsync(id, escrita: true, ct);
        if (autorizado.IsFailure)
        {
            return autorizado;
        }

        return autorizado.Value.Caso.UsuarioIdResponsavel == autorizado.Value.UsuarioId || CasoAcessoService.Gestao(currentUser)
            ? autorizado
            : Result.Failure<AcessoCaso>(ErrorFactory.From(ErrorCode.DRAFTING_CASE_FORBIDDEN));
    }

    private static Result<(string Titulo, string Area, string? Numero, string? Uf)> Validar(
        string? titulo, string? area, string? numeroProcesso, string? uf)
    {
        var t = InputSanitizer.Clean(titulo);
        if (t.Length == 0)
        {
            return Result.Failure<(string, string, string?, string?)>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "titulo"));
        }

        if (t.Length > Caso.TituloMaxLength)
        {
            return Result.Failure<(string, string, string?, string?)>(
                ErrorFactory.From(ErrorCode.DRAFTING_FIELD_TOO_LONG, "titulo", Caso.TituloMaxLength));
        }

        var a = (area ?? string.Empty).Trim().ToLowerInvariant();
        if (!PecaCatalogo.Areas.ContainsKey(a))
        {
            return Result.Failure<(string, string, string?, string?)>(ErrorFactory.From(ErrorCode.DRAFTING_INVALID_AREA));
        }

        var n = InputSanitizer.Clean(numeroProcesso);
        if (n.Length > Caso.NumeroProcessoMaxLength)
        {
            return Result.Failure<(string, string, string?, string?)>(
                ErrorFactory.From(ErrorCode.DRAFTING_FIELD_TOO_LONG, "numeroProcesso", Caso.NumeroProcessoMaxLength));
        }

        var u = InputSanitizer.Clean(uf);
        if (u.Length > 0 && (u.Length != 2 || !u.All(char.IsAsciiLetter)))
        {
            return Result.Failure<(string, string, string?, string?)>(ErrorFactory.From(ErrorCode.DRAFTING_FIELD_REQUIRED, "uf"));
        }

        return Result.Success<(string, string, string?, string?)>((t, a, n.Length == 0 ? null : n, u.Length == 0 ? null : u));
    }

    private static CasoResponse ToResponse(Caso c, bool podeEscrever) =>
        new(
            c.Id.Value, c.ClienteId, c.ClienteNome, c.Titulo, c.Area, c.ProcessoId, c.NumeroProcesso, c.Uf, c.Status.ToString(),
            c.UsuarioIdResponsavel, podeEscrever, c.CriadoEm,
            c.Membros.Select(m => new MembroResponse(m.UsuarioId, m.Permissao.ToString().ToLowerInvariant(), m.ConcedidoEm)).ToList());
}
