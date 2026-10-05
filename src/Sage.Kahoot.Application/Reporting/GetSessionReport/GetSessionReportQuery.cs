using FastEndpoints;
using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Common;
using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.Application.Reporting.GetSessionReport;

public sealed record GetSessionReportQuery(Guid SessionId) : ICommand<Result<SessionReport>>;

/// <summary>
/// Monta o relatório recalculando tudo a partir de <c>session_answers</c>, que é a fonte de verdade.
/// A pontuação desnormalizada no participante não é usada aqui — é justamente essa independência
/// que permite detectar divergência entre as duas.
/// </summary>
public sealed class GetSessionReportHandler(ISessionRepository sessions)
    : ICommandHandler<GetSessionReportQuery, Result<SessionReport>>
{
    public async Task<Result<SessionReport>> ExecuteAsync(GetSessionReportQuery query, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(query.SessionId, ct);

        if (session is null)
        {
            return Result<SessionReport>.Failure(SessionErrors.NotFound);
        }

        var answers = await sessions.GetAnswersAsync(session.Id, ct);

        return Build(session, answers);
    }

    public static SessionReport Build(Session session, IReadOnlyList<AnswerRecord> answers)
    {
        var totalQuestions = session.TotalQuestions;
        var answersByParticipant = answers
            .GroupBy(static a => a.ParticipantId)
            .ToDictionary(static g => g.Key, static g => g.ToList());

        var ranking = session.Participants
            .Select(p =>
            {
                var own = answersByParticipant.TryGetValue(p.Id, out var list) ? list : [];
                var correct = own.Count(static a => a.IsCorrect);

                return new
                {
                    Participant = p,
                    TotalScore = own.Sum(static a => a.PointsAwarded),
                    CorrectCount = correct,
                    WrongCount = own.Count - correct,
                    UnansweredCount = Math.Max(0, totalQuestions - own.Count),
                    TotalResponseTimeMs = own.Sum(static a => a.ResponseTimeMs),
                    AverageResponseTimeMs = own.Count == 0 ? 0d : own.Average(static a => (double)a.ResponseTimeMs)
                };
            })
            // Mesmo desempate do ranking ao vivo: pontuação, tempo total, ordem de entrada.
            .OrderByDescending(static x => x.TotalScore)
            .ThenBy(static x => x.TotalResponseTimeMs)
            .ThenBy(static x => x.Participant.JoinOrder)
            .Select(static (x, index) => new ParticipantReport(
                index + 1,
                x.Participant.Id,
                x.Participant.Nickname,
                x.TotalScore,
                x.CorrectCount,
                x.WrongCount,
                x.UnansweredCount,
                Math.Round(x.AverageResponseTimeMs, 2)))
            .ToList();

        var activeParticipantCount = session.Participants.Count;
        var answersByQuestion = answers.GroupBy(static a => a.QuestionId).ToDictionary(static g => g.Key, static g => g.ToList());

        var questions = session.Quiz.Questions
            .Select(q =>
            {
                var own = answersByQuestion.TryGetValue(q.Id, out var list) ? list : [];
                var countsByOption = own.GroupBy(static a => a.SelectedOptionId)
                    .ToDictionary(static g => g.Key, static g => g.Count());

                return new QuestionReport(
                    q.Id,
                    q.Order,
                    q.Text,
                    q.CorrectOptionId,
                    [.. q.Options.Select(o => new QuestionOptionReport(
                        o.Id,
                        o.Text,
                        o.IsCorrect,
                        countsByOption.GetValueOrDefault(o.Id, 0)))],
                    own.Count,
                    Math.Max(0, activeParticipantCount - own.Count),
                    // Pergunta sem nenhuma resposta reporta taxa de acerto 0, não divisão por zero.
                    own.Count == 0 ? 0d : Math.Round(own.Count(static a => a.IsCorrect) / (double)own.Count, 4),
                    own.Count == 0 ? 0d : Math.Round(own.Average(static a => (double)a.ResponseTimeMs), 2));
            })
            .ToList();

        return new SessionReport(
            session.Id,
            session.Pin,
            session.Quiz.QuizId,
            session.Quiz.Title,
            session.Status,
            IsFinal: session.Status is SessionStatus.Encerrada,
            session.CreatedAtUtc,
            session.StartedAtUtc,
            session.EndedAtUtc,
            session.Participants.Count,
            ranking,
            questions);
    }
}
