using MongoDB.Bson.Serialization.Attributes;

namespace Sage.Kahoot.Infrastructure.Mongo;

/// <summary>Documento da coleção <c>quizzes</c> (D4): perguntas embutidas no agregado.</summary>
public sealed class QuizDocument
{
    [BsonId]
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<QuestionDocument> Questions { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public bool IsSeed { get; set; }
}

public sealed class QuestionDocument
{
    public Guid Id { get; set; }

    public int Order { get; set; }

    public string Text { get; set; } = string.Empty;

    public int TimeLimitSeconds { get; set; }

    public int BasePoints { get; set; }

    public List<AnswerOptionDocument> Options { get; set; } = [];
}

public sealed class AnswerOptionDocument
{
    public Guid Id { get; set; }

    public string Text { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }
}

/// <summary>
/// Documento da coleção <c>sessions</c> (D4): snapshot imutável do quiz + participantes embutidos
/// + estado da pergunta corrente.
/// </summary>
public sealed class SessionDocument
{
    [BsonId]
    public Guid Id { get; set; }

    public string Pin { get; set; } = string.Empty;

    public Guid QuizId { get; set; }

    public QuizSnapshotDocument QuizSnapshot { get; set; } = new();

    /// <summary>Persistido como inteiro do enum SessionStatus.</summary>
    public int Status { get; set; }

    public int CurrentQuestionIndex { get; set; }

    public DateTime? CurrentQuestionOpenedAtUtc { get; set; }

    public DateTime? CurrentQuestionClosesAtUtc { get; set; }

    /// <summary>Trava de emissão única do fechamento da pergunta (ver Session.CurrentQuestionClosed).</summary>
    public bool CurrentQuestionClosed { get; set; }

    public List<ParticipantDocument> Participants { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? EndedAtUtc { get; set; }

    public DateTime LastActivityAtUtc { get; set; }
}

public sealed class QuizSnapshotDocument
{
    public string Title { get; set; } = string.Empty;

    public List<SessionQuestionDocument> Questions { get; set; } = [];
}

public sealed class SessionQuestionDocument
{
    public Guid Id { get; set; }

    public int Order { get; set; }

    public string Text { get; set; } = string.Empty;

    public int TimeLimitSeconds { get; set; }

    public int BasePoints { get; set; }

    public List<AnswerOptionDocument> Options { get; set; } = [];
}

public sealed class ParticipantDocument
{
    public Guid Id { get; set; }

    public string Nickname { get; set; } = string.Empty;

    public int JoinOrder { get; set; }

    public DateTime JoinedAtUtc { get; set; }

    public int TotalScore { get; set; }

    public long TotalResponseTimeMs { get; set; }

    public bool IsConnected { get; set; }
}

/// <summary>
/// Documento da coleção <c>session_answers</c> (D4): uma resposta por documento.
/// Fonte de verdade da sessão; o índice único garante uma resposta por participante por pergunta.
/// </summary>
public sealed class AnswerDocument
{
    [BsonId]
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public Guid QuestionId { get; set; }

    public int QuestionIndex { get; set; }

    public Guid ParticipantId { get; set; }

    public Guid SelectedOptionId { get; set; }

    public bool IsCorrect { get; set; }

    public long ResponseTimeMs { get; set; }

    public int PointsAwarded { get; set; }

    public DateTime AnsweredAtUtc { get; set; }
}
