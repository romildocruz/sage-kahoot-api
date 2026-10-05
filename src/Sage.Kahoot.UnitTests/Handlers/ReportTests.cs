using System.Text;
using Sage.Kahoot.Application.Reporting.ExportSessionReport;
using Sage.Kahoot.Application.Reporting.GetSessionReport;
using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.UnitTests.Handlers;

/// <summary>Cobre a spec session-reporting: ranking final, estatísticas por pergunta e exportação.</summary>
public sealed class ReportTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemorySessionRepository _sessions = new();

    private static QuizSnapshot Snapshot(int questionCount = 2)
    {
        var questions = Enumerable.Range(0, questionCount)
            .Select(i => new SessionQuestion(
                Guid.NewGuid(), i, $"Pergunta {i + 1}", 20, 1000,
                [
                    new SessionAnswerOption(Guid.NewGuid(), "Certa", true),
                    new SessionAnswerOption(Guid.NewGuid(), "Errada", false)
                ]))
            .ToList();

        return new QuizSnapshot(Guid.NewGuid(), "Boas práticas, parte 1", questions);
    }

    [Fact]
    public void Sessao_sem_participantes_devolve_ranking_vazio_sem_erro()
    {
        var session = Session.Open(Snapshot(), "123456", Now).Value;

        var report = GetSessionReportHandler.Build(session, []);

        Assert.Empty(report.Ranking);
        Assert.Equal(0, report.ParticipantCount);
        Assert.All(report.Questions, q => Assert.Equal(0d, q.CorrectRate));
    }

    [Fact]
    public void Pergunta_sem_nenhuma_resposta_reporta_taxa_zero_e_todos_como_nao_respondidas()
    {
        var session = Session.Open(Snapshot(questionCount: 1), "123456", Now).Value;
        session.Join("Ana", Now, 100);
        session.Join("Bruno", Now, 100);

        var report = GetSessionReportHandler.Build(session, []);

        var question = Assert.Single(report.Questions);
        Assert.Equal(0d, question.CorrectRate);
        Assert.Equal(0, question.AnswerCount);
        Assert.Equal(2, question.UnansweredCount);
    }

    [Fact]
    public void Ranking_final_ordena_por_pontuacao_e_marca_o_podio()
    {
        var session = Session.Open(Snapshot(questionCount: 1), "123456", Now).Value;
        session.Start(Now);

        var ana = session.Join("Ana", Now, 100).Value;
        var bruno = session.Join("Bruno", Now, 100).Value;
        var carla = session.Join("Carla", Now, 100).Value;

        var question = session.CurrentQuestion!;
        var correct = question.Options.First(o => o.IsCorrect).Id;
        var wrong = question.Options.First(o => !o.IsCorrect).Id;

        var answers = new[]
        {
            AnswerRecord.Create(session.Id, question.Id, 0, ana.Id, correct, true, 2_000, 900, Now),
            AnswerRecord.Create(session.Id, question.Id, 0, bruno.Id, correct, true, 10_000, 750, Now),
            AnswerRecord.Create(session.Id, question.Id, 0, carla.Id, wrong, false, 5_000, 0, Now)
        };

        var report = GetSessionReportHandler.Build(session, answers);

        Assert.Equal(["Ana", "Bruno", "Carla"], report.Ranking.Select(r => r.Nickname));
        Assert.Equal([1, 2, 3], report.Ranking.Select(r => r.Position));
        Assert.All(report.Ranking, r => Assert.True(r.IsPodium));
        Assert.Equal(900, report.Ranking[0].TotalScore);
        Assert.Equal(1, report.Ranking[0].CorrectCount);
        Assert.Equal(1, report.Ranking[2].WrongCount);
    }

    [Fact]
    public void Estatisticas_por_pergunta_trazem_distribuicao_e_taxa_de_acerto()
    {
        var session = Session.Open(Snapshot(questionCount: 1), "123456", Now).Value;
        session.Start(Now);
        var ana = session.Join("Ana", Now, 100).Value;
        var bruno = session.Join("Bruno", Now, 100).Value;

        var question = session.CurrentQuestion!;
        var correct = question.Options.First(o => o.IsCorrect).Id;
        var wrong = question.Options.First(o => !o.IsCorrect).Id;

        var answers = new[]
        {
            AnswerRecord.Create(session.Id, question.Id, 0, ana.Id, correct, true, 4_000, 800, Now),
            AnswerRecord.Create(session.Id, question.Id, 0, bruno.Id, wrong, false, 6_000, 0, Now)
        };

        var report = GetSessionReportHandler.Build(session, answers);
        var stats = Assert.Single(report.Questions);

        Assert.Equal(0.5d, stats.CorrectRate);
        Assert.Equal(5_000d, stats.AverageResponseTimeMs);
        Assert.Equal(1, stats.Options.First(o => o.OptionId == correct).AnswerCount);
        Assert.Equal(1, stats.Options.First(o => o.OptionId == wrong).AnswerCount);
    }

    [Fact]
    public void Sessao_em_andamento_devolve_relatorio_marcado_como_nao_final()
    {
        var session = Session.Open(Snapshot(), "123456", Now).Value;
        session.Start(Now);

        var report = GetSessionReportHandler.Build(session, []);

        Assert.False(report.IsFinal);
        Assert.Equal(SessionStatus.EmAndamento, report.Status);
    }

    [Fact]
    public async Task Exportacao_csv_escapa_apelido_com_virgula_e_aspas()
    {
        var session = Session.Open(Snapshot(questionCount: 1), "123456", Now).Value;
        session.Start(Now);
        var participant = session.Join("Silva, \"Zé\"", Now, 100).Value;

        var question = session.CurrentQuestion!;
        var correct = question.Options.First(o => o.IsCorrect).Id;

        await _sessions.InsertAsync(session, CancellationToken.None);
        await _sessions.TryInsertAnswerAsync(
            AnswerRecord.Create(session.Id, question.Id, 0, participant.Id, correct, true, 3_000, 850, Now),
            CancellationToken.None);

        var handler = new ExportSessionReportHandler(_sessions);
        var result = await handler.ExecuteAsync(
            new ExportSessionReportQuery(session.Id, ReportFormat.Csv),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var csv = Encoding.UTF8.GetString(result.Value.Content);
        var dataLine = csv.Split('\n').First(l => l.Contains("Silva", StringComparison.Ordinal));

        // O apelido inteiro precisa ficar em uma única coluna, com as aspas internas duplicadas.
        Assert.Contains("\"Silva, \"\"Zé\"\"\"", dataLine, StringComparison.Ordinal);
        Assert.EndsWith(".csv", result.Value.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exportacao_de_sessao_inexistente_devolve_nao_encontrado()
    {
        var handler = new ExportSessionReportHandler(_sessions);

        var result = await handler.ExecuteAsync(
            new ExportSessionReportQuery(Guid.NewGuid(), ReportFormat.Json),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("session.not_found", result.Error!.Code);
    }

    [Fact]
    public async Task Exportacao_json_traz_ranking_e_estatisticas_por_pergunta()
    {
        var session = Session.Open(Snapshot(questionCount: 1), "123456", Now).Value;
        session.Join("Ana", Now, 100);
        await _sessions.InsertAsync(session, CancellationToken.None);

        var handler = new ExportSessionReportHandler(_sessions);
        var result = await handler.ExecuteAsync(
            new ExportSessionReportQuery(session.Id, ReportFormat.Json),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var json = Encoding.UTF8.GetString(result.Value.Content);
        Assert.Contains("\"ranking\"", json, StringComparison.Ordinal);
        Assert.Contains("\"questions\"", json, StringComparison.Ordinal);
        Assert.EndsWith(".json", result.Value.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public void Pontuacao_desnormalizada_confere_com_o_recalculo_sobre_as_respostas()
    {
        var session = Session.Open(Snapshot(questionCount: 2), "123456", Now).Value;
        session.Start(Now);
        var ana = session.Join("Ana", Now, 100).Value;

        var answers = session.Quiz.Questions
            .Select((q, i) => AnswerRecord.Create(
                session.Id, q.Id, i, ana.Id, q.Options.First(o => o.IsCorrect).Id, true, 2_000, 900, Now))
            .ToList();

        // Simula o que o registro de resposta faz em produção: incrementa o cache no participante.
        foreach (var answer in answers)
        {
            session.FindParticipant(ana.Id)!.AddScore(answer.PointsAwarded, answer.ResponseTimeMs);
        }

        var report = GetSessionReportHandler.Build(session, answers);

        Assert.Equal(session.FindParticipant(ana.Id)!.TotalScore, report.Ranking[0].TotalScore);
    }
}
