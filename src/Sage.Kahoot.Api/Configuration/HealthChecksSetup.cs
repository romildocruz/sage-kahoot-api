using System.Text.Json;
using Sage.Kahoot.Infrastructure.Configuration;
using Sage.Kahoot.Infrastructure.Mongo;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Sage.Kahoot.Api.Configuration;

/// <summary>
/// Liveness reflete apenas se o processo responde; readiness inclui o MongoDB, porque sem banco
/// a aplicação não consegue atender — mas o pod não deve ser reiniciado por isso.
/// </summary>
public static class HealthChecksSetup
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<MongoHealthCheck>("mongodb", tags: [ReadyTag]);

        return services;
    }

    public static void MapApiHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new()
        {
            // Nenhum check: responde enquanto o processo estiver de pé.
            Predicate = static _ => false
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new()
        {
            Predicate = static check => check.Tags.Contains(ReadyTag),
            ResponseWriter = WriteHealthResponse
        }).AllowAnonymous();
    }

    private static Task WriteHealthResponse(HttpContext context, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                // Identifica qual check falhou, sem expor a connection string.
                description = entry.Value.Description
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}

/// <summary>Ping ao MongoDB com timeout curto: o probe de readiness não deve segurar o pod.</summary>
public sealed class MongoHealthCheck(
    MongoContext context,
    MongoReadinessState readiness,
    IOptions<MongoOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext healthContext,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Value.HealthCheckTimeoutMs);

        try
        {
            await context.PingAsync(timeout.Token);

            // Sem os índices, a unicidade do PIN e a resposta única não estão garantidas:
            // o pod não deve receber tráfego ainda.
            return readiness.IndexesReady
                ? HealthCheckResult.Healthy("MongoDB respondeu ao ping e os índices estão criados.")
                : HealthCheckResult.Unhealthy("MongoDB acessível, mas os índices ainda não foram criados.");
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy(
                $"MongoDB não respondeu em {options.Value.HealthCheckTimeoutMs} ms.");
        }
        catch (Exception ex)
        {
            // Mensagem do driver, nunca a connection string.
            return HealthCheckResult.Unhealthy("MongoDB indisponível.", ex);
        }
    }
}
