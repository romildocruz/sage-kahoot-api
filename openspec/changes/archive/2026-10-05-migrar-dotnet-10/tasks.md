## 1. Plataforma (TargetFramework e LangVersion)

- [x] 1.1 Atualizar `Directory.Build.props`: `TargetFramework` de `net9.0` para `net10.0` e
      `LangVersion` de `13.0` para `14.0`.
- [x] 1.2 Confirmar que nenhum dos 6 `.csproj` sobrescreve `TargetFramework`/`LangVersion`
      individualmente — **divergência encontrada:** todos os 6 redeclaravam `TargetFramework`
      explicitamente; os 6 foram corrigidos para `net10.0` (ver design.md, Decisions).
- [x] 1.3 `dotnet restore Sage.Kahoot.sln` e registrar todo erro/aviso de resolução de pacote
      (`NU1605` ou similar) que aparecer antes de seguir para a etapa 2. Restore concluído sem
      erro/aviso de downgrade mesmo com os pacotes ainda em `9.0.x`.

## 2. Pacotes do próprio .NET/ASP.NET Core

- [x] 2.1 Atualizar `Microsoft.AspNetCore.Authentication.JwtBearer` (Api) de `9.0.20` para a versão
      `10.0.x` que o restore resolver para `net10.0`. Resolvido para `10.0.12`.
- [x] 2.2 Atualizar `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.Abstractions`,
      `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options.ConfigurationExtensions`,
      `Microsoft.Extensions.Options.DataAnnotations` (Infrastructure) de `9.0.9` para a linha `10.0.x`.
      Todos resolvidos para `10.0.12`.
- [x] 2.3 Atualizar `Microsoft.Extensions.Logging.Abstractions` (Application) de `9.0.9` para a mesma
      linha `10.0.x` usada no passo 2.2, mantendo as duas referências ao pacote sincronizadas.
      Resolvido para `10.0.12`, igual ao de Infrastructure.
- [x] 2.4 Atualizar `Microsoft.AspNetCore.Mvc.Testing` e `Microsoft.AspNetCore.SignalR.Client`
      (IntegrationTests) de `9.0.20` para a linha `10.0.x`. Ambos resolvidos para `10.0.12`.
- [x] 2.5 `dotnet build Sage.Kahoot.sln -c Release` e corrigir todo aviso novo (lembrar que
      `TreatWarningsAsErrors=true` o transforma em erro) antes de seguir para a etapa 3 — nenhum aviso
      pode ser silenciado, apenas corrigido ou justificado individualmente. Build em `net10.0`: 0
      avisos, 0 erros.

## 3. Dependências de terceiros

- [x] 3.1 Verificar no NuGet a versão mínima de `FastEndpoints`, `FastEndpoints.Swagger` (Api) e
      `FastEndpoints.Messaging` (Application) com build declarado para `net10.0`; atualizar as três
      juntas a partir de `8.3.0` (não misturar versões entre os três pacotes do mesmo framework).
      `8.3.0` já é a versão estável mais recente e restaura normalmente sob `net10.0` — sem bump
      necessário.
- [x] 3.2 Verificar e, se necessário, atualizar `MongoDB.Driver` (Infrastructure) a partir de `3.11.2`
      para uma versão com `net10.0` confirmado. Atualizado para `3.12.0`.
- [x] 3.3 Verificar e, se necessário, atualizar `Testcontainers.MongoDb` (IntegrationTests) a partir de
      `4.15.0` para uma versão com `net10.0` confirmado. `4.15.0` já é a versão estável mais recente e
      restaura normalmente sob `net10.0` — sem bump necessário.
- [x] 3.4 Verificar `AspNetCore.HealthChecks.MongoDb` (Api, hoje `9.0.0`/`net8.0`): se já existir versão
      com build nativo `net10.0`, atualizar; caso contrário, manter `9.0.0` e registrar a decisão (ver
      design.md, Risks) — o build `net8.0` continua funcional sob runtime `net10.0`. Confirmado: `9.0.0`
      continua sendo a versão estável mais recente (sem build nativo `net10.0`); mantida como estava,
      conforme previsto no design.
- [x] 3.5 Alinhar `Microsoft.IdentityModel.JsonWebTokens` (Application, hoje `8.22.0`) à versão que
      `Microsoft.AspNetCore.Authentication.JwtBearer 10.0.x` (passo 2.1) traz por padrão, evitando
      downgrade forçado. Atualizado para `8.23.0`.
- [x] 3.6 `dotnet build Sage.Kahoot.sln -c Release` novamente e corrigir qualquer aviso/erro novo
      introduzido por este grupo de pacotes. Primeira tentativa falhou com `NU1605` (ver nota na
      3.5/design.md); corrigido e build ficou limpo: 0 avisos, 0 erros.

## 4. Testes

- [x] 4.1 `dotnet test src/Sage.Kahoot.UnitTests` — suíte de domínio/handlers, sem Docker. 76/76
      testes aprovados sob `net10.0`.
- [x] 4.2 `dotnet test src/Sage.Kahoot.IntegrationTests` — exige Docker; valida
      `Testcontainers.MongoDb` subindo `mongo:7.0` sob o novo TFM de ponta a ponta. Rodado manualmente
      pelo usuário em ambiente com Docker — passou.

## 5. Imagem de container

- [x] 5.1 Atualizar `Dockerfile`: estágio de build para `mcr.microsoft.com/dotnet/sdk:10.0` e estágio
      de runtime para `mcr.microsoft.com/dotnet/aspnet:10.0`.
- [x] 5.2 `docker build .` localmente como smoke test — confirma que a imagem resolve e publica sem
      depender do SDK já instalado na máquina de desenvolvimento. Rodado manualmente pelo usuário em
      ambiente com Docker — passou.

## 6. Documentação

- [x] 6.1 Atualizar a menção a ".NET 9" na linha de abertura do `README.md` para ".NET 10".
- [x] 6.2 Revisar se algum outro texto do repositório (fora de código e specs) cita ".NET 9"
      explicitamente e precisa do mesmo ajuste. Encontrado e corrigido: `CLAUDE.md` (seção "Padrão
      arquitetural", linha 126) — a suposição no design.md de que ele não mencionava a versão estava
      errada. Deixados como estão, por serem registro histórico: `docs/prompt-inicial.md` e os
      changes já arquivados em `openspec/changes/archive/`.
