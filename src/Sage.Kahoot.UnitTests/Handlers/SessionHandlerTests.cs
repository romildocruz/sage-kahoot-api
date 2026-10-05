using Sage.Kahoot.Application.Sessions.JoinSession;
using Sage.Kahoot.Domain.Common;
using Sage.Kahoot.Domain.Sessions;

namespace Sage.Kahoot.UnitTests.Handlers;

/// <summary>Cobre a entrada anônima por PIN: sanitização, unicidade do apelido e PIN inválido.</summary>
public sealed class SessionHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemorySessionRepository _sessions = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly FixedClock _clock = new(Now);

    private JoinSessionHandler CreateHandler(int maxParticipants = 100) =>
        new(_sessions, new FakeTokenIssuer(), _notifier, _clock, TestOptions.Session(maxParticipants));

    private async Task<Session> SeedSessionAsync(string pin = "123456")
    {
        var snapshot = new QuizSnapshot(
            Guid.NewGuid(),
            "Quiz",
            [
                new SessionQuestion(Guid.NewGuid(), 0, "Pergunta", 20, 1000,
                [
                    new SessionAnswerOption(Guid.NewGuid(), "Certa", true),
                    new SessionAnswerOption(Guid.NewGuid(), "Errada", false)
                ])
            ]);

        var session = Session.Open(snapshot, pin, Now).Value;
        await _sessions.InsertAsync(session, CancellationToken.None);
        return session;
    }

    [Fact]
    public async Task Entrada_valida_devolve_token_e_anuncia_o_participante()
    {
        await SeedSessionAsync();

        var result = await CreateHandler().ExecuteAsync(
            new JoinSessionCommand("123456", "Participante"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Participante", result.Value.Nickname);
        Assert.NotEmpty(result.Value.AccessToken);
        Assert.Single(_notifier.Events);
    }

    [Fact]
    public async Task Pin_inexistente_devolve_mensagem_generica()
    {
        var result = await CreateHandler().ExecuteAsync(
            new JoinSessionCommand("999999", "Alguém"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCategory.NotFound, result.Error!.Category);
        Assert.Equal("session.pin_not_found", result.Error.Code);
    }

    [Fact]
    public async Task Pin_de_sessao_encerrada_nao_resolve_mais()
    {
        var session = await SeedSessionAsync();
        session.Start(Now);
        session.End(Now.AddMinutes(1));
        await _sessions.ReplaceAsync(session, CancellationToken.None);

        var result = await CreateHandler().ExecuteAsync(
            new JoinSessionCommand("123456", "Atrasado"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("session.pin_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task Apelido_duplicado_devolve_conflito()
    {
        await SeedSessionAsync();
        var handler = CreateHandler();

        await handler.ExecuteAsync(new JoinSessionCommand("123456", "Ana"), CancellationToken.None);
        var result = await handler.ExecuteAsync(new JoinSessionCommand("123456", "ana"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCategory.Conflict, result.Error!.Category);
    }

    [Fact]
    public async Task Apelido_com_marcacao_html_e_sanitizado_antes_de_persistir()
    {
        await SeedSessionAsync();

        var result = await CreateHandler().ExecuteAsync(
            new JoinSessionCommand("123456", "<b>Bruno</b>"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain('<', result.Value.Nickname);
        Assert.DoesNotContain('>', result.Value.Nickname);
        Assert.Contains("Bruno", result.Value.Nickname, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Payload_de_script_longo_e_recusado_por_exceder_o_limite_apos_sanitizacao()
    {
        await SeedSessionAsync();

        // A sanitização remove a marcação, mas o que sobra ainda passa de 20 caracteres:
        // o apelido é recusado em vez de ser truncado silenciosamente.
        var result = await CreateHandler().ExecuteAsync(
            new JoinSessionCommand("123456", "<script>alert('x')</script>Bruno"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("session.invalid_nickname", result.Error!.Code);
    }

    [Fact]
    public async Task Apelido_curto_demais_apos_sanitizacao_e_rejeitado()
    {
        await SeedSessionAsync();

        var result = await CreateHandler().ExecuteAsync(
            new JoinSessionCommand("123456", "<<>>"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("session.invalid_nickname", result.Error!.Code);
    }

    [Fact]
    public async Task Sessao_lotada_devolve_conflito()
    {
        await SeedSessionAsync();
        var handler = CreateHandler(maxParticipants: 1);

        await handler.ExecuteAsync(new JoinSessionCommand("123456", "Primeiro"), CancellationToken.None);
        var result = await handler.ExecuteAsync(new JoinSessionCommand("123456", "Segundo"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("session.full", result.Error!.Code);
    }

    [Fact]
    public async Task Participante_pode_entrar_com_a_sessao_ja_em_andamento_comecando_com_zero()
    {
        var session = await SeedSessionAsync();
        session.Start(Now);
        await _sessions.ReplaceAsync(session, CancellationToken.None);

        var result = await CreateHandler().ExecuteAsync(
            new JoinSessionCommand("123456", "Atrasado"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _sessions.Sessions[session.Id].FindParticipant(result.Value.ParticipantId)!.TotalScore);
    }
}
