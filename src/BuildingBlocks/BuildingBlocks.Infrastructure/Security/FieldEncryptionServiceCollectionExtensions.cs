using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Advocacia.BuildingBlocks.Infrastructure.Security;

public static class FieldEncryptionServiceCollectionExtensions
{
    /// <summary>
    /// Registra o <see cref="IFieldEncryptor"/> e o publica em <see cref="FieldEncryption"/>.
    /// Valida as chaves na subida: configuração errada derruba o startup em vez de a aplicação
    /// subir e falhar (ou pior, gravar dados ilegíveis) na primeira escrita.
    ///
    /// Só em Development/Testing, sem chaves configuradas, usa chaves FIXAS e públicas de
    /// desenvolvimento — dão a criptografia real (mesmo código, mesmo formato) sem exigir setup
    /// no checkout, e não protegem nada: qualquer um com o repositório as conhece. Em qualquer
    /// outro ambiente a ausência de chave é erro.
    /// </summary>
    public static IServiceCollection AddAdvocaciaFieldEncryption(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<FieldEncryptionOptions>()
            .Bind(configuration.GetSection(FieldEncryptionOptions.SectionName))
            .PostConfigure(options =>
            {
                var isLocal = environment.IsDevelopment() || environment.IsEnvironment("Testing");
                if (isLocal && options.Keys.Count == 0 && string.IsNullOrEmpty(options.BlindIndexKey))
                {
                    options.ActiveKeyId = "dev";
                    options.Keys = new() { ["dev"] = DevelopmentKeys.Encryption };
                    options.BlindIndexKey = DevelopmentKeys.BlindIndex;
                }
            });

        services.AddSingleton<IFieldEncryptor>(serviceProvider =>
        {
            var encryptor = new AesGcmFieldEncryptor(serviceProvider.GetRequiredService<IOptions<FieldEncryptionOptions>>());
            FieldEncryption.Configure(encryptor);
            return encryptor;
        });

        // Resolve o encryptor no startup: valida as chaves e configura o acessor estático antes
        // da primeira requisição/job.
        services.AddHostedService<FieldEncryptionStartupValidator>();

        return services;
    }

    private sealed class FieldEncryptionStartupValidator(IFieldEncryptor encryptor) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Roundtrip de sanidade: pega chave utilizável mas inconsistente já na subida.
            const string probeValue = "startup-probe";
            var probe = encryptor.Encrypt(probeValue, "startup");
            if (encryptor.Decrypt(probe, "startup") != probeValue)
            {
                throw new CryptographicException("Falha no roundtrip da criptografia de campos.");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

/// <summary>Chaves PÚBLICAS de desenvolvimento/teste. Nunca usar com dado real.</summary>
internal static class DevelopmentKeys
{
    // 32 bytes (0x01..0x20) e 32 bytes (0x21..0x40) em Base64.
    public const string Encryption = "AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyA=";
    public const string BlindIndex = "ISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0+P0A=";
}
