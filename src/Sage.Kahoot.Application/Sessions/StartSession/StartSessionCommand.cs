using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Common;

namespace Sage.Kahoot.Application.Sessions.StartSession;

public sealed record StartSessionCommand(Guid SessionId) : ICommand<Result<SessionDto>>;

public sealed class StartSessionHandler(
    ISessionRepository sessions,
    ISessionNotifier notifier,
    SessionBroadcaster broadcaster,
    IClock clock)
    : ICommandHandler<StartSessionCommand, Result<SessionDto>>
{
    public async Task<Result<SessionDto>> ExecuteAsync(StartSessionCommand command, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(command.SessionId, ct);

        if (session is null)
        {
            return Result<SessionDto>.Failure(Domain.Sessions.SessionErrors.NotFound);
        }

        var started = session.Start(clock.UtcNow);

        if (!started.IsSuccess)
        {
            return Result<SessionDto>.Failure(started.Error!);
        }

        await sessions.ReplaceAsync(session, ct);

        await notifier.SessionStartedAsync(new SessionStartedPayload(session.Id, session.TotalQuestions), ct);
        await broadcaster.QuestionOpenedAsync(session, started.Value, ct);

        return SessionMapper.ToDto(session);
    }
}
