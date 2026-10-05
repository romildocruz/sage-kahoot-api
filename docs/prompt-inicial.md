# Prompt: Aplicativo de Quiz Interativo estilo Kahoot (uso interno, treinamento corporativo)

## Contexto
Sou arquiteto de software na Sages (consultoria de tecnologia). Preciso construir uma
plataforma interna de quiz gamificado, inspirada no Kahoot!, para uso em treinamentos
corporativos, aberto a participantes de qualquer área. A aplicação roda exclusivamente
dentro do Kubernetes local da empresa, sem exposição à internet pública.

## Funcionalidades (fluxo do Kahoot)
1. Um apresentador (admin/facilitador) cria e gerencia quizzes (perguntas de múltipla
   escolha, tempo limite por pergunta, pontuação).
2. O apresentador inicia uma sessão ao vivo e recebe um código de sessão (PIN curto).
3. Participantes entram via navegador (mobile/desktop) informando o PIN e um apelido —
   sem necessidade de conta/senha (sessão anônima, só para o treinamento).
4. A pergunta atual é exibida na "tela principal" do apresentador (projetor/TV) e,
   simultaneamente, as opções de resposta aparecem no dispositivo do participante.
5. Participantes respondem contra o tempo; pontuação considera acerto + velocidade.
6. Ranking em tempo real é exibido entre perguntas e ao final da sessão.
7. Apresentador controla o avanço das perguntas (start/next/encerrar sessão).

## Requisitos funcionais detalhados
- CRUD de quizzes e perguntas (criar, editar, duplicar, excluir, banco de perguntas reutilizável).
- Suporte a múltipla escolha (mínimo 2, sugestão até 4 opções), 1 resposta correta por pergunta.
- Configuração por pergunta: tempo limite (segundos) e pontuação base.
- Geração de PIN de sessão único e válido apenas enquanto a sessão está ativa.
- Comunicação em tempo real entre apresentador e participantes (troca de pergunta,
  contagem regressiva, revelação de resposta, atualização de ranking).
- Persistência de histórico de sessões (para relatório pós-treinamento: quem participou,
  pontuação final, taxa de acerto por pergunta) — usando dados sintéticos/apelidos, sem PII real.
- Tela de resultado final com ranking (pódio) e exportação simples (CSV/JSON) do relatório.

## Acesso / Autenticação
- **Participante**: sem login — acesso anônimo via PIN da sessão + apelido.
- **Admin/Apresentador**: acesso simples, sem cadastro de usuários nem múltiplos perfis
  (apenas um papel "admin"). Sugestão de implementação: login único com senha/credencial
  fixa configurada via Secret do Kubernetes (ou variável de ambiente), validada por um
  endpoint de login que emite um token (ex.: JWT simples) usado para proteger os
  endpoints administrativos (CRUD de quiz, controle de sessão) na sage-kahoot-api. Sem
  necessidade de fluxo de recuperação de senha, múltiplos admins ou integração com
  AD/SSO nesta primeira versão — manter simples, dado o uso interno e escala pequena.

## Requisitos não funcionais / ambiente
- **Infraestrutura**: Kubernetes local da empresa (namespace: {{NAMESPACE_K8S}}). Sem
  exposição pública — acesso restrito à rede interna (Ingress interno ou ClusterIP +
  port-forward, conforme padrão do cluster).
- **Banco de dados**: MongoDB (justificativa: schema flexível para perguntas com formatos
  variados e sessões com dados semi-estruturados). Executando em instância local ou em
  container Docker no servidor — não provisionar como parte do cluster Kubernetes por
  enquanto; a aplicação deve se conectar via connection string configurável (ConfigMap/Secret).
- **Comunicação real-time**: SignalR, hospedado dentro da própria **sage-kahoot-api**
  (mesmo processo/deployment da API — sem serviço separado para o hub).
- **Escala esperada**: uso interno, turmas de treinamento (dezenas de participantes
  simultâneos por sessão, não milhares) — dimensionar recursos do cluster de forma modesta.
- **Sem dados pessoais reais**: apenas apelidos de sessão; qualquer dado de exemplo/seed
  deve ser sintético.
- **Observabilidade mínima**: logs estruturados (ex.: Serilog) e health checks
  (liveness/readiness via ASP.NET Health Checks) para os pods, compatíveis com o padrão
  de monitoramento já usado no cluster ({{DOMINIO_INTERNO}}).
- **Documentação/testes de API**: Swagger/OpenAPI habilitado na sage-kahoot-api via o
  gerador nativo do FastEndpoints (NSwag), com UI acessível no ambiente interno para
  facilitar testes manuais dos endpoints (por padrão, habilitado ao menos em ambiente
  de desenvolvimento/homologação; decidir com o time se também fica exposto em
  produção interna).

## Stack definida
- **Duas aplicações separadas** (repositórios/projetos distintos, deploys independentes):
  - **sage-kahoot-api**: Backend em **.NET 9 / C#**, usando **FastEndpoints** (estilo
    Minimal API) no lugar de Controllers/MediatR — cada caso de uso implementado como um
    Endpoint próprio (Request/Response/Endpoint), o que já organiza o código em um
    padrão próximo de CQRS (comandos e queries isolados por endpoint, sem necessidade de
    MediatR). **SignalR hub** no mesmo processo da API para real-time. Persistência via
    driver oficial MongoDB.Driver (sem EF Core). **Swagger/OpenAPI** via o suporte nativo
    do FastEndpoints (NSwag). Endpoints administrativos protegidos por autenticação
    simples (login único + token).
  - **sage-kahoot-web**: Frontend em **React** (sugestão: Vite + TypeScript), com duas
    superfícies de UI: tela do apresentador (telão/projetor, com tela de login simples)
    e tela do participante (mobile-first, sem login), consumindo os endpoints REST e o
    hub SignalR do sage-kahoot-api.
- Empacotamento: Docker (um Dockerfile por aplicação), imagens publicadas em {{REGISTRY_IMAGENS}}.
- Deploy: manifests Kubernetes (Deployment, Service, ConfigMap/Secret, Ingress interno)
  ou Helm chart — um deployment por aplicação (sage-kahoot-api e sage-kahoot-web) —
  seguindo o padrão de outros serviços já rodando no cluster da empresa (perguntar se
  houver um padrão/chart-base a seguir).

## O que espero como entrega
1. Proposta de arquitetura (componentes, fluxo de dados, diagrama textual) com premissas,
   trade-offs e alternativas descartadas — incluindo organização dos Endpoints
   (FastEndpoints) por caso de uso, do SignalR Hub no mesmo processo, e do mecanismo de
   autenticação simples do admin.
2. Estrutura de projeto para as duas aplicações:
   - sage-kahoot-api: organização de pastas por feature/caso de uso (ex.:
     Features/Quizzes/CreateQuiz, Features/Sessions/StartSession, cada um com seu
     Endpoint + Request + Response + Validator), além de Domain, Infrastructure/Mongo,
     Hubs, Auth, configuração do Swagger (NSwag) e do SignalR, seguindo boas práticas
     (SOLID/Clean Code).
   - sage-kahoot-web: organização de componentes/páginas React (login do apresentador,
     tela apresentador, tela participante, tela de criação/edição de quiz, ranking), com
     camada de integração à API/SignalR isolada (hooks/services).
3. Modelagem das coleções MongoDB (quizzes, perguntas, sessões, respostas/ranking).
4. Implementação incremental: (a) autenticação simples do admin, (b) CRUD de quiz via
   FastEndpoints na API (com Swagger documentando os endpoints), (c) fluxo de sessão ao
   vivo com SignalR, (d) tela do apresentador (React), (e) tela do participante (React),
   (f) ranking/relatório.
5. Dockerfile(s) para sage-kahoot-api e sage-kahoot-web, e manifests Kubernetes para
   deploy no namespace {{NAMESPACE_K8S}}.
6. Configuração de conexão MongoDB via variável de ambiente/Secret (apontando para
   instância local ou container Docker no servidor, conforme ambiente), e da credencial
   de admin via Secret do Kubernetes.
7. Dados de exemplo (seed) sintéticos para teste (quiz de exemplo com ~5 perguntas).
8. Sinalizar riscos de segurança relevantes (ex.: rate limiting no join por PIN e no
   login do admin, validação de entrada, CORS restrito à rede interna, expiração do
   token de admin, exposição do Swagger em produção interna).

## Restrições
- Não alterar decisões de arquitetura por conta própria sem antes apresentar a proposta.
- Em caso de ambiguidade sobre padrões já existentes no cluster/empresa, perguntar antes
  de assumir (ex.: padrão de Ingress, chart base, registry de imagens, convenção de
  nomenclatura de namespaces).
- Sem credenciais reais em nenhum arquivo — usar placeholders e Secrets do Kubernetes.
- {{NAMESPACE_K8S}}, {{DOMINIO_INTERNO}} e {{REGISTRY_IMAGENS}} serão informados
  posteriormente — assumir placeholders até lá e não travar o desenvolvimento por
  causa deles.