using Advocacia.BuildingBlocks.Application.Abstractions;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Platform.Infrastructure.Push;

public static class PushServiceCollectionExtensions
{
    private const string FirebaseAppName = "advocacia-push";

    /// <summary>Registra o envio via FCM se houver credenciais; senão, o Noop.</summary>
    public static IServiceCollection AddPushNotifications(this IServiceCollection services, PushOptions options)
    {
        if (!options.IsConfigured)
        {
            services.AddScoped<IPushNotificationService, NoopPushNotificationService>();
            return services;
        }

        // Singleton: o FirebaseApp é por processo (criar duas vezes com o mesmo nome lança).
        services.AddSingleton(_ => CreateMessaging(options));
        services.AddSingleton<IFcmSender, FirebaseFcmSender>();
        services.AddScoped<IPushNotificationService, FcmPushNotificationService>();

        return services;
    }

    private static FirebaseMessaging CreateMessaging(PushOptions options)
    {
        var app = FirebaseApp.GetInstance(FirebaseAppName) ?? FirebaseApp.Create(
            new AppOptions
            {
                // O JSON tem precedência sobre o caminho (ex.: vindo direto do Key Vault).
                // CredentialFactory<ServiceAccountCredential> (e não GoogleCredential.FromJson/FromFile,
                // depreciados por risco de segurança): só aceita conta de serviço, então um arquivo de
                // outro tipo de credencial é recusado em vez de ser usado em silêncio.
                Credential = (!string.IsNullOrWhiteSpace(options.CredentialsJson)
                    ? CredentialFactory.FromJson<ServiceAccountCredential>(options.CredentialsJson)
                    : CredentialFactory.FromFile<ServiceAccountCredential>(options.CredentialsPath!))
                    .ToGoogleCredential(),
            },
            FirebaseAppName);

        return FirebaseMessaging.GetMessaging(app);
    }
}
