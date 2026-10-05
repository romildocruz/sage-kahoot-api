# sage-kahoot-api

Backend da plataforma interna de quiz gamificado, usada em treinamentos corporativos.
.NET 10 + FastEndpoints + SignalR + MongoDB, rodando no Kubernetes interno, sem exposição pública.

O frontend (`sage-kahoot-web`) é um projeto separado e não faz parte deste repositório.

## Estrutura

```
src/
  Sage.Kahoot.Domain/          entidades e regras puras (pontuação, transições de estado)
  Sage.Kahoot.Application/     um caso de uso por pasta: Command/Query + Handler
  Sage.Kahoot.Infrastructure/  MongoDB, JWT, gerador de PIN, workers em segundo plano
  Sage.Kahoot.Api/             Endpoints (FastEndpoints), QuizHub, composição e configuração
  Sage.Kahoot.UnitTests/       domínio e handlers — roda sem Docker
  Sage.Kahoot.IntegrationTests/API + Mongo real via Testcontainers — exige Docker
deploy/k8s/
  k8sdeploy.yml                ConfigMap + Deployment + Service + Ingress (reaplicável)
  secret.example.yml           template do Secret — copie para secret.yml (git-ignorado)
docs/                          ADR e contrato do hub para o time do frontend
```

Dependências: `Api → Infrastructure → Application → Domain`. O compilador garante essa direção.

Leia `docs/adr-0001-endpoint-fino.md` antes de adicionar um endpoint: o `HandleAsync` mapeia e
despacha um Command/Query, e nunca contém regra de negócio.

## Rodando localmente

**1. Suba um MongoDB.** A aplicação não o provisiona:

```bash
docker run -d --name sage-kahoot-mongo -p 27017:27017 mongo:7.0
```

Nas vezes seguintes basta `docker start sage-kahoot-mongo`.

**2. Configure os segredos.** Nenhum valor sensível é versionado — `appsettings.json` traz apenas
placeholders vazios, e o processo falha na inicialização nomeando a chave que faltar.

```bash
cd src/Sage.Kahoot.Api
dotnet user-secrets init
dotnet user-secrets set "Mongo:ConnectionString" "mongodb://localhost:27017"
dotnet user-secrets set "Auth:AdminUsername" "apresentador"
dotnet user-secrets set "Auth:AdminPassword" "<escolha uma senha local>"
dotnet user-secrets set "Auth:SigningKey" "<string com no minimo 32 bytes>"
```

Alternativa por variável de ambiente (note o duplo sublinhado): `Mongo__ConnectionString`,
`Auth__AdminUsername`, `Auth__AdminPassword`, `Auth__SigningKey`, `Cors__AllowedOrigins__0`.

**3. Execute:**

```bash
dotnet run --project src/Sage.Kahoot.Api
```

Swagger em **http://localhost:5000/swagger**. Para outra porta:

```bash
ASPNETCORE_URLS=http://localhost:5200 dotnet run --project src/Sage.Kahoot.Api
```

Em Development o seed sintético cria um quiz de exemplo com 5 perguntas na primeira execução
(`Seed:Enabled` já vem `true` em `appsettings.Development.json`), então `GET /api/v1/quizzes`
já devolve conteúdo.

### Testando rotas administrativas pelo Swagger

Tudo que é `AdminOnly` responde 401 sem token. Para autorizar na UI:

1. `POST /api/v1/auth/login` com o usuário e a senha do passo 2
2. copie o `accessToken` da resposta
3. clique em **Authorize** (canto superior direito) e cole o token — o esquema é Bearer

O token vale 8 horas. As rotas de participante (`/sessions/join`, `/sessions/{id}/answers`) usam
um token diferente, devolvido pelo próprio `join`.

### Verificando

```bash
curl http://localhost:5000/health/live     # 200 enquanto o processo responde
curl http://localhost:5000/health/ready    # 200 só com o MongoDB acessível e os índices criados
```

`ready` responder 503 com `live` em 200 é o comportamento esperado quando o banco está fora.

Se o processo encerrar logo no start com `Configuração inválida: ...`, é o passo 2 incompleto — a
mensagem nomeia exatamente a chave que faltou, sem expor valor nenhum.

## Testes

```bash
dotnet test src/Sage.Kahoot.UnitTests          # domínio e handlers, sem dependência externa
dotnet test src/Sage.Kahoot.IntegrationTests   # exige Docker (Testcontainers sobe um mongo:7.0)
```

Os dois projetos são separados justamente para que a suíte unitária rode em máquina sem Docker.

## Configuração

| Seção | Chave | Origem em produção |
|---|---|---|
| `Mongo` | `ConnectionString` | Secret |
| `Mongo` | `Database`, `HealthCheckTimeoutMs` | ConfigMap |
| `Auth` | `AdminUsername`, `AdminPassword`, `SigningKey` | Secret |
| `Auth` | `Issuer`, `Audience`, `*TokenLifetimeHours` | ConfigMap |
| `Cors` | `AllowedOrigins` | ConfigMap (curinga é recusado) |
| `Session` | `MaxParticipants`, `AnswerToleranceMs`, `AbandonedAfterHours` | ConfigMap |
| `RateLimiting` | limites de login e de join | ConfigMap |
| `Swagger` | `Enabled` | ConfigMap (padrão `false` em produção) |
| `Seed` | `Enabled` | ConfigMap |

`Auth:SigningKey` precisa de no mínimo 32 bytes (HS256); abaixo disso o processo não sobe.

## Deploy

```bash
# 1. Imagem
docker build -t {{REGISTRY_IMAGENS}}/sage-kahoot-api:<tag> .
docker push {{REGISTRY_IMAGENS}}/sage-kahoot-api:<tag>

# 2. Secret (valores reais vindos da infraestrutura; nunca versionar)
kubectl -n {{NAMESPACE_K8S}} create secret generic sage-kahoot-api-secret \
  --from-literal=Mongo__ConnectionString='mongodb://<host>:27017' \
  --from-literal=Auth__AdminUsername='<usuario>' \
  --from-literal=Auth__AdminPassword='<senha>' \
  --from-literal=Auth__SigningKey='<chave >= 32 bytes>'

# 3. Manifests (ConfigMap + Deployment + Service + Ingress em arquivo único)
kubectl -n {{NAMESPACE_K8S}} apply -f deploy/k8s/k8sdeploy.yml
```

O Secret fica em **`deploy/k8s/secret.example.yml`**, fora do `k8sdeploy.yml` de propósito:
incluí-lo com placeholders faria cada `kubectl apply` sobrescrever as credenciais reais por
`PLACEHOLDER`, derrubando a autenticação no primeiro redeploy. Assim, o `k8sdeploy.yml` é livre
para ser reaplicado e o Secret é criado uma vez.

Se preferir partir do arquivo em vez do comando: `cp secret.example.yml secret.yml`, preencha e
aplique a cópia. `secret.yml` está no `.gitignore` — o `git add` o recusa —, enquanto o
`.example` permanece versionado.

Rotação de credencial:

```bash
kubectl -n {{NAMESPACE_K8S}} create secret generic sage-kahoot-api-secret   --from-literal=... --dry-run=client -o yaml | kubectl apply -f -
kubectl -n {{NAMESPACE_K8S}} rollout restart deployment/sage-kahoot-api
```

Rollback: `kubectl -n {{NAMESPACE_K8S}} rollout undo deployment/sage-kahoot-api`. Os índices são
criados de forma idempotente, então não há intervenção no banco.

> **O deploy derruba sessões em andamento.** O SignalR roda sem backplane, então o Deployment é
> fixo em 1 réplica com estratégia `Recreate` — não existe rolling update sem duas instâncias
> simultâneas. Faça deploy fora do horário de treinamento.

`{{NAMESPACE_K8S}}`, `{{DOMINIO_INTERNO}}` e `{{REGISTRY_IMAGENS}}` continuam como placeholders nos
manifests até a infraestrutura informar os valores.

## Segurança

- Nenhuma credencial real em arquivo versionado; tudo por Secret/variável de ambiente.
- Rate limiting por IP no login do admin e na entrada por PIN.
- CORS restrito a origens listadas; curinga é recusado na inicialização.
- Token de admin com 8 horas de validade; token de participante amarrado a uma sessão.
- Apelido sanitizado no servidor antes de persistir (o telão do apresentador é o alvo de XSS).
- Swagger desabilitado por padrão em produção.
- Sem dados pessoais reais: apenas apelidos escolhidos na sessão.

## Documentação

- `docs/prompt-inicial.md` — requisito de origem
- `docs/adr-0001-endpoint-fino.md` — Endpoint → Command/Query → Handler e a regra de corte da validação
- `docs/contrato-hub-signalr.md` — contrato de tempo real para o `sage-kahoot-web`
- `openspec/changes/archive/2026-09-17-bootstrap-sage-kahoot-api/` — proposta, specs de comportamento, design e tasks
