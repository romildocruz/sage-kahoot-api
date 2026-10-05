using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.Application.Abstractions;

/// <summary>Critérios de listagem de sessões para o relatório.</summary>
public sealed record ListSessionsFilter(
    int Page,
    int PageSize,
    DateTime? FromUtc,
    DateTime? ToUtc,
    SessionStatus? Status);

/// <summary>Projeção enxuta para a listagem de sessões anteriores.</summary>
public sealed record SessionListItem(
    Guid Id,
    string Pin,
    Guid QuizId,
    string QuizTitle,
    SessionStatus Status,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? EndedAtUtc,
    int ParticipantCount,
    int TopScore);

/// <summary>Contagem de respostas por opção, para as estatísticas por pergunta.</summary>
public sealed record OptionAnswerCount(Guid OptionId, int Count);

/// <summary>
/// Porta de persistência da sessão e das respostas. Modela explicitamente as duas etapas de D5:
/// a inserção da resposta (fonte de verdade, protegida por índice único) e a atualização
/// da pontuação acumulada (cache reconstruível).
/// </summary>
public interface ISessionRepository
{
    Task<Session?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Busca por PIN restrita a sessões ativas — PIN de sessão encerrada não resolve.</summary>
    Task<Session?> GetActiveByPinAsync(string pin, CancellationToken ct);

    Task InsertAsync(Session session, CancellationToken ct);

    Task ReplaceAsync(Session session, CancellationToken ct);

    /// <summary>
    /// Atualiza o estado da sessão apenas se o índice da pergunta corrente ainda for o esperado.
    /// Garante que somente um caminho de execução feche a pergunta e emita o evento (D5).
    /// </summary>
    Task<bool> ReplaceIfCurrentQuestionAsync(Session session, int expectedQuestionIndex, CancellationToken ct);

    /// <summary>
    /// Marca a pergunta corrente como fechada, mas apenas se ainda não estiver.
    /// Retorna verdadeiro somente para quem venceu a corrida — é esse retorno que autoriza
    /// emitir os eventos de fechamento uma única vez.
    /// </summary>
    Task<bool> TryCloseCurrentQuestionAsync(
        Guid sessionId,
        int questionIndex,
        DateTime closedAtUtc,
        CancellationToken ct);

    /// <summary>Verdadeiro se o quiz está vinculado a alguma sessão em Aguardando ou EmAndamento.</summary>
    Task<bool> HasActiveSessionForQuizAsync(Guid quizId, CancellationToken ct);

    Task<bool> PinInUseAsync(string pin, CancellationToken ct);

    /// <summary>
    /// Insere a resposta. Retorna falso quando o índice único rejeita a duplicidade,
    /// ou seja, quando o participante já respondeu aquela pergunta.
    /// </summary>
    Task<bool> TryInsertAnswerAsync(AnswerRecord answer, CancellationToken ct);

    /// <summary>Incremento atômico da pontuação acumulada do participante.</summary>
    Task IncrementParticipantScoreAsync(
        Guid sessionId,
        Guid participantId,
        int points,
        long responseTimeMs,
        CancellationToken ct);

    /// <summary>
    /// Atualiza a presença de um participante sem reescrever o documento inteiro da sessão.
    /// Conexão e desconexão são concorrentes com o registro de respostas, então a escrita precisa
    /// ser posicional — um ReplaceOne sobrescreveria pontuações gravadas no intervalo.
    /// </summary>
    Task SetParticipantConnectedAsync(
        Guid sessionId,
        Guid participantId,
        bool isConnected,
        DateTime nowUtc,
        CancellationToken ct);

    Task<int> CountAnswersForQuestionAsync(Guid sessionId, Guid questionId, CancellationToken ct);

    Task<IReadOnlyList<AnswerRecord>> GetAnswersAsync(Guid sessionId, CancellationToken ct);

    Task<IReadOnlyList<AnswerRecord>> GetAnswersForQuestionAsync(
        Guid sessionId,
        Guid questionId,
        CancellationToken ct);

    Task<IReadOnlyList<OptionAnswerCount>> CountAnswersByOptionAsync(
        Guid sessionId,
        Guid questionId,
        CancellationToken ct);

    Task<PagedResult<SessionListItem>> ListAsync(ListSessionsFilter filter, CancellationToken ct);

    /// <summary>
    /// Sessões em andamento cuja pergunta corrente já passou do instante de fechamento e
    /// ainda não foi fechada. Alimenta o fechamento por expiração de tempo.
    /// </summary>
    Task<IReadOnlyList<Session>> GetWithExpiredQuestionAsync(DateTime nowUtc, CancellationToken ct);

    /// <summary>Sessões ativas sem atividade desde o limite informado, para o varredor de sessões abandonadas.</summary>
    Task<IReadOnlyList<Session>> GetAbandonedAsync(DateTime inactiveSinceUtc, CancellationToken ct);
}
