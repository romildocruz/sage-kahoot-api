using FastEndpoints;
using Sage.Kahoot.Api.Common;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Quizzes;
using Sage.Kahoot.Application.Quizzes.DuplicateQuiz;

namespace Sage.Kahoot.Api.Features.Quizzes.DuplicateQuiz;

public sealed record DuplicateQuizRequest(Guid QuizId);

public sealed class DuplicateQuizEndpoint : Endpoint<DuplicateQuizRequest, QuizDto>
{
    public override void Configure()
    {
        Post("/quizzes/{quizId}/duplicate");
        Policies(AuthClaims.AdminPolicy);

        Summary(s => s.Summary =
            "Duplica um quiz. A cópia é independente: editar uma não afeta a outra.");

        Description(b => b
            .Produces<QuizDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    public override async Task HandleAsync(DuplicateQuizRequest request, CancellationToken ct)
    {
        var result = await new DuplicateQuizCommand(request.QuizId).ExecuteAsync(ct);
        await Send.ResultAsync(result.ToCreated(static quiz => $"/api/v1/quizzes/{quiz.Id}"));
    }
}
