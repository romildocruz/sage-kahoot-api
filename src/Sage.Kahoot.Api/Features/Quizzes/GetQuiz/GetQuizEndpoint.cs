using FastEndpoints;
using Sage.Kahoot.Api.Common;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Quizzes;
using Sage.Kahoot.Application.Quizzes.GetQuizById;

namespace Sage.Kahoot.Api.Features.Quizzes.GetQuiz;

public sealed record GetQuizRequest(Guid QuizId);

public sealed class GetQuizEndpoint : Endpoint<GetQuizRequest, QuizDto>
{
    public override void Configure()
    {
        Get("/quizzes/{quizId}");
        Policies(AuthClaims.AdminPolicy);

        Summary(s => s.Summary =
            "Consulta um quiz com todas as perguntas e opções, incluindo a marcação da correta.");

        Description(b => b
            .Produces<QuizDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    public override async Task HandleAsync(GetQuizRequest request, CancellationToken ct)
    {
        var result = await new GetQuizByIdQuery(request.QuizId).ExecuteAsync(ct);
        await Send.ResultAsync(result.ToHttp());
    }
}
