using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.UnitTests.Domain;

/// <summary>
/// Cobre a spec live-session: transições de estado, janela de resposta, entrada de participante
/// e desempate do ranking.
/// </summary>
public sealed class SessionTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    private static QuizSnapshot Snapshot(int questionCount = 2)
    {
        var questions = Enumerable.Range(0, questionCount)
            .Select(i => new SessionQuestion(
                Guid.NewGuid(),
                i,
                $"Pergunta {i + 1}",
                20,
                1000,
                [
                    new SessionAnswerOption(Guid.NewGuid(), "Certa", true),
                    new SessionAnswerOption(Guid.NewGuid(), "Errada", false)
                ]))
            .ToList();

        return new QuizSnapshot(Guid.NewGuid(), "Quiz de teste", questions);
    }

    private static Session OpenSession(int questionCount = 2) =>
        Session.Open(Snapshot(questionCount), "123456", Now).Value;

    [Fact]
    public void Sessao_sem_perguntas_nao_pode_ser_aberta()
    {
        var result = Session.Open(new QuizSnapshot(Guid.NewGuid(), "Vazio", []), "123456", Now);

        Assert.False(result.IsSuccess);
        Assert.Equal("session.quiz_without_questions", result.Error!.Code);
    }

    [Fact]
    public void Inicio_move_para_em_andamento_e_abre_a_primeira_pergunta()
    {
        var session = OpenSession();

        var started = session.Start(Now);

        Assert.True(started.IsSuccess);
        Assert.Equal(SessionStatus.EmAndamento, session.Status);
        Assert.Equal(0, session.CurrentQuestionIndex);
        Assert.Equal(Now.AddSeconds(20), session.CurrentQuestionClosesAtUtc);
    }

    [Fact]
    public void Iniciar_sessao_encerrada_e_rejeitado_e_nao_muda_o_estado()
    {
        var session = OpenSession();
        session.Start(Now);
        session.End(Now.AddSeconds(5));

        var result = session.Start(Now.AddSeconds(10));

        Assert.False(result.IsSuccess);
        Assert.Equal("session.invalid_transition", result.Error!.Code);
        Assert.Equal(SessionStatus.Encerrada, session.Status);
    }

    [Fact]
    public void Avancar_na_ultima_pergunta_encerra_a_sessao()
    {
        var session = OpenSession(questionCount: 1);
        session.Start(Now);

        var advanced = session.Advance(Now.AddSeconds(5));

        Assert.True(advanced.IsSuccess);
        Assert.True(advanced.Value.SessionEnded);
        Assert.Equal(SessionStatus.Encerrada, session.Status);
    }

    [Fact]
    public void Avancar_abre_a_proxima_pergunta_com_nova_janela()
    {
        var session = OpenSession(questionCount: 2);
        session.Start(Now);

        var advanced = session.Advance(Now.AddSeconds(8));

        Assert.True(advanced.IsSuccess);
        Assert.False(advanced.Value.SessionEnded);
        Assert.Equal(1, session.CurrentQuestionIndex);
        Assert.Equal(Now.AddSeconds(28), session.CurrentQuestionClosesAtUtc);
        Assert.False(session.CurrentQuestionClosed);
    }

    [Fact]
    public void Apelido_duplicado_na_mesma_sessao_e_rejeitado()
    {
        var session = OpenSession();
        session.Join("Participante 1", Now, 100);

        var result = session.Join("participante 1", Now, 100);

        Assert.False(result.IsSuccess);
        Assert.Equal("session.nickname_taken", result.Error!.Code);
    }

    [Fact]
    public void Sessao_lotada_recusa_novo_participante()
    {
        var session = OpenSession();
        session.Join("Um", Now, maxParticipants: 1);

        var result = session.Join("Dois", Now, maxParticipants: 1);

        Assert.False(result.IsSuccess);
        Assert.Equal("session.full", result.Error!.Code);
    }

    [Fact]
    public void Resposta_para_pergunta_que_nao_e_a_atual_e_rejeitada()
    {
        var session = OpenSession();
        session.Start(Now);
        var outraPergunta = session.Quiz.Questions[1];

        var result = session.ValidateAnswer(outraPergunta.Id, outraPergunta.Options[0].Id, Now.AddSeconds(1), 500);

        Assert.False(result.IsSuccess);
        Assert.Equal("session.not_current_question", result.Error!.Code);
    }

    [Fact]
    public void Resposta_depois_do_fechamento_mais_tolerancia_e_rejeitada()
    {
        var session = OpenSession();
        session.Start(Now);
        var question = session.CurrentQuestion!;

        var result = session.ValidateAnswer(
            question.Id,
            question.Options[0].Id,
            Now.AddSeconds(20).AddMilliseconds(501),
            toleranceMs: 500);

        Assert.False(result.IsSuccess);
        Assert.Equal("session.answer_window_closed", result.Error!.Code);
    }

    [Fact]
    public void Resposta_dentro_da_tolerancia_ainda_e_aceita()
    {
        var session = OpenSession();
        session.Start(Now);
        var question = session.CurrentQuestion!;

        var result = session.ValidateAnswer(
            question.Id,
            question.Options[0].Id,
            Now.AddSeconds(20).AddMilliseconds(300),
            toleranceMs: 500);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsCorrect);
    }

    [Fact]
    public void Pergunta_fechada_nao_aceita_mais_respostas()
    {
        var session = OpenSession();
        session.Start(Now);
        var question = session.CurrentQuestion!;
        session.CloseCurrentQuestionOnly(Now.AddSeconds(3));

        var result = session.ValidateAnswer(question.Id, question.Options[0].Id, Now.AddSeconds(4), 500);

        Assert.False(result.IsSuccess);
        Assert.Equal("session.answer_window_closed", result.Error!.Code);
    }

    [Fact]
    public void Fechar_duas_vezes_a_mesma_pergunta_e_rejeitado()
    {
        var session = OpenSession();
        session.Start(Now);
        session.CloseCurrentQuestionOnly(Now.AddSeconds(3));

        var second = session.CloseCurrentQuestionOnly(Now.AddSeconds(4));

        Assert.False(second.IsSuccess);
        Assert.Equal("session.no_question_open", second.Error!.Code);
    }

    [Fact]
    public void Empate_em_pontuacao_e_desempatado_pelo_menor_tempo_total()
    {
        var session = OpenSession();
        session.Join("Lenta", Now, 100);
        session.Join("Rapida", Now, 100);

        session.FindParticipant(session.Participants[0].Id)!.AddScore(1000, responseTimeMs: 9_000);
        session.FindParticipant(session.Participants[1].Id)!.AddScore(1000, responseTimeMs: 3_000);

        var ranking = session.Ranking();

        Assert.Equal("Rapida", ranking[0].Nickname);
        Assert.Equal("Lenta", ranking[1].Nickname);
    }

    [Fact]
    public void Empate_total_e_desempatado_pela_ordem_de_entrada()
    {
        var session = OpenSession();
        session.Join("Primeiro", Now, 100);
        session.Join("Segundo", Now, 100);

        foreach (var participant in session.Participants)
        {
            participant.AddScore(500, responseTimeMs: 5_000);
        }

        var ranking = session.Ranking();

        Assert.Equal("Primeiro", ranking[0].Nickname);
    }

    [Fact]
    public void Cancelamento_libera_a_sessao_de_qualquer_estado_ativo()
    {
        var session = OpenSession();

        var result = session.Cancel(Now.AddHours(5));

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionStatus.Cancelada, session.Status);
        Assert.False(session.Status.IsActive());
    }
}
