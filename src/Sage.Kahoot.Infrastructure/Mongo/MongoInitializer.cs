using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Infrastructure.Configuration;
using Sage.Kahoot.Infrastructure.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sage.Kahoot.Infrastructure.Mongo;

/// <summary>Sinaliza se os índices já foram criados. Consumido pelo health check de readiness.</summary>
public sealed class MongoReadinessState
{
    private volatile bool _indexesReady;

    public bool IndexesReady => _indexesReady;

    internal void MarkReady() => _indexesReady = true;
}

/// <summary>
/// Cria os índices e executa a carga sintética assim que o MongoDB estiver acessível.
/// </summary>
/// <remarks>
/// Roda em segundo plano, com retentativa, em vez de bloquear a inicialização: se o banco
/// estiver fora no momento do deploy, o processo precisa subir mesmo assim para que
/// <c>/health/live</c> responda 200 e apenas <c>/health/ready</c> acuse indisponibilidade —
/// derrubar o processo faria o Kubernetes reiniciar o pod em laço sem resolver nada.
/// Até os índices existirem, a readiness permanece negativa e o pod não recebe tráfego.
/// </remarks>
public sealed class MongoInitializer(
    MongoContext context,
    MongoReadinessState state,
    IServiceScopeFactory scopeFactory,
    IOptions<SeedOptions> seedOptions,
    ILogger<MongoInitializer> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await context.EnsureIndexesAsync(stoppingToken);
                state.MarkReady();
                logger.LogInformation("Índices do MongoDB verificados.");

                await SeedIfEnabledAsync(stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "MongoDB indisponível para criar índices. Nova tentativa em {Delay}s; readiness permanece negativa.",
                    RetryDelay.TotalSeconds);

                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    private async Task SeedIfEnabledAsync(CancellationToken ct)
    {
        if (!seedOptions.Value.Enabled)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();

        await scope.ServiceProvider.GetRequiredService<SyntheticQuizSeeder>()
            .SeedAsync(scope.ServiceProvider.GetRequiredService<IQuizRepository>(), ct);
    }
}
