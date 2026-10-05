## Purpose

Define o comportamento transversal da `sage-kahoot-api` como serviço: como ela é configurada, se expõe para diagnóstico, documenta seus endpoints, registra o que acontece, protege-se de abuso e responde a erros de forma previsível para qualquer consumidor.

## ADDED Requirements

### Requirement: Configuração externalizada sem segredos em código
A API SHALL obter toda configuração sensível (connection string do MongoDB, credencial do admin, chave de assinatura do token, origens CORS permitidas) de variáveis de ambiente injetadas por ConfigMap/Secret do Kubernetes. Nenhum valor real de segredo MUST estar presente em arquivos versionados; arquivos de configuração do repositório MUST conter apenas placeholders.

#### Scenario: Variável obrigatória ausente
- **WHEN** a API inicia sem uma variável de configuração obrigatória definida
- **THEN** o processo SHALL falhar na inicialização com log de erro identificando a chave ausente, sem expor o valor de qualquer outra variável

#### Scenario: Configuração completa
- **WHEN** todas as variáveis obrigatórias estão definidas e válidas
- **THEN** a API SHALL iniciar e aceitar requisições

### Requirement: Health checks de liveness e readiness
A API SHALL expor `GET /health/live` e `GET /health/ready` sem autenticação, acessíveis apenas a partir da rede interna do cluster. `live` SHALL refletir apenas se o processo está respondendo; `ready` SHALL refletir também a disponibilidade da conexão com o MongoDB.

#### Scenario: Processo no ar e MongoDB acessível
- **WHEN** um probe chama `GET /health/ready` com o MongoDB respondendo
- **THEN** a API SHALL retornar HTTP 200 com o status agregado `Healthy`

#### Scenario: MongoDB indisponível
- **WHEN** um probe chama `GET /health/ready` e o MongoDB não responde dentro do timeout configurado
- **THEN** a API SHALL retornar HTTP 503 com status `Unhealthy` e a identificação do check que falhou
- **AND** `GET /health/live` SHALL continuar retornando HTTP 200

### Requirement: Documentação OpenAPI navegável
A API SHALL publicar um documento OpenAPI 3 descrevendo todos os endpoints REST, com esquemas de request/response, códigos de erro e o esquema de autenticação Bearer. A UI interativa SHALL estar habilitada em Desenvolvimento e Homologação; em Produção interna sua exposição SHALL ser controlada por flag de configuração, desabilitada por padrão.

#### Scenario: Ambiente de desenvolvimento
- **WHEN** um desenvolvedor acessa a rota da UI de documentação em ambiente de Desenvolvimento
- **THEN** a API SHALL servir a UI com todos os endpoints listados e permitir autorizar chamadas com um token de admin

#### Scenario: Produção com flag desabilitada
- **WHEN** alguém acessa a rota da UI de documentação em Produção com a flag desabilitada
- **THEN** a API SHALL retornar HTTP 404

### Requirement: Respostas de erro padronizadas
A API SHALL responder qualquer erro com `application/problem+json` conforme RFC 7807, contendo `type`, `title`, `status`, `detail` e um `traceId` correlacionável com os logs. Erros de validação SHALL retornar HTTP 400 com a lista de campos inválidos e suas mensagens. Exceções não tratadas SHALL retornar HTTP 500 sem stack trace, nome de tipo interno ou fragmento de query no corpo.

#### Scenario: Payload inválido
- **WHEN** um cliente envia um corpo que viola as regras de validação do endpoint
- **THEN** a API SHALL retornar HTTP 400 com `problem+json` listando cada campo inválido e o motivo

#### Scenario: Falha inesperada
- **WHEN** um handler lança uma exceção não tratada
- **THEN** a API SHALL retornar HTTP 500 com `problem+json` genérico e `traceId`
- **AND** SHALL registrar o erro completo, com stack trace, apenas no log estruturado do servidor

### Requirement: Logs estruturados e correlacionáveis
A API SHALL emitir logs estruturados em JSON para stdout, cada entrada contendo timestamp, nível, mensagem, nome da aplicação, ambiente e `traceId` da requisição. Toda requisição HTTP e toda invocação do hub SHALL produzir uma entrada com rota/método, status e duração. Logs MUST NOT conter credenciais, tokens, connection strings ou o corpo integral de requisições autenticadas.

#### Scenario: Requisição concluída
- **WHEN** uma requisição HTTP termina
- **THEN** a API SHALL emitir uma entrada de log estruturada com rota, método, status, duração em milissegundos e `traceId`

#### Scenario: Tentativa de login
- **WHEN** um login de admin é processado
- **THEN** o log SHALL registrar o resultado (sucesso/falha) e o IP de origem, sem registrar a senha enviada

### Requirement: CORS restrito à rede interna
A API SHALL aceitar requisições cross-origin apenas de origens explicitamente listadas em configuração, permitindo credenciais e os métodos usados pela aplicação. Requisições de origem não listada SHALL ser rejeitadas pelo navegador por ausência dos cabeçalhos de permissão. Curinga `*` MUST NOT ser usado em nenhum ambiente.

#### Scenario: Origem permitida
- **WHEN** o `sage-kahoot-web` publicado em uma origem listada chama a API
- **THEN** a API SHALL responder com os cabeçalhos CORS permitindo aquela origem

#### Scenario: Origem não listada
- **WHEN** uma página em origem não listada chama a API
- **THEN** a resposta MUST NOT conter cabeçalho `Access-Control-Allow-Origin` para aquela origem

### Requirement: Rate limiting em endpoints sensíveis a abuso
A API SHALL aplicar limite de taxa por IP no endpoint de login do admin e no endpoint de entrada em sessão por PIN, com limites configuráveis. Requisições acima do limite SHALL receber HTTP 429 com cabeçalho `Retry-After`.

#### Scenario: Força bruta no login
- **WHEN** um mesmo IP excede o limite configurado de tentativas de login na janela de tempo
- **THEN** a API SHALL retornar HTTP 429 com `Retry-After` para as tentativas seguintes daquele IP na janela

#### Scenario: Varredura de PINs
- **WHEN** um mesmo IP excede o limite configurado de tentativas de entrada por PIN na janela de tempo
- **THEN** a API SHALL retornar HTTP 429 para as tentativas seguintes daquele IP na janela

### Requirement: Ausência de dados pessoais reais
O sistema SHALL persistir apenas apelidos escolhidos pelos participantes no momento da sessão, sem e-mail, documento, matrícula ou qualquer identificador corporativo. Dados de seed e exemplos de documentação MUST ser sintéticos.

#### Scenario: Participante entra na sessão
- **WHEN** um participante entra informando PIN e apelido
- **THEN** o sistema SHALL persistir apenas o apelido, um identificador opaco de participante e os carimbos de tempo da sessão
