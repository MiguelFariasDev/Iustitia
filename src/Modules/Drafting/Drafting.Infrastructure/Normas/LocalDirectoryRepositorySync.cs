using Advocacia.Modules.Drafting.Application.Normas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Infrastructure.Normas;

/// <summary>
/// Lê os Markdowns JÁ baixados, sem rede, sem executar git e sem escrever na pasta. Percorre só as pastas
/// incluídas e nem entra nas excluídas (<c>_fonte</c> tem centenas de milhares de arquivos).
/// </summary>
public sealed class LocalDirectoryRepositorySync(
    IOptions<NormasIngestaoOptions> options, ILogger<LocalDirectoryRepositorySync> logger) : IRepositorySync
{
    public Task<RepoSnapshot> SincronizarAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var raiz = Path.GetFullPath(settings.DiretorioLocal);
        if (!Directory.Exists(raiz))
        {
            throw new InvalidOperationException("Ingestao:DiretorioLocal não existe. Baixe o acervo ou use Ingestao:Modo=Git.");
        }

        var arquivos = new List<RepoArquivo>();
        foreach (var pasta in settings.Incluidas)
        {
            var caminho = Path.GetFullPath(Path.Combine(raiz, pasta.Replace('\\', '/').Trim('/')));
            if (!caminho.StartsWith(raiz + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Ingestao:PastasIncluidas aponta para fora do diretório do acervo.");
            }

            if (File.Exists(caminho))
            {
                Adicionar(caminho);
            }
            else if (Directory.Exists(caminho))
            {
                Percorrer(caminho);
            }
            else
            {
                logger.LogWarning("Acervo: '{Pasta}' não existe em {Raiz}.", pasta, raiz);
            }
        }

        var commit = Commit(raiz);
        logger.LogInformation("Acervo local ({Commit}): {N} Markdowns elegíveis.", commit, arquivos.Count);

        return Task.FromResult(new RepoSnapshot(commit, arquivos));

        void Percorrer(string diretorio)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var arquivo in Directory.EnumerateFiles(diretorio, "*.md"))
            {
                Adicionar(arquivo);
            }

            foreach (var sub in Directory.EnumerateDirectories(diretorio))
            {
                var nome = Path.GetFileName(sub);
                var link = (File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0;
                if (!link && !nome.StartsWith('_') && !nome.StartsWith('.')
                    && !settings.Excluidas.Contains(nome, StringComparer.OrdinalIgnoreCase))
                {
                    Percorrer(sub);
                }
            }
        }

        void Adicionar(string caminho)
        {
            var relativo = Path.GetRelativePath(raiz, caminho).Replace('\\', '/');
            if (NormaFilePolicy.Elegivel(relativo, settings))
            {
                arquivos.Add(new RepoArquivo(relativo, caminho, new FileInfo(caminho).Length));
            }
        }
    }

    /// <summary>Commit do clone, lido dos arquivos do .git (rastreabilidade da versão ingerida). "local" se não houver.</summary>
    internal static string Commit(string raiz)
    {
        try
        {
            var head = File.ReadAllText(Path.Combine(raiz, ".git", "HEAD")).Trim();
            if (!head.StartsWith("ref: ", StringComparison.Ordinal))
            {
                return head;
            }

            var referencia = head[5..];
            var solto = Path.Combine(raiz, ".git", referencia);
            if (File.Exists(solto))
            {
                return File.ReadAllText(solto).Trim();
            }

            return File.ReadLines(Path.Combine(raiz, ".git", "packed-refs"))
                .FirstOrDefault(l => l.EndsWith(" " + referencia, StringComparison.Ordinal))?.Split(' ')[0] ?? "local";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "local";
        }
    }
}
