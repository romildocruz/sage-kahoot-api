using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sage.Kahoot.IntegrationTests.Infrastructure;

namespace Sage.Kahoot.IntegrationTests.Sessions;

/// <summary>
/// Verifica a garantia de D5: o índice único em session_answers impede duas respostas do mesmo
/// participante para a mesma pergunta, mesmo com envios simultâneos.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ConcurrencyTests(ApiFixture fixture)
{
    [Fact]
    public async Task Respostas_simultaneas_do_mesmo_participante_persistem_exatamente_uma()
    {
        await fixture.WaitUntilReadyAsync();

        using var admin = fixture.CreateClient();
        admin.UseBearer(await admin.LoginAsAdminAsync());

        var quiz = await admin.CreateQuizAsync($"Corrida {Guid.NewGuid():N}", questionCount: 1);
        var session = await admin.OpenSessionAsync(quiz.GetProperty("id").GetGuid());
        var sessionId = session.GetProperty("id").GetGuid();

        using var anon = fixture.CreateClient();
        var participant = await anon.JoinAsync(session.GetProperty("pin").GetString()!, "Ana");
        var token = participant.GetProperty("accessToken").GetString()!;

        await admin.PostAsync($"/api/v1/sessions/{sessionId}/start", null);

        var state = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/sessions/{sessionId}/state");
        var questionId = state.GetProperty("currentQuestion").GetProperty("questionId").GetGuid();
        var optionId = state.GetProperty("currentQuestion").GetProperty("options")[0].GetProperty("id").GetGuid();

        // Dez envios concorrentes da mesma resposta.
        var attempts = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            using var client = fixture.CreateClient();
            client.UseBearer(token);

            return await client.PostAsJsonAsync(
                $"/api/v1/sessions/{sessionId}/answers",
                new { sessionId, questionId, optionId });
        }));

        var accepted = attempts.Count(r => r.StatusCode == HttpStatusCode.OK);
        var rejected = attempts.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, accepted);
        Assert.Equal(9, rejected);

        // E o relatório confirma: uma única resposta registrada para o participante.
        var report = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/reports/sessions/{sessionId}");
        var entry = report.GetProperty("ranking")[0];

        Assert.Equal(1, entry.GetProperty("correctCount").GetInt32() + entry.GetProperty("wrongCount").GetInt32());
    }
}
