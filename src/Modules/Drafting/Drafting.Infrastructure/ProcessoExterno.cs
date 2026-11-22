using System.ComponentModel;
using System.Diagnostics;

namespace Advocacia.Modules.Drafting.Infrastructure;

internal sealed record SaidaProcesso(int Codigo, byte[] Saida, string Erro);

/// <summary>
/// Executa uma ferramenta local (tesseract, pdftoppm, soffice) sem shell, com argumentos em lista, prazo e
/// encerramento da árvore de processos. Nomes de arquivo do usuário NUNCA viram argumento: só caminhos
/// temporários gerados aqui.
/// </summary>
internal static class ProcessoExterno
{
    /// <summary>Nulo = a ferramenta não está instalada.</summary>
    public static async Task<SaidaProcesso?> ExecutarAsync(
        string executavel, IReadOnlyList<string> argumentos, byte[]? entrada, TimeSpan prazo, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo(executavel)
        {
            RedirectStandardInput = entrada is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argumento in argumentos)
        {
            info.ArgumentList.Add(argumento);
        }

        // O Tesseract paraleliza por conta própria e disputa CPU com as outras páginas.
        info.Environment["OMP_THREAD_LIMIT"] = "1";

        Process processo;
        try
        {
            processo = Process.Start(info) ?? throw new InvalidOperationException($"Não foi possível executar {executavel}.");
        }
        catch (Win32Exception)
        {
            return null;
        }

        using (processo)
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limite.CancelAfter(prazo);
            try
            {
                using var memoria = new MemoryStream();
                var saida = processo.StandardOutput.BaseStream.CopyToAsync(memoria, limite.Token);
                var erro = processo.StandardError.ReadToEndAsync(limite.Token);
                if (entrada is not null)
                {
                    try
                    {
                        await processo.StandardInput.BaseStream.WriteAsync(entrada, limite.Token);
                        processo.StandardInput.Close();
                    }
                    catch (IOException)
                    {
                        // A ferramenta saiu antes de ler tudo: o código de saída conta o resto.
                    }
                }

                await saida;
                var textoErro = await erro;
                await processo.WaitForExitAsync(limite.Token);

                return new SaidaProcesso(processo.ExitCode, memoria.ToArray(), textoErro);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    processo.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // Já terminou.
                }

                cancellationToken.ThrowIfCancellationRequested();

                throw new TimeoutException($"{Path.GetFileName(executavel)} não terminou no prazo.");
            }
        }
    }

    /// <summary>Diretório temporário só deste processamento, apagado ao final (contém dado de cliente).</summary>
    public static async Task<T> EmDiretorioTemporarioAsync<T>(Func<string, Task<T>> acao)
    {
        var diretorio = Directory.CreateTempSubdirectory("drafting-").FullName;
        try
        {
            return await acao(diretorio);
        }
        finally
        {
            try
            {
                Directory.Delete(diretorio, recursive: true);
            }
            catch (IOException)
            {
                // Melhor esforço: o diretório é privado do usuário do processo.
            }
        }
    }
}
