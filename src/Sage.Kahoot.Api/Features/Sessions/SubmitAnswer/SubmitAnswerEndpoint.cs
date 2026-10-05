using FastEndpoints;
using FluentValidation;
using Sage.Kahoot.Api.Common;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Sessions;
using Sage.Kahoot.Application.Sessions.SubmitAnswer;
using Sage.Kahoot.Infrastructure.Auth;

namespace Sage.Kahoot.Api.Features.Sessions.SubmitAnswer;

/// <summary>
/// Não há campo de participante nem de tempo decorrido: a identidade vem do token e o tempo,
/// do relógio do servidor. Aceitar qualquer um dos dois do cliente permitiria forjar pontuação.
/// </summary>
public sealed record SubmitAnswerRequest(Guid SessionId, Guid QuestionId, Guid OptionId);

public sealed class SubmitAnswerValidator : Validator<SubmitAnswerRequest>
{
    public SubmitAnswerValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.OptionId).NotEmpty();
    }
}

public sealed class SubmitAnswerEndpoint : Endpoint<SubmitAnswerRequest, AnswerAcceptedResult>
{
    public override void Configure()
    {
        Post("/sessions/{sessionId}/answers");
        Policies(AuthClaims.ParticipantPolicy);

        Summary(s =>
        {
            s.Summary = "Registra a resposta do participante à pergunta aberta.";
            s.Description =
                "Confirma o recebimento sem revelar acerto: o resultado só é divulgado no fechamento da pergunta.";
        });

        Description(b => b
            .Produces<AnswerAcceptedResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict));
    }

    public override async Task HandleAsync(SubmitAnswerRequest request, CancellationToken ct)
    {
        var participantId = User.ParticipantId();
        var tokenSessionId = User.SessionId();

        // O token amarra o participante a uma sessão: responder em outra é recusado aqui.
        if (participantId is null || tokenSessionId != request.SessionId)
        {
            await Send.ResultAsync(Domain.Sessions.SessionErrors.ParticipantNotInSession.ToProblem());
            return;
        }

        var result = await new SubmitAnswerCommand(
            request.SessionId,
            participantId.Value,
            request.QuestionId,
            request.OptionId).ExecuteAsync(ct);

        await Send.ResultAsync(result.ToHttp());
    }
}
