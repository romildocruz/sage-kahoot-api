using FastEndpoints;
using Sage.Kahoot.Api.Common;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Quizzes.DeleteQuiz;

namespace Sage.Kahoot.Api.Features.Quizzes.DeleteQuiz;

public sealed record DeleteQuizRequest(Guid QuizId);

public sealed class DeleteQuizEndpoint : Endpoint<DeleteQuizRequest>
{
    public override void Configure()
    {
        Delete("/quizzes/{quizId}");
        Policies(AuthClaims.AdminPolicy);

        Summary(s => s.Summary =
            "Exclui um quiz. Relatórios de sessões encerradas continuam consultáveis, pois cada sessão guarda seu próprio snapshot.");

        Description(b => b
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict));
    }

    public override async Task HandleAsync(DeleteQuizRequest request, CancellationToken ct)
    {
        var result = await new DeleteQuizCommand(request.QuizId).ExecuteAsync(ct);
        await Send.ResultAsync(result.ToNoContent());
    }
}
