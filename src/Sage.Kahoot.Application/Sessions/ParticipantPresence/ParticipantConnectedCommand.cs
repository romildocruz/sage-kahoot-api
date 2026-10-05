using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Common;
using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.Application.Sessions.ParticipantPresence;

/// <summary>
/// Registra que o participante tem conexão de tempo real aberta. Despachado pelo hub em
/// <c>OnConnectedAsync</c>, inclusive em reconexões.
/// </summary>
public sealed record ParticipantConnectedCommand(Guid SessionId, Guid ParticipantId) : ICommand<Result<Unit>>;

public sealed class ParticipantConnectedHandler(ISessionRepository sessions, IClock clock)
    : ICommandHandler<ParticipantConnectedCommand, Result<Unit>>
{
    public async Task<Result<Unit>> ExecuteAsync(ParticipantConnectedCommand command, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(command.SessionId, ct);

        if (session is null)
        {
            return Result<Unit>.Failure(SessionErrors.NotFound);
        }

        var marked = session.MarkParticipantConnected(command.ParticipantId, clock.UtcNow);

        if (!marked.IsSuccess)
        {
            return Result<Unit>.Failure(marked.Error!);
        }

        // Escrita posicional: conexões concorrem com o registro de respostas, e reescrever o
        // documento inteiro descartaria pontuação gravada entre a leitura e a gravação.
        await sessions.SetParticipantConnectedAsync(
            command.SessionId,
            command.ParticipantId,
            isConnected: true,
            clock.UtcNow,
            ct);

        return Result.Success();
    }
}
