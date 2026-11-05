using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Advocacia.Modules.Atendimentos.Application;

/// <summary>
/// Composition root de Atendimento.Application. Os behaviors abertos do MediatR NÃO são
/// registrados de novo aqui (rodariam duas vezes) — ver o equivalente em Legal.Application.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddAtendimentoApplication(this IServiceCollection services)
    {
        services.AddMediatR(mediatr => mediatr.RegisterServicesFromAssembly(AtendimentoApplicationAssembly.Reference));
        services.AddValidatorsFromAssembly(AtendimentoApplicationAssembly.Reference);

        return services;
    }
}
