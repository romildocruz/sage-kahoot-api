namespace Sage.Kahoot.Domain.Sessions;

/// <summary>Opção de resposta congelada no momento da abertura da sessão.</summary>
public sealed record SessionAnswerOption(Guid Id, string Text, bool IsCorrect);

/// <summary>Pergunta congelada no momento da abertura da sessão.</summary>
public sealed record SessionQuestion(
    Guid Id,
    int Order,
    string Text,
    int TimeLimitSeconds,
    int BasePoints,
    IReadOnlyList<SessionAnswerOption> Options)
{
    public Guid CorrectOptionId => Options.Single(static o => o.IsCorrect).Id;

    public bool IsCorrect(Guid optionId) => Options.Any(o => o.Id == optionId && o.IsCorrect);

    public bool HasOption(Guid optionId) => Options.Any(o => o.Id == optionId);
}

/// <summary>
/// Cópia imutável do quiz no instante da abertura da sessão. Garante que editar ou excluir o quiz
/// não altere a sessão em curso nem o relatório posterior.
/// </summary>
public sealed record QuizSnapshot(Guid QuizId, string Title, IReadOnlyList<SessionQuestion> Questions);
