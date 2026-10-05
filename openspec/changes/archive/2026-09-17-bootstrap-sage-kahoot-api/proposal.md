## Why

Uma empresa precisa de uma plataforma interna de quiz gamificado (estilo Kahoot!) para treinamento corporativo, rodando apenas na rede interna sobre o Kubernetes da empresa. Este repositório (`C:\dev\sage-kahoot-api`) hospeda **somente o backend `sage-kahoot-api`** — o frontend `sage-kahoot-web` será um projeto/repositório separado e está fora do escopo desta change.

Antes de escrever qualquer linha de código, é preciso fixar em especificação: o recorte de capabilities, os contratos REST e SignalR, a modelagem das coleções MongoDB, a organização de camadas sob CQRS e a sequência incremental de entrega. Esta change entrega **apenas documentação de especificação** — nenhum código de aplicação.

## What Changes

- Define a estrutura da solution .NET 9 (`Sage.Kahoot.Api`, `Sage.Kahoot.Application`, `Sage.Kahoot.Domain`, `Sage.Kahoot.Infrastructure` + projetos de teste) e a organização de pastas **por feature/caso de uso**, com Endpoints FastEndpoints finos delegando a Commands/Queries.
- Especifica a autenticação simples do admin: credencial única vinda de Secret/variável de ambiente, endpoint de login emitindo JWT de curta duração, rotas administrativas protegidas por policy.
- Especifica o CRUD de quizzes e perguntas (criar, editar, duplicar, excluir, listar) com regras de validação (2–4 opções, exatamente 1 correta, tempo limite e pontuação base por pergunta).
- Especifica o ciclo de vida da sessão ao vivo: geração de PIN único, join anônimo por PIN + apelido, avanço controlado pelo apresentador, janela de resposta por tempo, cálculo de pontuação por acerto + velocidade.
- Especifica o contrato do hub SignalR hospedado no mesmo processo da API: grupos por sessão, eventos apresentador→participantes e participantes→servidor, payloads e garantias de entrega.
- Especifica persistência e relatório pós-sessão: histórico de sessões, ranking final/pódio, taxa de acerto por pergunta e exportação CSV/JSON.
- Especifica a camada de plataforma da API: Swagger/OpenAPI via o gerador nativo do FastEndpoints (NSwag), health checks liveness/readiness, logs estruturados (Serilog), CORS restrito, rate limiting em login e join, tratamento global de erros (ProblemDetails) e configuração via ConfigMap/Secret.
- Define a modelagem das coleções MongoDB (`quizzes`, `sessions`, `session_answers`) com índices e política de seed sintético.
- Define o plano incremental de implementação em 7 etapas, da fundação ao relatório.

Sem **BREAKING** — projeto greenfield, nenhum consumidor existente.

## Capabilities

### New Capabilities
- `api-platform`: fundação transversal da API — configuração, secrets, Swagger, health checks, logging estruturado, CORS, rate limiting, tratamento de erros, conexão MongoDB e pipeline CQRS.
- `admin-auth`: autenticação do apresentador/admin — login com credencial única, emissão e validação de token, proteção das rotas administrativas.
- `quiz-management`: CRUD de quizzes e perguntas, duplicação, banco de perguntas reutilizável e regras de validação do conteúdo.
- `live-session`: ciclo de vida da sessão ao vivo — criação, PIN, join anônimo, avanço de perguntas, registro de respostas e pontuação.
- `realtime-hub`: contrato de comunicação em tempo real via SignalR entre apresentador, participantes e servidor.
- `session-reporting`: histórico de sessões, ranking final, estatísticas por pergunta e exportação CSV/JSON.

### Modified Capabilities
<!-- Nenhuma: não existem specs publicadas em openspec/specs/ neste repositório. -->

## Impact

- **Repositório**: `C:\dev\sage-kahoot-api` — hoje contém apenas `docs/` e `openspec/`. Esta change adiciona somente artefatos em `openspec/changes/bootstrap-sage-kahoot-api/`.
- **Código afetado**: nenhum nesta change. A implementação posterior criará a solution .NET 9 na raiz (`src/`, `tests/`), `Dockerfile` e `deploy/k8s/`.
- **Dependências previstas**: .NET 9 SDK, `FastEndpoints` 8.3.0 (traz junto o command bus `FastEndpoints.Messaging` e o `FluentValidation`), `FastEndpoints.Swagger` (NSwag 14), `MongoDB.Driver`, `Microsoft.AspNetCore.SignalR` (in-box), `Serilog.AspNetCore`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `AspNetCore.HealthChecks.MongoDb`, `xUnit` + `Testcontainers` (testes de integração).
- **Sistemas externos**: MongoDB em instância local/container Docker no servidor (fora do cluster), acessado por connection string em Secret; Kubernetes interno no namespace `{{NAMESPACE_K8S}}`; registry `{{REGISTRY_IMAGENS}}`; domínio interno `{{DOMINIO_INTERNO}}`.
- **Fora de escopo**: `sage-kahoot-web` (React), SSO/AD, múltiplos admins, exposição pública, escalonamento horizontal do hub (backplane Redis).

## Premissas registradas

Perguntas de escopo foram levantadas e não respondidas; assumido o seguinte, sujeito a revisão:

1. **Change única e completa** cobrindo todas as capabilities do backend, com `tasks.md` organizado em etapas incrementais — em vez de uma change por capability.
2. **Stack HTTP e CQRS**: conforme o `prompt-inicial.md` revisado, **FastEndpoints 8.3.0** (MIT, .NET 8+) substitui Controllers/MediatR, e o Swagger passa a ser gerado por NSwag. Por **instrução explícita do arquiteto, que prevalece sobre a letra do arquivo**, o Endpoint não concentra a lógica: ele monta e despacha um Command/Query tratado por um Handler em `Application`, usando o command bus que já vem no pacote `FastEndpoints`. Motivo de peso: o `QuizHub` precisa executar os mesmos casos de uso. Detalhes e trade-offs em `design.md` — D3.
3. Placeholders `{{NAMESPACE_K8S}}`, `{{DOMINIO_INTERNO}}` e `{{REGISTRY_IMAGENS}}` permanecem não resolvidos e não bloqueiam a especificação.
4. Instância única do pod da API (sem backplane SignalR) dado o porte (dezenas de participantes por sessão).
