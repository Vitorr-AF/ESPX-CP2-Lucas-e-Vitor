# Checkpoint 2 — ExpenseHub

Checkpoint de C# em grupos de até 3 pessoas para construção de uma Application Programming Interface (API) corporativa de reembolsos.

O prazo de entrega é **13 de outubro de 2026**. O grupo deverá implementar autenticação, autorização, fluxo de aprovação e reprovação, pagamento simulado, histórico e testes unitários.

## Criar seu repositório

1. Clique em **Use this template**.
2. Selecione **Create a new repository**.
3. Crie um repositório **público** em uma das contas do grupo.
4. Adicione os demais integrantes como colaboradores.
5. Clone o repositório.

Não use fork. As issues permanecem neste repositório original como especificação comum da turma.

Os commits serão utilizados para avaliar a participação. Todos os membros do grupo
devem possuir mais de um commit no repositório.

## Fluxo de trabalho

Para cada issue:

1. leia os critérios no repositório original;
2. crie uma branch com o identificador, por exemplo `i06-ownership`;
3. implemente e valide a feature;
4. abra uma pull request no seu próprio repositório;
5. use um título como `I06 — Ownership e matriz de acesso`;
6. adicione na descrição uma referência completa, como `Racass/checkpoint-csharpracass-expensehub#6`;
7. não use `Closes`, `Fixes` ou `Resolves`, pois a issue original deve permanecer aberta;
8. conclua a auto-revisão e faça o merge.

## Estrutura inicial

```text
sources/
├── ExpenseHub.slnx
├── ExpenseHub.Api/
└── ExpenseHub.UnitTests/
```

A solução começa sem Identity, banco, domínio ou testes funcionais. Toda implementação avaliada deve ser criada por você.

## Comandos

```shell
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

O endpoint inicial `GET /health` existe apenas para confirmar que a aplicação inicia.

## Documentação

- [Enunciado](docs/ENUNCIADO.md)
- [Requisitos e contratos](docs/REQUISITOS.md)
- [Rubrica](docs/RUBRICA.md)
- [Matriz de autorização](docs/MATRIZ-AUTORIZACAO.md)
- [Processo no GitHub](docs/PROCESSO-GITHUB.md)
- [Uso de Inteligência Artificial](docs/USO-DE-IA.md)
- [Regras do pipeline de qualidade](docs/code-quality-rules.md)

## Banco de dados

Você pode utilizar Microsoft SQL Server LocalDB, Oracle Database, SQLite ou outro provider relacional compatível com Entity Framework Core.

A escolha não gera pontos. Documente no README do seu repositório:

- provider e pacote utilizado;
- configuração necessária;
- criação ou atualização do banco;
- como iniciar a aplicação.

Não versione senhas, tokens ou connection strings sensíveis.

## Testes

Somente testes unitários escritos por você entram na nota. Testes de integração, end-to-end ou de interface são permitidos, mas opcionais e sem pontuação.

Os testes unitários devem executar sem banco, rede ou serviço externo.

## Entrega

Entregue:

- URL do repositório público;
- commit Secure Hash Algorithm (SHA) final;
- integração contínua executada;
- documentação atualizada.

O projeto deve compilar sem erros e ser entregue sem warnings para receber a pontuação integral de Qualidade de Código.

---

## Implementação (ExpenseHub)

API ASP.NET Core (.NET 10) com Identity, autenticação bearer, EF Core + SQLite e testes unitários em MSTest.

### Estrutura

```text
sources/ExpenseHub.Api/
├── Domain/           # Expense (máquina de estados), histórico, pagamento, exceções, roles
├── Application/      # Serviços (regras de role/ownership/estado), DTOs, portas, escopos de leitura
├── Infrastructure/   # EF Core (DbContext, repositório), Identity, seed
└── Endpoints/        # Minimal APIs, policies, tratamento de erros (ProblemDetails)
sources/ExpenseHub.UnitTests/   # Testes unitários sem banco, rede ou serviço externo
```

Decisões principais:

- A policy de cada rota só verifica **role**; **ownership e estado** são validados na camada de serviço.
- A visibilidade (`ExpenseVisibility`) é uma `Expression` aplicada na consulta, antes de materializar dados. Recurso fora do escopo retorna `404`.
- Cada transição altera o `Expense`, adiciona o histórico (e o `PaymentRecord`, no pagamento) e faz um único `SaveChanges`: atômico.
- `Expense.Version` é um token de concorrência; corrida entre duas transições resulta em `409`.
- Repetir uma transição retorna `409` e não duplica histórico.

### Banco de dados

- Provider: **SQLite** (`Microsoft.EntityFrameworkCore.Sqlite`).
- Connection string: `ConnectionStrings:Default` em `appsettings.json` (`Data Source=expensehub.db`, arquivo local ignorado pelo Git).
- Criação: o schema é criado automaticamente na inicialização (`Database.EnsureCreated`), junto com roles e categorias. Para recriar do zero, apague `expensehub.db`.

### Como executar

A senha do Admin inicial **não** está no código. Configure com User Secrets (ou variável de ambiente):

```shell
dotnet user-secrets set "Seed:AdminPassword" "<senha-forte>" --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
# opcional: dotnet user-secrets set "Seed:AdminEmail" "outro@email"   (padrão: admin@expensehub.local)
# alternativa: export Seed__AdminPassword="<senha-forte>"

dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

Sem `Seed:AdminPassword`, a aplicação inicia, cria as roles e registra um aviso, mas não cria o Admin. O seed é idempotente: cria só o Admin e as cinco roles.
A senha precisa ter ao menos 8 caracteres, com maiúscula, minúscula, dígito e símbolo (regras padrão do Identity).

Requisições de exemplo para todos os endpoints estão em `sources/ExpenseHub.Api/ExpenseHub.Api.http`.

### Autenticação

`POST /login` devolve um token bearer (`accessToken`). Envie `Authorization: Bearer <token>`. O token carrega as roles do momento do login, então **após alterar roles o usuário deve autenticar novamente**.

### Fluxo de ponta a ponta

1. Admin faz login e atribui roles aos usuários registrados com `PUT /api/admin/users/{id}/roles`.
2. Employee cria, edita e envia (`submit`) o reembolso.
3. Approver (outra pessoa) aprova ou reprova com justificativa.
4. Finance (outra pessoa) registra o pagamento.
5. Todos os envolvidos e Auditor consultam `GET /api/expenses/{id}/history` conforme visibilidade.

### Contrato JSON

- Despesa: `description`, `amount`, `expenseDate` (`yyyy-MM-dd`), `categoryId` (opcional; seed: 1 Travel, 2 Meals, 3 Lodging, 4 Transport, 5 Other).
- Reprovação: `reason`. Roles: `{ "roles": ["Employee", ...] }` (substitui o conjunto).
- Erros: `ProblemDetails` com `400`, `401`, `403`, `404` ou `409`.

### Limitações conhecidas

- Não há migrations versionadas: usa `EnsureCreated`. Para migrations, rode `dotnet ef migrations add Initial` e troque por `MigrateAsync`.
- Tokens já emitidos mantêm as roles antigas até expirarem ou até novo login.
