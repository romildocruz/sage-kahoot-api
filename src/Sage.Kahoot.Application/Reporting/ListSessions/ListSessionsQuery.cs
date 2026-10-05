using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Common;
using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.Application.Reporting.ListSessions;

public sealed record ListSessionsQuery(
    int Page,
    int PageSize,
    DateTime? FromUtc,
    DateTime? ToUtc,
    SessionStatus? Status) : ICommand<Result<PagedResult<SessionListItem>>>;

public sealed class ListSessionsHandler(ISessionRepository sessions)
    : ICommandHandler<ListSessionsQuery, Result<PagedResult<SessionListItem>>>
{
    public async Task<Result<PagedResult<SessionListItem>>> ExecuteAsync(ListSessionsQuery query, CancellationToken ct)
    {
        var page = await sessions.ListAsync(
            new ListSessionsFilter(query.Page, query.PageSize, query.FromUtc, query.ToUtc, query.Status),
            ct);

        return page;
    }
}
