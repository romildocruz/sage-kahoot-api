namespace Sage.Kahoot.Domain.Sessions;

/// <summary>O que aconteceu ao avançar: abriu a próxima pergunta ou encerrou a sessão.</summary>
public sealed record AdvanceOutcome(SessionQuestion? ClosedQuestion, SessionQuestion? OpenedQuestion, bool SessionEnded);
