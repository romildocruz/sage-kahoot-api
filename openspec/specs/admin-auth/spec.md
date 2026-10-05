## Purpose

Garante que apenas o apresentador autorizado consiga criar, editar e conduzir quizzes e sessões, usando um mecanismo de autenticação deliberadamente simples — credencial única configurada por Secret e token de curta duração — adequado ao uso interno e à escala pequena.

## Requirements

### Requirement: Login do admin com credencial única
A API SHALL expor um endpoint público de login que recebe usuário e senha e os compara com a credencial única definida em configuração (Secret do Kubernetes). Em sucesso, SHALL retornar um token de acesso, seu instante de expiração e o papel `admin`. Em falha, SHALL retornar HTTP 401 com mensagem genérica que não distingue usuário inexistente de senha incorreta.

#### Scenario: Credencial correta
- **WHEN** um cliente envia usuário e senha iguais aos configurados
- **THEN** a API SHALL retornar HTTP 200 com o token de acesso, o instante de expiração em UTC e o papel `admin`

#### Scenario: Senha incorreta
- **WHEN** um cliente envia o usuário correto com senha incorreta
- **THEN** a API SHALL retornar HTTP 401 com mensagem genérica de credenciais inválidas
- **AND** a resposta MUST NOT indicar qual dos campos está errado

#### Scenario: Comparação resistente a timing
- **WHEN** a senha enviada é comparada com a configurada
- **THEN** a comparação SHALL ser feita em tempo constante, sem curto-circuito no primeiro caractere divergente

### Requirement: Token de acesso assinado e com expiração curta
O token emitido SHALL ser um JWT assinado com chave simétrica vinda de configuração, contendo no mínimo a claim de papel `admin`, um identificador único do token, o emissor, a audiência e a expiração. O tempo de vida SHALL ser configurável, com padrão de 8 horas, cobrindo uma jornada de treinamento sem renovação.

#### Scenario: Token válido em rota protegida
- **WHEN** um cliente chama uma rota administrativa com token válido e não expirado no cabeçalho `Authorization: Bearer`
- **THEN** a API SHALL processar a requisição normalmente

#### Scenario: Token expirado
- **WHEN** um cliente chama uma rota administrativa com token cuja expiração já passou
- **THEN** a API SHALL retornar HTTP 401

#### Scenario: Token com assinatura inválida
- **WHEN** um cliente chama uma rota administrativa com token cuja assinatura não confere com a chave configurada
- **THEN** a API SHALL retornar HTTP 401

### Requirement: Proteção das rotas administrativas
Todas as rotas de gestão de quizzes e de controle de sessão SHALL exigir token válido com papel `admin`. As rotas de participante (entrar por PIN, responder) e os health checks SHALL permanecer públicas.

#### Scenario: Rota administrativa sem token
- **WHEN** um cliente chama uma rota administrativa sem cabeçalho `Authorization`
- **THEN** a API SHALL retornar HTTP 401 sem executar qualquer efeito colateral

#### Scenario: Rota de participante sem token
- **WHEN** um participante chama a rota de entrada por PIN sem token
- **THEN** a API SHALL processar a requisição normalmente

### Requirement: Autenticação do hub em tempo real
As conexões do hub SignalR feitas pelo apresentador SHALL ser autenticadas pelo mesmo token de admin, aceito via query string de acesso do handshake. Invocações de métodos de controle da sessão no hub SHALL ser rejeitadas quando a conexão não tiver o papel `admin`.

#### Scenario: Apresentador conecta com token válido
- **WHEN** o apresentador abre a conexão do hub com token de admin válido
- **THEN** o servidor SHALL aceitar o handshake e associar a conexão ao papel `admin`

#### Scenario: Participante tenta comando de apresentador
- **WHEN** uma conexão sem papel `admin` invoca um método de controle da sessão
- **THEN** o servidor SHALL rejeitar a invocação com erro de autorização e não alterar o estado da sessão

### Requirement: Credencial configurada de forma segura
A senha do admin SHALL ser fornecida exclusivamente por Secret/variável de ambiente e MUST NOT ter valor padrão embutido no código ou em `appsettings` versionado. A chave de assinatura do token SHALL ter comprimento mínimo compatível com o algoritmo usado e a inicialização SHALL falhar se esse mínimo não for atendido.

#### Scenario: Chave de assinatura fraca
- **WHEN** a API inicia com chave de assinatura mais curta que o mínimo exigido
- **THEN** o processo SHALL falhar na inicialização com log de erro descrevendo a exigência, sem registrar o valor da chave
