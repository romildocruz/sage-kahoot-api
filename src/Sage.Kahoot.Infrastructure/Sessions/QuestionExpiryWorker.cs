using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Sessions.CloseQuestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sage.Kahoot.Infrastructure.Sessions;

/// <summary>
/// Fecha perguntas cuja janela de tempo expirou.
/// </summary>
/// <remarks>
/// Varredura periódica curta em vez de um temporizador agendado por pergunta: o estado
/// autoritativo está no banco, então a varredura reencontra perguntas expiradas mesmo após
/// um reinício do processo — um temporizador em memória as perderia. O fechamento em si
/// continua protegido pela trava atômica, de modo que concorrer com o avanço do apresentador
/// ou com "todos responderam" não duplica a revelação do resultado.
/// </remarks>
public sealed class QuestionExpiryWorker(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<QuestionExpiryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CloseExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Uma falha de varredura não pode derrubar o worker: a próxima tentativa corrige.
                logger.LogError(ex, "Falha ao fechar perguntas expiradas");
            }
        }
    }

    private async Task CloseExpiredAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionRepository>();

        var expired = await sessions.GetWithExpiredQuestionAsync(clock.UtcNow, ct);

        foreach (var session in expired)
        {
            var closed = await new CloseQuestionCommand(session.Id, session.CurrentQuestionIndex).ExecuteAsync(ct);

            if (closed.IsSuccess && closed.Value)
            {
                logger.LogInformation(
                    "Pergunta {QuestionIndex} da sessão {SessionId} fechada por expiração de tempo",
                    session.CurrentQuestionIndex,
                    session.Id);
            }
        }
    }
}
