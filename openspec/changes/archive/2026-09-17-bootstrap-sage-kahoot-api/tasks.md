> Etapas incrementais da `sage-kahoot-api`. Cada grupo é entregável e verificável isoladamente.
> Referências: `specs/` para o comportamento exigido, `design.md` para a estrutura (D1–D10).
> Os grupos 1–3 são pré-requisito de todos os demais; 4→5→6→7 seguem a ordem de dependência.

## 1. Fundação da solution

- [x] 1.1 Criar `Sage.Kahoot.sln` e os projetos `src/Sage.Kahoot.Domain`, `src/Sage.Kahoot.Application`, `src/Sage.Kahoot.Infrastructure`, `src/Sage.Kahoot.Api` (.NET 9) com as referências na direção definida em D1
- [x] 1.2 Criar `src/Sage.Kahoot.UnitTests` e `src/Sage.Kahoot.IntegrationTests` (xUnit), com Testcontainers apenas no segundo
- [x] 1.3 Adicionar `.gitignore`, `.editorconfig`, `Directory.Build.props` (nullable enabled, warnings as errors, versão da linguagem) e inicializar o repositório git
- [x] 1.4 Criar a árvore de pastas por feature/caso de uso conforme D2 (`Api/Features/<Área>/<CasoDeUso>/` e `Application/<Área>/<CasoDeUso>/`, homônimas), com um arquivo marcador por pasta
- [x] 1.5 Definir `Result<T>`, `DomainError` (com categorias NotFound/Conflict/Validation) e guards em `Domain/Common`
- [x] 1.6 Definir em `Application/Abstractions` as portas de domínio `IClock`, `IPinGenerator`, `ITokenIssuer`, `IQuizRepository`, `ISessionRepository`, `ISessionNotifier`
- [x] 1.7 Adicionar `FastEndpoints` ao `Api` e ao `Application` (o command bus vem no mesmo pacote), registrar `AddFastEndpoints()`/`UseFastEndpoints(...)` com `RoutePrefix = "api/v1"` e apontar a varredura de handlers para o assembly de `Application` (D3)
- [x] 1.8 Implementar a extensão compartilhada de tradução `Result<T>`/`DomainError` → resposta HTTP (`NotFound` → 404, `Conflict` → 409, `Validation` → 400), usada por todos os endpoints
- [x] 1.9 Implementar o `ICommandMiddleware<,>` de correlação/logging e registrá-lo em `AddCommandMiddleware(...)`
- [x] 1.10 Escrever o ADR curto em `docs/` fixando a regra de D3 — Endpoint fino despacha Command/Query, nunca contém regra — e a regra de corte da validação de D2 (forma no `Validator<TRequest>`, estado/domínio no handler); incluir ambos como item fixo de code review
- [x] 1.11 Implementar `SystemClock` em `Infrastructure/Time` e registrar a composição de DI em `Api/Configuration/DependencyInjection`

## 2. Plataforma da API (capability `api-platform`)

- [x] 2.1 Criar as opções tipadas `MongoOptions`, `AuthOptions`, `CorsOptions`, `SessionOptions`, `RateLimitingOptions`, `SwaggerOptions`, `SeedOptions` com validação e `ValidateOnStart` — falha rápida quando faltar variável obrigatória
- [x] 2.2 Escrever `appsettings.json` e `appsettings.Development.json` apenas com defaults não sensíveis e placeholders; nenhum segredo versionado
- [x] 2.3 Configurar Serilog com sink de console JSON e enriquecimento de `TraceId`/`Environment`/`Application`, mais o middleware de log por requisição
- [x] 2.4 Habilitar `c.Errors.UseProblemDetails()` no FastEndpoints e implementar `CorrelationIdMiddleware` + `ExceptionHandlingMiddleware` para as exceções não tratadas, emitindo `application/problem+json` com `traceId` e sem vazar stack trace
- [x] 2.5 Implementar `MongoContext` com registro de serializadores, convenções de camelCase e criação idempotente dos índices de D4 no startup
- [x] 2.6 Configurar health checks `/health/live` (self) e `/health/ready` (ping ao Mongo com timeout curto)
- [x] 2.7 Configurar `FastEndpoints.Swagger` (NSwag) com `SwaggerDocument()`/`UseSwaggerGen()` e esquema de segurança Bearer, habilitado em Development/Staging e condicionado à flag em Production
- [x] 2.8 Configurar CORS por lista explícita de origens (sem curinga) e o rate limiting nativo do ASP.NET Core por IP, aplicado nos endpoints via `Options(x => x.RequireRateLimiting(...))` — sem usar o `Throttle()` embutido (D9)
- [x] 2.9 Teste de integração: API sobe, `/health/ready` retorna 200 com Mongo do Testcontainers e 503 quando o Mongo está indisponível

## 3. Autenticação do admin (capability `admin-auth`)

- [x] 3.1 Implementar `AdminCredentialValidator` com comparação em tempo constante e validação do comprimento mínimo da chave de assinatura no startup
- [x] 3.2 Implementar `JwtTokenIssuer` emitindo o token de admin (`role=admin`, `jti`, `iss`, `aud`, `exp` configurável com padrão 8h)
- [x] 3.3 Implementar `AdminLoginCommand` + handler retornando token, expiração e papel; falha genérica que não distingue usuário de senha
- [x] 3.4 Criar `Features/Auth/Login/` (Endpoint + Request + Response + Validator) expondo `POST /api/v1/auth/login` sob rate limiting, delegando ao `AdminLoginCommand`
- [x] 3.5 Configurar autenticação JWT Bearer e as políticas `AdminOnly` e `SessionParticipant`
- [x] 3.6 Implementar a leitura do `access_token` da query string restrita ao caminho do hub (`OnMessageReceived`)
- [x] 3.7 Testes: login correto, senha incorreta (401 genérico), token expirado (401), assinatura inválida (401), rota administrativa sem token (401)

## 4. Gestão de quizzes (capability `quiz-management`)

- [x] 4.1 Modelar `Quiz`, `Question` e `AnswerOption` em `Domain/Quizzes` com as invariantes de conteúdo (2–4 opções, exatamente 1 correta, faixas de tempo e pontuação, ordem explícita)
- [x] 4.2 Implementar `QuizRepository` em `Infrastructure/Mongo` sobre o documento agregado de D4
- [x] 4.3 Implementar `CreateQuiz` e `UpdateQuiz` (command, handler, validator), com bloqueio de edição quando houver sessão ativa vinculada (409)
- [x] 4.4 Implementar `DuplicateQuiz` gerando cópia independente com novo identificador e título derivado
- [x] 4.5 Implementar `DeleteQuiz` com a mesma proteção de sessão ativa, preservando o histórico de sessões encerradas
- [x] 4.6 Implementar `GetQuizById` e `ListQuizzes` (paginação com padrão 20/máximo 100, ordenação por atualização, filtro por título)
- [x] 4.7 Criar os endpoints de `Features/Quizzes/*` (um por caso de uso, cada um despachando seu Command/Query) com as rotas de D8 sob `AdminOnly`, documentados no Swagger
- [x] 4.8 Implementar `SyntheticQuizSeeder` idempotente com quiz de exemplo de ~5 perguntas, controlado por `Seed:Enabled`
- [x] 4.9 Testes unitários das invariantes de validação e testes de integração do CRUD, duplicação, conflito 409 e idempotência do seed

## 5. Sessão ao vivo (capabilities `live-session` e `realtime-hub`)

- [x] 5.1 Modelar `Session`, `SessionStatus`, `Participant`, `SessionQuestion` e a máquina de transições de estado em `Domain/Sessions`
- [x] 5.2 Implementar `Scoring` no domínio: 0 para erro, decaimento linear de 100% a 50% da pontuação base conforme o tempo de resposta, arredondado para inteiro
- [x] 5.3 Implementar `SecurePinGenerator` (6 dígitos, fonte criptográfica) e o índice único parcial do PIN sobre sessões ativas
- [x] 5.4 Implementar `SessionRepository` com o snapshot do quiz, participantes embutidos e as atualizações condicionais/atômicas descritas em D5
- [x] 5.5 Implementar `OpenSessionCommand` (snapshot do quiz, PIN, status `aguardando`; rejeita quiz sem perguntas)
- [x] 5.6 Implementar `JoinSessionCommand`: apelido de 2–20 caracteres sanitizado e único na sessão, limite de participantes, emissão do token de participante, 404 genérico para PIN inválido
- [x] 5.7 Implementar `StartSessionCommand` e `AdvanceQuestionCommand` com abertura/fechamento de pergunta baseados no relógio do servidor e encerramento automático após a última pergunta
- [x] 5.8 Implementar `SubmitAnswerCommand`: aceita apenas a pergunta aberta dentro da janela + tolerância de 500 ms, insert único em `session_answers` (409 na duplicidade) e `$inc` da pontuação acumulada
- [x] 5.9 Implementar o fechamento automático da pergunta por expiração de tempo (temporizador do servidor) e por "todos responderam", com emissão única do evento
- [x] 5.10 Implementar `GetSessionState` e `GetPartialRanking` com o desempate por menor tempo total acumulado e depois por ordem de entrada
- [x] 5.11 Criar o `QuizHub` em `/hubs/quiz` com grupos `session:{id}` e `session:{id}:host` derivados do token, métodos finos despachando os commands pelo command bus (`ExecuteAsync`)
- [x] 5.12 Implementar `SignalRSessionNotifier` (porta `ISessionNotifier`) com todos os eventos servidor→apresentador e servidor→participante de `specs/realtime-hub`, garantindo que o payload do participante não revele a opção correta antes do fechamento
- [x] 5.13 Implementar a sincronização de estado na reconexão (tempo restante calculado pelo servidor) e a reconexão do participante por token
- [x] 5.14 Aplicar limites de tamanho de mensagem e de taxa de invocação por conexão no hub
- [x] 5.15 Criar os endpoints de `Features/Sessions/*` com as rotas REST de D8, despachando exatamente os mesmos Commands/Queries usados pelo `QuizHub`
- [x] 5.16 Implementar `AbandonedSessionSweeper` (BackgroundService) cancelando sessões sem atividade além do período configurado (padrão 4h)
- [x] 5.17 Testes unitários: pontuação nos extremos (imediato/limite/erro/sem resposta), transições válidas e inválidas, desempate do ranking
- [x] 5.18 Teste de integração ponta a ponta: abrir sessão, 3 participantes entram, 2 perguntas respondidas via hub, ranking correto e sessão encerrada
- [x] 5.19 Teste de concorrência: respostas simultâneas do mesmo participante resultam em exatamente um registro persistido

## 6. Relatórios e exportação (capability `session-reporting`)

- [x] 6.1 Implementar `GetSessionReport` recalculando ranking e estatísticas a partir de `session_answers` (fonte de verdade), incluindo acertos, erros, não respondidas e tempo médio
- [x] 6.2 Implementar as estatísticas por pergunta (distribuição por opção, taxa de acerto, tempo médio, não respondidas), tratando pergunta sem nenhuma resposta
- [x] 6.3 Implementar `ListSessions` com paginação, ordenação por início decrescente e filtros por intervalo de datas e status
- [x] 6.4 Implementar `ExportSessionReport` em CSV (UTF-8, cabeçalho, escape de vírgula/aspas/quebra de linha no apelido) e em JSON, com nome de arquivo identificando sessão e data
- [x] 6.5 Criar os endpoints de `Features/Reports/*` com as rotas de D8 sob `AdminOnly`
- [x] 6.6 Testes: ranking final, sessão sem participantes, ranking parcial marcado como não final, escape do CSV, 404 de sessão inexistente, 401 sem token
- [x] 6.7 Teste de consistência: ao fim de uma sessão simulada, `sessions.participants[].totalScore` confere com o recálculo sobre `session_answers`

## 7. Empacotamento e deploy

- [x] 7.1 Criar `Dockerfile` multi-stage (`sdk:9.0` → `aspnet:9.0`), usuário não-root, porta 8080, e `.dockerignore`
- [x] 7.2 Criar `deploy/k8s/k8sdeploy.yml` (arquivo único) com o Deployment de 1 réplica, estratégia `Recreate`, probes de liveness/readiness e requests/limits modestos
- [x] 7.3 Incluir no mesmo arquivo o Service (ClusterIP) e o ConfigMap; o Secret vai em `deploy/k8s/secret.example.yml` (arquivo separado, com a cópia preenchida ignorada pelo git), para que reaplicar os manifests não sobrescreva credenciais reais
- [x] 7.4 Incluir no mesmo arquivo o Ingress interno usando `{{DOMINIO_INTERNO}}`, com comentário sobre a pendência do padrão de Ingress da empresa
- [x] 7.5 Escrever `README.md` com execução local (Mongo em Docker), variáveis de ambiente obrigatórias, execução dos testes e passos de deploy
- [x] 7.6 Documentar em `docs/` o contrato do hub (eventos e payloads) para consumo pelo time do `sage-kahoot-web`

## 8. Fechamento

- [x] 8.1 Revisar cada requirement de `specs/` contra a implementação e registrar a cobertura de testes correspondente
- [x] 8.2 Revisar que nenhum `HandleAsync` de endpoint ou método de hub contém regra de negócio — todos apenas mapeiam e despacham (D3)
- [x] 8.3 Executar a revisão de segurança dos itens sinalizados em `design.md` (rate limiting, CORS, expiração de token, exposição do Swagger, sanitização do apelido, ausência de segredos versionados)
- [ ] 8.4 Validar um fluxo completo em ambiente de homologação com dados sintéticos e registrar os pontos em aberto restantes
      <!-- BLOQUEADA: não há ambiente de homologação nem Docker/MongoDB nesta máquina.
           Depende de: cluster com o namespace informado, Secret criado e imagem publicada.
           Junto com ela ficam pendentes de execução os testes de integração (Testcontainers),
           que estão escritos e compilam — ver docs/cobertura-specs.md, "Lacunas conhecidas". -->
