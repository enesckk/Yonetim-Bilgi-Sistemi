using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace PersonelYonetim.Application;

/// <summary>
/// Application: FluentValidation kayıtları.
/// MediatR kaydı Infrastructure'da (handler'lar EF kullandığı için orada toplanır).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
