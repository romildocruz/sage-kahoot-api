using Sage.Kahoot.Domain.Common;

namespace Sage.Kahoot.Domain.Quizzes;

/// <summary>
/// Agregado de conteúdo do treinamento. Pode ser criado sem perguntas e recebê-las depois.
/// A ordem das perguntas é normalizada pelo próprio agregado, para ser explícita e estável.
/// </summary>
public sealed class Quiz
{
    private readonly List<Question> _questions;

    private Quiz(
        Guid id,
        string title,
        string? description,
        List<Question> questions,
        DateTime createdAtUtc,
        DateTime updatedAtUtc,
        bool isSeed)
    {
        Id = id;
        Title = title;
        Description = description;
        _questions = questions;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        IsSeed = isSeed;
    }

    public Guid Id { get; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public IReadOnlyList<Question> Questions => _questions;

    public DateTime CreatedAtUtc { get; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Marca o quiz de exemplo sintético, para tornar a carga inicial idempotente.</summary>
    public bool IsSeed { get; }

    public static Result<Quiz> Create(
        string title,
        string? description,
        IReadOnlyList<Question> questions,
        DateTime nowUtc,
        bool isSeed = false,
        Guid? id = null)
    {
        var failure = ValidateHeader(title, description);

        if (failure is not null)
        {
            return Result<Quiz>.Failure(failure);
        }

        var quiz = new Quiz(
            id ?? Guid.NewGuid(),
            title.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            [.. questions],
            nowUtc,
            nowUtc,
            isSeed);

        quiz.NormalizeOrder();
        return quiz;
    }

    /// <summary>Revalida as mesmas regras da criação. Só muda o estado se tudo for válido (all-or-nothing).</summary>
    public Result<Unit> Update(string title, string? description, IReadOnlyList<Question> questions, DateTime nowUtc)
    {
        var failure = ValidateHeader(title, description);

        if (failure is not null)
        {
            return Result<Unit>.Failure(failure);
        }

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        _questions.Clear();
        _questions.AddRange(questions);
        NormalizeOrder();
        UpdatedAtUtc = nowUtc;

        return Result.Success();
    }

    /// <summary>Cópia independente: novo identificador em quiz, perguntas e opções.</summary>
    public Quiz Duplicate(string newTitle, DateTime nowUtc) =>
        new(Guid.NewGuid(),
            newTitle.Trim(),
            Description,
            [.. _questions.Select(static q => q.CloneWithNewIds())],
            nowUtc,
            nowUtc,
            isSeed: false);

    /// <summary>Reconstrução a partir do armazenamento. Não revalida: o que está persistido já foi validado.</summary>
    public static Quiz Rehydrate(
        Guid id,
        string title,
        string? description,
        IReadOnlyList<Question> questions,
        DateTime createdAtUtc,
        DateTime updatedAtUtc,
        bool isSeed) =>
        new(id, title, description, [.. questions], createdAtUtc, updatedAtUtc, isSeed);

    private static DomainError? ValidateHeader(string? title, string? description)
    {
        var trimmedTitle = title?.Trim() ?? string.Empty;

        return Guard.FirstFailure(
            Guard.AgainstNullOrWhiteSpace(trimmedTitle, "quiz.title_required", "Título"),
            trimmedTitle.Length > QuizRules.TitleMaxLength
                ? DomainError.Validation(
                    "quiz.title_too_long",
                    $"Título deve ter entre {QuizRules.TitleMinLength} e {QuizRules.TitleMaxLength} caracteres.")
                : null,
            description is { Length: > QuizRules.DescriptionMaxLength }
                ? DomainError.Validation(
                    "quiz.description_too_long",
                    $"Descrição deve ter no máximo {QuizRules.DescriptionMaxLength} caracteres.")
                : null);
    }

    private void NormalizeOrder()
    {
        for (var i = 0; i < _questions.Count; i++)
        {
            _questions[i].Reorder(i);
        }
    }
}
