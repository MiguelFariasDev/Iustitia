namespace Advocacia.BuildingBlocks.Infrastructure.Messaging;

/// <summary>Configuração de transporte do MassTransit — seção "Messaging" do appsettings (ver ADR-016).</summary>
public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    /// <summary>"RabbitMq" (dev local, via docker-compose) ou "AzureServiceBus" (produção). Padrão: AzureServiceBus.</summary>
    public string Transport { get; init; } = "AzureServiceBus";

    /// <summary>Obrigatória quando Transport = "AzureServiceBus". Em produção, injetada via Azure Key Vault.</summary>
    public string? AzureServiceBusConnectionString { get; init; }

    public RabbitMqOptions RabbitMq { get; init; } = new();

    public sealed class RabbitMqOptions
    {
        public string Host { get; init; } = "localhost";

        public ushort Port { get; init; } = 5673;

        public string VirtualHost { get; init; } = "/";

        public string Username { get; init; } = "guest";

        public string Password { get; init; } = "guest";
    }
}
