using FastEndpoints;
using Sage.Kahoot.Api.Common;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Quizzes;
using Sage.Kahoot.Application.Quizzes.CreateQuiz;

namespace Sage.Kahoot.Api.Features.Quizzes.CreateQuiz;

public sealed class CreateQuizEndpoint : Endpoint<CreateQuizRequest, QuizDto>
{
    public override void Configure()
    {
        Post("/quizzes");
        Policies(AuthClaims.AdminPolicy);

        Summary(s => s.Summary = "Cria um quiz. Pode ser criado sem perguntas e recebê-las depois.");

        Description(b => b
            .Produces<QuizDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized));
    }

    public override async Task HandleAsync(CreateQuizRequest request, CancellationToken ct)
    {
        var result = await new CreateQuizCommand(
            request.Title,
            request.Description,
            QuizRequestRules.ToInputs(request.Questions)).ExecuteAsync(ct);

        await Send.ResultAsync(result.ToCreated(static quiz => $"/api/v1/quizzes/{quiz.Id}"));
    }
}
