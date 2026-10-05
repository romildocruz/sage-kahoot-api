using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sage.Kahoot.IntegrationTests.Infrastructure;

namespace Sage.Kahoot.IntegrationTests.Sessions;

/// <summary>
/// Fluxo ao vivo ponta a ponta: abrir sessão, três participantes entram, duas perguntas
/// respondidas, ranking e encerramento.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SessionFlowTests(ApiFixture fixture)
{
    private async Task<HttpClient> AdminClientAsync()
    {
        await fixture.WaitUntilReadyAsync();
        var client = fixture.CreateClient();
        client.UseBearer(await client.LoginAsAdminAsync());
        return client;
    }

    [Fact]
    public async Task Sessao_completa_com_tres_participantes_produz_ranking_correto()
    {
        using var admin = await AdminClientAsync();

        var quiz = await admin.CreateQuizAsync($"Sessão {Guid.NewGuid():N}", questionCount: 2);
        var session = await admin.OpenSessionAsync(quiz.GetProperty("id").GetGuid());
        var sessionId = session.GetProperty("id").GetGuid();
        var pin = session.GetProperty("pin").GetString()!;

        Assert.Equal(6, pin.Length);

        // Três participantes entram anonimamente.
        using var anon = fixture.CreateClient();
        var ana = await anon.JoinAsync(pin, "Ana");
        var bruno = await anon.JoinAsync(pin, "Bruno");
        await anon.JoinAsync(pin, "Carla");

        await admin.PostAsync($"/api/v1/sessions/{sessionId}/start", null);

        var state = await StateAsync(admin, sessionId);
        var question = state.GetProperty("currentQuestion");
        var questionId = question.GetProperty("questionId").GetGuid();
        var options = question.GetProperty("options");

        // O payload aberto não pode revelar qual é a correta.
        Assert.False(options[0].TryGetProperty("isCorrect", out _));

        // Ana e Bruno respondem a primeira; Carla não responde.
        await AnswerAsync(ana, sessionId, questionId, options[0].GetProperty("id").GetGuid());
        await AnswerAsync(bruno, sessionId, questionId, options[1].GetProperty("id").GetGuid());

        await admin.PostAsync($"/api/v1/sessions/{sessionId}/advance", null);

        var second = await StateAsync(admin, sessionId);
        var secondQuestionId = second.GetProperty("currentQuestion").GetProperty("questionId").GetGuid();
        var secondOptions = second.GetProperty("currentQuestion").GetProperty("options");

        await AnswerAsync(ana, sessionId, secondQuestionId, secondOptions[0].GetProperty("id").GetGuid());

        // Avança na última pergunta: a sessão encerra.
        await admin.PostAsync($"/api/v1/sessions/{sessionId}/advance", null);

        var report = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/reports/sessions/{sessionId}");

        Assert.True(report.GetProperty("isFinal").GetBoolean());
        Assert.Equal(3, report.GetProperty("ranking").GetArrayLength());

        var ranking = report.GetProperty("ranking");
        Assert.Equal("Ana", ranking[0].GetProperty("nickname").GetString());
        Assert.Equal(1, ranking[0].GetProperty("position").GetInt32());
        Assert.True(ranking[0].GetProperty("totalScore").GetInt32() > 0);

        // Carla não respondeu nenhuma das duas.
        var carlaEntry = ranking.EnumerateArray().First(e => e.GetProperty("nickname").GetString() == "Carla");
        Assert.Equal(0, carlaEntry.GetProperty("totalScore").GetInt32());
        Assert.Equal(2, carlaEntry.GetProperty("unansweredCount").GetInt32());
    }

    [Fact]
    public async Task Segunda_resposta_para_a_mesma_pergunta_responde_409()
    {
        using var admin = await AdminClientAsync();

        var quiz = await admin.CreateQuizAsync($"Dupla {Guid.NewGuid():N}", questionCount: 1);
        var session = await admin.OpenSessionAsync(quiz.GetProperty("id").GetGuid());
        var sessionId = session.GetProperty("id").GetGuid();

        using var anon = fixture.CreateClient();
        var participant = await anon.JoinAsync(session.GetProperty("pin").GetString()!, "Ana");

        await admin.PostAsync($"/api/v1/sessions/{sessionId}/start", null);

        var state = await StateAsync(admin, sessionId);
        var questionId = state.GetProperty("currentQuestion").GetProperty("questionId").GetGuid();
        var optionId = state.GetProperty("currentQuestion").GetProperty("options")[0].GetProperty("id").GetGuid();

        var first = await AnswerAsync(participant, sessionId, questionId, optionId);
        var second = await AnswerAsync(participant, sessionId, questionId, optionId);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Apelido_duplicado_na_mesma_sessao_responde_409()
    {
        using var admin = await AdminClientAsync();

        var quiz = await admin.CreateQuizAsync($"Apelido {Guid.NewGuid():N}", questionCount: 1);
        var session = await admin.OpenSessionAsync(quiz.GetProperty("id").GetGuid());
        var pin = session.GetProperty("pin").GetString()!;

        using var anon = fixture.CreateClient();
        await anon.JoinAsync(pin, "Repetido");

        var response = await anon.PostAsJsonAsync("/api/v1/sessions/join", new { pin, nickname = "repetido" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Participante_nao_enxerga_o_estado_de_outra_sessao()
    {
        using var admin = await AdminClientAsync();

        var quiz = await admin.CreateQuizAsync($"Isolamento {Guid.NewGuid():N}", questionCount: 1);
        var first = await admin.OpenSessionAsync(quiz.GetProperty("id").GetGuid());
        var otherQuiz = await admin.CreateQuizAsync($"Outra {Guid.NewGuid():N}", questionCount: 1);
        var second = await admin.OpenSessionAsync(otherQuiz.GetProperty("id").GetGuid());

        using var anon = fixture.CreateClient();
        var participant = await anon.JoinAsync(first.GetProperty("pin").GetString()!, "Ana");

        using var participantClient = fixture.CreateClient();
        participantClient.UseBearer(participant.GetProperty("accessToken").GetString()!);

        var response = await participantClient.GetAsync(
            $"/api/v1/sessions/{second.GetProperty("id").GetGuid()}/state");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Iniciar_sessao_ja_encerrada_responde_409()
    {
        using var admin = await AdminClientAsync();

        var quiz = await admin.CreateQuizAsync($"Transição {Guid.NewGuid():N}", questionCount: 1);
        var session = await admin.OpenSessionAsync(quiz.GetProperty("id").GetGuid());
        var sessionId = session.GetProperty("id").GetGuid();

        await admin.PostAsync($"/api/v1/sessions/{sessionId}/start", null);
        await admin.PostAsync($"/api/v1/sessions/{sessionId}/end", null);

        var response = await admin.PostAsync($"/api/v1/sessions/{sessionId}/start", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private async Task<JsonElement> StateAsync(HttpClient admin, Guid sessionId) =>
        await admin.GetFromJsonAsync<JsonElement>($"/api/v1/sessions/{sessionId}/state");

    private async Task<HttpResponseMessage> AnswerAsync(
        JsonElement participant,
        Guid sessionId,
        Guid questionId,
        Guid optionId)
    {
        using var client = fixture.CreateClient();
        client.UseBearer(participant.GetProperty("accessToken").GetString()!);

        return await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/answers",
            new { sessionId, questionId, optionId });
    }
}
