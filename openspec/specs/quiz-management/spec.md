## Purpose

Permite que o apresentador monte e mantenha o conteúdo dos treinamentos: quizzes compostos de perguntas de múltipla escolha, com tempo e pontuação configuráveis por pergunta, reaproveitáveis entre turmas por meio de duplicação.

## Requirements

### Requirement: Criação de quiz
A API SHALL permitir ao admin criar um quiz com título obrigatório (1 a 120 caracteres), descrição opcional (até 500 caracteres) e uma lista de perguntas. O quiz criado SHALL receber um identificador próprio e registrar o instante de criação em UTC. Um quiz SHALL poder ser criado sem perguntas e receber perguntas depois.

#### Scenario: Quiz válido criado
- **WHEN** o admin cria um quiz com título válido e perguntas válidas
- **THEN** a API SHALL retornar HTTP 201 com o identificador do quiz e a representação completa do recurso criado

#### Scenario: Título ausente
- **WHEN** o admin cria um quiz sem título ou com título vazio
- **THEN** a API SHALL retornar HTTP 400 indicando que o título é obrigatório e nenhum quiz SHALL ser persistido

### Requirement: Estrutura e validação da pergunta
Cada pergunta SHALL conter enunciado obrigatório (1 a 300 caracteres), entre 2 e 4 opções de resposta, exatamente uma opção marcada como correta, tempo limite em segundos entre 5 e 120, e pontuação base entre 100 e 2000. As opções SHALL ter texto não vazio (até 120 caracteres) e SHALL ser distintas entre si dentro da mesma pergunta. A ordem das perguntas dentro do quiz SHALL ser explícita e estável.

#### Scenario: Nenhuma opção correta
- **WHEN** o admin envia uma pergunta sem nenhuma opção marcada como correta
- **THEN** a API SHALL retornar HTTP 400 indicando que exatamente uma opção correta é exigida

#### Scenario: Mais de uma opção correta
- **WHEN** o admin envia uma pergunta com duas ou mais opções marcadas como corretas
- **THEN** a API SHALL retornar HTTP 400 indicando que exatamente uma opção correta é exigida

#### Scenario: Número de opções fora da faixa
- **WHEN** o admin envia uma pergunta com menos de 2 ou mais de 4 opções
- **THEN** a API SHALL retornar HTTP 400 indicando a faixa permitida

#### Scenario: Tempo limite fora da faixa
- **WHEN** o admin envia uma pergunta com tempo limite menor que 5 ou maior que 120 segundos
- **THEN** a API SHALL retornar HTTP 400 indicando a faixa permitida

### Requirement: Listagem e consulta de quizzes
A API SHALL permitir ao admin listar quizzes com paginação (tamanho de página padrão 20, máximo 100), ordenados por data de atualização decrescente, e filtrar por trecho do título. SHALL permitir também consultar um quiz específico com todas as suas perguntas e opções, incluindo a marcação de qual é a correta.

#### Scenario: Listagem paginada
- **WHEN** o admin solicita a segunda página de quizzes com tamanho de página 20
- **THEN** a API SHALL retornar os quizzes correspondentes, o total de registros e os dados de paginação

#### Scenario: Quiz inexistente
- **WHEN** o admin consulta um quiz cujo identificador não existe
- **THEN** a API SHALL retornar HTTP 404

### Requirement: Edição de quiz e perguntas
A API SHALL permitir ao admin alterar título, descrição, o conjunto de perguntas e sua ordem. Toda alteração SHALL revalidar as mesmas regras da criação e atualizar o instante de modificação em UTC.

#### Scenario: Edição válida
- **WHEN** o admin altera o enunciado de uma pergunta e reordena as perguntas de um quiz existente
- **THEN** a API SHALL persistir as alterações e retornar a representação atualizada do quiz

#### Scenario: Edição com pergunta inválida
- **WHEN** o admin altera um quiz introduzindo uma pergunta que viola as regras de validação
- **THEN** a API SHALL retornar HTTP 400 e nenhuma das alterações daquela requisição SHALL ser persistida

### Requirement: Edição de quiz em uso por sessão ativa
A API MUST NOT permitir a edição ou exclusão de um quiz que esteja vinculado a uma sessão em andamento, para que o conteúdo apresentado não mude no meio do treinamento.

#### Scenario: Tentativa de editar quiz em sessão ativa
- **WHEN** o admin tenta editar um quiz vinculado a uma sessão com status `aguardando` ou `em andamento`
- **THEN** a API SHALL retornar HTTP 409 indicando que o quiz está em uso e nenhuma alteração SHALL ser persistida

### Requirement: Duplicação de quiz
A API SHALL permitir ao admin duplicar um quiz existente, criando um novo quiz independente com todas as perguntas e opções copiadas, título derivado do original e novo identificador. Alterações no duplicado MUST NOT afetar o original.

#### Scenario: Duplicação bem-sucedida
- **WHEN** o admin duplica um quiz com 5 perguntas
- **THEN** a API SHALL retornar HTTP 201 com um novo quiz contendo as mesmas 5 perguntas, identificador distinto e título indicando que é uma cópia

#### Scenario: Independência da cópia
- **WHEN** o admin edita uma pergunta do quiz duplicado
- **THEN** o quiz original SHALL permanecer inalterado

### Requirement: Exclusão de quiz
A API SHALL permitir ao admin excluir um quiz não vinculado a sessão ativa. O histórico de sessões já encerradas que usaram aquele quiz SHALL permanecer consultável após a exclusão.

#### Scenario: Exclusão permitida
- **WHEN** o admin exclui um quiz sem sessão ativa vinculada
- **THEN** a API SHALL retornar HTTP 204 e o quiz MUST NOT aparecer em listagens subsequentes

#### Scenario: Relatório de sessão anterior preservado
- **WHEN** o admin consulta o relatório de uma sessão encerrada cujo quiz foi posteriormente excluído
- **THEN** a API SHALL retornar o relatório completo, incluindo os enunciados das perguntas como estavam na ocasião da sessão

### Requirement: Dados de exemplo sintéticos
O sistema SHALL oferecer um mecanismo de carga inicial opcional, controlado por configuração, que cria um quiz de exemplo com aproximadamente 5 perguntas usando exclusivamente conteúdo sintético. A carga SHALL ser idempotente e MUST NOT sobrescrever quizzes criados pelo usuário.

#### Scenario: Seed em base vazia
- **WHEN** a API inicia com o seed habilitado e nenhum quiz de exemplo presente
- **THEN** o quiz de exemplo SHALL ser criado e ficar disponível na listagem

#### Scenario: Seed reexecutado
- **WHEN** a API reinicia com o seed habilitado e o quiz de exemplo já existente
- **THEN** nenhum quiz duplicado SHALL ser criado
