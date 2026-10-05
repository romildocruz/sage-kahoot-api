using FastEndpoints;
using Sage.Kahoot.Api.Common;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Sessions.GetSessionState;
using Sage.Kahoot.Infrastructure.Auth;

namespace Sage.Kahoot.Api.Features.Sessions.GetState;

public sealed record GetSessionStateRequest(Guid SessionId);

/// <summary>
/// Estado autoritativo da sessão. É o que um cliente consulta ao reconectar: o tempo restante
/// vem recalculado pelo servidor, e o estado local do cliente é descartado.
/// </summary>
public sealed class GetSessionStateEndpoint : Endpoint<GetSessionStateRequest, SessionStatePayload>
{
    public override void Configure()
    {
        Get("/sessions/{sessionId}/state");
        Roles(AuthClaims.AdminRole, AuthClaims.ParticipantRole);

        Summary(s => s.Summary = "Estado corrente da sessão, com o tempo restante calculado pelo servidor.");

        Description(b => b
            .Produces<SessionStatePayload>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    public override async Task HandleAsync(GetSessionStateRequest request, CancellationToken ct)
    {
        var participantId = User.ParticipantId();

        // Participante só enxerga a sessão do seu token; admin consulta qualquer uma.
        if (participantId is not null && User.SessionId() != request.SessionId)
        {
            await Send.ResultAsync(Domain.Sessions.SessionErrors.ParticipantNotInSession.ToProblem());
            return;
        }

        var result = await new GetSessionStateQuery(request.SessionId, participantId).ExecuteAsync(ct);
        await Send.ResultAsync(result.ToHttp());
    }
}
