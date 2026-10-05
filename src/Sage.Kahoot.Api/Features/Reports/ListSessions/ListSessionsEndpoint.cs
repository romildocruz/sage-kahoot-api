using FastEndpoints;
using Sage.Kahoot.Api.Common;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Reporting.ListSessions;
using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.Api.Features.Reports.ListSessions;

public sealed record ListSessionsRequest(
    int Page = 1,
    int PageSize = Paging.DefaultPageSize,
    DateTime? From = null,
    DateTime? To = null,
    SessionStatus? Status = null);

public sealed class ListSessionsEndpoint : Endpoint<ListSessionsRequest, PagedResult<SessionListItem>>
{
    public override void Configure()
    {
        Get("/reports/sessions");
        Policies(AuthClaims.AdminPolicy);

        Summary(s => s.Summary =
            "Lista sessões anteriores, da mais recente para a mais antiga, com filtro por período e status.");

        Description(b => b
            .Produces<PagedResult<SessionListItem>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized));
    }

    public override async Task HandleAsync(ListSessionsRequest request, CancellationToken ct)
    {
        var result = await new ListSessionsQuery(
            request.Page,
            request.PageSize,
            request.From,
            request.To,
            request.Status).ExecuteAsync(ct);

        await Send.ResultAsync(result.ToHttp());
    }
}
