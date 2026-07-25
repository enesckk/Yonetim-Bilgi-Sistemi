using FluentValidation;
using MediatR;

namespace PersonelYonetim.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior: handler çalışmadan önce FluentValidation kurallarını uygular.
/// Controller'da validator çağırmaya gerek kalmaz — çapraz kesen (cross-cutting) doğrulama.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        var errors = failures
            .GroupBy(f => ToCamelCase(f.PropertyName))
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).Distinct().ToArray());

        throw new Exceptions.ValidationException(errors);
    }

    private static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return propertyName;
        var first = propertyName.Split('.')[0];
        return char.ToLowerInvariant(first[0]) + first[1..];
    }
}
