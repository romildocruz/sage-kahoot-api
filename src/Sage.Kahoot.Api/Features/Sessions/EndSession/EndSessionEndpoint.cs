using FastEndpoints;
using Sage.Kahoot.Api.Common;
using Sage.Kahoot.Api.Features.Sessions.StartSession;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Sessions;
using Sage.Kahoot.Application.Sessions.EndSession;

namespace Sage.Kahoot.Api.Features.Sessions.EndSession;

public sealed class EndSessionEndpoint : Endpoint<SessionControlRequest, SessionDto>
{
    public override void Configure()
    {
        Post("/sessions/{sessionId}/end");
        Policies(AuthClaims.AdminPolicy);

        Summary(s => s.Summary = "Encerra a sessão manualmente e consolida o ranking final.");

        Description(b => b
            .Produces<SessionDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict));
    }

    public override async Task HandleAsync(SessionControlRequest request, CancellationToken ct)
    {
        var result = await new EndSessionCommand(request.SessionId).ExecuteAsync(ct);
        await Send.ResultAsync(result.ToHttp());
    }
}
