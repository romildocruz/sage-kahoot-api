using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Common;

namespace Sage.Kahoot.Application.Quizzes.DeleteQuiz;

public sealed record DeleteQuizCommand(Guid QuizId) : ICommand<Result<Unit>>;

public sealed class DeleteQuizHandler(IQuizRepository quizzes, ISessionRepository sessions)
    : ICommandHandler<DeleteQuizCommand, Result<Unit>>
{
    public async Task<Result<Unit>> ExecuteAsync(DeleteQuizCommand command, CancellationToken ct)
    {
        if (await sessions.HasActiveSessionForQuizAsync(command.QuizId, ct))
        {
            return Result<Unit>.Failure(QuizErrors.InUseByActiveSession);
        }

        // Sessões encerradas guardam seu próprio snapshot do quiz, então o histórico
        // e os relatórios continuam consultáveis depois desta exclusão.
        var deleted = await quizzes.DeleteAsync(command.QuizId, ct);

        return deleted
            ? Result.Success()
            : Result<Unit>.Failure(QuizErrors.NotFound);
    }
}
