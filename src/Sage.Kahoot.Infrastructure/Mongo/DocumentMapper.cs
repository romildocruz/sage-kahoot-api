using Sage.Kahoot.Domain.Quizzes;
using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.Infrastructure.Mongo;

/// <summary>
/// Tradução entre documentos e agregados. A reidratação não revalida invariantes:
/// o que está persistido já passou pela validação na escrita.
/// </summary>
internal static class DocumentMapper
{
    public static QuizDocument ToDocument(Quiz quiz) =>
        new()
        {
            Id = quiz.Id,
            Title = quiz.Title,
            Description = quiz.Description,
            CreatedAtUtc = quiz.CreatedAtUtc,
            UpdatedAtUtc = quiz.UpdatedAtUtc,
            IsSeed = quiz.IsSeed,
            Questions =
            [
                .. quiz.Questions.Select(static q => new QuestionDocument
                {
                    Id = q.Id,
                    Order = q.Order,
                    Text = q.Text,
                    TimeLimitSeconds = q.TimeLimitSeconds,
                    BasePoints = q.BasePoints,
                    Options =
                    [
                        .. q.Options.Select(static o => new AnswerOptionDocument
                        {
                            Id = o.Id,
                            Text = o.Text,
                            IsCorrect = o.IsCorrect
                        })
                    ]
                })
            ]
        };

    public static Quiz ToDomain(QuizDocument document)
    {
        var questions = document.Questions
            .OrderBy(static q => q.Order)
            .Select(static q =>
            {
                var options = q.Options
                    .Select(static o => AnswerOption.Create(o.Text, o.IsCorrect, o.Id).Value)
                    .ToList();

                return Question.Create(q.Text, q.Order, q.TimeLimitSeconds, q.BasePoints, options, q.Id).Value;
            })
            .ToList();

        return Quiz.Rehydrate(
            document.Id,
            document.Title,
            document.Description,
            questions,
            document.CreatedAtUtc,
            document.UpdatedAtUtc,
            document.IsSeed);
    }

    public static SessionDocument ToDocument(Session session) =>
        new()
        {
            Id = session.Id,
            Pin = session.Pin,
            QuizId = session.Quiz.QuizId,
            Status = (int)session.Status,
            CurrentQuestionIndex = session.CurrentQuestionIndex,
            CurrentQuestionOpenedAtUtc = session.CurrentQuestionOpenedAtUtc,
            CurrentQuestionClosesAtUtc = session.CurrentQuestionClosesAtUtc,
            CurrentQuestionClosed = session.CurrentQuestionClosed,
            CreatedAtUtc = session.CreatedAtUtc,
            StartedAtUtc = session.StartedAtUtc,
            EndedAtUtc = session.EndedAtUtc,
            LastActivityAtUtc = session.LastActivityAtUtc,
            QuizSnapshot = new QuizSnapshotDocument
            {
                Title = session.Quiz.Title,
                Questions =
                [
                    .. session.Quiz.Questions.Select(static q => new SessionQuestionDocument
                    {
                        Id = q.Id,
                        Order = q.Order,
                        Text = q.Text,
                        TimeLimitSeconds = q.TimeLimitSeconds,
                        BasePoints = q.BasePoints,
                        Options =
                        [
                            .. q.Options.Select(static o => new AnswerOptionDocument
                            {
                                Id = o.Id,
                                Text = o.Text,
                                IsCorrect = o.IsCorrect
                            })
                        ]
                    })
                ]
            },
            Participants =
            [
                .. session.Participants.Select(static p => new ParticipantDocument
                {
                    Id = p.Id,
                    Nickname = p.Nickname,
                    JoinOrder = p.JoinOrder,
                    JoinedAtUtc = p.JoinedAtUtc,
                    TotalScore = p.TotalScore,
                    TotalResponseTimeMs = p.TotalResponseTimeMs,
                    IsConnected = p.IsConnected
                })
            ]
        };

    public static Session ToDomain(SessionDocument document)
    {
        var questions = document.QuizSnapshot.Questions
            .OrderBy(static q => q.Order)
            .Select(static q => new SessionQuestion(
                q.Id,
                q.Order,
                q.Text,
                q.TimeLimitSeconds,
                q.BasePoints,
                [.. q.Options.Select(static o => new SessionAnswerOption(o.Id, o.Text, o.IsCorrect))]))
            .ToList();

        var participants = document.Participants
            .OrderBy(static p => p.JoinOrder)
            .Select(static p => Participant.Rehydrate(
                p.Id,
                p.Nickname,
                p.JoinOrder,
                p.JoinedAtUtc,
                p.TotalScore,
                p.TotalResponseTimeMs,
                p.IsConnected))
            .ToList();

        return Session.Rehydrate(
            document.Id,
            document.Pin,
            new QuizSnapshot(document.QuizId, document.QuizSnapshot.Title, questions),
            (SessionStatus)document.Status,
            document.CurrentQuestionIndex,
            document.CurrentQuestionOpenedAtUtc,
            document.CurrentQuestionClosesAtUtc,
            document.CurrentQuestionClosed,
            participants,
            document.CreatedAtUtc,
            document.StartedAtUtc,
            document.EndedAtUtc,
            document.LastActivityAtUtc);
    }

    public static AnswerDocument ToDocument(AnswerRecord answer) =>
        new()
        {
            Id = answer.Id,
            SessionId = answer.SessionId,
            QuestionId = answer.QuestionId,
            QuestionIndex = answer.QuestionIndex,
            ParticipantId = answer.ParticipantId,
            SelectedOptionId = answer.SelectedOptionId,
            IsCorrect = answer.IsCorrect,
            ResponseTimeMs = answer.ResponseTimeMs,
            PointsAwarded = answer.PointsAwarded,
            AnsweredAtUtc = answer.AnsweredAtUtc
        };

    public static AnswerRecord ToDomain(AnswerDocument document) =>
        new(document.Id,
            document.SessionId,
            document.QuestionId,
            document.QuestionIndex,
            document.ParticipantId,
            document.SelectedOptionId,
            document.IsCorrect,
            document.ResponseTimeMs,
            document.PointsAwarded,
            document.AnsweredAtUtc);
}
