using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Common;
using Sage.Kahoot.Domain.Quizzes;

namespace Sage.Kahoot.Application.Quizzes.DuplicateQuiz;

public sealed record DuplicateQuizCommand(Guid QuizId) : ICommand<Result<QuizDto>>;

public sealed class DuplicateQuizHandler(IQuizRepository quizzes, IClock clock)
    : ICommandHandler<DuplicateQuizCommand, Result<QuizDto>>
{
    private const string CopySuffix = " (cópia)";

    public async Task<Result<QuizDto>> ExecuteAsync(DuplicateQuizCommand command, CancellationToken ct)
    {
        var original = await quizzes.GetByIdAsync(command.QuizId, ct);

        if (original is null)
        {
            return Result<QuizDto>.Failure(QuizErrors.NotFound);
        }

        // A cópia recebe novos identificadores em quiz, perguntas e opções: editar uma não afeta a outra.
        var copy = original.Duplicate(BuildCopyTitle(original.Title), clock.UtcNow);

        await quizzes.InsertAsync(copy, ct);

        return QuizMapper.ToDto(copy);
    }

    /// <summary>Mantém o título dentro do limite mesmo quando o original já usa todo o espaço.</summary>
    private static string BuildCopyTitle(string originalTitle)
    {
        var candidate = originalTitle + CopySuffix;

        return candidate.Length <= QuizRules.TitleMaxLength
            ? candidate
            : string.Concat(
                originalTitle.AsSpan(0, QuizRules.TitleMaxLength - CopySuffix.Length),
                CopySuffix);
    }
}
