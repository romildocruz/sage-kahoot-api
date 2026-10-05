## Purpose

Mantém um acervo de perguntas independente de qualquer quiz, para que o facilitador reaproveite
conteúdo entre treinamentos sem reescrever as mesmas perguntas a cada turma.

## ADDED Requirements

### Requirement: Cadastro de pergunta no banco
A API SHALL permitir ao admin cadastrar uma pergunta avulsa no banco, com as mesmas regras de
conteúdo já exigidas de uma pergunta dentro de um quiz: enunciado obrigatório, entre 2 e 4 opções
distintas, exatamente uma correta, tempo limite e pontuação base dentro das faixas vigentes.
A pergunta SHALL receber identificador próprio e registrar os instantes de criação e atualização
em UTC.

#### Scenario: Pergunta válida cadastrada
- **WHEN** o admin cadastra uma pergunta que atende às regras de conteúdo
- **THEN** a API SHALL retornar HTTP 201 com o identificador e a representação da pergunta

#### Scenario: Pergunta sem opção correta
- **WHEN** o admin cadastra uma pergunta sem nenhuma opção marcada como correta
- **THEN** a API SHALL retornar HTTP 400 e nada SHALL ser persistido

### Requirement: Organização e busca do banco
A API SHALL permitir ao admin listar as perguntas do banco com paginação e filtrar por trecho do
enunciado. Perguntas SHALL poder receber etiquetas de texto livre, e a listagem SHALL permitir
filtrar por etiqueta, para que acervos grandes continuem navegáveis.

#### Scenario: Busca por enunciado
- **WHEN** o admin lista o banco filtrando por um trecho de enunciado
- **THEN** a API SHALL retornar apenas as perguntas cujo enunciado contém aquele trecho, com os dados de paginação

#### Scenario: Filtro por etiqueta
- **WHEN** o admin lista o banco filtrando por uma etiqueta
- **THEN** a API SHALL retornar apenas as perguntas que possuem aquela etiqueta

### Requirement: Edição e exclusão de pergunta do banco
A API SHALL permitir ao admin editar e excluir uma pergunta do banco. A operação SHALL afetar
apenas o acervo: quizzes montados a partir daquela pergunta e sessões já abertas MUST NOT ser
alterados.

#### Scenario: Edição não afeta quiz já montado
- **WHEN** o admin edita no banco uma pergunta que já foi importada para um quiz
- **THEN** o enunciado e as opções daquele quiz SHALL permanecer como estavam na importação

#### Scenario: Exclusão não afeta sessão em andamento
- **WHEN** o admin exclui do banco uma pergunta usada por uma sessão em andamento
- **THEN** a sessão SHALL continuar apresentando a pergunta normalmente

### Requirement: Importação do banco para um quiz
A API SHALL permitir ao admin importar uma ou mais perguntas do banco para um quiz. A importação
SHALL ser feita **por cópia**: a pergunta importada recebe novos identificadores dentro do quiz e
passa a ser independente da origem. O quiz de destino SHALL revalidar suas regras após a importação,
e a importação MUST NOT ser permitida em quiz vinculado a sessão ativa.

#### Scenario: Importação bem-sucedida
- **WHEN** o admin importa duas perguntas do banco para um quiz sem sessão ativa
- **THEN** a API SHALL retornar o quiz atualizado com as duas perguntas ao final da ordem existente

#### Scenario: Cópia é independente da origem
- **WHEN** o admin importa uma pergunta e depois edita a versão no banco
- **THEN** a cópia dentro do quiz SHALL permanecer inalterada

#### Scenario: Quiz em uso por sessão ativa
- **WHEN** o admin tenta importar para um quiz vinculado a uma sessão `aguardando` ou `em andamento`
- **THEN** a API SHALL retornar HTTP 409 e nada SHALL ser importado

### Requirement: Promoção de pergunta de quiz para o banco
A API SHALL permitir ao admin promover uma pergunta existente em um quiz para o banco, criando uma
cópia independente no acervo. O quiz de origem MUST NOT ser alterado pela promoção.

#### Scenario: Promoção bem-sucedida
- **WHEN** o admin promove uma pergunta de um quiz para o banco
- **THEN** a API SHALL retornar HTTP 201 com a pergunta criada no banco
- **AND** o quiz de origem SHALL permanecer inalterado

### Requirement: Restrição ao admin
Todas as rotas do banco de perguntas SHALL exigir token de admin válido.

#### Scenario: Acesso sem token
- **WHEN** um cliente sem token chama qualquer rota do banco de perguntas
- **THEN** a API SHALL retornar HTTP 401
