using Sage.Kahoot.Domain.Common;

namespace Sage.Kahoot.Domain.Quizzes;

/// <summary>
/// Pergunta de múltipla escolha: 2 a 4 opções distintas, exatamente uma correta,
/// tempo limite e pontuação base próprios.
/// </summary>
public sealed class Question
{
    private readonly List<AnswerOption> _options;

    private Question(Guid id, int order, string text, int timeLimitSeconds, int basePoints, List<AnswerOption> options)
    {
        Id = id;
        Order = order;
        Text = text;
        TimeLimitSeconds = timeLimitSeconds;
        BasePoints = basePoints;
        _options = options;
    }

    public Guid Id { get; }

    /// <summary>Posição explícita e estável dentro do quiz (base 0).</summary>
    public int Order { get; private set; }

    public string Text { get; private set; }

    public int TimeLimitSeconds { get; private set; }

    public int BasePoints { get; private set; }

    public IReadOnlyList<AnswerOption> Options => _options;

    public AnswerOption CorrectOption => _options.Single(static o => o.IsCorrect);

    public static Result<Question> Create(
        string text,
        int order,
        int timeLimitSeconds,
        int basePoints,
        IReadOnlyList<AnswerOption> options,
        Guid? id = null)
    {
        var trimmed = text?.Trim() ?? string.Empty;

        var failure = Guard.FirstFailure(
            Guard.AgainstNullOrWhiteSpace(trimmed, "quiz.question.text_required", "Enunciado"),
            trimmed.Length > QuizRules.QuestionTextMaxLength
                ? DomainError.Validation(
                    "quiz.question.text_too_long",
                    $"Enunciado deve ter entre {QuizRules.QuestionTextMinLength} e {QuizRules.QuestionTextMaxLength} caracteres.")
                : null,
            Guard.AgainstOutOfRange(
                timeLimitSeconds,
                QuizRules.MinTimeLimitSeconds,
                QuizRules.MaxTimeLimitSeconds,
                "quiz.question.time_limit_out_of_range",
                "Tempo limite (segundos)"),
            Guard.AgainstOutOfRange(
                basePoints,
                QuizRules.MinBasePoints,
                QuizRules.MaxBasePoints,
                "quiz.question.base_points_out_of_range",
                "Pontuação base"),
            Guard.AgainstCountOutOfRange(
                options,
                QuizRules.MinOptionsPerQuestion,
                QuizRules.MaxOptionsPerQuestion,
                "quiz.question.option_count_out_of_range",
                "Opções de resposta"),
            ValidateExactlyOneCorrect(options),
            ValidateDistinctTexts(options));

        return failure is not null
            ? Result<Question>.Failure(failure)
            : new Question(id ?? Guid.NewGuid(), order, trimmed, timeLimitSeconds, basePoints, [.. options]);
    }

    private static DomainError? ValidateExactlyOneCorrect(IReadOnlyList<AnswerOption> options)
    {
        var correctCount = options.Count(static o => o.IsCorrect);

        return correctCount == 1
            ? null
            : DomainError.Validation(
                "quiz.question.correct_option_count",
                $"A pergunta deve ter exatamente uma opção correta; foram informadas {correctCount}.");
    }

    private static DomainError? ValidateDistinctTexts(IReadOnlyList<AnswerOption> options)
    {
        var distinct = options
            .Select(static o => o.Text)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return distinct == options.Count
            ? null
            : DomainError.Validation(
                "quiz.question.duplicate_option_text",
                "As opções de resposta devem ser distintas entre si dentro da mesma pergunta.");
    }

    internal void Reorder(int order) => Order = order;

    /// <summary>Cópia independente, com novos identificadores — usada na duplicação de quiz.</summary>
    public Question CloneWithNewIds() =>
        new(Guid.NewGuid(), Order, Text, TimeLimitSeconds, BasePoints,
            [.. _options.Select(static o => o.CloneWithNewId())]);
}
