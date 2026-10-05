using FastEndpoints;
using Sage.Kahoot.Api.Common;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Sessions;
using Sage.Kahoot.Application.Sessions.StartSession;

namespace Sage.Kahoot.Api.Features.Sessions.StartSession;

public sealed record SessionControlRequest(Guid SessionId);

/// <summary>
/// Equivalente REST da invocação do hub. Compartilha exatamente o mesmo handler —
/// serve de alternativa e facilita o teste manual pelo Swagger.
/// </summary>
public sealed class StartSessionEndpoint : Endpoint<SessionControlRequest, SessionDto>
{
    public override void Configure()
    {
        Post("/sessions/{sessionId}/start");
        Policies(AuthClaims.AdminPolicy);

        Summary(s => s.Summary = "Inicia a sessão e abre a primeira pergunta.");

        Description(b => b
            .Produces<SessionDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict));
    }

    public override async Task HandleAsync(SessionControlRequest request, CancellationToken ct)
    {
        var result = await new StartSessionCommand(request.SessionId).ExecuteAsync(ct);
        await Send.ResultAsync(result.ToHttp());
    }
}
