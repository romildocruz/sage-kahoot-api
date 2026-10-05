using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sage.Kahoot.IntegrationTests.Infrastructure;

namespace Sage.Kahoot.IntegrationTests.Platform;

/// <summary>Cobre a spec api-platform: health checks, erros padronizados e proteção das rotas.</summary>
[Collection(ApiCollection.Name)]
public sealed class PlatformTests(ApiFixture fixture)
{
    [Fact]
    public async Task Health_ready_responde_200_com_mongo_acessivel()
    {
        await fixture.WaitUntilReadyAsync();
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_live_responde_200_sem_autenticacao()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Rota_administrativa_sem_token_responde_401()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/v1/quizzes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Rota_de_participante_sem_token_e_processada_normalmente()
    {
        await fixture.WaitUntilReadyAsync();
        using var client = fixture.CreateClient();

        // PIN inexistente: responde 404, e não 401 — a rota é pública.
        var response = await client.PostAsJsonAsync("/api/v1/sessions/join", new { pin = "000000", nickname = "Visitante" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Payload_invalido_responde_400_com_problem_json_listando_os_campos()
    {
        await fixture.WaitUntilReadyAsync();
        using var client = fixture.CreateClient();
        client.UseBearer(await client.LoginAsAdminAsync());

        var response = await client.PostAsJsonAsync("/api/v1/quizzes", new
        {
            title = string.Empty,
            questions = new[]
            {
                new
                {
                    text = "Pergunta",
                    timeLimitSeconds = 1,
                    basePoints = 10,
                    options = new[] { new { text = "Única", isCorrect = true } }
                }
            }
        });

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(problem.TryGetProperty("errors", out var errors));
        Assert.True(errors.GetArrayLength() >= 3);
    }

    [Fact]
    public async Task Senha_incorreta_responde_401_generico()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = ApiFixture.AdminUsername,
            password = "senha-errada"
        });

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // A mensagem não pode indicar qual dos dois campos está errado.
        Assert.Equal("Credenciais inválidas.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Token_com_assinatura_invalida_responde_401()
    {
        using var client = fixture.CreateClient();
        var token = await client.LoginAsAdminAsync();

        // Corrompe a assinatura mantendo o formato do JWT.
        var parts = token.Split('.');
        client.UseBearer($"{parts[0]}.{parts[1]}.assinatura-invalida");

        var response = await client.GetAsync("/api/v1/quizzes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
