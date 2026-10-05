using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Common;

namespace Sage.Kahoot.Application.Quizzes.UpdateQuiz;

public sealed record UpdateQuizCommand(
    Guid QuizId,
    string Title,
    string? Description,
    IReadOnlyList<QuestionInput> Questions) : ICommand<Result<QuizDto>>;

public sealed class UpdateQuizHandler(IQuizRepository quizzes, ISessionRepository sessions, IClock clock)
    : ICommandHandler<UpdateQuizCommand, Result<QuizDto>>
{
    public async Task<Result<QuizDto>> ExecuteAsync(UpdateQuizCommand command, CancellationToken ct)
    {
        var quiz = await quizzes.GetByIdAsync(command.QuizId, ct);

        if (quiz is null)
        {
            return Result<QuizDto>.Failure(QuizErrors.NotFound);
        }

        if (await sessions.HasActiveSessionForQuizAsync(command.QuizId, ct))
        {
            return Result<QuizDto>.Failure(QuizErrors.InUseByActiveSession);
        }

        var questions = QuizMapper.BuildQuestions(command.Questions);

        if (!questions.IsSuccess)
        {
            return Result<QuizDto>.Failure(questions.Error!);
        }

        // O agregado só muda de estado se o cabeçalho também for válido — nada parcial é persistido.
        var updated = quiz.Update(command.Title, command.Description, questions.Value, clock.UtcNow);

        if (!updated.IsSuccess)
        {
            return Result<QuizDto>.Failure(updated.Error!);
        }

        await quizzes.ReplaceAsync(quiz, ct);

        return QuizMapper.ToDto(quiz);
    }
}
