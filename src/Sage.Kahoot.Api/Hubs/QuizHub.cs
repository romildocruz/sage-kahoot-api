using System.Collections.Concurrent;
using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Application.Sessions;
using Sage.Kahoot.Application.Sessions.AdvanceQuestion;
using Sage.Kahoot.Application.Sessions.EndSession;
using Sage.Kahoot.Application.Sessions.GetPartialRanking;
using Sage.Kahoot.Application.Sessions.GetSessionState;
using Sage.Kahoot.Application.Sessions.ParticipantPresence;
using Sage.Kahoot.Application.Sessions.StartSession;
using Sage.Kahoot.Application.Sessions.SubmitAnswer;
using Sage.Kahoot.Domain.Common;
using Sage.Kahoot.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Sage.Kahoot.Api.Hubs;

/// <summary>
/// Adaptador de transporte em tempo real. Assim como os endpoints HTTP, cada método aqui
/// apenas autentica, monta o comando e despacha pelo command bus — nenhuma regra de negócio
/// vive neste arquivo. É o mesmo handler que atende a rota REST equivalente.
/// </summary>
[Authorize]
public sealed class QuizHub(ParticipantConnectionTracker connections, ILogger<QuizHub> logger) : Hub<IQuizClient>
{
    /// <summary>Teto de invocações por conexão na janela, para conter cliente abusivo.</summary>
    private const int MaxInvocationsPerWindow = 60;

    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    private static readonly ConcurrentDictionary<string, InvocationCounter> Counters = new();


    public override async Task OnConnectedAsync()
    {
        // Participante: a sessão vem do token e a conexão é amarrada a ela.
        if (Context.User?.SessionId() is { } sessionId && Context.User.ParticipantId() is { } participantId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Session(sessionId));
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Participant(participantId));

            connections.Acquire(participantId);

            // CancellationToken.None de propósito: se a conexão cair no meio, a escrita de presença
            // ainda precisa concluir — cancelá-la deixaria o participante marcado incorretamente.
            await new ParticipantConnectedCommand(sessionId, participantId).ExecuteAsync(CancellationToken.None);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Counters.TryRemove(Context.ConnectionId, out _);

        if (Context.User?.SessionId() is { } sessionId && Context.User.ParticipantId() is { } participantId)
        {
            // Só anuncia saída quando esta era a última conexão do participante.
            if (connections.Release(participantId))
            {
                await new ParticipantDisconnectedCommand(sessionId, participantId).ExecuteAsync(CancellationToken.None);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }


    /// <summary>
    /// Ingresso do apresentador em uma sessão. O token de admin não é vinculado a uma sessão
    /// específica — ele autoriza conduzir qualquer uma —, então o identificador vem por parâmetro.
    /// Já o participante tem a sessão gravada no token: se pedir outra, é recusado.
    /// </summary>
    public async Task JoinSessionGroup(Guid sessionId)
    {
        EnsureWithinRateLimit();

        if (Context.User?.IsAdmin() == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Session(sessionId));
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Host(sessionId));
            return;
        }

        if (Context.User?.SessionId() != sessionId)
        {
            throw new HubException("Conexão não pertence a esta sessão.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Session(sessionId));
    }

    public Task<SessionStatePayload> SyncState(Guid sessionId) =>
        DispatchAsync(new GetSessionStateQuery(sessionId, Context.User?.ParticipantId()));

    /// <summary>
    /// Ranking corrente sob demanda. Mesma regra de autorização do endpoint REST equivalente:
    /// admin em qualquer sessão, participante apenas na do seu token.
    /// </summary>
    public Task<RankingPayload> GetRanking(Guid sessionId)
    {
        if (Context.User?.ParticipantId() is not null && Context.User.SessionId() != sessionId)
        {
            throw new HubException("Conexão não pertence a esta sessão.");
        }

        return DispatchAsync(new GetPartialRankingQuery(sessionId));
    }

    public Task<SessionDto> StartSession(Guid sessionId)
    {
        EnsureAdmin();
        return DispatchAsync(new StartSessionCommand(sessionId));
    }

    public Task<SessionDto> AdvanceQuestion(Guid sessionId)
    {
        EnsureAdmin();
        return DispatchAsync(new AdvanceQuestionCommand(sessionId));
    }

    public Task<SessionDto> EndSession(Guid sessionId)
    {
        EnsureAdmin();
        return DispatchAsync(new EndSessionCommand(sessionId));
    }

    public Task SubmitAnswer(Guid sessionId, Guid questionId, Guid optionId)
    {
        var participantId = Context.User?.ParticipantId()
            ?? throw new HubException("Somente participantes podem responder.");

        if (Context.User?.SessionId() != sessionId)
        {
            throw new HubException("Conexão não pertence a esta sessão.");
        }

        return DispatchAsync(new SubmitAnswerCommand(sessionId, participantId, questionId, optionId));
    }

    /// <summary>
    /// Despacha pelo command bus e converte falha previsível de regra em <see cref="HubException"/>,
    /// que é como o SignalR reporta erro ao cliente.
    /// </summary>
    private async Task<T> DispatchAsync<T>(ICommand<Result<T>> command)
    {
        EnsureWithinRateLimit();

        var result = await command.ExecuteAsync(Context.ConnectionAborted);

        if (!result.IsSuccess)
        {
            logger.LogInformation(
                "Invocação do hub recusada: {ErrorCode} ({Message})",
                result.Error!.Code,
                result.Error.Message);

            throw new HubException(result.Error.Message);
        }

        return result.Value;
    }

    private void EnsureAdmin()
    {
        if (Context.User?.IsAdmin() != true)
        {
            throw new HubException("Ação restrita ao apresentador.");
        }
    }

    /// <summary>
    /// Rejeita o excedente da própria conexão sem afetar as demais: a sessão dos outros
    /// participantes continua normalmente.
    /// </summary>
    private void EnsureWithinRateLimit()
    {
        var counter = Counters.GetOrAdd(Context.ConnectionId, static _ => new InvocationCounter());

        if (!counter.TryRegister(MaxInvocationsPerWindow, Window))
        {
            throw new HubException("Muitas invocações. Aguarde alguns instantes.");
        }
    }

    private sealed class InvocationCounter
    {
        private readonly Lock _gate = new();
        private DateTime _windowStartUtc = DateTime.UtcNow;
        private int _count;

        public bool TryRegister(int limit, TimeSpan window)
        {
            lock (_gate)
            {
                var now = DateTime.UtcNow;

                if (now - _windowStartUtc > window)
                {
                    _windowStartUtc = now;
                    _count = 0;
                }

                return ++_count <= limit;
            }
        }
    }
}
