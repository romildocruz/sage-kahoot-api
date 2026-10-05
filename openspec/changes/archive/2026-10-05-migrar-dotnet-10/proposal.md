## Why

O projeto está em .NET 9, cujo suporte padrão termina antes do .NET 10 (LTS). Migrar para o .NET 10
move o projeto para uma base com suporte de longo prazo, evita ficar numa versão STS perto do fim do
ciclo de patches de segurança, e dá acesso às melhorias de performance e runtime da nova versão. O SDK
10.0.401 e o runtime ASP.NET Core 10.0.12 já estão instalados na máquina de desenvolvimento, então a
migração pode ser validada localmente sem depender de infraestrutura nova.

## What Changes

- Atualizar `<TargetFramework>` de `net9.0` para `net10.0` em `Directory.Build.props` e nos 6 `.csproj`
  do repositório (Domain, Application, Infrastructure, Api, UnitTests, IntegrationTests).
- Atualizar `<LangVersion>` em `Directory.Build.props` para a versão de C# que acompanha o .NET 10.
- Atualizar as imagens base do `Dockerfile` (`mcr.microsoft.com/dotnet/sdk:9.0` →
  `mcr.microsoft.com/dotnet/sdk:10.0`, `mcr.microsoft.com/dotnet/aspnet:9.0` →
  `mcr.microsoft.com/dotnet/aspnet:10.0`).
- Atualizar os pacotes NuGet pinados na família `9.0.x`/`8.x` para as versões equivalentes compatíveis
  com `net10.0` (famílias `Microsoft.AspNetCore.*`, `Microsoft.Extensions.*`, `Microsoft.IdentityModel.*`),
  e revalidar as demais dependências (`FastEndpoints*`, `MongoDB.Driver`, `Testcontainers.MongoDb`,
  `AspNetCore.HealthChecks.MongoDb`) quanto a compatibilidade declarada com `net10.0`.
- Rodar a suíte completa (unitários + integração com Testcontainers) sob o novo TFM antes de considerar
  a migração concluída.
- **Nenhuma mudança de comportamento, contrato HTTP/SignalR ou regra de negócio está prevista.** É uma
  troca de plataforma/tooling: TargetFramework, imagens de build/runtime e versões de pacote.

## Capabilities

### New Capabilities
(nenhuma)

### Modified Capabilities
(nenhuma — troca de plataforma/runtime é detalhe de implementação, não requisito de comportamento do
sistema; esta change declara `skip_specs: true`)

## Impact

- **Código**: todos os `.csproj` do repositório, `Directory.Build.props`, `Dockerfile`.
- **Build/CI**: pipeline de build/publish passa a exigir o SDK 10.0.x (já presente na máquina local;
  precisa ser validado/instalado onde quer que o build de imagem Docker rode).
- **Dependências NuGet**: pacotes `Microsoft.AspNetCore.Authentication.JwtBearer`,
  `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.AspNetCore.SignalR.Client`,
  `Microsoft.Extensions.Hosting[.Abstractions]`, `Microsoft.Extensions.Logging.Abstractions`,
  `Microsoft.Extensions.Options.ConfigurationExtensions`, `Microsoft.Extensions.Options.DataAnnotations`,
  `Microsoft.IdentityModel.JsonWebTokens`, `FastEndpoints`, `FastEndpoints.Swagger`,
  `FastEndpoints.Messaging`, `AspNetCore.HealthChecks.MongoDb`, `MongoDB.Driver`,
  `Testcontainers.MongoDb`.
- **Imagem de container**: nova imagem base (SDK e runtime 10.0) consumida pelo deploy em
  `deploy/k8s/k8sdeploy.yml` — mesma estratégia de deploy (`Recreate`, 1 réplica), sem mudança de
  manifesto necessária além da imagem publicada no registry.
- **Consumidores externos** (`sage-kahoot-web`): nenhum impacto no contrato público REST/SignalR —
  não é necessário reexportar `openapi.json` nem regenerar client.
- **OpenSpec**: nenhuma spec em `openspec/specs/` é criada ou modificada; `skip_specs: true` registrado
  em `.openspec.yaml` desta change.
