> Banco de perguntas reutilizável. Referências: `specs/` para o comportamento exigido,
> `design.md` (D1–D4) para as decisões técnicas.
> O grupo 1 é pré-requisito dos demais: extrair o validador de conteúdo antes de criar a segunda
> entidade é o que impede a regra de validação de nascer duplicada.

## 1. Invariantes compartilhadas

- [ ] 1.1 Extrair de `Question.Create` o validador de conteúdo (2–4 opções distintas, exatamente uma correta, faixas de tempo e pontuação) para um ponto único em `Domain/Quizzes`, mantendo `QuizRules` como fonte das faixas
- [ ] 1.2 Fazer `Question.Create` passar a usar o validador extraído, sem mudança de comportamento
- [ ] 1.3 Rodar a suíte unitária e confirmar que os testes de invariante de `QuizTests` passam sem qualquer alteração — se algum precisar mudar, o comportamento mudou e a extração está errada (design.md — riscos)

## 2. Domínio e persistência do acervo

- [ ] 2.1 Modelar `BankQuestion` em `Domain/Quizzes` com enunciado, opções, tempo limite, pontuação base, etiquetas e carimbos de criação/atualização, reutilizando o validador do grupo 1 (design.md — D2)
- [ ] 2.2 Implementar `CloneToQuizQuestion()` em `BankQuestion`, gerando novos identificadores para pergunta e opções (design.md — D1)
- [ ] 2.3 Criar `QuestionBankDocument` e o mapeamento documento↔domínio em `Infrastructure/Mongo`
- [ ] 2.4 Adicionar a coleção `question_bank` ao `MongoContext` com os três índices de D2, na mesma criação idempotente das demais
- [ ] 2.5 Definir a porta `IQuestionBankRepository` em `Application/Abstractions` e implementá-la em `Infrastructure/Mongo`

## 3. Casos de uso do acervo

- [ ] 3.1 Implementar `CreateBankQuestion` (command, handler) aplicando as invariantes de conteúdo
- [ ] 3.2 Implementar `UpdateBankQuestion`, atualizando o carimbo de modificação e sem afetar quizzes já montados
- [ ] 3.3 Implementar `DeleteBankQuestion`, sem afetar quizzes nem sessões em andamento
- [ ] 3.4 Implementar `GetBankQuestionById`
- [ ] 3.5 Implementar `ListBankQuestions` com paginação (padrão 20, máximo 100), filtro por trecho de enunciado e filtro por etiqueta
- [ ] 3.6 Implementar `ImportQuestionsToQuiz`: recebe lista de ids, copia para o quiz ao final da ordem existente, revalida o quiz e recusa com conflito se houver sessão ativa (design.md — D3, D4)
- [ ] 3.7 Implementar `PromoteQuestionToBank`: cria cópia no acervo sem alterar o quiz de origem, com a mesma recusa por sessão ativa

## 4. Superfície HTTP

- [ ] 4.1 Criar `Features/QuestionBank/CreateQuestion/` (Endpoint + Request + Validator) — `POST /api/v1/question-bank`
- [ ] 4.2 Criar `Features/QuestionBank/ListQuestions/` — `GET /api/v1/question-bank` com os filtros `text` e `tag`
- [ ] 4.3 Criar `Features/QuestionBank/GetQuestion/` — `GET /api/v1/question-bank/{questionId}`
- [ ] 4.4 Criar `Features/QuestionBank/UpdateQuestion/` — `PUT /api/v1/question-bank/{questionId}`
- [ ] 4.5 Criar `Features/QuestionBank/DeleteQuestion/` — `DELETE /api/v1/question-bank/{questionId}`
- [ ] 4.6 Criar `Features/Quizzes/ImportQuestions/` — `POST /api/v1/quizzes/{quizId}/questions/import`
- [ ] 4.7 Criar `Features/Quizzes/PromoteQuestion/` — `POST /api/v1/quizzes/{quizId}/questions/{questionId}/promote`
- [ ] 4.8 Aplicar `AdminOnly` a todas as sete rotas e documentá-las no Swagger com os códigos de erro

## 5. Testes

- [ ] 5.1 Unitário: invariantes de conteúdo rejeitam pergunta de acervo sem opção correta, com opções duplicadas e com faixas fora do permitido
- [ ] 5.2 Unitário: `CloneToQuizQuestion` gera identificadores novos em pergunta e opções, preservando o texto
- [ ] 5.3 Unitário: editar a pergunta no acervo não altera a cópia já importada em um quiz
- [ ] 5.4 Unitário: importar em quiz com sessão ativa devolve conflito e nada é importado
- [ ] 5.5 Unitário: promover não altera o quiz de origem
- [ ] 5.6 Integração: CRUD completo do acervo contra o MongoDB real
- [ ] 5.7 Integração: listagem com filtro por enunciado e por etiqueta
- [ ] 5.8 Integração: importar duas perguntas e verificar que entram ao final da ordem do quiz
- [ ] 5.9 Integração: excluir do acervo uma pergunta usada por sessão em andamento e confirmar que a sessão segue apresentando a pergunta
- [ ] 5.10 Integração: rotas do acervo sem token respondem 401

## 6. Fechamento

- [ ] 6.1 Rodar as duas suítes e confirmar que nenhum teste existente regrediu
- [ ] 6.2 Revisar que nenhum `HandleAsync` dos endpoints novos contém regra de negócio (ADR 0001)
- [ ] 6.3 Atualizar `docs/cobertura-specs.md` com a capability `question-bank` e a verificação de cada requisito
- [ ] 6.4 Decidir as Open Questions do `design.md` (seed do acervo, exclusão física × lógica) e registrar o que foi escolhido
