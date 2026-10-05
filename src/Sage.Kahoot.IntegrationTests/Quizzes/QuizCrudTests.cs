using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sage.Kahoot.IntegrationTests.Infrastructure;

namespace Sage.Kahoot.IntegrationTests.Quizzes;

/// <summary>Cobre a spec quiz-management de ponta a ponta contra o MongoDB real.</summary>
[Collection(ApiCollection.Name)]
public sealed class QuizCrudTests(ApiFixture fixture)
{
    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        await fixture.WaitUntilReadyAsync();
        var client = fixture.CreateClient();
        client.UseBearer(await client.LoginAsAdminAsync());
        return client;
    }

    [Fact]
    public async Task Cria_consulta_e_lista_quiz()
    {
        using var client = await AuthenticatedClientAsync();

        var created = await client.CreateQuizAsync($"CRUD {Guid.NewGuid():N}");
        var id = created.GetProperty("id").GetGuid();

        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/v1/quizzes/{id}");
        Assert.Equal(2, fetched.GetProperty("questions").GetArrayLength());

        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/quizzes?page=1&pageSize=20");
        Assert.True(list.GetProperty("totalCount").GetInt64() >= 1);
    }

    [Fact]
    public async Task Quiz_inexistente_responde_404()
    {
        using var client = await AuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/v1/quizzes/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Duplicacao_cria_quiz_independente()
    {
        using var client = await AuthenticatedClientAsync();
        var original = await client.CreateQuizAsync($"Original {Guid.NewGuid():N}", questionCount: 5);
        var originalId = original.GetProperty("id").GetGuid();

        var response = await client.PostAsync($"/api/v1/quizzes/{originalId}/duplicate", null);
        var copy = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEqual(originalId, copy.GetProperty("id").GetGuid());
        Assert.Equal(5, copy.GetProperty("questions").GetArrayLength());
    }

    [Fact]
    public async Task Edicao_de_quiz_com_sessao_ativa_responde_409()
    {
        using var client = await AuthenticatedClientAsync();
        var quiz = await client.CreateQuizAsync($"Em uso {Guid.NewGuid():N}");
        var quizId = quiz.GetProperty("id").GetGuid();

        await client.OpenSessionAsync(quizId);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/quizzes/{quizId}",
            ApiClientExtensions.BuildQuizPayload("Tentativa de alteração"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Exclusao_de_quiz_sem_sessao_ativa_responde_204_e_some_da_listagem()
    {
        using var client = await AuthenticatedClientAsync();
        var quiz = await client.CreateQuizAsync($"Descartável {Guid.NewGuid():N}");
        var quizId = quiz.GetProperty("id").GetGuid();

        var delete = await client.DeleteAsync($"/api/v1/quizzes/{quizId}");
        var fetch = await client.GetAsync($"/api/v1/quizzes/{quizId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, fetch.StatusCode);
    }

    [Fact]
    public async Task Relatorio_de_sessao_sobrevive_a_exclusao_do_quiz_de_origem()
    {
        using var client = await AuthenticatedClientAsync();
        var quiz = await client.CreateQuizAsync($"Efêmero {Guid.NewGuid():N}", questionCount: 1);
        var quizId = quiz.GetProperty("id").GetGuid();

        var session = await client.OpenSessionAsync(quizId);
        var sessionId = session.GetProperty("id").GetGuid();

        await client.PostAsync($"/api/v1/sessions/{sessionId}/start", null);
        await client.PostAsync($"/api/v1/sessions/{sessionId}/advance", null);
        await client.DeleteAsync($"/api/v1/quizzes/{quizId}");

        var report = await client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/sessions/{sessionId}");

        // O snapshot mantém o enunciado como estava na ocasião da sessão.
        Assert.Equal(1, report.GetProperty("questions").GetArrayLength());
        Assert.Equal("Pergunta 1", report.GetProperty("questions")[0].GetProperty("text").GetString());
    }
}
