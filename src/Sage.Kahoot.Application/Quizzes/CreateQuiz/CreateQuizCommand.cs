using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Common;
using Sage.Kahoot.Domain.Quizzes;

namespace Sage.Kahoot.Application.Quizzes.CreateQuiz;

public sealed record CreateQuizCommand(
    string Title,
    string? Description,
    IReadOnlyList<QuestionInput> Questions) : ICommand<Result<QuizDto>>;

public sealed class CreateQuizHandler(IQuizRepository quizzes, IClock clock)
    : ICommandHandler<CreateQuizCommand, Result<QuizDto>>
{
    public async Task<Result<QuizDto>> ExecuteAsync(CreateQuizCommand command, CancellationToken ct)
    {
        var questions = QuizMapper.BuildQuestions(command.Questions);

        if (!questions.IsSuccess)
        {
            return Result<QuizDto>.Failure(questions.Error!);
        }

        var quiz = Quiz.Create(command.Title, command.Description, questions.Value, clock.UtcNow);

        if (!quiz.IsSuccess)
        {
            return Result<QuizDto>.Failure(quiz.Error!);
        }

        await quizzes.InsertAsync(quiz.Value, ct);

        return QuizMapper.ToDto(quiz.Value);
    }
}
