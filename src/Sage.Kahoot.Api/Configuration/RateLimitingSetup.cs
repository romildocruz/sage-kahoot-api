using System.Globalization;
using System.Threading.RateLimiting;
using Sage.Kahoot.Infrastructure.Configuration;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Sage.Kahoot.Api.Configuration;

/// <summary>
/// Limite de taxa por IP nos dois endpoints públicos: login do admin e entrada por PIN.
/// Usa o limitador nativo do ASP.NET Core, particionado pelo IP da conexão — e não o
/// Throttle() do FastEndpoints, que particiona por cabeçalho enviado pelo cliente e
/// portanto seria trivial de burlar justamente nestes dois casos.
/// </summary>
public static class RateLimitingSetup
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, ct) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var metadata)
                    ? (int)metadata.TotalSeconds
                    : ResolveLimits(context.HttpContext).LoginWindowSeconds;

                context.HttpContext.Response.Headers.RetryAfter =
                    retryAfter.ToString(CultureInfo.InvariantCulture);

                context.HttpContext.Response.ContentType = "application/problem+json";

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        type = "https://sage-kahoot/errors/rate_limited",
                        title = "Muitas requisições",
                        status = StatusCodes.Status429TooManyRequests,
                        detail = "Limite de tentativas excedido. Tente novamente em instantes."
                    },
                    cancellationToken: ct);
            };

            // Os limites são resolvidos por requisição, a partir do provider da própria requisição.
            // Capturá-los aqui exigiria um BuildServiceProvider — um segundo container, que lê
            // configuração antes de fontes adicionadas depois e duplica singletons.
            options.AddPolicy(RateLimitingOptions.LoginPolicy, context =>
            {
                var limits = ResolveLimits(context);
                return FixedWindowByIp(context, limits.LoginPermitLimit, limits.LoginWindowSeconds);
            });

            options.AddPolicy(RateLimitingOptions.JoinPolicy, context =>
            {
                var limits = ResolveLimits(context);
                return FixedWindowByIp(context, limits.JoinPermitLimit, limits.JoinWindowSeconds);
            });
        });

        return services;
    }

    private static RateLimitingOptions ResolveLimits(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

    private static RateLimitPartition<string> FixedWindowByIp(HttpContext context, int permitLimit, int windowSeconds) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
}
