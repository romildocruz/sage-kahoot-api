using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Sage.Kahoot.IntegrationTests.Infrastructure;

/// <summary>Atalhos para os fluxos repetidos nos testes: autenticar, criar quiz, abrir sessão, entrar.</summary>
public static class ApiClientExtensions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<string> LoginAsAdminAsync(this HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = ApiFixture.AdminUsername,
            password = ApiFixture.AdminPassword
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return payload.GetProperty("accessToken").GetString()!;
    }

    public static void UseBearer(this HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public static object BuildQuizPayload(string title, int questionCount = 2) => new
    {
        title,
        description = "Quiz sintético de teste",
        questions = Enumerable.Range(0, questionCount).Select(i => new
        {
            text = $"Pergunta {i + 1}",
            timeLimitSeconds = 20,
            basePoints = 1000,
            options = new object[]
            {
                new { text = "Certa", isCorrect = true },
                new { text = "Errada", isCorrect = false }
            }
        }).ToArray()
    };

    public static async Task<JsonElement> CreateQuizAsync(this HttpClient client, string title, int questionCount = 2)
    {
        var response = await client.PostAsJsonAsync("/api/v1/quizzes", BuildQuizPayload(title, questionCount));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    public static async Task<JsonElement> OpenSessionAsync(this HttpClient client, Guid quizId)
    {
        var response = await client.PostAsJsonAsync("/api/v1/sessions", new { quizId });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    public static async Task<JsonElement> JoinAsync(this HttpClient client, string pin, string nickname)
    {
        var response = await client.PostAsJsonAsync("/api/v1/sessions/join", new { pin, nickname });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }
}
