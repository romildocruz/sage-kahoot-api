using Sage.Kahoot.Application.Sessions.ParticipantPresence;
using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.UnitTests.Handlers;

/// <summary>
/// Cobre o requisito "Presença do participante na sessão" e o efeito dele no fechamento
/// antecipado da pergunta.
/// </summary>
public sealed class ParticipantPresenceTests
{
    private static readonly DateTime Now = new(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemorySessionRepository _sessions = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly FixedClock _clock = new(Now);

    private static Session BuildSession(int questionCount = 1)
    {
        var questions = Enumerable.Range(0, questionCount)
            .Select(i => new SessionQuestion(
                Guid.NewGuid(), i, $"Pergunta {i + 1}", 20, 1000,
                [
                    new SessionAnswerOption(Guid.NewGuid(), "Certa", true),
                    new SessionAnswerOption(Guid.NewGuid(), "Errada", false)
                ]))
            .ToList();

        return Session.Open(new QuizSnapshot(Guid.NewGuid(), "Quiz", questions), "123456", Now).Value;
    }

    private async Task<Session> SeedAsync(int questionCount = 1)
    {
        var session = BuildSession(questionCount);
        await _sessions.InsertAsync(session, CancellationToken.None);
        return session;
    }

    private ParticipantDisconnectedHandler DisconnectHandler() => new(_sessions, _notifier, _clock);

    private ParticipantConnectedHandler ConnectHandler() => new(_sessions, _clock);

    [Fact]
    public async Task Participante_desconectado_sai_da_contagem_de_ativos_sem_perder_pontuacao()
    {
        var session = await SeedAsync();
        var ana = session.Join("Ana", Now, 100).Value;
        session.Join("Bruno", Now, 100);
        ana.AddScore(850, responseTimeMs: 3_000);

        var result = await DisconnectHandler().ExecuteAsync(
            new ParticipantDisconnectedCommand(session.Id, ana.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(ana.IsConnected);
        Assert.Single(session.ActiveParticipants);
        // Pontuação, apelido e tempo acumulado seguem intactos.
        Assert.Equal(850, ana.TotalScore);
        Assert.Equal(3_000, ana.TotalResponseTimeMs);
        Assert.Equal("Ana", ana.Nickname);
    }

    [Fact]
    public async Task Desconexao_anuncia_a_saida_com_o_total_de_ativos_atualizado()
    {
        var session = await SeedAsync();
        var ana = session.Join("Ana", Now, 100).Value;
        session.Join("Bruno", Now, 100);

        await DisconnectHandler().ExecuteAsync(
            new ParticipantDisconnectedCommand(session.Id, ana.Id),
            CancellationToken.None);

        var evento = Assert.IsType<Application.Abstractions.ParticipantPresencePayload>(
            Assert.Single(_notifier.Events));

        Assert.Equal("Ana", evento.Nickname);
        Assert.Equal(1, evento.TotalParticipants);
    }

    [Fact]
    public async Task Participante_inativo_volta_a_ser_ativo_ao_reconectar()
    {
        var session = await SeedAsync();
        var ana = session.Join("Ana", Now, 100).Value;

        await DisconnectHandler().ExecuteAsync(
            new ParticipantDisconnectedCommand(session.Id, ana.Id), CancellationToken.None);
        Assert.Empty(session.ActiveParticipants);

        var result = await ConnectHandler().ExecuteAsync(
            new ParticipantConnectedCommand(session.Id, ana.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(ana.IsConnected);
        Assert.Single(session.ActiveParticipants);
    }

    [Fact]
    public async Task Desconexao_de_participante_que_nao_pertence_a_sessao_e_recusada()
    {
        var session = await SeedAsync();

        var result = await DisconnectHandler().ExecuteAsync(
            new ParticipantDisconnectedCommand(session.Id, Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("session.participant_not_found", result.Error!.Code);
        Assert.Empty(_notifier.Events);
    }

    [Fact]
    public void Participante_desconectado_nao_bloqueia_o_fechamento_antecipado()
    {
        var session = BuildSession();
        session.Start(Now);
        session.Join("Ana", Now, 100);
        var bruno = session.Join("Bruno", Now, 100).Value;

        // Bruno fecha o navegador sem responder; Ana, única ativa, responde.
        session.MarkParticipantDisconnected(bruno.Id, Now);

        Assert.True(session.ShouldCloseAfterAnswer(answeredCount: 1));
    }

    [Fact]
    public void Participante_ativo_que_nao_respondeu_mantem_a_pergunta_aberta()
    {
        var session = BuildSession();
        session.Start(Now);
        session.Join("Ana", Now, 100);
        session.Join("Bruno", Now, 100);

        // Ninguém desconectou: uma resposta só, com dois ativos, não fecha a pergunta.
        Assert.False(session.ShouldCloseAfterAnswer(answeredCount: 1));
        Assert.True(session.ShouldCloseAfterAnswer(answeredCount: 2));
    }

    [Fact]
    public void Pergunta_ja_fechada_nao_fecha_de_novo_por_todos_responderam()
    {
        var session = BuildSession();
        session.Start(Now);
        session.Join("Ana", Now, 100);
        session.CloseCurrentQuestionOnly(Now.AddSeconds(3));

        // Evita emissão dupla do resultado quando a expiração vence a corrida.
        Assert.False(session.ShouldCloseAfterAnswer(answeredCount: 1));
    }

    [Fact]
    public void Sessao_sem_participante_ativo_nao_dispara_fechamento_antecipado()
    {
        var session = BuildSession();
        session.Start(Now);
        var ana = session.Join("Ana", Now, 100).Value;
        session.MarkParticipantDisconnected(ana.Id, Now);

        // Sem ninguém ativo, a pergunta deve esperar o tempo expirar.
        Assert.False(session.ShouldCloseAfterAnswer(answeredCount: 0));
    }
}

/// <summary>Cobre a contagem de conexões por participante (design.md — D1).</summary>
public sealed class ParticipantConnectionTrackerTests
{
    [Fact]
    public void Primeira_conexao_marca_o_participante_como_recem_ativo()
    {
        var tracker = new ParticipantConnectionTracker();
        var id = Guid.NewGuid();

        Assert.True(tracker.Acquire(id));
        Assert.Equal(1, tracker.OpenConnections(id));
    }

    [Fact]
    public void Segunda_conexao_do_mesmo_participante_nao_o_marca_como_recem_ativo()
    {
        var tracker = new ParticipantConnectionTracker();
        var id = Guid.NewGuid();

        tracker.Acquire(id);

        Assert.False(tracker.Acquire(id));
        Assert.Equal(2, tracker.OpenConnections(id));
    }

    [Fact]
    public void Queda_de_uma_entre_duas_conexoes_nao_libera_a_saida()
    {
        var tracker = new ParticipantConnectionTracker();
        var id = Guid.NewGuid();
        tracker.Acquire(id);
        tracker.Acquire(id);

        // Fechar uma aba não é sair: o participante segue ativo e nenhum evento deve ser emitido.
        Assert.False(tracker.Release(id));
        Assert.Equal(1, tracker.OpenConnections(id));
    }

    [Fact]
    public void Queda_da_ultima_conexao_libera_a_saida()
    {
        var tracker = new ParticipantConnectionTracker();
        var id = Guid.NewGuid();
        tracker.Acquire(id);
        tracker.Acquire(id);
        tracker.Release(id);

        Assert.True(tracker.Release(id));
        Assert.Equal(0, tracker.OpenConnections(id));
    }

    [Fact]
    public void Release_sem_registro_previo_e_tratado_como_ultima_conexao()
    {
        var tracker = new ParticipantConnectionTracker();

        // Acontece após reinício do processo: o lado seguro é marcar inativo quem de fato saiu.
        Assert.True(tracker.Release(Guid.NewGuid()));
    }

    [Fact]
    public void Contagem_suporta_conexoes_concorrentes_do_mesmo_participante()
    {
        var tracker = new ParticipantConnectionTracker();
        var id = Guid.NewGuid();

        Parallel.For(0, 50, _ => tracker.Acquire(id));
        Assert.Equal(50, tracker.OpenConnections(id));

        var liberacoes = 0;
        Parallel.For(0, 50, _ =>
        {
            if (tracker.Release(id))
            {
                Interlocked.Increment(ref liberacoes);
            }
        });

        // Exatamente uma das 50 quedas pode anunciar a saída.
        Assert.Equal(1, liberacoes);
        Assert.Equal(0, tracker.OpenConnections(id));
    }
}
