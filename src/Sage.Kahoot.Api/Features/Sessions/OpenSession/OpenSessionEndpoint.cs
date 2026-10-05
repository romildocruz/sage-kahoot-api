using FastEndpoints;
using FluentValidation;
using Sage.Kahoot.Api.Common;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Sessions;
using Sage.Kahoot.Application.Sessions.OpenSession;

namespace Sage.Kahoot.Api.Features.Sessions.OpenSession;

public sealed record OpenSessionRequest(Guid QuizId);

public sealed class OpenSessionValidator : Validator<OpenSessionRequest>
{
    public OpenSessionValidator() => RuleFor(x => x.QuizId).NotEmpty();
}

public sealed class OpenSessionEndpoint : Endpoint<OpenSessionRequest, SessionDto>
{
    public override void Configure()
    {
        Post("/sessions");
        Policies(AuthClaims.AdminPolicy);

        Summary(s => s.Summary =
            "Abre uma sessão a partir de um quiz e devolve o PIN. A sessão congela uma cópia do quiz neste instante.");

        Description(b => b
            .Produces<SessionDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    public override async Task HandleAsync(OpenSessionRequest request, CancellationToken ct)
    {
        var result = await new OpenSessionCommand(request.QuizId).ExecuteAsync(ct);
        await Send.ResultAsync(result.ToCreated(static session => $"/api/v1/sessions/{session.Id}/state"));
    }
}
