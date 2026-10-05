using Sage.Kahoot.Domain.Quizzes;

namespace Sage.Kahoot.Application.Abstractions;

/// <summary>Critérios de listagem de quizzes.</summary>
public sealed record ListQuizzesFilter(int Page, int PageSize, string? TitleContains);

/// <summary>Projeção enxuta para listagem — evita trazer todas as perguntas na página.</summary>
public sealed record QuizListItem(
    Guid Id,
    string Title,
    string? Description,
    int QuestionCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>Porta de persistência do agregado Quiz.</summary>
public interface IQuizRepository
{
    Task<Quiz?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<PagedResult<QuizListItem>> ListAsync(ListQuizzesFilter filter, CancellationToken ct);

    Task InsertAsync(Quiz quiz, CancellationToken ct);

    Task ReplaceAsync(Quiz quiz, CancellationToken ct);

    Task<bool> DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>Suporta a idempotência da carga inicial sintética.</summary>
    Task<bool> SeedQuizExistsAsync(CancellationToken ct);
}
