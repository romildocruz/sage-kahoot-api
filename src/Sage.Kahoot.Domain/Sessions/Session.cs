using Sage.Kahoot.Domain.Common;

namespace Sage.Kahoot.Domain.Sessions;

/// <summary>
/// Agregado da sessão ao vivo. Detém o estado autoritativo: snapshot do quiz, participantes,
/// pergunta corrente e sua janela de resposta. Todo prazo deriva de instantes UTC do servidor.
/// </summary>
public sealed class Session
{
    private readonly List<Participant> _participants;

    private Session(
        Guid id,
        string pin,
        QuizSnapshot quiz,
        SessionStatus status,
        int currentQuestionIndex,
        DateTime? currentQuestionOpenedAtUtc,
        DateTime? currentQuestionClosesAtUtc,
        bool currentQuestionClosed,
        List<Participant> participants,
        DateTime createdAtUtc,
        DateTime? startedAtUtc,
        DateTime? endedAtUtc,
        DateTime lastActivityAtUtc)
    {
        Id = id;
        Pin = pin;
        Quiz = quiz;
        Status = status;
        CurrentQuestionIndex = currentQuestionIndex;
        CurrentQuestionOpenedAtUtc = currentQuestionOpenedAtUtc;
        CurrentQuestionClosesAtUtc = currentQuestionClosesAtUtc;
        CurrentQuestionClosed = currentQuestionClosed;
        _participants = participants;
        CreatedAtUtc = createdAtUtc;
        StartedAtUtc = startedAtUtc;
        EndedAtUtc = endedAtUtc;
        LastActivityAtUtc = lastActivityAtUtc;
    }

    public Guid Id { get; }

    public string Pin { get; }

    public QuizSnapshot Quiz { get; }

    public SessionStatus Status { get; private set; }

    /// <summary>-1 enquanto nenhuma pergunta foi aberta.</summary>
    public int CurrentQuestionIndex { get; private set; }

    public DateTime? CurrentQuestionOpenedAtUtc { get; private set; }

    public DateTime? CurrentQuestionClosesAtUtc { get; private set; }

    /// <summary>
    /// Marca que a pergunta corrente já teve seu resultado revelado. É a trava que garante
    /// emissão única do fechamento entre os três caminhos possíveis: avanço do apresentador,
    /// expiração do tempo e "todos responderam".
    /// </summary>
    public bool CurrentQuestionClosed { get; private set; }

    public IReadOnlyList<Participant> Participants => _participants;

    public DateTime CreatedAtUtc { get; }

    public DateTime? StartedAtUtc { get; private set; }

    public DateTime? EndedAtUtc { get; private set; }

    public DateTime LastActivityAtUtc { get; private set; }

    public SessionQuestion? CurrentQuestion =>
        CurrentQuestionIndex >= 0 && CurrentQuestionIndex < Quiz.Questions.Count
            ? Quiz.Questions[CurrentQuestionIndex]
            : null;

    public int TotalQuestions => Quiz.Questions.Count;

    public IReadOnlyList<Participant> ActiveParticipants =>
        [.. _participants.Where(static p => p.IsConnected)];

    public static Result<Session> Open(QuizSnapshot quiz, string pin, DateTime nowUtc)
    {
        if (quiz.Questions.Count == 0)
        {
            return Result<Session>.Failure(SessionErrors.QuizWithoutQuestions);
        }

        return new Session(
            Guid.NewGuid(),
            pin,
            quiz,
            SessionStatus.Aguardando,
            currentQuestionIndex: -1,
            currentQuestionOpenedAtUtc: null,
            currentQuestionClosesAtUtc: null,
            currentQuestionClosed: false,
            participants: [],
            createdAtUtc: nowUtc,
            startedAtUtc: null,
            endedAtUtc: null,
            lastActivityAtUtc: nowUtc);
    }

    /// <summary>Entrada anônima. O apelido já deve chegar sanitizado pela camada de aplicação.</summary>
    public Result<Participant> Join(string nickname, DateTime nowUtc, int maxParticipants)
    {
        if (Status is not (SessionStatus.Aguardando or SessionStatus.EmAndamento))
        {
            return Result<Participant>.Failure(SessionErrors.InvalidTransition(Status, "entrar em"));
        }

        if (_participants.Count >= maxParticipants)
        {
            return Result<Participant>.Failure(SessionErrors.SessionFull);
        }

        if (_participants.Any(p => string.Equals(p.Nickname, nickname, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<Participant>.Failure(SessionErrors.NicknameTaken);
        }

        var participant = new Participant(Guid.NewGuid(), nickname, _participants.Count, nowUtc);
        _participants.Add(participant);
        Touch(nowUtc);

        return participant;
    }

    /// <summary>Aguardando para EmAndamento, abrindo a primeira pergunta.</summary>
    public Result<SessionQuestion> Start(DateTime nowUtc)
    {
        if (Status is not SessionStatus.Aguardando)
        {
            return Result<SessionQuestion>.Failure(SessionErrors.InvalidTransition(Status, "iniciar"));
        }

        Status = SessionStatus.EmAndamento;
        StartedAtUtc = nowUtc;
        OpenQuestion(0, nowUtc);
        Touch(nowUtc);

        return CurrentQuestion!;
    }

    /// <summary>
    /// Fecha a pergunta atual e abre a próxima; na última, encerra a sessão.
    /// Único caminho que altera <see cref="CurrentQuestionIndex"/> durante a sessão.
    /// </summary>
    public Result<AdvanceOutcome> Advance(DateTime nowUtc)
    {
        if (Status is not SessionStatus.EmAndamento)
        {
            return Result<AdvanceOutcome>.Failure(SessionErrors.InvalidTransition(Status, "avançar"));
        }

        var closed = CurrentQuestion;
        var nextIndex = CurrentQuestionIndex + 1;

        if (nextIndex >= Quiz.Questions.Count)
        {
            CloseCurrentQuestion(nowUtc);
            Status = SessionStatus.Encerrada;
            EndedAtUtc = nowUtc;
            Touch(nowUtc);
            return new AdvanceOutcome(closed, null, SessionEnded: true);
        }

        OpenQuestion(nextIndex, nowUtc);
        Touch(nowUtc);

        return new AdvanceOutcome(closed, CurrentQuestion, SessionEnded: false);
    }

    /// <summary>Encerramento manual pelo apresentador, antes da última pergunta.</summary>
    public Result<Unit> End(DateTime nowUtc)
    {
        if (Status is not SessionStatus.EmAndamento)
        {
            return Result<Unit>.Failure(SessionErrors.InvalidTransition(Status, "encerrar"));
        }

        CloseCurrentQuestion(nowUtc);
        Status = SessionStatus.Encerrada;
        EndedAtUtc = nowUtc;
        Touch(nowUtc);

        return Result.Success();
    }

    public Result<Unit> Cancel(DateTime nowUtc)
    {
        if (!Status.IsActive())
        {
            return Result<Unit>.Failure(SessionErrors.InvalidTransition(Status, "cancelar"));
        }

        CloseCurrentQuestion(nowUtc);
        Status = SessionStatus.Cancelada;
        EndedAtUtc = nowUtc;
        Touch(nowUtc);

        return Result.Success();
    }

    /// <summary>Fecha a pergunta corrente sem avançar — usado quando o tempo expira ou todos responderam.</summary>
    public Result<SessionQuestion> CloseCurrentQuestionOnly(DateTime nowUtc)
    {
        if (Status is not SessionStatus.EmAndamento || CurrentQuestion is null || CurrentQuestionClosed)
        {
            return Result<SessionQuestion>.Failure(SessionErrors.NoQuestionOpen);
        }

        var question = CurrentQuestion;
        CloseCurrentQuestion(nowUtc);
        Touch(nowUtc);

        return question;
    }

    /// <summary>
    /// Valida a resposta contra o estado autoritativo: sessão em andamento, pergunta correta,
    /// opção existente e janela aberta considerando a tolerância de rede.
    /// </summary>
    public Result<AnswerWindow> ValidateAnswer(Guid questionId, Guid optionId, DateTime nowUtc, int toleranceMs)
    {
        if (Status is not SessionStatus.EmAndamento)
        {
            return Result<AnswerWindow>.Failure(SessionErrors.InvalidTransition(Status, "responder em"));
        }

        var question = CurrentQuestion;

        if (question is null || CurrentQuestionOpenedAtUtc is null || CurrentQuestionClosesAtUtc is null)
        {
            return Result<AnswerWindow>.Failure(SessionErrors.NoQuestionOpen);
        }

        if (question.Id != questionId)
        {
            return Result<AnswerWindow>.Failure(SessionErrors.NotCurrentQuestion);
        }

        if (!question.HasOption(optionId))
        {
            return Result<AnswerWindow>.Failure(SessionErrors.UnknownOption);
        }

        if (CurrentQuestionClosed || nowUtc > CurrentQuestionClosesAtUtc.Value.AddMilliseconds(toleranceMs))
        {
            return Result<AnswerWindow>.Failure(SessionErrors.AnswerWindowClosed);
        }

        var responseTimeMs = (long)Math.Max(0d, (nowUtc - CurrentQuestionOpenedAtUtc.Value).TotalMilliseconds);

        return new AnswerWindow(question, responseTimeMs, question.IsCorrect(optionId));
    }

    /// <summary>
    /// Marca o participante como conectado. Um participante conectado conta como ativo e, portanto,
    /// entra no total exigido para o fechamento antecipado da pergunta.
    /// </summary>
    public Result<Participant> MarkParticipantConnected(Guid participantId, DateTime nowUtc) =>
        SetParticipantPresence(participantId, connected: true, nowUtc);

    /// <summary>
    /// Marca o participante como desconectado. A camada de aplicação só chama isto quando a última
    /// conexão dele cai — trocar de aba não tira ninguém da sessão.
    /// Pontuação, apelido e respostas já registradas permanecem intactos.
    /// </summary>
    public Result<Participant> MarkParticipantDisconnected(Guid participantId, DateTime nowUtc) =>
        SetParticipantPresence(participantId, connected: false, nowUtc);

    private Result<Participant> SetParticipantPresence(Guid participantId, bool connected, DateTime nowUtc)
    {
        var participant = FindParticipant(participantId);

        if (participant is null)
        {
            return Result<Participant>.Failure(SessionErrors.ParticipantNotInSession);
        }

        participant.SetConnected(connected);
        Touch(nowUtc);

        return participant;
    }

    /// <summary>
    /// Decide se a pergunta corrente deve ser fechada por "todos responderam".
    /// A comparação é contra os participantes <b>ativos</b>: quem perdeu a conexão não pode
    /// impedir o fechamento antecipado, ou a regra nunca dispararia em uma turma real.
    /// </summary>
    public bool ShouldCloseAfterAnswer(int answeredCount)
    {
        if (Status is not SessionStatus.EmAndamento || CurrentQuestion is null || CurrentQuestionClosed)
        {
            return false;
        }

        var activeCount = ActiveParticipants.Count;

        return activeCount > 0 && answeredCount >= activeCount;
    }

    public Participant? FindParticipant(Guid participantId) =>
        _participants.FirstOrDefault(p => p.Id == participantId);

    /// <summary>Ranking ordenado: pontuação desc, tempo total acumulado asc, ordem de entrada asc.</summary>
    public IReadOnlyList<Participant> Ranking() =>
        [.. _participants
            .OrderByDescending(static p => p.TotalScore)
            .ThenBy(static p => p.TotalResponseTimeMs)
            .ThenBy(static p => p.JoinOrder)];

    public void Touch(DateTime nowUtc) => LastActivityAtUtc = nowUtc;

    public double? RemainingSeconds(DateTime nowUtc) =>
        CurrentQuestionClosesAtUtc is null
            ? null
            : Math.Max(0d, (CurrentQuestionClosesAtUtc.Value - nowUtc).TotalSeconds);

    public static Session Rehydrate(
        Guid id,
        string pin,
        QuizSnapshot quiz,
        SessionStatus status,
        int currentQuestionIndex,
        DateTime? currentQuestionOpenedAtUtc,
        DateTime? currentQuestionClosesAtUtc,
        bool currentQuestionClosed,
        IReadOnlyList<Participant> participants,
        DateTime createdAtUtc,
        DateTime? startedAtUtc,
        DateTime? endedAtUtc,
        DateTime lastActivityAtUtc) =>
        new(id, pin, quiz, status, currentQuestionIndex, currentQuestionOpenedAtUtc, currentQuestionClosesAtUtc,
            currentQuestionClosed, [.. participants], createdAtUtc, startedAtUtc, endedAtUtc, lastActivityAtUtc);

    private void OpenQuestion(int index, DateTime nowUtc)
    {
        CurrentQuestionIndex = index;
        CurrentQuestionOpenedAtUtc = nowUtc;
        CurrentQuestionClosesAtUtc = nowUtc.AddSeconds(Quiz.Questions[index].TimeLimitSeconds);
        CurrentQuestionClosed = false;
    }

    private void CloseCurrentQuestion(DateTime nowUtc)
    {
        CurrentQuestionClosesAtUtc = nowUtc;
        CurrentQuestionClosed = true;
    }
}

/// <summary>Resultado da validação de uma resposta contra a janela aberta.</summary>
public sealed record AnswerWindow(SessionQuestion Question, long ResponseTimeMs, bool IsCorrect)
{
    public int Points => Scoring.Calculate(IsCorrect, ResponseTimeMs, Question.TimeLimitSeconds, Question.BasePoints);
}
