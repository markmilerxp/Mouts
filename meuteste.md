# Plano de Implementação — Sales API (Revisão Final)

## Contexto geral

Desafio técnico da Ambev para implementar uma **API de Vendas** completa sobre um template .NET 8 com DDD + CQRS.
Prazo: 7 dias corridos. O repositório deve ser público no GitHub com instruções de setup.

---

## Infraestrutura confirmada (docker-compose)

| Serviço | Imagem | Porta | Uso |
|---|---|---|---|
| WebApi | Dockerfile local | 8080 / 8081 | API ASP.NET Core |
| PostgreSQL | postgres:13 | 5432 | Escrita transacional (EF Core) |
| MongoDB | mongo:8.0 | 27017 | Leitura desnormalizada (read model) |
| Redis | redis:7.4.1-alpine | 6379 | Cache distribuído |

Credenciais padrão nos três bancos: `developer` / `ev@luAt10n`

---

## Arquitetura e padrões obrigatórios

### Stack
- .NET 8 / C#, EF Core (PostgreSQL), MongoDB Driver, StackExchange.Redis
- MediatR (CQRS), AutoMapper, FluentValidation
- xUnit + NSubstitute + Bogus (Faker)

### Camadas (Clean Architecture + DDD)

| Camada | Responsabilidade |
|---|---|
| `Domain` | Entidades, Enums, Eventos, Interfaces de repositório, Validators de domínio |
| `Application` | Commands/Queries + Handlers + Validators + Results + Profiles — um folder por caso de uso |
| `ORM` | EF Core: DbContext, configurações, migrations, repositório de escrita (PostgreSQL) |
| `NoSQL` | MongoDB: modelo de leitura (SaleDocument), repositório de leitura |
| `Cache` | Redis via `IDistributedCache`: cache de GetSale e GetSales |
| `WebApi` | Controllers + Request/Response/Validators/Profiles por feature |
| `IoC` | Módulos de inicialização (Application, Infrastructure, WebApi) |

### Padrão External Identities
O README diz: *"to reference entities from other domains, we use the External Identities pattern with denormalization of entity descriptions."*

Isso significa: Customer, Branch e Product pertencem a outros domínios. A venda **não tem FK** para essas entidades — armazena o ID + o nome denormalizado no momento da venda.

```
Sale.CustomerId     + Sale.CustomerName
Sale.BranchId       + Sale.BranchName
SaleItem.ProductId  + SaleItem.ProductName
```

### Formato de erro obrigatório (general-api.md)
A spec define um formato único para todos os erros:

```json
{
  "type": "string",
  "error": "string",
  "detail": "string"
}
```

O middleware atual do template (`ValidationExceptionMiddleware`) usa `{ success, message, errors[] }` — **está incorreto e deve ser corrigido**.

### Regras de negócio — Descontos por quantidade

| Quantidade de itens idênticos | Desconto | Decisão |
|---|---|---|
| 1 a 3 | Sem desconto | Proibido aplicar desconto |
| 4 a 9 | 10% | Seguimos o "4+" da seção estruturada do README |
| 10 a 20 | 20% | |
| Acima de 20 | `DomainException` | Venda não permitida |

> Nota: o README tem uma ambiguidade entre "above 4" (>4) e "4+ items" (>=4). Adotamos "4+" por ser a definição estruturada e mais específica. Documentar essa decisão no README de setup.

### Git Flow e Semantic Commits
Branches:
- `main` → código de produção (protegida)
- `develop` → integração das features
- `feature/nome-da-feature` → criada a partir de `develop`
- `release/x.x.x` → preparação para release, criada a partir de `develop`
- `hotfix/nome-do-fix` → criada a partir de `main`

Commits semânticos (formato: `tipo(escopo): descrição`):
```
feat(sales): add CreateSale command and handler
fix(sales): correct discount calculation for boundary of 4 items
refactor(domain): extract ApplyDiscount to SaleItem entity
test(sales): add unit tests for SaleItem discount rules
chore(docker): add Redis configuration to docker-compose
docs(readme): add setup instructions
```

Tipos: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`, `perf`

---

## Plano de implementação — 9 Fases

---

### FASE 0 — Infraestrutura e configuração base

**Passo 0.1 — Configurar MongoDB no código**
- Adicionar pacote `MongoDB.Driver` ao projeto `ORM` (ou novo projeto `NoSQL`)
- Adicionar `ConnectionStrings:MongoConnection` e `MongoDB:DatabaseName` no `appsettings.json` e nas env vars do docker-compose
- Criar classe `MongoDbContext` com `IMongoDatabase` configurado via DI
- Registrar no `InfrastructureModuleInitializer`

**Passo 0.2 — Configurar Redis no código**
- Adicionar pacote `Microsoft.Extensions.Caching.StackExchangeRedis`
- Adicionar `ConnectionStrings:RedisConnection` no `appsettings.json` e nas env vars do docker-compose
- Registrar `services.AddStackExchangeRedisCache(...)` no `InfrastructureModuleInitializer`
- Criar serviço wrapper `ICacheService` com métodos `GetAsync<T>`, `SetAsync<T>`, `RemoveAsync`

**Passo 0.3 — Corrigir formato de erro (ValidationExceptionMiddleware)**
- Substituir o `ApiResponse { success, message, errors[] }` pelo formato da spec:
  ```json
  { "type": "ValidationError", "error": "Invalid input data", "detail": "..." }
  ```
- Reescrever `ValidationExceptionMiddleware` para produzir esse formato

**Passo 0.4 — Adicionar GlobalExceptionHandlerMiddleware**
- Criar middleware que captura:
  - `DomainException` → HTTP 400, `type: "DomainError"`
  - `KeyNotFoundException` → HTTP 404, `type: "ResourceNotFound"`
  - `InvalidOperationException` → HTTP 400, `type: "BusinessError"`
  - `Exception` genérica → HTTP 500, `type: "InternalError"`
- Todos no formato `{ type, error, detail }`
- Registrar no `Program.cs` **antes** do `ValidationExceptionMiddleware`

---

### FASE 1 — Domain Layer

**Passo 1.1 — Entidade SaleItem**
- Campos: `ProductId (Guid)`, `ProductName (string)`, `Quantity (int)`, `UnitPrice (decimal)`, `Discount (decimal)`, `TotalAmount (decimal)`, `IsCancelled (bool)`
- Método `ApplyDiscount()` com toda a lógica de negócio de desconto
- Herda `BaseEntity`

**Passo 1.2 — Entidade Sale (agregado raiz)**
- Campos: `SaleNumber (string)`, `SaleDate (DateTime)`, `CustomerId (Guid)`, `CustomerName (string)`, `BranchId (Guid)`, `BranchName (string)`, `TotalAmount (decimal)`, `Status (SaleStatus)`, `CreatedAt (DateTime)`, `UpdatedAt (DateTime?)`
- Coleção: `Items (IReadOnlyList<SaleItem>)` — private setter para encapsulamento
- Métodos:
  - `AddItem(...)` — adiciona item, chama `ApplyDiscount()`, chama `Recalculate()`
  - `UpdateItem(...)` — atualiza item existente, recalcula
  - `Cancel()` — muda status para Cancelled, seta `UpdatedAt`
  - `CancelItem(Guid itemId)` — cancela item específico, recalcula
  - `Recalculate()` — soma os `TotalAmount` dos itens não cancelados

**Passo 1.3 — Enum SaleStatus**
- `Unknown = 0` (sentinela, rejeitado pelo validator)
- `Active = 1`
- `Cancelled = 2`

**Passo 1.4 — Validators de domínio**
- `SaleValidator : AbstractValidator<Sale>` — valida campos obrigatórios, status
- `SaleItemValidator : AbstractValidator<SaleItem>` — valida quantidade (1–20), preço positivo

**Passo 1.5 — Interface ISaleRepository (escrita — PostgreSQL)**
```csharp
Task<Sale> CreateAsync(Sale sale, CancellationToken ct);
Task<Sale?> GetByIdAsync(Guid id, CancellationToken ct);
Task<Sale> UpdateAsync(Sale sale, CancellationToken ct);
Task<bool> DeleteAsync(Guid id, CancellationToken ct);
```

**Passo 1.6 — Interface ISaleReadRepository (leitura — MongoDB)**
```csharp
Task UpsertAsync(SaleDocument document, CancellationToken ct);
Task<SaleDocument?> GetByIdAsync(Guid id, CancellationToken ct);
Task DeleteAsync(Guid id, CancellationToken ct);
Task<(IEnumerable<SaleDocument> Items, int Total)> GetPagedAsync(
    int page, int size, string? order,
    IDictionary<string, string>? filters, CancellationToken ct);
```

**Passo 1.7 — Eventos de domínio**
- `SaleCreatedEvent` com os dados da venda
- `SaleModifiedEvent` com os dados atualizados
- `SaleCancelledEvent` com o ID da venda
- `ItemCancelledEvent` com o ID da venda e o ID do item

Publicação: via `ILogger` (log estruturado com Serilog) — sem Message Broker, conforme o README permite.

---

### FASE 2 — Application Layer (CQRS)

Cada caso de uso tem seu próprio folder com: `Command/Query`, `Handler`, `Validator`, `Result`, `Profile`.

**Passo 2.1 — CreateSale**
- `CreateSaleCommand` → `IRequest<CreateSaleResult>`
- Handler:
  1. Valida command
  2. Cria entidade `Sale`, chama `AddItem()` para cada item (desconto aplicado automaticamente)
  3. Persiste no PostgreSQL via `ISaleRepository`
  4. Sincroniza read model no MongoDB via `ISaleReadRepository.UpsertAsync()`
  5. Loga evento `SaleCreatedEvent`
- `CreateSaleResult`: Id da venda criada

**Passo 2.2 — GetSale (por ID)**
- `GetSaleQuery` → `IRequest<GetSaleResult>`
- Handler:
  1. Verifica cache Redis — se hit, retorna deserializado
  2. Se miss, busca no MongoDB via `ISaleReadRepository.GetByIdAsync()`
  3. Se não encontrado, lança `KeyNotFoundException`
  4. Armazena no Redis com TTL e retorna

**Passo 2.3 — GetSales (lista paginada)**
- `GetSalesQuery` com parâmetros: `int Page`, `int Size`, `string? Order`, `IDictionary<string, string>? Filters`
- Handler: consulta MongoDB via `ISaleReadRepository.GetPagedAsync()` com paginação, ordenação e filtros conforme `general-api.md`:
  - `_page`, `_size` → paginação
  - `_order` → ex: `"saleDate desc, customerName asc"`
  - Filtros por campo: `customerName=João*`, `_minSaleDate=2024-01-01`, `_maxTotalAmount=5000`
- Retorna `PaginatedList<GetSaleResult>`

**Passo 2.4 — UpdateSale**
- `UpdateSaleCommand` → `IRequest<UpdateSaleResult>`
- Handler:
  1. Busca `Sale` no PostgreSQL
  2. Atualiza campos e itens via métodos da entidade (recalcula descontos)
  3. Persiste no PostgreSQL
  4. Atualiza read model no MongoDB
  5. Invalida cache Redis
  6. Loga `SaleModifiedEvent`

**Passo 2.5 — DeleteSale**
- `DeleteSaleCommand` → `IRequest<DeleteSaleResponse>`
- Handler: remove do PostgreSQL, remove do MongoDB, invalida Redis

**Passo 2.6 — CancelSale**
- `CancelSaleCommand` → `IRequest<CancelSaleResponse>`
- Handler: busca Sale, chama `sale.Cancel()`, persiste, atualiza MongoDB, invalida Redis, loga `SaleCancelledEvent`

**Passo 2.7 — CancelSaleItem**
- `CancelSaleItemCommand` (SaleId + ItemId) → `IRequest<CancelSaleItemResponse>`
- Handler: busca Sale, chama `sale.CancelItem(itemId)`, persiste, atualiza MongoDB, invalida Redis, loga `ItemCancelledEvent`

---

### FASE 3 — ORM Layer (PostgreSQL — Escrita)

**Passo 3.1 — EF Core Mappings**
- `SaleConfiguration : IEntityTypeConfiguration<Sale>`
  - Tabela `Sales`, PK uuid, propriedades mapeadas, enum como string, `HasMany(s => s.Items)`
- `SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>`
  - Tabela `SaleItems`, FK `SaleId`, propriedades mapeadas

**Passo 3.2 — DefaultContext**
- Adicionar `DbSet<Sale> Sales`
- EF Core faz o cascade via configuração do `HasMany`/`WithOne`

**Passo 3.3 — SaleRepository : ISaleRepository**
- Implementação com EF Core
- `GetByIdAsync` usa `.Include(s => s.Items)` para eager loading

**Passo 3.4 — Migration**
```bash
dotnet ef migrations add AddSalesSchema --project Ambev.DeveloperEvaluation.ORM
dotnet ef database update
```

---

### FASE 4 — NoSQL Layer (MongoDB — Leitura)

**Passo 4.1 — SaleDocument (modelo de leitura)**
- Classe POCO mapeada para coleção MongoDB `sales`
- Campos denormalizados: todos os campos da venda + lista completa de itens
- `[BsonId]` e `[BsonRepresentation(BsonType.String)]` nos Guids

**Passo 4.2 — MongoDbContext**
- Classe que recebe `IMongoDatabase` via DI
- Expõe `IMongoCollection<SaleDocument> Sales`

**Passo 4.3 — SaleReadRepository : ISaleReadRepository**
- `UpsertAsync`: `ReplaceOneAsync` com `IsUpsert = true`
- `GetByIdAsync`: `FindAsync` por `_id`
- `DeleteAsync`: `DeleteOneAsync` por `_id`
- `GetPagedAsync`: constrói `FilterDefinition` e `SortDefinition` dinamicamente a partir dos parâmetros, usa `Skip`/`Limit` para paginação

---

### FASE 5 — Cache Layer (Redis)

**Passo 5.1 — ICacheService**
```csharp
Task<T?> GetAsync<T>(string key, CancellationToken ct);
Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct);
Task RemoveAsync(string key, CancellationToken ct);
```
- Implementação com `IDistributedCache` (StackExchange.Redis)
- Serialização com `System.Text.Json`

**Passo 5.2 — Estratégia de cache**
- Key de GetSale: `"sale:{id}"`
- Key de GetSales: `"sales:page:{page}:size:{size}:order:{order}:filters:{hash}"` — ou simplesmente invalidar tudo com prefixo `"sales:*"` em qualquer escrita
- TTL padrão: 5 minutos (configurável via `appsettings.json`)
- Invalidação: qualquer write (Create/Update/Delete/Cancel) remove as keys relevantes

---

### FASE 6 — WebApi Layer

**Passo 6.1 — Request/Response por feature**
- `CreateSaleRequest` / `CreateSaleResponse`
- `UpdateSaleRequest` / `UpdateSaleResponse`
- `GetSaleResponse` / `GetSalesResponse`
- `CancelSaleResponse`
- `CancelSaleItemResponse`

**Passo 6.2 — Validators de Request**
- `CreateSaleRequestValidator`, `UpdateSaleRequestValidator` com FluentValidation

**Passo 6.3 — Profiles AutoMapper (WebApi)**
- Request → Command
- Result → Response

**Passo 6.4 — SalesController**
Herda `BaseController`. Endpoints:

| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/sales` | Criar venda |
| GET | `/api/sales` | Listar vendas (paginado, filtros, ordenação) |
| GET | `/api/sales/{id}` | Buscar venda por ID |
| PUT | `/api/sales/{id}` | Atualizar venda |
| DELETE | `/api/sales/{id}` | Deletar venda |
| PATCH | `/api/sales/{id}/cancel` | Cancelar venda |
| PATCH | `/api/sales/{id}/items/{itemId}/cancel` | Cancelar item da venda |

---

### FASE 7 — IoC / Registro de dependências

**Passo 7.1 — InfrastructureModuleInitializer (adicionar)**
```csharp
// PostgreSQL
services.AddScoped<ISaleRepository, SaleRepository>();

// MongoDB
services.AddSingleton<IMongoClient>(sp => new MongoClient(config["ConnectionStrings:MongoConnection"]));
services.AddScoped<MongoDbContext>();
services.AddScoped<ISaleReadRepository, SaleReadRepository>();

// Redis
services.AddStackExchangeRedisCache(opt => {
    opt.Configuration = config["ConnectionStrings:RedisConnection"];
});
services.AddScoped<ICacheService, CacheService>();
```

**Passo 7.2 — appsettings.json (adicionar)**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=developer_evaluation;Username=developer;Password=ev@luAt10n",
    "MongoConnection": "mongodb://developer:ev%40luAt10n@localhost:27017",
    "RedisConnection": "localhost:6379,password=ev@luAt10n"
  },
  "MongoDB": {
    "DatabaseName": "developer_evaluation"
  },
  "Cache": {
    "DefaultTtlMinutes": 5
  }
}
```

---

### FASE 8 — Testes

**Passo 8.1 — Unit Tests: Domínio**
- `SaleItemTests`: testar `ApplyDiscount()` para todos os cenários (1–3, 4–9, 10–20, 21+)
- `SaleTests`: testar `Cancel()`, `CancelItem()`, `Recalculate()`, `AddItem()`
- Usar Bogus para gerar dados realistas

**Passo 8.2 — Unit Tests: Handlers (NSubstitute)**
- Mock de `ISaleRepository`, `ISaleReadRepository`, `ICacheService`, `IMapper`, `ILogger`
- Testar cenários de sucesso e de erro (venda não encontrada, item não encontrado, etc.)
- Um arquivo de teste por handler

**Passo 8.3 — Unit Tests: Validators**
- Testar `CreateSaleCommandValidator` e `UpdateSaleCommandValidator`
- Cenários válidos e inválidos (quantidade > 20, preço negativo, campos obrigatórios, etc.)

**Passo 8.4 — Integration Tests**
- Testar endpoints via `WebApplicationFactory`
- Banco de dados em memória ou container de teste (Testcontainers)

---

### FASE 9 — Documentação e Git

**Passo 9.1 — README de setup (raiz do repositório)**
Conteúdo obrigatório:
- Pré-requisitos (Docker, .NET 8 SDK)
- Como clonar e subir o ambiente: `docker-compose up -d`
- Como rodar as migrations: `dotnet ef database update`
- Como rodar a API: `dotnet run --project src/Ambev.DeveloperEvaluation.WebApi`
- Como rodar os testes: `dotnet test`
- Como rodar o coverage report: `./coverage-report.bat` (Windows)
- Decisão de interpretação da regra de desconto ("4+" adotado)
- Decisão de arquitetura MongoDB (read model) + Redis (cache)

**Passo 9.2 — Git Flow na prática**

O repositório GitHub público é onde os avaliadores verão o Git Flow completo:
- Aba **Branches** → todas as feature branches criadas
- Aba **Commits** → histórico de commits semânticos
- Aba **Pull Requests** → cada feature mergeada via PR no develop
- **Tag v1.0.0** → na main ao finalizar tudo

#### Branches por fase do plano

| Branch | Base | Fase(s) que cobre |
|---|---|---|
| `main` | — | Código de produção (protegida) |
| `develop` | `main` | Integração de todas as features |
| `feature/infra-config` | `develop` | Fase 0 — MongoDB, Redis, middleware de erros |
| `feature/sales-domain` | `develop` | Fase 1 — Entidades, enums, eventos, interfaces |
| `feature/sales-orm` | `develop` | Fase 3 — EF Core mappings, repository, migration |
| `feature/sales-nosql` | `develop` | Fase 4 — MongoDB document, SaleReadRepository |
| `feature/sales-cache` | `develop` | Fase 5 — ICacheService, Redis implementation |
| `feature/sales-application` | `develop` | Fase 2 — Todos os commands/queries/handlers |
| `feature/sales-webapi` | `develop` | Fase 6 — Controller, requests, responses |
| `feature/sales-ioc` | `develop` | Fase 7 — Registro de dependências |
| `feature/sales-tests` | `develop` | Fase 8 — Unit e integration tests |
| `release/1.0.0` | `develop` | Fase 9 — README final, ajustes de entrega |

#### Fluxo de trabalho — exemplo para cada feature

```bash
# 1. Sempre partir do develop atualizado
git checkout develop
git pull origin develop

# 2. Criar a branch da feature
git checkout -b feature/sales-domain

# 3. Commits pequenos e semânticos durante o desenvolvimento
git add .
git commit -m "feat(domain): add SaleStatus enum with Unknown sentinel"

git add .
git commit -m "feat(domain): add SaleItem entity with ApplyDiscount business rule"

git add .
git commit -m "feat(domain): add Sale aggregate root with Cancel and CancelItem methods"

git add .
git commit -m "feat(domain): add ISaleRepository and ISaleReadRepository interfaces"

git add .
git commit -m "feat(domain): add SaleCreated, SaleModified, SaleCancelled, ItemCancelled events"

git add .
git commit -m "feat(domain): add SaleValidator and SaleItemValidator"

# 4. Subir a branch e abrir PR no GitHub: feature/sales-domain → develop
git push origin feature/sales-domain
# (abrir Pull Request no GitHub com título e descrição da feature)

# 5. Após aprovação/merge do PR, partir para a próxima feature
git checkout develop
git pull origin develop
git checkout -b feature/sales-orm
```

#### Commits semânticos — referência rápida

| Tipo | Quando usar | Exemplo |
|---|---|---|
| `feat` | Nova funcionalidade | `feat(application): add CreateSale command and handler` |
| `fix` | Correção de bug | `fix(webapi): normalize error format to { type, error, detail }` |
| `refactor` | Melhoria sem mudar comportamento | `refactor(domain): extract discount logic to SaleItem.ApplyDiscount` |
| `test` | Adição ou correção de testes | `test(domain): add unit tests for SaleItem discount tiers` |
| `chore` | Configuração, infra, dependências | `chore(docker): add Redis and MongoDB env vars to docker-compose` |
| `docs` | Documentação | `docs(readme): add setup and configuration instructions` |
| `perf` | Melhoria de performance | `perf(cache): add Redis caching to GetSale query` |

#### Finalizando — merge develop → main e tag

```bash
# Ao terminar todas as features e o README
git checkout develop
git pull origin develop
git checkout -b release/1.0.0

# Ajustes finais se necessário
git commit -m "chore(release): prepare v1.0.0"

# Merge no main
git checkout main
git merge release/1.0.0 --no-ff
git tag v1.0.0
git push origin main --tags

# Merge de volta no develop para sincronizar
git checkout develop
git merge release/1.0.0 --no-ff
git push origin develop
```

---

## Ordem de execução recomendada

```
FASE 0 (Infra + erros)
  → FASE 1 (Domain)
    → FASE 3 (ORM/PostgreSQL)
      → FASE 4 (MongoDB)
        → FASE 5 (Redis/Cache)
          → FASE 2 (Application/CQRS)
            → FASE 6 (WebApi)
              → FASE 7 (IoC)
                → FASE 8 (Testes)
                  → FASE 9 (Docs + Git)
```

A sequência garante que nenhuma camada depende de algo ainda não criado, e que os testes são escritos depois da implementação (podendo fazer TDD invertendo Fase 8 e Fase 2 se preferir).

---

## Checklist final de cobertura dos READMEs

| Requisito | Fonte | Coberto |
|---|---|---|
| CRUD completo de vendas | README.md | Fases 2 + 6 |
| Todos os campos da venda | README.md | Fase 1 (entidade Sale + SaleItem) |
| External Identities pattern | README.md | Fase 1 (IDs + nomes denormalizados) |
| Regras de desconto por quantidade | README.md | Passo 1.1 (ApplyDiscount) |
| Limite de 20 itens | README.md | Passo 1.1 (DomainException) |
| Eventos de domínio (bonus) | README.md | Passo 1.7 + handlers |
| .NET 8 / C# | tech-stack.md | Todo o projeto |
| xUnit | tech-stack.md | Fase 8 |
| PostgreSQL | tech-stack.md | Fase 3 |
| MongoDB | tech-stack.md | Fase 4 |
| MediatR | frameworks.md | Fase 2 (CQRS) |
| AutoMapper | frameworks.md | Profiles em Application + WebApi |
| Rebus | frameworks.md | Descartado — sem broker no docker-compose |
| Bogus/Faker | frameworks.md | Fase 8 (testes) |
| NSubstitute | frameworks.md | Fase 8 (mocks) |
| EF Core | frameworks.md | Fase 3 |
| Paginação `_page` / `_size` | general-api.md | Passo 2.3 + 4.3 |
| Ordenação `_order` | general-api.md | Passo 2.3 + 4.3 |
| Filtros por campo, wildcard, range | general-api.md | Passo 2.3 + 4.3 |
| Formato de erro `{ type, error, detail }` | general-api.md | Passo 0.3 + 0.4 |
| Redis (cache) | docker-compose | Fase 5 |
| Git Flow | overview.md | Fase 9 |
| Semantic Commits | overview.md | Fase 9 |
| README com instruções de setup | README.md | Fase 9 |
| Async programming | overview.md | Todos os handlers são async/await |
| Separação de camadas | overview.md | Clean Architecture aplicada |
