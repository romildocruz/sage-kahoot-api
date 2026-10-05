using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Common;

namespace Sage.Kahoot.Application.Sessions.CloseQuestion;

/// <summary>
/// Fecha a pergunta corrente sem avançar. Disparado pela expiração do tempo e por
/// "todos os participantes ativos responderam". A trava atômica no repositório garante que,
/// mesmo com os dois caminhos concorrendo, o resultado seja revelado uma única vez.
/// </summary>
public sealed record CloseQuestionCommand(Guid SessionId, int QuestionIndex) : ICommand<Result<bool>>;

public sealed class CloseQuestionHandler(
    ISessionRepository sessions,
    SessionBroadcaster broadcaster,
    IClock clock)
    : ICommandHandler<CloseQuestionCommand, Result<bool>>
{
    /// <summary>Retorna verdadeiro somente quando este caminho foi o que efetivamente fechou a pergunta.</summary>
    public async Task<Result<bool>> ExecuteAsync(CloseQuestionCommand command, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(command.SessionId, ct);

        if (session?.CurrentQuestion is null ||
            session.CurrentQuestionIndex != command.QuestionIndex ||
            session.CurrentQuestionClosed)
        {
            return false;
        }

        if (!await sessions.TryCloseCurrentQuestionAsync(session.Id, command.QuestionIndex, clock.UtcNow, ct))
        {
            return false;
        }

        // Recarrega para que o ranking anunciado já inclua as pontuações da própria pergunta.
        var refreshed = await sessions.GetByIdAsync(command.SessionId, ct);

        if (refreshed?.CurrentQuestion is not null)
        {
            await broadcaster.QuestionClosedAsync(refreshed, refreshed.CurrentQuestion, ct);
        }

        return true;
    }
}
