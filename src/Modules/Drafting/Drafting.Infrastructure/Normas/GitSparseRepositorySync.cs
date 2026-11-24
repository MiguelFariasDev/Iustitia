using System.Diagnostics;
using System.Text;
using Advocacia.Modules.Drafting.Application.Normas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Infrastructure.Normas;

/// <summary>
/// Clone parcial do acervo: <c>--depth N --filter=blob:none --sparse</c> + sparse-checkout em modo "não cone"
/// com padrões estilo .gitignore. Só os blobs dos arquivos que casam com o padrão são baixados — o histórico
/// (.git de dezenas de GB), <c>_fonte/</c>, scripts e qualquer arquivo/pasta iniciado por "_" ficam de fora.
///
/// Usa o executável <c>git</c> do host (precisa estar instalado no worker; ver Dockerfile). Os argumentos
/// vão como lista (sem shell), e URL/branch vêm só da configuração do operador.
/// Atualização incremental: <c>fetch --depth N</c> + <c>reset --hard FETCH_HEAD</c>; arquivos removidos no
/// remoto somem do disco e são detectados pela diferença de hashes/arquivos.
/// </summary>
public sealed class GitSparseRepositorySync(
    IOptions<NormasIngestaoOptions> options,
    ILogger<GitSparseRepositorySync> logger) : IRepositorySync
{
    private static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(60);

    public async Task<RepoSnapshot> SincronizarAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        ValidarConfiguracao(settings);

        var dir = Path.GetFullPath(settings.DiretorioLocal);
        Directory.CreateDirectory(dir);

        if (!Directory.Exists(Path.Combine(dir, ".git")))
        {
            logger.LogInformation("Clonando {Url} (parcial, profundidade {Depth}) em {Dir}.", settings.RepositorioUrl, settings.ProfundidadeGit, dir);
            await Git(dir, cancellationToken, "init", "--quiet");
            await Git(dir, cancellationToken, "remote", "add", "origin", settings.RepositorioUrl);
            await Git(dir, cancellationToken, "config", "extensions.partialClone", "origin");
            await Git(dir, cancellationToken, "config", "core.sparseCheckout", "true");
        }

        await File.WriteAllTextAsync(
            Path.Combine(dir, ".git", "info", "sparse-checkout"), PadroesSparse(settings), new UTF8Encoding(false), cancellationToken);

        await Git(
            dir, cancellationToken,
            "fetch", "--quiet", $"--depth={Math.Max(1, settings.ProfundidadeGit)}", "--filter=blob:none", "--no-tags",
            "origin", settings.Branch);
        await Git(dir, cancellationToken, "reset", "--quiet", "--hard", "FETCH_HEAD");

        var commit = (await Git(dir, cancellationToken, "rev-parse", "HEAD")).Trim();
        var arquivos = Listar(dir, settings);
        logger.LogInformation("Repositório em {Commit}: {N} Markdowns elegíveis no disco.", commit[..Math.Min(12, commit.Length)], arquivos.Count);

        return new RepoSnapshot(commit, arquivos);
    }

    /// <summary>
    /// Padrões do sparse-checkout (ordem importa: o último que casar vence). Inclui as pastas pedidas e
    /// EXCLUI auditorias/lotes ("_*"), pastas de exclusão e tudo que não for Markdown dentro das pastas.
    /// </summary>
    internal static string PadroesSparse(NormasIngestaoOptions settings)
    {
        var sb = new StringBuilder();
        foreach (var pasta in settings.PastasIncluidas)
        {
            var p = pasta.Replace('\\', '/').Trim('/');
            sb.Append('/').Append(p).Append(p.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? "\n" : "/\n");
        }

        sb.Append("!_*\n");
        foreach (var excluida in settings.PastasExcluidas.Where(e => e != ".git"))
        {
            sb.Append('!').Append(excluida.Trim('/')).Append(excluida.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? "\n" : "/\n");
        }

        foreach (var extensao in new[] { "*.json", "*.jsonl", "*.gz", "*.zst", "*.txt", "*.py", "*.html", "*.pdf", "*.csv", "*.xml" })
        {
            sb.Append('!').Append(extensao).Append('\n');
        }

        if (settings.BaixarOriginais)
        {
            sb.Append("/_fonte/\n");
        }

        return sb.ToString();
    }

    private static List<RepoArquivo> Listar(string dir, NormasIngestaoOptions settings)
    {
        var lista = new List<RepoArquivo>();
        foreach (var path in Directory.EnumerateFiles(dir, "*.md", SearchOption.AllDirectories))
        {
            var relativo = Path.GetRelativePath(dir, path).Replace('\\', '/');
            if (relativo.StartsWith(".git/", StringComparison.Ordinal) || !NormaFilePolicy.Elegivel(relativo, settings))
            {
                continue;
            }

            lista.Add(new RepoArquivo(relativo, path, new FileInfo(path).Length));
        }

        return lista;
    }

    private static void ValidarConfiguracao(NormasIngestaoOptions settings)
    {
        if (!Uri.TryCreate(settings.RepositorioUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Ingestao:RepositorioUrl precisa ser uma URL https.");
        }

        // Branch e pastas viram argumento de git / linha do sparse-checkout: só caracteres previsíveis.
        if (!SeguroParaGit(settings.Branch) || settings.PastasIncluidas.Any(p => !SeguroParaGit(p)))
        {
            throw new InvalidOperationException("Ingestao: branch ou pasta com caracteres inválidos.");
        }
    }

    private static bool SeguroParaGit(string valor) =>
        valor.Length is > 0 and <= 200
        && !valor.StartsWith('-')
        && !valor.Contains("..", StringComparison.Ordinal)
        && valor.All(c => char.IsLetterOrDigit(c) || c is '/' or '.' or '-' or '_');

    private async Task<string> Git(string workingDirectory, CancellationToken cancellationToken, params string[] args)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        // Nunca pedir credencial nem abrir editor/pager: o worker não é interativo.
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        info.Environment["GIT_ASKPASS"] = "echo";

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Não foi possível executar o git.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(GitTimeout);

        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Já terminou.
            }

            throw;
        }

        var output = await stdout;
        var error = await stderr;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {args[0]} falhou (código {process.ExitCode}): {Resumir(error)}");
        }

        return output;
    }

    private static string Resumir(string texto) => texto.Length <= 300 ? texto.Trim() : texto[..300].Trim() + "…";
}
