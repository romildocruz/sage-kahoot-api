## Why

O `docs/prompt-inicial.md` pede *"CRUD de quizzes e perguntas (criar, editar, duplicar, excluir,
**banco de perguntas reutilizável**)"*. O banco nunca foi implementado: perguntas existem apenas
embutidas em um quiz, sem coleção, entidade ou rota própria. A baseline em `openspec/specs/`
herdou a omissão e também não registrava o requisito — ou seja, a lacuna era invisível tanto no
produto quanto na documentação.

A revisão retroativa registrada em `ajustar-divergencias-baseline` identificou isso como o item A,
e a decisão foi implementar o banco (alternativa A1) **em change própria**: sozinho ele é maior que
as três correções daquela change somadas, e misturá-los faria com que nenhum dos quatro itens
pudesse ser revisado com atenção.

O ganho concreto é operacional: hoje, aplicar o mesmo conteúdo em duas turmas exige duplicar o quiz
inteiro ou redigitar as perguntas. Com o banco, o facilitador monta turmas novas a partir de um
acervo curado.

## What Changes

- Nova coleção `question_bank` com perguntas avulsas, independentes de qualquer quiz.
- CRUD completo de pergunta do banco: cadastrar, editar, excluir, listar com paginação, buscar por
  trecho de enunciado e filtrar por etiqueta.
- Importação do banco para um quiz **por cópia** — a pergunta importada recebe novos identificadores
  e passa a pertencer ao quiz, de modo que editar o banco depois não altera quizzes já montados nem
  sessões em andamento.
- Promoção no sentido inverso: transformar uma pergunta que já existe em um quiz em item do acervo.
- Bloqueio de importação em quiz vinculado a sessão ativa, pela mesma razão que já bloqueia edição:
  o conteúdo não pode mudar no meio do treinamento.

Sem **BREAKING**: tudo é aditivo. Quizzes montados com perguntas escritas diretamente continuam
funcionando sem qualquer dependência do banco.

## Capabilities

### New Capabilities
- `question-bank`: acervo de perguntas reutilizáveis independente de quiz — cadastro, curadoria,
  busca, importação para quiz e promoção de quiz para o acervo.

### Modified Capabilities
- `quiz-management`: registra que um quiz pode ser montado com perguntas próprias ou importadas, e
  que em ambos os casos o quiz é dono das suas perguntas.

## Impact

- **Domínio**: nova entidade de pergunta de acervo em `Domain/Quizzes`, reaproveitando as invariantes
  de `Question` e as faixas de `QuizRules`.
- **Persistência**: quarta coleção (`question_bank`) com três índices, criada pelo `MongoInitializer`
  idempotente que já existe; nova porta `IQuestionBankRepository`.
- **Aplicação**: seis casos de uso novos em `Application/QuestionBank/` mais dois de integração com
  quiz (importar, promover).
- **API**: quatro rotas de acervo sob `/api/v1/question-bank` e duas sob `/api/v1/quizzes/...`, todas
  `AdminOnly`.
- **Migração de dados**: nenhuma. A coleção nasce vazia; nada nas coleções existentes muda de formato.
- **Documentação**: `docs/cobertura-specs.md` ganha a nova capability.

## Premissas registradas

1. **Importação por cópia, não por referência.** Detalhada em `design.md` — D1. É o mesmo trade-off
   já aceito no snapshot da sessão.
2. **Etiquetas são texto livre**, não lista fechada. Simplifica a primeira versão; migrar para
   vocabulário controlado depois não quebra contrato.
3. **Importar a mesma pergunta duas vezes é permitido.** O quiz é dono das cópias; impedir duplicata
   exigiria rastrear origem, o que recriaria o acoplamento que a cópia evita.
4. Esta change foi separada de `ajustar-divergencias-baseline` por decisão registrada na task 0.6
   daquela change.
