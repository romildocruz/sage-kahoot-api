using Sage.Kahoot.Domain.Sessions;
using Sage.Kahoot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Sage.Kahoot.Infrastructure.Mongo;

/// <summary>
/// Acesso às três coleções de D4 e criação idempotente dos índices no startup.
/// Registra serializadores e convenções uma única vez por processo.
/// </summary>
public sealed class MongoContext
{
    public const string QuizzesCollection = "quizzes";
    public const string SessionsCollection = "sessions";
    public const string AnswersCollection = "session_answers";

    private static readonly object RegistrationLock = new();
    private static bool _registered;

    public MongoContext(IOptions<MongoOptions> options)
    {
        RegisterConventionsOnce();

        var settings = MongoClientSettings.FromConnectionString(options.Value.ConnectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);

        Client = new MongoClient(settings);
        Database = Client.GetDatabase(options.Value.Database);
    }

    public IMongoClient Client { get; }

    public IMongoDatabase Database { get; }

    public IMongoCollection<QuizDocument> Quizzes => Database.GetCollection<QuizDocument>(QuizzesCollection);

    public IMongoCollection<SessionDocument> Sessions => Database.GetCollection<SessionDocument>(SessionsCollection);

    public IMongoCollection<AnswerDocument> Answers => Database.GetCollection<AnswerDocument>(AnswersCollection);

    /// <summary>
    /// Cria os índices de D4. Idempotente: reexecutar não altera nada, o que mantém o rollback
    /// livre de intervenção no banco.
    /// </summary>
    public async Task EnsureIndexesAsync(CancellationToken ct)
    {
        await Quizzes.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<QuizDocument>(
                    Builders<QuizDocument>.IndexKeys.Descending(q => q.UpdatedAtUtc),
                    new CreateIndexOptions { Name = "ix_quizzes_updatedAt" }),
                new CreateIndexModel<QuizDocument>(
                    Builders<QuizDocument>.IndexKeys.Text(q => q.Title),
                    new CreateIndexOptions { Name = "ix_quizzes_title_text" })
            ],
            ct);

        var activeStatuses = new BsonArray
        {
            (int)SessionStatus.Aguardando,
            (int)SessionStatus.EmAndamento
        };

        await Sessions.Indexes.CreateManyAsync(
            [
                // Unicidade do PIN apenas entre sessões ativas: libera o PIN para reuso após encerrar.
                new CreateIndexModel<SessionDocument>(
                    Builders<SessionDocument>.IndexKeys.Ascending(s => s.Pin),
                    new CreateIndexOptions<SessionDocument>
                    {
                        Name = "ux_sessions_pin_active",
                        Unique = true,
                        PartialFilterExpression = new BsonDocument(
                            nameof(SessionDocument.Status),
                            new BsonDocument("$in", activeStatuses))
                    }),
                new CreateIndexModel<SessionDocument>(
                    Builders<SessionDocument>.IndexKeys
                        .Ascending(s => s.Status)
                        .Descending(s => s.StartedAtUtc),
                    new CreateIndexOptions { Name = "ix_sessions_status_startedAt" }),
                new CreateIndexModel<SessionDocument>(
                    Builders<SessionDocument>.IndexKeys.Ascending(s => s.LastActivityAtUtc),
                    new CreateIndexOptions { Name = "ix_sessions_lastActivity" }),
                new CreateIndexModel<SessionDocument>(
                    Builders<SessionDocument>.IndexKeys.Ascending(s => s.QuizId),
                    new CreateIndexOptions { Name = "ix_sessions_quizId" })
            ],
            ct);

        await Answers.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<AnswerDocument>(
                    Builders<AnswerDocument>.IndexKeys
                        .Ascending(a => a.SessionId)
                        .Ascending(a => a.QuestionId),
                    new CreateIndexOptions { Name = "ix_answers_session_question" }),
                // Uma resposta por participante por pergunta, garantido pelo banco e não só pelo aplicativo.
                new CreateIndexModel<AnswerDocument>(
                    Builders<AnswerDocument>.IndexKeys
                        .Ascending(a => a.SessionId)
                        .Ascending(a => a.ParticipantId)
                        .Ascending(a => a.QuestionId),
                    new CreateIndexOptions { Name = "ux_answers_session_participant_question", Unique = true })
            ],
            ct);
    }

    /// <summary>Ping usado pelo health check de readiness.</summary>
    public Task PingAsync(CancellationToken ct) =>
        Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct);

    private static void RegisterConventionsOnce()
    {
        lock (RegistrationLock)
        {
            if (_registered)
            {
                return;
            }

            // Guid como UUID padrão (driver 3.x exige registro explícito) e DateTime sempre em UTC.
            BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            BsonSerializer.TryRegisterSerializer(new DateTimeSerializer(DateTimeKind.Utc));

            ConventionRegistry.Register(
                "sage-kahoot",
                new ConventionPack
                {
                    new CamelCaseElementNameConvention(),
                    new IgnoreExtraElementsConvention(true)
                },
                _ => true);

            _registered = true;
        }
    }
}
