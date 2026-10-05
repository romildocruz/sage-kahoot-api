using Sage.Kahoot.Application.Abstractions;
using Sage.Kahoot.Domain.Quizzes;
using Microsoft.Extensions.Logging;

namespace Sage.Kahoot.Infrastructure.Seed;

/// <summary>
/// Carga inicial de um quiz de exemplo. Todo o conteúdo é sintético — perguntas genéricas
/// de treinamento, sem qualquer dado real ou pessoal.
/// </summary>
public sealed class SyntheticQuizSeeder(IClock clock, ILogger<SyntheticQuizSeeder> logger)
{
    private const string SeedTitle = "Quiz de exemplo — Boas práticas de TI";

    /// <summary>
    /// Idempotente: se já existe um quiz marcado como seed, não faz nada. Nunca sobrescreve
    /// quizzes criados pelo apresentador.
    /// </summary>
    public async Task SeedAsync(IQuizRepository quizzes, CancellationToken ct)
    {
        if (await quizzes.SeedQuizExistsAsync(ct))
        {
            logger.LogInformation("Seed ignorado: quiz de exemplo já existe.");
            return;
        }

        var questions = BuildQuestions();
        var quiz = Quiz.Create(SeedTitle, "Quiz sintético para validar o fluxo da plataforma.", questions, clock.UtcNow, isSeed: true);

        if (!quiz.IsSuccess)
        {
            logger.LogError("Seed falhou na validação do domínio: {Error}", quiz.Error!.Message);
            return;
        }

        await quizzes.InsertAsync(quiz.Value, ct);
        logger.LogInformation("Quiz de exemplo criado com {Count} perguntas.", questions.Count);
    }

    private static List<Question> BuildQuestions()
    {
        var definitions = new (string Text, int TimeLimit, int Points, (string Text, bool Correct)[] Options)[]
        {
            ("Qual prática reduz o risco de expor credenciais em um repositório?",
                20, 1000,
                [
                    ("Guardar segredos em um cofre e injetá-los por variável de ambiente", true),
                    ("Comitar um arquivo .env com os valores reais", false),
                    ("Escrever a senha direto no código com um comentário", false),
                    ("Enviar a senha por mensagem para a equipe", false)
                ]),
            ("O que um health check de readiness deve refletir?",
                20, 1000,
                [
                    ("Se a aplicação consegue atender requisições, incluindo suas dependências", true),
                    ("Apenas se o processo está em execução", false),
                    ("A quantidade de memória livre no servidor", false),
                    ("O número de usuários conectados", false)
                ]),
            ("Em uma API REST, qual status indica conflito com o estado atual do recurso?",
                15, 1000,
                [
                    ("409", true),
                    ("404", false),
                    ("400", false),
                    ("500", false)
                ]),
            ("Por que logs estruturados são preferíveis a texto livre?",
                25, 1000,
                [
                    ("Permitem consulta e correlação por campo em ferramentas de observabilidade", true),
                    ("Ocupam menos espaço em disco", false),
                    ("São mais bonitos no terminal", false),
                    ("Dispensam a necessidade de níveis de log", false)
                ]),
            ("Qual é o objetivo de um índice único em um banco de dados?",
                20, 1000,
                [
                    ("Garantir no próprio banco que não existam registros duplicados", true),
                    ("Acelerar exclusivamente operações de escrita", false),
                    ("Compactar os dados da coleção", false),
                    ("Substituir a validação na aplicação em todos os casos", false)
                ])
        };

        var questions = new List<Question>(definitions.Length);

        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index];

            var options = definition.Options
                .Select(o => AnswerOption.Create(o.Text, o.Correct).Value)
                .ToList();

            questions.Add(Question.Create(
                definition.Text,
                index,
                definition.TimeLimit,
                definition.Points,
                options).Value);
        }

        return questions;
    }
}
