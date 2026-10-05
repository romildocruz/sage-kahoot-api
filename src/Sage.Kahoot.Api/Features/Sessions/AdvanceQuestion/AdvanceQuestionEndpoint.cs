using FastEndpoints;
using Sage.Kahoot.Api.Common;
using Sage.Kahoot.Api.Features.Sessions.StartSession;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Sessions;
using Sage.Kahoot.Application.Sessions.AdvanceQuestion;

namespace Sage.Kahoot.Api.Features.Sessions.AdvanceQuestion;

public sealed class AdvanceQuestionEndpoint : Endpoint<SessionControlRequest, SessionDto>
{
    public override void Configure()
    {
        Post("/sessions/{sessionId}/advance");
        Policies(AuthClaims.AdminPolicy);

        Summary(s => s.Summary =
            "Fecha a pergunta atual, revela a resposta e abre a próxima. Na última, encerra a sessão.");

        Description(b => b
            .Produces<SessionDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict));
    }

    public override async Task HandleAsync(SessionControlRequest request, CancellationToken ct)
    {
        var result = await new AdvanceQuestionCommand(request.SessionId).ExecuteAsync(ct);
        await Send.ResultAsync(result.ToHttp());
    }
}
