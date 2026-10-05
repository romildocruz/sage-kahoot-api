using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Common;
using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.Application.Sessions.ParticipantPresence;

/// <summary>
/// Registra que o participante perdeu a sua última conexão de tempo real e anuncia a saída.
/// O hub só despacha isto quando não resta nenhuma outra conexão do mesmo participante: trocar de
/// aba não é sair, e emitir saída nesse caso tiraria alguém ativo da contagem, fazendo a pergunta
/// fechar cedo demais.
/// </summary>
public sealed record ParticipantDisconnectedCommand(Guid SessionId, Guid ParticipantId) : ICommand<Result<Unit>>;

public sealed class ParticipantDisconnectedHandler(
    ISessionRepository sessions,
    ISessionNotifier notifier,
    IClock clock)
    : ICommandHandler<ParticipantDisconnectedCommand, Result<Unit>>
{
    public async Task<Result<Unit>> ExecuteAsync(ParticipantDisconnectedCommand command, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(command.SessionId, ct);

        if (session is null)
        {
            return Result<Unit>.Failure(SessionErrors.NotFound);
        }

        var marked = session.MarkParticipantDisconnected(command.ParticipantId, clock.UtcNow);

        if (!marked.IsSuccess)
        {
            return Result<Unit>.Failure(marked.Error!);
        }

        await sessions.SetParticipantConnectedAsync(
            command.SessionId,
            command.ParticipantId,
            isConnected: false,
            clock.UtcNow,
            ct);

        // O total anunciado é o de participantes ativos, que é o número que o apresentador
        // precisa ver no telão — e o mesmo que governa o fechamento antecipado.
        await notifier.ParticipantLeftAsync(
            new ParticipantPresencePayload(
                session.Id,
                marked.Value.Nickname,
                session.ActiveParticipants.Count),
            ct);

        return Result.Success();
    }
}
