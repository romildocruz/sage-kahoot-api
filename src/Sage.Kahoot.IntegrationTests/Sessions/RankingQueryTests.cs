using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sage.Kahoot.IntegrationTests.Infrastructure;

namespace Sage.Kahoot.IntegrationTests.Sessions;

/// <summary>Cobre o requisito "Consulta de ranking sob demanda".</summary>
[Collection(ApiCollection.Name)]
public sealed class RankingQueryTests(ApiFixture fixture)
{
    private async Task<HttpClient> AdminClientAsync()
    {
        await fixture.WaitUntilReadyAsync();
        var client = fixture.CreateClient();
        client.UseBearer(await client.LoginAsAdminAsync());
        return client;
    }

    [Fact]
    public async Task Ranking_durante_a_sessao_vem_marcado_como_nao_final()
    {
        using var admin = await AdminClientAsync();

        var quiz = await admin.CreateQuizAsync($"Ranking {Guid.NewGuid():N}", questionCount: 2);
        var session = await admin.OpenSessionAsync(quiz.GetProperty("id").GetGuid());
        var sessionId = session.GetProperty("id").GetGuid();

        using var anon = fixture.CreateClient();
        await anon.JoinAsync(session.GetProperty("pin").GetString()!, "Ana");
        await anon.JoinAsync(session.GetProperty("pin").GetString()!, "Bruno");

        await admin.PostAsync($"/api/v1/sessions/{sessionId}/start", null);

        var ranking = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/sessions/{sessionId}/ranking");

        Assert.False(ranking.GetProperty("isFinal").GetBoolean());
        Assert.Equal(2, ranking.GetProperty("entries").GetArrayLength());
        Assert.Equal(1, ranking.GetProperty("entries")[0].GetProperty("position").GetInt32());
    }

    [Fact]
    public async Task Ranking_apos_encerramento_vem_marcado_como_final()
    {
        using var admin = await AdminClientAsync();

        var quiz = await admin.CreateQuizAsync($"Ranking final {Guid.NewGuid():N}", questionCount: 1);
        var session = await admin.OpenSessionAsync(quiz.GetProperty("id").GetGuid());
        var sessionId = session.GetProperty("id").GetGuid();

        using var anon = fixture.CreateClient();
        await anon.JoinAsync(session.GetProperty("pin").GetString()!, "Ana");

        await admin.PostAsync($"/api/v1/sessions/{sessionId}/start", null);
        await admin.PostAsync($"/api/v1/sessions/{sessionId}/end", null);

        var ranking = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/sessions/{sessionId}/ranking");

        Assert.True(ranking.GetProperty("isFinal").GetBoolean());
    }

    [Fact]
    public async Task Participante_consulta_o_ranking_da_propria_sessao()
    {
        using var admin = await AdminClientAsync();

        var quiz = await admin.CreateQuizAsync($"Ranking participante {Guid.NewGuid():N}", questionCount: 1);
        var session = await admin.OpenSessionAsync(quiz.GetProperty("id").GetGuid());
        var sessionId = session.GetProperty("id").GetGuid();

        using var anon = fixture.CreateClient();
        var ana = await anon.JoinAsync(session.GetProperty("pin").GetString()!, "Ana");

        using var participante = fixture.CreateClient();
        participante.UseBearer(ana.GetProperty("accessToken").GetString()!);

        var response = await participante.GetAsync($"/api/v1/sessions/{sessionId}/ranking");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Participante_recebe_recusa_ao_consultar_ranking_de_outra_sessao()
    {
        using var admin = await AdminClientAsync();

        var quizA = await admin.CreateQuizAsync($"Sessão A {Guid.NewGuid():N}", questionCount: 1);
        var sessaoA = await admin.OpenSessionAsync(quizA.GetProperty("id").GetGuid());
        var quizB = await admin.CreateQuizAsync($"Sessão B {Guid.NewGuid():N}", questionCount: 1);
        var sessaoB = await admin.OpenSessionAsync(quizB.GetProperty("id").GetGuid());

        using var anon = fixture.CreateClient();
        var ana = await anon.JoinAsync(sessaoA.GetProperty("pin").GetString()!, "Ana");

        using var participante = fixture.CreateClient();
        participante.UseBearer(ana.GetProperty("accessToken").GetString()!);

        var response = await participante.GetAsync(
            $"/api/v1/sessions/{sessaoB.GetProperty("id").GetGuid()}/ranking");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Ranking_sem_token_responde_401()
    {
        await fixture.WaitUntilReadyAsync();
        using var client = fixture.CreateClient();

        var response = await client.GetAsync($"/api/v1/sessions/{Guid.NewGuid()}/ranking");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ranking_de_sessao_inexistente_responde_404()
    {
        using var admin = await AdminClientAsync();

        var response = await admin.GetAsync($"/api/v1/sessions/{Guid.NewGuid()}/ranking");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
