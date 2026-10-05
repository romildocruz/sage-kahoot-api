using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Common;
using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.Application.Sessions.EndSession;

public sealed record EndSessionCommand(Guid SessionId) : ICommand<Result<SessionDto>>;

public sealed class EndSessionHandler(
    ISessionRepository sessions,
    SessionBroadcaster broadcaster,
    IClock clock)
    : ICommandHandler<EndSessionCommand, Result<SessionDto>>
{
    public async Task<Result<SessionDto>> ExecuteAsync(EndSessionCommand command, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(command.SessionId, ct);

        if (session is null)
        {
            return Result<SessionDto>.Failure(SessionErrors.NotFound);
        }

        var openQuestion = session.CurrentQuestionClosed ? null : session.CurrentQuestion;
        var ended = session.End(clock.UtcNow);

        if (!ended.IsSuccess)
        {
            return Result<SessionDto>.Failure(ended.Error!);
        }

        await sessions.ReplaceAsync(session, ct);

        if (openQuestion is not null)
        {
            await broadcaster.QuestionClosedAsync(session, openQuestion, ct);
        }

        await broadcaster.SessionEndedAsync(session, ct);

        return SessionMapper.ToDto(session);
    }
}
