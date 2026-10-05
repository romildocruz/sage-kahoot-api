using Sage.Kahoot.Application.Abstractions;

namespace Sage.Kahoot.Api.Hubs;

/// <summary>
/// Contrato tipado que o cliente implementa. Define os nomes de método que o
/// sage-kahoot-web precisa registrar — é a referência do contrato de tempo real.
/// </summary>
public interface IQuizClient
{
    Task ParticipantJoined(ParticipantPresencePayload payload);

    Task ParticipantLeft(ParticipantPresencePayload payload);

    Task SessionStarted(SessionStartedPayload payload);

    Task QuestionOpened(QuestionOpenedPayload payload);

    Task AnswerCountChanged(AnswerCountPayload payload);

    Task AnswerAccepted(AnswerAcceptedPayload payload);

    Task QuestionClosed(QuestionClosedHostPayload payload);

    Task QuestionResult(QuestionResultPayload payload);

    Task RankingUpdated(RankingPayload payload);

    Task SessionEnded(RankingPayload payload);

    Task ParticipantSessionEnded(SessionEndedParticipantPayload payload);
}

/// <summary>Nomes dos grupos SignalR, derivados sempre do token e nunca de parâmetro do cliente.</summary>
public static class HubGroups
{
    public static string Session(Guid sessionId) => $"session:{sessionId}";

    /// <summary>
    /// Grupo exclusivo do apresentador. É o que separa os eventos que revelam a resposta correta
    /// dos que vão para os participantes.
    /// </summary>
    public static string Host(Guid sessionId) => $"session:{sessionId}:host";

    public static string Participant(Guid participantId) => $"participant:{participantId}";
}
