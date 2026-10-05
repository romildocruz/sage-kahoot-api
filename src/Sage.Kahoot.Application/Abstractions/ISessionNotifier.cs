namespace Sage.Kahoot.Application.Abstractions;

/// <summary>
/// Porta de tempo real. Os handlers emitem eventos por aqui e não conhecem SignalR —
/// a implementação vive na camada Api, junto do hub (ver D7).
/// Os métodos com sufixo ToHost alcançam apenas a conexão do apresentador.
/// </summary>
public interface ISessionNotifier
{
    Task ParticipantJoinedAsync(ParticipantPresencePayload payload, CancellationToken ct);

    Task ParticipantLeftAsync(ParticipantPresencePayload payload, CancellationToken ct);

    Task SessionStartedAsync(SessionStartedPayload payload, CancellationToken ct);

    Task QuestionOpenedAsync(QuestionOpenedPayload payload, CancellationToken ct);

    Task AnswerCountChangedToHostAsync(AnswerCountPayload payload, CancellationToken ct);

    Task AnswerAcceptedAsync(Guid participantId, AnswerAcceptedPayload payload, CancellationToken ct);

    Task QuestionClosedToHostAsync(QuestionClosedHostPayload payload, CancellationToken ct);

    Task QuestionResultAsync(Guid participantId, QuestionResultPayload payload, CancellationToken ct);

    Task RankingUpdatedAsync(RankingPayload payload, CancellationToken ct);

    Task SessionEndedToHostAsync(RankingPayload finalRanking, CancellationToken ct);

    Task SessionEndedAsync(Guid participantId, SessionEndedParticipantPayload payload, CancellationToken ct);
}
