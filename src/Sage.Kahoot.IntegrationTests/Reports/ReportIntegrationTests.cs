using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sage.Kahoot.IntegrationTests.Infrastructure;
using Sage.Kahoot.Infrastructure.Mongo;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Sage.Kahoot.IntegrationTests.Reports;

/// <summary>Cobre a spec session-reporting contra o banco real, incluindo a consistência de D4.</summary>
[Collection(ApiCollection.Name)]
public sealed class ReportIntegrationTests(ApiFixture fixture)
{
    private async Task<HttpClient> AdminClientAsync()
    {
        await fixture.WaitUntilReadyAsync();
        var client = fixture.CreateClient();
        client.UseBearer(await client.LoginAsAdminAsync());
        return client;
    }

    [Fact]
    public async Task Relatorio_de_sessao_inexistente_responde_404()
    {
        using var admin = await AdminClientAsync();

        var response = await admin.GetAsync($"/api/v1/reports/sessions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Relatorio_sem_token_responde_401()
    {
        await fixture.WaitUntilReadyAsync();
        using var client = fixture.CreateClient();

        var response = await client.GetAsync($"/api/v1/reports/sessions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sessao_sem_participantes_devolve_ranking_vazio_com_200()
    {
        using var admin = await AdminClientAsync();

        var quiz = await admin.CreateQuizAsync($"Ninguém {Guid.NewGuid():N}", questionCount: 1);
        var session = await admin.OpenSessionAsync(quiz.GetProperty("id").GetGuid());

        var report = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/reports/sessions/{session.GetProperty("id").GetGuid()}");

        Assert.Equal(0, report.GetProperty("ranking").GetArrayLength());
    }

    [Fact]
    public async Task Exportacao_csv_devolve_cabecalho_e_uma_linha_por_participante()
    {
        using var admin = await AdminClientAsync();

        var quiz = await admin.CreateQuizAsync($"Export {Guid.NewGuid():N}", questionCount: 1);
        var session = await admin.OpenSessionAsync(quiz.GetProperty("id").GetGuid());
        var sessionId = session.GetProperty("id").GetGuid();

        using var anon = fixture.CreateClient();
        await anon.JoinAsync(session.GetProperty("pin").GetString()!, "Ana");
        await anon.JoinAsync(session.GetProperty("pin").GetString()!, "Bruno");

        var response = await admin.GetAsync($"/api/v1/reports/sessions/{sessionId}/export?format=csv");
        var csv = await response.Content.ReadAsStringAsync();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("posicao,apelido,pontuacao", lines[0], StringComparison.Ordinal);
        Assert.Equal(3, lines.Length); // cabeçalho + 2 participantes
    }

    [Fact]
    public async Task Formato_de_exportacao_invalido_responde_400()
    {
        using var admin = await AdminClientAsync();

        var quiz = await admin.CreateQuizAsync($"Formato {Guid.NewGuid():N}", questionCount: 1);
        var session = await admin.OpenSessionAsync(quiz.GetProperty("id").GetGuid());

        var response = await admin.GetAsync(
            $"/api/v1/reports/sessions/{session.GetProperty("id").GetGuid()}/export?format=xlsx");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Pontuacao_desnormalizada_confere_com_o_recalculo_sobre_as_respostas()
    {
        using var admin = await AdminClientAsync();

        var quiz = await admin.CreateQuizAsync($"Consistência {Guid.NewGuid():N}", questionCount: 2);
        var session = await admin.OpenSessionAsync(quiz.GetProperty("id").GetGuid());
        var sessionId = session.GetProperty("id").GetGuid();

        using var anon = fixture.CreateClient();
        var participant = await anon.JoinAsync(session.GetProperty("pin").GetString()!, "Ana");
        var token = participant.GetProperty("accessToken").GetString()!;
        var participantId = participant.GetProperty("participantId").GetGuid();

        await admin.PostAsync($"/api/v1/sessions/{sessionId}/start", null);
        await AnswerCurrentAsync(admin, token, sessionId);
        await admin.PostAsync($"/api/v1/sessions/{sessionId}/advance", null);
        await AnswerCurrentAsync(admin, token, sessionId);
        await admin.PostAsync($"/api/v1/sessions/{sessionId}/advance", null);

        // Cache desnormalizado no documento da sessão.
        using var scope = fixture.Services.CreateScope();
        var mongo = scope.ServiceProvider.GetRequiredService<MongoContext>();

        var document = await mongo.Sessions.Find(s => s.Id == sessionId).FirstAsync();
        var cachedScore = document.Participants.First(p => p.Id == participantId).TotalScore;

        // Recálculo a partir da fonte de verdade.
        var report = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/reports/sessions/{sessionId}");
        var recalculated = report.GetProperty("ranking")[0].GetProperty("totalScore").GetInt32();

        Assert.Equal(cachedScore, recalculated);
    }

    private async Task AnswerCurrentAsync(HttpClient admin, string token, Guid sessionId)
    {
        var state = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/sessions/{sessionId}/state");
        var question = state.GetProperty("currentQuestion");
        var questionId = question.GetProperty("questionId").GetGuid();
        var optionId = question.GetProperty("options")[0].GetProperty("id").GetGuid();

        using var client = fixture.CreateClient();
        client.UseBearer(token);

        await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/answers",
            new { sessionId, questionId, optionId });
    }
}
