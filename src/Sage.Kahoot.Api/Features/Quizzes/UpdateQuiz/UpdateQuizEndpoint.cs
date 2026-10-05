using FastEndpoints;
using Sage.Kahoot.Api.Common;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Quizzes;
using Sage.Kahoot.Application.Quizzes.UpdateQuiz;

namespace Sage.Kahoot.Api.Features.Quizzes.UpdateQuiz;

public sealed class UpdateQuizEndpoint : Endpoint<UpdateQuizRequest, QuizDto>
{
    public override void Configure()
    {
        Put("/quizzes/{quizId}");
        Policies(AuthClaims.AdminPolicy);

        Summary(s => s.Summary = "Altera um quiz. Recusado com 409 se houver sessão ativa vinculada.");

        Description(b => b
            .Produces<QuizDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict));
    }

    public override async Task HandleAsync(UpdateQuizRequest request, CancellationToken ct)
    {
        var result = await new UpdateQuizCommand(
            request.QuizId,
            request.Title,
            request.Description,
            QuizRequestRules.ToInputs(request.Questions)).ExecuteAsync(ct);

        await Send.ResultAsync(result.ToHttp());
    }
}
