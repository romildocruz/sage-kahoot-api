using Sage.Kahoot.Api.Hubs;
using Sage.Kahoot.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace Sage.Kahoot.Api.Realtime;

/// <summary>
/// Implementação da porta <see cref="ISessionNotifier"/> sobre SignalR.
/// Vive na camada Api, e não em Infrastructure, porque <c>IHubContext&lt;QuizHub&gt;</c> nomeia
/// o próprio hub — colocá-la em Infrastructure inverteria a direção de dependência de D1.
/// </summary>
public sealed class SignalRSessionNotifier(IHubContext<QuizHub, IQuizClient> hub) : ISessionNotifier
{
    public Task ParticipantJoinedAsync(ParticipantPresencePayload payload, CancellationToken ct) =>
        hub.Clients.Group(HubGroups.Session(payload.SessionId)).ParticipantJoined(payload);

    public Task ParticipantLeftAsync(ParticipantPresencePayload payload, CancellationToken ct) =>
        hub.Clients.Group(HubGroups.Session(payload.SessionId)).ParticipantLeft(payload);

    public Task SessionStartedAsync(SessionStartedPayload payload, CancellationToken ct) =>
        hub.Clients.Group(HubGroups.Session(payload.SessionId)).SessionStarted(payload);

    /// <summary>Vai para todos: o payload já não carrega qual opção é a correta.</summary>
    public Task QuestionOpenedAsync(QuestionOpenedPayload payload, CancellationToken ct) =>
        hub.Clients.Group(HubGroups.Session(payload.SessionId)).QuestionOpened(payload);

    public Task AnswerCountChangedToHostAsync(AnswerCountPayload payload, CancellationToken ct) =>
        hub.Clients.Group(HubGroups.Host(payload.SessionId)).AnswerCountChanged(payload);

    public Task AnswerAcceptedAsync(Guid participantId, AnswerAcceptedPayload payload, CancellationToken ct) =>
        hub.Clients.Group(HubGroups.Participant(participantId)).AnswerAccepted(payload);

    /// <summary>Somente o apresentador: é aqui que a opção correta é revelada.</summary>
    public Task QuestionClosedToHostAsync(QuestionClosedHostPayload payload, CancellationToken ct) =>
        hub.Clients.Group(HubGroups.Host(payload.SessionId)).QuestionClosed(payload);

    public Task QuestionResultAsync(Guid participantId, QuestionResultPayload payload, CancellationToken ct) =>
        hub.Clients.Group(HubGroups.Participant(participantId)).QuestionResult(payload);

    public Task RankingUpdatedAsync(RankingPayload payload, CancellationToken ct) =>
        hub.Clients.Group(HubGroups.Session(payload.SessionId)).RankingUpdated(payload);

    public Task SessionEndedToHostAsync(RankingPayload finalRanking, CancellationToken ct) =>
        hub.Clients.Group(HubGroups.Host(finalRanking.SessionId)).SessionEnded(finalRanking);

    public Task SessionEndedAsync(Guid participantId, SessionEndedParticipantPayload payload, CancellationToken ct) =>
        hub.Clients.Group(HubGroups.Participant(participantId)).ParticipantSessionEnded(payload);
}
