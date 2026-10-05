using Sage.Kahoot.Domain.Common;

namespace Sage.Kahoot.Domain.Quizzes;

/// <summary>Opção de resposta de uma pergunta de múltipla escolha.</summary>
public sealed class AnswerOption
{
    private AnswerOption(Guid id, string text, bool isCorrect)
    {
        Id = id;
        Text = text;
        IsCorrect = isCorrect;
    }

    public Guid Id { get; }

    public string Text { get; private set; }

    public bool IsCorrect { get; private set; }

    public static Result<AnswerOption> Create(string text, bool isCorrect, Guid? id = null)
    {
        var trimmed = text?.Trim() ?? string.Empty;

        var failure = Guard.FirstFailure(
            Guard.AgainstNullOrWhiteSpace(trimmed, "quiz.option.text_required", "Texto da opção"),
            trimmed.Length > QuizRules.OptionTextMaxLength
                ? DomainError.Validation(
                    "quiz.option.text_too_long",
                    $"Texto da opção deve ter no máximo {QuizRules.OptionTextMaxLength} caracteres.")
                : null);

        return failure is not null
            ? Result<AnswerOption>.Failure(failure)
            : new AnswerOption(id ?? Guid.NewGuid(), trimmed, isCorrect);
    }

    /// <summary>Cópia independente, com novo identificador — usada na duplicação de quiz.</summary>
    public AnswerOption CloneWithNewId() => new(Guid.NewGuid(), Text, IsCorrect);
}
