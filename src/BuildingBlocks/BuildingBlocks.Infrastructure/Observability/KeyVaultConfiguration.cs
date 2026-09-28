using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace Advocacia.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Adiciona o Azure Key Vault como fonte de configuração (ver ADR-036/docs/compliance/
/// production-config.md) — só quando "KeyVault:Uri" está definido (produção/staging via
/// variável de ambiente ou appsettings.Production.json). Em dev local, sem essa variável,
/// isso é um no-op silencioso: os segredos continuam vindo do appsettings.Development.json.
/// Usa <see cref="DefaultAzureCredential"/>, que resolve para a Managed Identity do Container
/// App em produção, sem nenhuma credencial armazenada no código/configuração.
/// </summary>
public static class KeyVaultConfiguration
{
    public static IConfigurationBuilder AddAdvocaciaKeyVault(this IConfigurationBuilder configurationBuilder)
    {
        var builtConfiguration = configurationBuilder.Build();
        var keyVaultUri = builtConfiguration["KeyVault:Uri"];

        if (!string.IsNullOrWhiteSpace(keyVaultUri))
        {
            configurationBuilder.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
        }

        return configurationBuilder;
    }
}
