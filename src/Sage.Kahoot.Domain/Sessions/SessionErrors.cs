using Sage.Kahoot.Domain.Common;

namespace Sage.Kahoot.Domain.Sessions;

/// <summary>Erros de domínio da sessão, em um lugar só para manter códigos e mensagens consistentes.</summary>
public static class SessionErrors
{
    public static DomainError InvalidTransition(SessionStatus from, string action) =>
        DomainError.Conflict("session.invalid_transition", $"Não é possível {action} uma sessão com status {from}.");

    public static readonly DomainError QuizWithoutQuestions =
        DomainError.Validation("session.quiz_without_questions", "O quiz precisa ter ao menos uma pergunta para abrir uma sessão.");

    public static readonly DomainError NicknameTaken =
        DomainError.Conflict("session.nickname_taken", "Este apelido já está em uso nesta sessão.");

    public static readonly DomainError SessionFull =
        DomainError.Conflict("session.full", "A sessão atingiu o número máximo de participantes.");

    /// <summary>Mensagem deliberadamente genérica: não confirma se existem sessões ativas.</summary>
    public static readonly DomainError PinNotFound =
        DomainError.NotFound("session.pin_not_found", "PIN inválido ou sessão indisponível.");

    public static readonly DomainError NotFound =
        DomainError.NotFound("session.not_found", "Sessão não encontrada.");

    public static readonly DomainError NoQuestionOpen =
        DomainError.Conflict("session.no_question_open", "Não há pergunta aberta nesta sessão.");

    public static readonly DomainError NotCurrentQuestion =
        DomainError.Conflict("session.not_current_question", "A pergunta informada não é a pergunta aberta no momento.");

    public static readonly DomainError AnswerWindowClosed =
        DomainError.Conflict("session.answer_window_closed", "A janela de resposta desta pergunta já foi encerrada.");

    public static readonly DomainError AlreadyAnswered =
        DomainError.Conflict("session.already_answered", "Este participante já respondeu a esta pergunta.");

    public static readonly DomainError UnknownOption =
        DomainError.Validation("session.unknown_option", "A opção informada não pertence à pergunta atual.");

    public static readonly DomainError ParticipantNotInSession =
        DomainError.NotFound("session.participant_not_found", "Participante não pertence a esta sessão.");
}
