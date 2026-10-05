## Context

O repositório tem 6 `.csproj` (Domain, Application, Infrastructure, Api, UnitTests, IntegrationTests),
um `Directory.Build.props` central que fixa `TargetFramework`, `LangVersion` e
`TreatWarningsAsErrors=true` para todos eles, e um `Dockerfile` multi-stage que usa
`mcr.microsoft.com/dotnet/sdk:9.0` para build e `mcr.microsoft.com/dotnet/aspnet:9.0` para runtime. Não
existe `global.json` hoje — o build usa o SDK mais recente instalado na máquina. Na máquina onde esta
proposta foi escrita, o SDK 10.0.401 e o runtime ASP.NET Core 10.0.12 já estão instalados, e um build
em `net9.0` atual passa com 0 avisos/0 erros — essa é a baseline de comparação.

Os pacotes do próprio time de ASP.NET Core/.NET (`Microsoft.AspNetCore.*`, `Microsoft.Extensions.*`)
estão hoje pinados em versões `9.0.x`, e os de terceiros (`FastEndpoints*`, `MongoDB.Driver`,
`Testcontainers.MongoDb`, `AspNetCore.HealthChecks.MongoDb`, `Microsoft.IdentityModel.JsonWebTokens`)
em versões próprias que não seguem o número do TFM. Ver proposal.md (Why/What Changes) para a
motivação; este documento cobre apenas o "como".

## Goals / Non-Goals

**Goals:**
- Compilar e rodar os 6 projetos em `net10.0`, com o `Dockerfile` usando as imagens `10.0` do SDK e do
  ASP.NET runtime.
- Manter `TreatWarningsAsErrors=true` ativo durante toda a migração — nenhum aviso novo introduzido
  pelo SDK 10 deve ser silenciado para "fazer passar".
- Sair da migração com as duas suítes de teste (unitária e de integração com Testcontainers) verdes.

**Non-Goals:**
- Mudar comportamento de qualquer endpoint, evento do `QuizHub` ou regra de domínio — por isso esta
  change não tem specs (`skip_specs: true`).
- Adotar novas features de linguagem/runtime do .NET 10 nesta change (ex.: novas APIs). A migração é
  intencionalmente mecânica; adoção de features novas é uma change futura, separada.
- Introduzir `global.json` — o repositório nunca pinou SDK, e esta migração não é o momento de mudar
  essa convenção.
- Atualizar a versão do MongoDB server (`mongo:7.0`, usado localmente e pelo Testcontainers) — é
  ortogonal ao runtime .NET do lado da aplicação.

## Decisions

**TargetFramework e LangVersion centralizados — com correção feita na implementação.** `LangVersion`
só é declarado em `Directory.Build.props`, então sua troca (`13.0`→`14.0`, a versão de C# que acompanha
o .NET 10) é feita em um único lugar. `TargetFramework`, porém, **é redeclarado explicitamente em cada
um dos 6 `.csproj`** (`<TargetFramework>net9.0</TargetFramework>` na própria `PropertyGroup` de cada
projeto) — a suposição inicial deste design, de que nenhum `.csproj` sobrescrevia esse valor, estava
errada: como a declaração do próprio projeto vem depois da importação de `Directory.Build.props` na
ordem de avaliação do MSBuild, ela vence. A implementação editou `TargetFramework` em
`Directory.Build.props` **e** nos 6 `.csproj`, não só no arquivo central.

**LangVersion pinado em `14.0`, não `latest`.** Alternativa considerada: usar `LangVersion=latest` para
acompanhar automaticamente versões futuras do compilador. Rejeitada porque o repositório já pina a
versão exata hoje (`13.0`) — manter o padrão evita que um bump futuro de SDK habilite silenciosamente
novas regras de análise que, com `TreatWarningsAsErrors=true`, quebrariam o build sem relação com a
mudança que o desenvolvedor estava fazendo.

**Pacotes `Microsoft.AspNetCore.*`/`Microsoft.Extensions.*` sobem para a linha `10.0.x`.** Esses
pacotes espelham o shared framework: mantê-los em `9.0.x` sob um projeto `net10.0` gera downgrade
implícito (NU1605) porque o SDK resolve a versão do framework de referência pelo TFM. A versão de patch
exata (ex.: `10.0.x`) é escolhida na implementação, buscando a mesma que o restore resolveria por
padrão para `net10.0`, para ficar alinhada ao runtime 10.0.12 já disponível.

**Dependências de terceiros são revalidadas, não re-pinadas a ciegas.** `FastEndpoints`,
`FastEndpoints.Swagger`, `FastEndpoints.Messaging`, `MongoDB.Driver`, `Testcontainers.MongoDb` e
`Microsoft.IdentityModel.JsonWebTokens` têm ciclos de release próprios; a versão mínima necessária para
suportar `net10.0` deve ser confirmada no NuGet no momento da implementação (`tasks.md`), não travada
nesta proposta — pesquisa feita durante o design indica que já existem versões estáveis compatíveis com
`net10.0` para a maioria desses pacotes.

**`AspNetCore.HealthChecks.MongoDb` pode continuar no build-alvo `net8.0` dele.** Na pesquisa feita para
este design, a última versão estável publicada (`9.0.0`) ainda declara `net8.0`/`netstandard2.1` como
TFMs, sem build nativo para `net10.0`. Isso não bloqueia a migração: um assembly compilado para
`net8.0` roda normalmente sob o runtime `net10.0` (compatibilidade direta para trás do .NET). A
alternativa — trocar de biblioteca de health check — foi descartada por ser desproporcional ao escopo
desta change; ver Risks abaixo.

## Risks / Trade-offs

- **[Risco]** `AspNetCore.HealthChecks.MongoDb` sem build nativo `net10.0` → **Mitigação:** aceitar o
  build `net8.0` (compatibilidade para trás garantida pelo runtime); checar no NuGet, no momento da
  implementação, se já existe uma versão mais nova com TFM `net10.0` nativo.
- **[Risco]** `TreatWarningsAsErrors=true` converte em erro qualquer aviso novo que os analisadores do
  SDK 10 passem a emitir (ex.: novas regras de nullable, APIs marcadas obsoletas) → **Mitigação:**
  compilar logo depois de cada bump incremental (primeiro o TFM, depois cada grupo de pacotes) e tratar
  cada aviso novo individualmente — nunca desabilitar a flag para "destravar" o build.
  **Divergência do code review ADR 0001 a verificar antes de implementar:** nenhuma — este risco é de
  build, não de regra de negócio em endpoint/validator/hub.
- **[Risco]** Conflito de versão transitiva entre `Microsoft.IdentityModel.JsonWebTokens` (hoje
  `8.22.0`, pinado direto) e a versão que `Microsoft.AspNetCore.Authentication.JwtBearer 10.0.x` traz
  por padrão → **Mitigação:** alinhar o pin explícito à versão que o restore resolveria sem o pin, para
  não forçar um downgrade. **Confirmado na implementação:** o risco se manifestou como `NU1605`
  (convertido em erro por `TreatWarningsAsErrors=true`) porque `Application` foi atualizada para
  `8.23.0` mas `Infrastructure` manteve o pin explícito em `8.22.0` — corrigido alinhando os dois
  projetos em `8.23.0`.
- **[Trade-off]** Versões exatas de pacote continuam pinadas (não ranges) — build reprodutível, ao
  custo de exigir bump manual em migrações futuras. Mesmo trade-off já aceito hoje no repositório; não
  é revisado por esta change.

## Migration Plan

1. Atualizar `Directory.Build.props` (`TargetFramework` e `LangVersion`) — ponto único, afeta os 6
   projetos de uma vez.
2. `dotnet restore` + `dotnet build -c Release` no solution. Qualquer erro de resolução de pacote
   (`Microsoft.AspNetCore.*`/`Microsoft.Extensions.*`) é resolvido bump a bump nesse passo, confirmando
   a versão real que o restore pede para `net10.0`.
3. Revalidar e, se necessário, atualizar `FastEndpoints*`, `MongoDB.Driver`, `Testcontainers.MongoDb`,
   `AspNetCore.HealthChecks.MongoDb`, `Microsoft.IdentityModel.JsonWebTokens` — nessa ordem, recompilando
   entre grupos para isolar qual bump introduziu um aviso novo.
4. `dotnet test src/Sage.Kahoot.UnitTests` (sem Docker) e só depois
   `dotnet test src/Sage.Kahoot.IntegrationTests` (exige Docker — valida `Testcontainers.MongoDb` sob o
   novo TFM de ponta a ponta).
5. Atualizar `Dockerfile` (imagens `sdk:10.0` e `aspnet:10.0`) e construir a imagem localmente
   (`docker build .`) como smoke test — sem isso, o passo 2–4 valida o código mas não a imagem publicada.
6. Atualizar menções textuais a ".NET 9" em `README.md` (abertura do arquivo) e em `CLAUDE.md`
   (seção "Padrão arquitetural") para ".NET 10". **Correção feita na implementação:** a suposição
   original de que `CLAUDE.md` não mencionava a versão estava errada — ele cita ".NET 9" na linha 126;
   foi corrigido junto com o `README.md`. `docs/prompt-inicial.md` é o registro histórico do pedido
   original do projeto e não é atualizado por esta nem por nenhuma outra change.

**Rollback:** não há migração de dado nem mudança de contrato — reverter o commit/PR e republicar a
imagem de container da tag anterior é suficiente. A estratégia de deploy já existente
(`Recreate`, 1 réplica, documentada em `deploy/k8s/k8sdeploy.yml`) se aplica normalmente; nenhuma
consideração adicional de rollback é introduzida por esta change.
