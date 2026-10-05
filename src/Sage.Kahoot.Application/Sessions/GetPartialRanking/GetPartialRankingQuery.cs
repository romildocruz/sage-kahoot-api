using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Common;
using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.Application.Sessions.GetPartialRanking;

public sealed record GetPartialRankingQuery(Guid SessionId) : ICommand<Result<RankingPayload>>;

public sealed class GetPartialRankingHandler(ISessionRepository sessions)
    : ICommandHandler<GetPartialRankingQuery, Result<RankingPayload>>
{
    public async Task<Result<RankingPayload>> ExecuteAsync(GetPartialRankingQuery query, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(query.SessionId, ct);

        if (session is null)
        {
            return Result<RankingPayload>.Failure(SessionErrors.NotFound);
        }

        // Final apenas quando a sessão encerrou: o cliente precisa saber se pode anunciar o pódio.
        var isFinal = session.Status is SessionStatus.Encerrada;

        return SessionBroadcaster.BuildRanking(session, isFinal);
    }
}
