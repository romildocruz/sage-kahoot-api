using Sage.Kahoot.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sage.Kahoot.Infrastructure.Sessions;

/// <summary>
/// Cancela sessões ativas que ficaram sem atividade além do limite configurado.
/// Além de limpar o estado, libera o PIN para reuso — sem isso, um treinamento
/// interrompido manteria o PIN preso indefinidamente.
/// </summary>
public sealed class AbandonedSessionSweeper(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    IOptions<SessionOptions> options,
    ILogger<AbandonedSessionSweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.AbandonedSweepIntervalMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao varrer sessões abandonadas");
            }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionRepository>();

        var now = clock.UtcNow;
        var threshold = now.AddHours(-options.Value.AbandonedAfterHours);
        var abandoned = await sessions.GetAbandonedAsync(threshold, ct);

        foreach (var session in abandoned)
        {
            var cancelled = session.Cancel(now);

            if (!cancelled.IsSuccess)
            {
                continue;
            }

            await sessions.ReplaceAsync(session, ct);

            logger.LogInformation(
                "Sessão {SessionId} (PIN {Pin}) cancelada por inatividade desde {LastActivity}",
                session.Id,
                session.Pin,
                session.LastActivityAtUtc);
        }
    }
}
