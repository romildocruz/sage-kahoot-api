## ADDED Requirements

### Requirement: Origem das perguntas de um quiz
Um quiz SHALL poder ser montado tanto com perguntas escritas diretamente nele quanto com perguntas
importadas do banco reutilizável. Independentemente da origem, o quiz SHALL ser dono das suas
perguntas: alterações posteriores no banco MUST NOT modificar quizzes já montados, e a abertura de
sessão continua congelando o seu próprio snapshot.

Este requisito registra na baseline o vínculo entre `quiz-management` e a capability
`question-bank` — vínculo que o `prompt-inicial.md` pedia e que a baseline omitia sem qualquer nota.

#### Scenario: Quiz montado com perguntas próprias
- **WHEN** o admin cria um quiz escrevendo as perguntas diretamente
- **THEN** o quiz SHALL ser criado normalmente, sem qualquer dependência do banco

#### Scenario: Quiz montado a partir do banco
- **WHEN** o admin importa perguntas do banco para um quiz
- **THEN** as perguntas SHALL passar a pertencer ao quiz, com identificadores próprios

#### Scenario: Quiz permanece estável após mudança no banco
- **WHEN** uma pergunta do banco que já foi importada é editada ou excluída
- **THEN** o quiz que a importou SHALL continuar apresentando o conteúdo como estava na importação
