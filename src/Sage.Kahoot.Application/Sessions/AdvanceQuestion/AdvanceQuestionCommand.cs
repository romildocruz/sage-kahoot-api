using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Common;
using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.Application.Sessions.AdvanceQuestion;

public sealed record AdvanceQuestionCommand(Guid SessionId) : ICommand<Result<SessionDto>>;

public sealed class AdvanceQuestionHandler(
    ISessionRepository sessions,
    SessionBroadcaster broadcaster,
    IClock clock)
    : ICommandHandler<AdvanceQuestionCommand, Result<SessionDto>>
{
    public async Task<Result<SessionDto>> ExecuteAsync(AdvanceQuestionCommand command, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(command.SessionId, ct);

        if (session is null)
        {
            return Result<SessionDto>.Failure(SessionErrors.NotFound);
        }

        var expectedIndex = session.CurrentQuestionIndex;
        // Se outro caminho já revelou o resultado desta pergunta, não o revelamos de novo.
        var alreadyClosed = session.CurrentQuestionClosed;
        var advanced = session.Advance(clock.UtcNow);

        if (!advanced.IsSuccess)
        {
            return Result<SessionDto>.Failure(advanced.Error!);
        }

        // Escrita condicional: se outro caminho (expiração ou "todos responderam") já avançou,
        // esta não se aplica e os eventos não são emitidos em duplicidade.
        if (!await sessions.ReplaceIfCurrentQuestionAsync(session, expectedIndex, ct))
        {
            return SessionMapper.ToDto(session);
        }

        if (advanced.Value.ClosedQuestion is not null && !alreadyClosed)
        {
            await broadcaster.QuestionClosedAsync(session, advanced.Value.ClosedQuestion, ct);
        }

        if (advanced.Value.OpenedQuestion is not null)
        {
            await broadcaster.QuestionOpenedAsync(session, advanced.Value.OpenedQuestion, ct);
        }

        if (advanced.Value.SessionEnded)
        {
            await broadcaster.SessionEndedAsync(session, ct);
        }

        return SessionMapper.ToDto(session);
    }
}
