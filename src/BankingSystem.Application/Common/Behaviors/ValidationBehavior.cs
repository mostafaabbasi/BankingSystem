using BankingSystem.Application.Common.Dispatcher;
using BankingSystem.Domain.Common;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : class
{
    public async Task<TResponse> HandleAsync(
        TRequest request,
        Func<CancellationToken, Task<TResponse>> next,
        CancellationToken ct)
    {
        if (!validators.Any())
            return await next(ct);

        var context = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next(ct);

        logger.LogWarning("Validation failed for {RequestType}: {Errors}",
            typeof(TRequest).Name,
            string.Join(", ", failures.Select(f => f.ErrorMessage)));

        var error = Error.Validation(
            "Validation.Failed",
            string.Join("; ", failures.Select(f => f.ErrorMessage)));

        var resultType = typeof(TResponse);
        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var failureMethod = resultType.GetMethod("Failure",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            return (TResponse)failureMethod!.Invoke(null, [error])!;
        }

        throw new ValidationException(failures);
    }
}
