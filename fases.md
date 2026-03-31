# Fases de Implementação — Sales API

Documento de rastreabilidade da implementação. Registra, fase a fase, o que foi feito, os arquivos modificados e os commits gerados.

---

## FASE 0 — Infraestrutura e configuração base

**Branch:** `feature/infra-config`
**Mergeada em:** `develop`
**Objetivo:** Configurar toda a infraestrutura de dados e padronizar o tratamento de erros antes de começar o domínio.

---

### O que foi feito

#### 1. docker-compose atualizado
O arquivo original tinha problemas:
- As portas dos containers não estavam mapeadas para o host (impossível acessar localmente)
- O WebApi não recebia as connection strings via variáveis de ambiente
- O `version` estava obsoleto e gerava warning

O que foi corrigido:
- Portas mapeadas `host:container` para os três serviços de dados
- `depends_on` adicionado para o WebApi esperar os bancos subirem
- Connection strings injetadas via `environment` no serviço do WebApi
- Atributo `version` removido

#### 2. appsettings.json corrigido e expandido
O arquivo original tinha a connection string do PostgreSQL no formato SQL Server (errado). Foi corrigido para o formato Npgsql e foram adicionadas as strings de conexão do MongoDB e Redis, além de seções de configuração `MongoDB` e `Cache`.

#### 3. MongoDB configurado no código
- Pacote `MongoDB.Driver` adicionado ao projeto `ORM`
- Criado `MongoDbContext` no projeto `ORM` — abstrai o `IMongoDatabase` e expõe `GetCollection<T>()` para os repositórios de leitura que serão criados nas próximas fases

#### 4. Redis configurado no código
- Pacote `Microsoft.Extensions.Caching.StackExchangeRedis` adicionado ao projeto `ORM`
- `AddStackExchangeRedisCache` registrado no `InfrastructureModuleInitializer` com a connection string e o prefixo `DeveloperEvaluation:`

#### 5. Tratamento de erros normalizado
O middleware original (`ValidationExceptionMiddleware`) produzia respostas no formato `{ success, message, errors[] }`, que é **diferente** do que a especificação da API define (`{ type, error, detail }`).

O que foi feito:
- Criado `ApiErrorResponse` — novo DTO de erro no formato correto da spec
- `ValidationExceptionMiddleware` reescrito para usar o novo formato com `type: "ValidationError"`
- Criado `GlobalExceptionHandlerMiddleware` — captura globalmente:
  - `KeyNotFoundException` → HTTP 404, `type: "ResourceNotFound"`
  - `InvalidOperationException` → HTTP 400, `type: "BusinessError"`
  - `DomainException` → HTTP 400, `type: "DomainError"`
  - `UnauthorizedAccessException` → HTTP 401, `type: "AuthenticationError"`
  - `Exception` genérica → HTTP 500, `type: "InternalError"`
- `Program.cs` atualizado para registrar o `GlobalExceptionHandlerMiddleware` antes do `ValidationExceptionMiddleware`

---

### Arquivos modificados

| Arquivo | Tipo de alteração |
|---|---|
| `template/backend/docker-compose.yml` | Modificado |
| `template/backend/src/.../WebApi/appsettings.json` | Modificado |
| `template/backend/src/.../ORM/Ambev.DeveloperEvaluation.ORM.csproj` | Modificado (pacotes adicionados) |
| `template/backend/src/.../ORM/MongoDB/MongoDbContext.cs` | Criado |
| `template/backend/src/.../IoC/ModuleInitializers/InfrastructureModuleInitializer.cs` | Modificado |
| `template/backend/src/.../WebApi/Common/ApiErrorResponse.cs` | Criado |
| `template/backend/src/.../WebApi/Middleware/ValidationExceptionMiddleware.cs` | Modificado |
| `template/backend/src/.../WebApi/Middleware/GlobalExceptionHandlerMiddleware.cs` | Criado |
| `template/backend/src/.../WebApi/Program.cs` | Modificado |

---

### Commits da fase

| Hash | Tipo | Descrição |
|---|---|---|
| `d189a02` | `chore(docker)` | Expose ports, add depends_on and env vars for MongoDB and Redis |
| `80adb23` | `chore(config)` | Fix PostgreSQL connection string and add MongoDB and Redis connection strings |
| `febf695` | `feat(infra)` | Add MongoDB.Driver and Redis packages, create MongoDbContext |
| `6ac0937` | `feat(infra)` | Register MongoDB client, MongoDbContext and Redis distributed cache in IoC |
| `5aadac7` | `fix(webapi)` | Normalize error responses to spec format `{ type, error, detail }` and add GlobalExceptionHandlerMiddleware |
| `4faba85` | merge | Merge `feature/infra-config` into `develop` |

---

### Containers Docker rodando após esta fase

| Container | Imagem | Porta | Finalidade |
|---|---|---|---|
| `ambev_developer_evaluation_database` | postgres:13 | 5432 | Escrita transacional (EF Core) |
| `ambev_developer_evaluation_nosql` | mongo:8.0 | 27017 | Leitura desnormalizada (read model) |
| `ambev_developer_evaluation_cache` | redis:7.4.1-alpine | 6379 | Cache distribuído |

---

## FASE 1 — Domain Layer

**Branch:** `feature/sales-domain`
**Mergeada em:** `develop`
**Objetivo:** Implementar todo o modelo de domínio de vendas seguindo DDD — entidades, regras de negócio, validators, interfaces de repositório e eventos.

---

### O que foi feito

#### 1. SaleStatus enum
Enum com sentinela `Unknown = 0` (padrão do template), `Active = 1` e `Cancelled = 2`.
O validator rejeita o valor `Unknown` — garantia de que o status sempre foi explicitamente definido.

#### 2. SaleItem entity + SaleItemValidator
`SaleItem` representa um item individual da venda. Campos:
- `ProductId` + `ProductName` — External Identity pattern (produto pertence a outro domínio)
- `Quantity`, `UnitPrice`, `Discount`, `TotalAmount`, `IsCancelled`

Método `ApplyDiscount()` encapsula as regras de negócio de desconto:
- **< 4 itens** → sem desconto
- **4 a 9 itens** → 10% de desconto
- **10 a 20 itens** → 20% de desconto
- **> 20 itens** → lança `DomainException`

`SaleItemValidator` valida via FluentValidation: quantidade entre 1 e 20, preço positivo, campos obrigatórios.

#### 3. Sale aggregate root + SaleValidator
`Sale` é o agregado raiz do domínio de vendas. Campos principais:
- `SaleNumber`, `SaleDate`
- `CustomerId` + `CustomerName` — External Identity
- `BranchId` + `BranchName` — External Identity
- `TotalAmount` (calculado, private setter), `Status`, `CreatedAt`, `UpdatedAt`
- `Items` como `IReadOnlyList<SaleItem>` — encapsulamento via lista privada

Métodos de domínio:
- `AddItem(productId, productName, quantity, unitPrice)` — cria item, aplica desconto, recalcula total
- `UpdateItem(itemId, quantity, unitPrice)` — atualiza item existente, recalcula
- `Cancel()` — muda status para `Cancelled`, lança exceção se já cancelada
- `CancelItem(itemId)` — cancela item específico, recalcula total
- `Recalculate()` — soma `TotalAmount` dos itens não cancelados

`SaleValidator` valida todos os campos obrigatórios e delega a validação de cada item ao `SaleItemValidator` via `RuleForEach`.

#### 4. Interfaces dos repositórios
`ISaleRepository` (escrita — PostgreSQL via EF Core):
- `CreateAsync`, `GetByIdAsync`, `UpdateAsync`, `DeleteAsync`

`ISaleReadRepository` (leitura — MongoDB):
- `UpsertAsync`, `GetByIdAsync`, `DeleteAsync`
- `GetPagedAsync(page, size, order, filters)` — suporta paginação, ordenação e filtros conforme `general-api.md`

#### 5. Eventos de domínio
Quatro eventos criados para publicação via log (sem Message Broker, conforme README):
- `SaleCreatedEvent` — carrega a entidade `Sale` completa
- `SaleModifiedEvent` — carrega a entidade `Sale` atualizada
- `SaleCancelledEvent` — carrega `SaleId` e `SaleNumber`
- `ItemCancelledEvent` — carrega `SaleId`, `SaleNumber` e `ItemId`

---

### Arquivos criados

| Arquivo | Descrição |
|---|---|
| `Domain/Enums/SaleStatus.cs` | Enum com Unknown/Active/Cancelled |
| `Domain/Entities/SaleItem.cs` | Entidade item com regras de desconto |
| `Domain/Validation/SaleItemValidator.cs` | Validator FluentValidation para SaleItem |
| `Domain/Entities/Sale.cs` | Agregado raiz com todos os métodos de domínio |
| `Domain/Validation/SaleValidator.cs` | Validator FluentValidation para Sale |
| `Domain/Repositories/ISaleRepository.cs` | Interface escrita (PostgreSQL) |
| `Domain/Repositories/ISaleReadRepository.cs` | Interface leitura (MongoDB) com paginação |
| `Domain/Events/SaleCreatedEvent.cs` | Evento de criação |
| `Domain/Events/SaleModifiedEvent.cs` | Evento de modificação |
| `Domain/Events/SaleCancelledEvent.cs` | Evento de cancelamento de venda |
| `Domain/Events/ItemCancelledEvent.cs` | Evento de cancelamento de item |

---

### Commits da fase

| Hash | Tipo | Descrição |
|---|---|---|
| `befd56b` | `feat(domain)` | Add SaleStatus enum with Unknown sentinel value |
| `c782921` | `feat(domain)` | Add SaleItem entity with quantity-based discount business rules |
| `773a194` | `feat(domain)` | Add Sale aggregate root with AddItem, Cancel, CancelItem and Recalculate methods |
| `46037b9` | `feat(domain)` | Add ISaleRepository and ISaleReadRepository interfaces with pagination support |
| `da75ae2` | `feat(domain)` | Add SaleCreated, SaleModified, SaleCancelled and ItemCancelled domain events |

---

## FASE 2 — Application Layer (CQRS)

**Branch:** `feature/sales-application`
**Mergeada em:** `develop`
**Objetivo:** Implementar todos os casos de uso de vendas via CQRS com MediatR — commands, queries, handlers, validators, results e profiles de mapeamento AutoMapper.

---

### O que foi feito

#### 1. CreateSale
Criação de uma nova venda com validação completa:
- `CreateSaleCommand` — carrega `SaleNumber`, `SaleDate`, External Identities (CustomerId/Name, BranchId/Name) e lista de `CreateSaleItemCommand`
- `CreateSaleValidator` — valida campos obrigatórios, datas, preços positivos e máximo de 20 itens por linha (`RuleForEach`)
- `CreateSaleHandler` — valida, constrói o agregado `Sale`, adiciona itens (que aplicam desconto automaticamente), persiste via `ISaleRepository`
- `CreateSaleResult` — retorna o estado completo da venda criada com totais e descontos calculados
- `CreateSaleProfile` — AutoMapper: `Sale → CreateSaleResult`, `SaleItem → CreateSaleItemResult`

#### 2. UpdateSale
Atualização de uma venda existente:
- `UpdateSaleCommand` — inclui `Id` da venda + todos os campos atualizáveis + lista de itens
- `UpdateSaleValidator` — igual ao de criação, mais validação do `Id`
- `UpdateSaleHandler` — busca venda, atualiza campos, adiciona/atualiza itens via `AddItem()`, persiste
- `UpdateSaleResult` — inclui `UpdatedAt`
- `UpdateSaleProfile` — AutoMapper completo

#### 3. CancelSale
Cancelamento de uma venda inteira:
- `CancelSaleCommand` — apenas o `Id` da venda
- `CancelSaleValidator` — valida que `Id` não é vazio
- `CancelSaleHandler` — busca venda, chama `sale.Cancel()` (que valida regra de negócio), persiste
- `CancelSaleResult` — retorna `Id`, `SaleNumber`, `Status` e `UpdatedAt`
- `CancelSaleProfile` — AutoMapper

#### 4. CancelSaleItem
Cancelamento de um item específico dentro de uma venda:
- `CancelSaleItemCommand` — `SaleId` + `ItemId`
- `CancelSaleItemValidator` — valida ambos os IDs
- `CancelSaleItemHandler` — busca venda, chama `sale.CancelItem(itemId)`, persiste, retorna novo total
- `CancelSaleItemResult` — `SaleId`, `ItemId`, `IsCancelled`, `NewSaleTotal`

#### 5. GetSale
Consulta de uma venda por ID (leitura via MongoDB):
- `GetSaleQuery` — apenas o `Id`
- `GetSaleValidator` — valida que `Id` não é vazio
- `GetSaleHandler` — consulta via `ISaleReadRepository` (MongoDB), mapeia resultado
- `GetSaleResult` — venda completa com lista de itens
- `GetSaleProfile` — AutoMapper: `Sale → GetSaleResult`, `SaleItem → GetSaleItemResult`

#### 6. ListSales
Listagem paginada de vendas (leitura via MongoDB):
- `ListSalesQuery` — `Page`, `Size`, `Order`, `Filters` (conforme `general-api.md`)
- `ListSalesValidator` — valida page > 0 e size entre 1 e 100
- `ListSalesHandler` — chama `ISaleReadRepository.GetPagedAsync()` com todos os parâmetros, calcula `TotalPages`
- `ListSalesResult` — `Data`, `TotalItems`, `CurrentPage`, `TotalPages`
- `ListSalesProfile` — AutoMapper: `Sale → ListSaleItemResult`

---

### Arquivos criados

| Arquivo | Descrição |
|---|---|
| `Application/Sales/CreateSale/CreateSaleCommand.cs` | Command de criação |
| `Application/Sales/CreateSale/CreateSaleValidator.cs` | Validator FluentValidation |
| `Application/Sales/CreateSale/CreateSaleHandler.cs` | Handler MediatR |
| `Application/Sales/CreateSale/CreateSaleResult.cs` | DTO de resultado |
| `Application/Sales/CreateSale/CreateSaleProfile.cs` | Profile AutoMapper |
| `Application/Sales/UpdateSale/UpdateSaleCommand.cs` | Command de atualização |
| `Application/Sales/UpdateSale/UpdateSaleValidator.cs` | Validator FluentValidation |
| `Application/Sales/UpdateSale/UpdateSaleHandler.cs` | Handler MediatR |
| `Application/Sales/UpdateSale/UpdateSaleResult.cs` | DTO de resultado |
| `Application/Sales/UpdateSale/UpdateSaleProfile.cs` | Profile AutoMapper |
| `Application/Sales/CancelSale/CancelSaleCommand.cs` | Command de cancelamento |
| `Application/Sales/CancelSale/CancelSaleValidator.cs` | Validator FluentValidation |
| `Application/Sales/CancelSale/CancelSaleHandler.cs` | Handler MediatR |
| `Application/Sales/CancelSale/CancelSaleResult.cs` | DTO de resultado |
| `Application/Sales/CancelSale/CancelSaleProfile.cs` | Profile AutoMapper |
| `Application/Sales/CancelSaleItem/CancelSaleItemCommand.cs` | Command de cancelamento de item |
| `Application/Sales/CancelSaleItem/CancelSaleItemValidator.cs` | Validator FluentValidation |
| `Application/Sales/CancelSaleItem/CancelSaleItemHandler.cs` | Handler MediatR |
| `Application/Sales/CancelSaleItem/CancelSaleItemResult.cs` | DTO de resultado |
| `Application/Sales/GetSale/GetSaleQuery.cs` | Query de consulta por ID |
| `Application/Sales/GetSale/GetSaleValidator.cs` | Validator FluentValidation |
| `Application/Sales/GetSale/GetSaleHandler.cs` | Handler MediatR (leitura MongoDB) |
| `Application/Sales/GetSale/GetSaleResult.cs` | DTO de resultado completo |
| `Application/Sales/GetSale/GetSaleProfile.cs` | Profile AutoMapper |
| `Application/Sales/ListSales/ListSalesQuery.cs` | Query de listagem paginada |
| `Application/Sales/ListSales/ListSalesValidator.cs` | Validator FluentValidation |
| `Application/Sales/ListSales/ListSalesHandler.cs` | Handler MediatR (leitura MongoDB) |
| `Application/Sales/ListSales/ListSalesResult.cs` | DTO de resultado paginado |
| `Application/Sales/ListSales/ListSalesProfile.cs` | Profile AutoMapper |

---

### Commits da fase

| Hash | Tipo | Horário | Descrição |
|---|---|---|---|
| `3796b1f` | `feat(application)` | 18:12 | Add CreateSale command, handler, validator, result and profile |
| `45bc4e6` | `feat(application)` | 18:33 | Add UpdateSale command, handler, validator, result and profile |
| `2bf6d7e` | `feat(application)` | 18:52 | Add CancelSale command, handler, validator, result and profile |
| `e9537d0` | `feat(application)` | 19:05 | Add CancelSaleItem command, handler, validator and result |
| `398a06c` | `feat(application)` | 19:22 | Add GetSale query, handler, validator, result and profile |
| `deeb512` | `feat(application)` | 19:38 | Add ListSales query, handler, validator, result and profile |
| `e05478a` | merge | 19:48 | Merge `feature/sales-application` into `develop` |

---

## FASE 3 — ORM Layer (PostgreSQL — Escrita)

**Branch:** `feature/sales-orm`
**Objetivo:** Mapear as entidades do domínio para o banco relacional PostgreSQL via EF Core, implementar o repositório de escrita e gerar a migration.

---

### O que foi feito

#### 1. SaleConfiguration e SaleItemConfiguration
Mapeamentos Fluent API para as entidades do domínio:

- `SaleConfiguration` — mapeia `Sale` para a tabela `Sales`:
  - `SaleStatus` convertido para string com `HasConversion<string>()` (padrão do template)
  - Relação 1:N com `SaleItem` configurada como `HasMany / WithOne` com `OnDelete(Cascade)`
  - `TotalAmount` com precisão decimal `(18, 2)`
  - `SaleNumber` indexado como único (`HasIndex(...).IsUnique()`)

- `SaleItemConfiguration` — mapeia `SaleItem` para a tabela `SaleItems`:
  - `UnitPrice`, `Discount` e `TotalAmount` com precisão `(18, 2)`
  - `ProductName` limitado a 500 caracteres
  - `SaleId` como foreign key

#### 2. DefaultContext
- `DbSet<Sale> Sales` e `DbSet<SaleItem> SaleItems` adicionados ao contexto
- `MigrationsAssembly` corrigido de `WebApi` para `ORM` no `YourDbContextFactory`

#### 3. SaleRepository
Implementação de `ISaleRepository` usando EF Core:
- `CreateAsync` — adiciona e salva
- `GetByIdAsync` — `Include(s => s.Items)` para carregar os itens junto
- `UpdateAsync` — `Update` + `SaveChanges`
- `DeleteAsync` — busca por id, remove e salva

#### 4. IoC — registro de ISaleRepository
`ISaleRepository` registrado como `Scoped` em `InfrastructureModuleInitializer`.

#### 5. Migration EF Core
Migration `AddSalesAndSaleItems` gerada automaticamente com `dotnet ef migrations add`. Cria as tabelas `Sales` e `SaleItems` com todos os campos, constraints e foreign keys.

---

### Arquivos modificados / criados

| Arquivo | Tipo |
|---|---|
| `ORM/Mapping/SaleConfiguration.cs` | Criado |
| `ORM/Mapping/SaleItemConfiguration.cs` | Criado |
| `ORM/DefaultContext.cs` | Modificado |
| `ORM/Repositories/SaleRepository.cs` | Criado |
| `IoC/ModuleInitializers/InfrastructureModuleInitializer.cs` | Modificado |
| `ORM/Migrations/..._AddSalesAndSaleItems.cs` | Criado (gerado) |
| `ORM/Migrations/..._AddSalesAndSaleItems.Designer.cs` | Criado (gerado) |

---

### Commits da fase

| Hash | Tipo | Descrição |
|---|---|---|
| `c02af54` | `feat(orm)` | Add SaleConfiguration EF Core mapping for Sales table |
| `a9e36aa` | `feat(orm)` | Add SaleItemConfiguration EF Core mapping for SaleItems table |
| `fefb8e1` | `feat(orm)` | Add DbSet Sales and SaleItems to DefaultContext |
| `541d981` | `feat(orm)` | Implement SaleRepository with EF Core |
| `4268d1a` | `feat(ioc)` | Register ISaleRepository in DI container |
| `6f168eb` | `feat(orm)` | Add EF Core migration for Sales and SaleItems tables |

---

---

## FASE 4 — NoSQL Layer (MongoDB — Leitura)

**Branch:** `feature/sales-nosql` + `fix/sales-handlers-nosql-sync`
**Objetivo:** Implementar o repositório de leitura no MongoDB (`ISaleReadRepository`) e sincronizar o read model após cada operação de escrita nos handlers.

---

### O que foi feito

#### 1. MongoClassMapConfig
Configuração de serialização BSON para as entidades de domínio, sem poluir o domínio com atributos de infraestrutura:
- Registra `GuidSerializer` com `GuidRepresentation.Standard`
- Mapeia `Sale`: usa o campo privado `_items` como fonte de serialização (`SetElementName("Items")`), desvinculando a propriedade pública `IReadOnlyList<SaleItem> Items` que não pode ser desserializada diretamente
- Mapeia `SaleItem`: configura `Id` como `_id` do documento
- Thread-safe com lock e flag `_registered` para evitar duplo registro

#### 2. SaleReadRepository
Implementação completa de `ISaleReadRepository` para MongoDB:
- `UpsertAsync` — substitui o documento existente (ou cria novo) via `ReplaceOneAsync` com `IsUpsert = true`
- `GetByIdAsync` — busca por `Id` com `Find` + `FirstOrDefaultAsync`
- `DeleteAsync` — remove documento por `Id`
- `GetPagedAsync` — paginação, ordenação e filtros dinâmicos:
  - Filtros exatos: `campo=valor`
  - Filtros wildcard: `campo=valor*` → regex case-insensitive
  - Filtros de range: `_minCampo=100` e `_maxCampo=500` → `Gte` / `Lte`
  - Ordenação: `campo asc` ou `campo desc` (default: `CreatedAt desc`)
  - Retorna tupla `(IEnumerable<Sale> Items, int Total)`

#### 3. InfrastructureModuleInitializer atualizado
- Chamada a `MongoClassMapConfig.Register()` no startup, antes de qualquer operação MongoDB
- Registro de `ISaleReadRepository → SaleReadRepository` como `Scoped`

#### 4. Sync dos handlers (fix/sales-handlers-nosql-sync)
Os 4 handlers de escrita foram atualizados para manter o MongoDB sincronizado após cada operação no PostgreSQL:
- `CreateSaleHandler` → `await _saleReadRepository.UpsertAsync(created)`
- `UpdateSaleHandler` → `await _saleReadRepository.UpsertAsync(updated)`
- `CancelSaleHandler` → `await _saleReadRepository.UpsertAsync(updated)`
- `CancelSaleItemHandler` → `await _saleReadRepository.UpsertAsync(sale)`

---

### Arquivos criados / modificados

| Arquivo | Tipo |
|---|---|
| `ORM/MongoDB/MongoClassMapConfig.cs` | Criado |
| `ORM/MongoDB/Repositories/SaleReadRepository.cs` | Criado |
| `IoC/ModuleInitializers/InfrastructureModuleInitializer.cs` | Modificado |
| `Application/Sales/CreateSale/CreateSaleHandler.cs` | Modificado (sync MongoDB) |
| `Application/Sales/UpdateSale/UpdateSaleHandler.cs` | Modificado (sync MongoDB) |
| `Application/Sales/CancelSale/CancelSaleHandler.cs` | Modificado (sync MongoDB) |
| `Application/Sales/CancelSaleItem/CancelSaleItemHandler.cs` | Modificado (sync MongoDB) |

---

### Commits da fase

**Branch `feature/sales-nosql`:**

| Hash | Tipo | Horário | Descrição |
|---|---|---|---|
| `b2b4e25` | `feat(nosql)` | 19:55 | Add MongoClassMapConfig for BSON serialization of Sale aggregate |
| `325e1f4` | `feat(nosql)` | 20:08 | Implement SaleReadRepository with pagination, ordering and dynamic filtering |
| `ad322c5` | `feat(ioc)` | 20:18 | Register ISaleReadRepository and configure MongoClassMapConfig on startup |

**Branch `fix/sales-handlers-nosql-sync`:**

| Hash | Tipo | Horário | Descrição |
|---|---|---|---|
| `b93c4a5` | `fix(application)` | 20:22 | Sync CreateSale and UpdateSale handlers with MongoDB read store |
| `e7480e0` | `fix(application)` | 20:27 | Sync CancelSale and CancelSaleItem handlers with MongoDB read store |

---

## FASE 5 — Cache Layer (Redis)

**Branch:** `fix/sales-handlers-redis-cache`
**Objetivo:** Adicionar cache Redis nos handlers de leitura e invalidar o cache nos handlers de escrita.

---

### O que foi feito

#### 1. Pacote e handlers de leitura
- Referência `Microsoft.Extensions.Caching.Abstractions` adicionada ao projeto Application (o Redis já estava registrado no IoC na Fase 0).
- **GetSaleHandler:** consulta o cache com chave `Sale:{id}` antes do MongoDB; em caso de hit retorna o resultado deserializado; em caso de miss busca no MongoDB, mapeia para `GetSaleResult`, grava no cache (TTL 5 min) e retorna.
- **ListSalesHandler:** chave de cache construída a partir de `Page`, `Size`, `Order` e `Filters` (ordenados para consistência); mesmo fluxo: cache → MongoDB → grava no cache (TTL 5 min).

#### 2. Invalidação nos handlers de escrita
Para evitar leitura de venda desatualizada, o cache da venda é removido após cada alteração:
- **CreateSaleHandler**, **UpdateSaleHandler**, **CancelSaleHandler**, **CancelSaleItemHandler:** após `UpsertAsync` no MongoDB, chamam `_cache.RemoveAsync("Sale:" + sale.Id)`.

---

### Arquivos modificados

| Arquivo | Alteração |
|---|---|
| `Application/Ambev.DeveloperEvaluation.Application.csproj` | Pacote `Microsoft.Extensions.Caching.Abstractions` |
| `Application/Sales/GetSale/GetSaleHandler.cs` | Cache Redis (leitura + gravação com TTL 5 min) |
| `Application/Sales/ListSales/ListSalesHandler.cs` | Cache Redis + `BuildListCacheKey` |
| `Application/Sales/CreateSale/CreateSaleHandler.cs` | Invalidação `Sale:{id}` |
| `Application/Sales/UpdateSale/UpdateSaleHandler.cs` | Invalidação `Sale:{id}` |
| `Application/Sales/CancelSale/CancelSaleHandler.cs` | Invalidação `Sale:{id}` |
| `Application/Sales/CancelSaleItem/CancelSaleItemHandler.cs` | Invalidação `Sale:{id}` |

---

### Commits da fase

**Branch `fix/sales-handlers-redis-cache`:**

| Hash | Tipo | Descrição |
|---|---|---|
| `d3b1bf2` | `fix(cache)` | Add Redis cache to GetSale and ListSales read handlers |
| `039e077` | `fix(cache)` | Invalidate sale cache on create, update, cancel and cancel item |

---

## FASE 6 — WebApi Layer

**Branch:** `feature/sales-webapi`
**Objetivo:** Expor as operações de vendas via REST (SalesController) com paginação, ordenação e filtros conforme `general-api.md`.

---

### O que foi feito

#### 1. Referência ao Application
- Projeto WebApi passou a referenciar diretamente `Ambev.DeveloperEvaluation.Application` para usar commands, queries e results no controller.

#### 2. SalesController
Controller em `Features/Sales/SalesController.cs` com os endpoints:

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/Sales` | Cria venda — body: `CreateSaleCommand` (SaleNumber, SaleDate, CustomerId/Name, BranchId/Name, Items[]) |
| PUT | `/api/Sales/{id}` | Atualiza venda — body: mesmo shape do create (sem Id; Id vem da rota) |
| DELETE | `/api/Sales/{id}` | Cancela venda — `CancelSaleCommand` |
| DELETE | `/api/Sales/{saleId}/items/{itemId}` | Cancela item da venda — `CancelSaleItemCommand` |
| GET | `/api/Sales/{id}` | Obtém venda por id — `GetSaleQuery` |
| GET | `/api/Sales` | Lista vendas — query params: `_page`, `_size`, `_order` e demais como filtros (field=value, _minField, _maxField) |

Respostas encapsuladas em `ApiResponseWithData<T>` (Success, Message, Data). List usa `ListSalesQuery` montado a partir da query string (todos os parâmetros exceto `_page`, `_size`, `_order` viram dicionário de filtros).

---

### Arquivos criados / modificados

| Arquivo | Tipo |
|---------|------|
| `WebApi/Ambev.DeveloperEvaluation.WebApi.csproj` | Modificado (ProjectReference Application) |
| `WebApi/Features/Sales/SalesController.cs` | Criado |

---

### Commits da fase

**Branch `feature/sales-webapi`:**

| Hash | Tipo | Descrição |
|------|------|------------|
| `5598eb0` | `feat(webapi)` | Add SalesController with create, update, cancel, cancel item, get and list endpoints |

---

## FASE 7 — IoC / Registro de dependências

**Branch:** `fix/ioc-cleanup`
**Objetivo:** Revisar registros de dependências e remover duplicidades.

---

### Revisão realizada

- **InfrastructureModuleInitializer:** `ISaleRepository`, `ISaleReadRepository`, `MongoDbContext`, `IMongoClient`, Redis (`IDistributedCache`), `MongoClassMapConfig.Register()` — tudo registrado.
- **Program.cs:** MediatR (Application + WebApi assemblies), AutoMapper (Application + WebApi), `ValidationBehavior`, `AddControllers()` — handlers e profiles de Sales descobertos por assembly.
- **ApplicationModuleInitializer:** `IPasswordHasher`.
- **WebApiModuleInitializer:** continha `AddControllers()` em duplicidade com `Program.cs`.

### Ajuste aplicado (fix/ioc-cleanup)

- Remoção de `AddControllers()` do `WebApiModuleInitializer`, mantendo o registro apenas no composition root (`Program.cs`). `AddHealthChecks()` mantido no módulo.

---

### Arquivos modificados

| Arquivo | Alteração |
|---------|-----------|
| `IoC/ModuleInitializers/WebApiModuleInitializer.cs` | Removido `AddControllers()` duplicado; comentário indicando que Controllers/HealthChecks vêm do Program. |

---

### Commits da fase

**Branch `fix/ioc-cleanup`:**

| Hash | Tipo | Descrição |
|------|------|-----------|
| `3dafa58` | `fix(ioc)` | Remove duplicate AddControllers from WebApiModuleInitializer |

*(Merge em develop: fix(ioc): merge fix/ioc-cleanup into develop)*

---

## Correções — User API e Auth

**Branch:** `fix/user-create-response`
**Objetivo:** Corrigir a API de Users (POST/GET) e a autenticação (POST /api/Auth) para que as respostas retornem os dados corretos e não ocorra 500.

---

### Problema 1 — POST User: resposta sem dados

- **Sintoma:** O usuário era gravado corretamente no banco (Username, Email, Phone, Role, Status, etc.), mas o corpo da resposta do POST trazia apenas o `Id` (ou dados vazios).
- **Causa:** O DTO da Application `CreateUserResult` tinha apenas a propriedade `Id`. O handler mapeava `User` → `CreateUserResult`, então Name, Email, Phone, Role e Status não existiam no result e não eram enviados na API.
- **Correção:**
  - **Application — `CreateUserResult.cs`:** Inclusão das propriedades `Username`, `Email`, `Phone`, `Role`, `Status` e `CreatedAt`, alinhadas à entidade `User`. O profile `User` → `CreateUserResult` passou a preencher todos os campos.
  - **WebApi — `CreateUserProfile.cs`:** Mapeamento explícito `CreateUserResult` → `CreateUserResponse` com `ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Username))`, pois a API expõe `Name` e o domínio usa `Username`.

---

### Problema 2 — GET User: erro 500 Internal Server Error

- **Sintoma:** GET `/api/Users/{id}` retornava HTTP 500 com `type: "InternalError"`.
- **Causa:** Na camada Application, o mapeamento `User` → `GetUserResult` não definia a origem do campo `Name`. A entidade `User` tem `Username` e o result tem `Name`; sem mapeamento explícito o fluxo podia falhar ou a resposta ficar incoerente. Na WebApi não havia mapeamento explícito de `GetUserResult` → `GetUserResponse`.
- **Correção:**
  - **Application — `GetUserProfile.cs`:** Inclusão de `ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Username))` no mapeamento `User` → `GetUserResult`.
  - **WebApi — `GetUserProfile.cs`:** Inclusão do mapeamento explícito `GetUserResult` → `GetUserResponse` para garantir que o resultado do handler seja convertido corretamente no DTO da API.

---

### Arquivos modificados

| Arquivo | Alteração |
|---------|-----------|
| `Application/Users/CreateUser/CreateUserResult.cs` | Inclusão de Username, Email, Phone, Role, Status, CreatedAt |
| `Application/Users/CreateUser/` (profile) | Já mapeava User → CreateUserResult; passou a preencher todos os campos por convenção |
| `WebApi/Features/Users/CreateUser/CreateUserProfile.cs` | CreateUserResult → CreateUserResponse com Name ← Username |
| `Application/Users/GetUser/GetUserProfile.cs` | User → GetUserResult com Name ← Username |
| `WebApi/Features/Users/GetUser/GetUserProfile.cs` | GetUserResult → GetUserResponse explícito |

---

### Problema 3 — POST Auth: erro 500 Internal Server Error

- **Sintoma:** POST `/api/Auth` (login com email/senha) retornava HTTP 500 com `type: "InternalError"`.
- **Contexto:** A autenticação **usa apenas a tabela Users existente** — não há tabela nem componente novo de auth. O fluxo é: `IUserRepository.GetByEmailAsync` → verificação de senha com `IPasswordHasher` (BCrypt) → `ActiveUserSpecification` (usuário ativo) → `IJwtTokenGenerator.GenerateToken(user)` → retorno com token e dados do usuário.
- **Causa:** O controller mapeia o resultado do handler (`AuthenticateUserResult`) para `AuthenticateUserResponse`, mas o profile de AutoMapper só tinha mapeamento `User` → `AuthenticateUserResponse`. Não existia mapeamento `AuthenticateUserResult` → `AuthenticateUserResponse`, o que podia gerar exceção no mapper. Além disso, se `Jwt:SecretKey` estivesse ausente na configuração, `Encoding.ASCII.GetBytes(null)` no `JwtTokenGenerator` geraria exceção.
- **Correção:**
  - **WebApi — `AuthenticateUserProfile.cs`:** Definição do mapeamento correto `AuthenticateUserResult` → `AuthenticateUserResponse` (Token, Email, Name, Role), removendo o mapeamento antigo de `User` → `AuthenticateUserResponse`.
  - **Common — `JwtTokenGenerator.cs`:** Validação de que `Jwt:SecretKey` está configurado antes de usar; em caso de ausência, lança `InvalidOperationException` com mensagem clara em vez de falha genérica.

---

### Arquivos modificados (resumo geral da branch)

| Arquivo | Alteração |
|---------|-----------|
| `Application/Users/CreateUser/CreateUserResult.cs` | Inclusão de Username, Email, Phone, Role, Status, CreatedAt |
| `WebApi/Features/Users/CreateUser/CreateUserProfile.cs` | CreateUserResult → CreateUserResponse com Name ← Username |
| `Application/Users/GetUser/GetUserProfile.cs` | User → GetUserResult com Name ← Username |
| `WebApi/Features/Users/GetUser/GetUserProfile.cs` | GetUserResult → GetUserResponse explícito |
| `WebApi/Features/Auth/AuthenticateUserFeature/AuthenticateUserProfile.cs` | AuthenticateUserResult → AuthenticateUserResponse (correção do 500 no login) |
| `Common/Security/JwtTokenGenerator.cs` | Validação de Jwt:SecretKey antes de usar |

---

### Commits da fase

**Branch `fix/user-create-response`:** *(commitada, publicada e incorporada ao fluxo principal)*

| Hash | Tipo | Descrição |
|------|------|-----------|
| `55f4fd6` | `fix(users)` | Align CreateUser/GetUser response mapping and fix auth response/JWT profile issues |

---

### UserRole e UserStatus (enums)

**UserRole** e **UserStatus** são enums do domínio com valores numéricos em C# (ex.: `UserRole`: None=0, Customer=1, Manager=2, Admin=3; **UserStatus**: Unknown=0, Active=1, Inactive=2, Suspended=3). No **banco (PostgreSQL)** eles são persistidos como **texto** (nome do enum) graças ao `HasConversion<string>()` no mapeamento EF Core (`UserConfiguration`), ou seja, na tabela aparecem valores como `"Customer"` e `"Active"`. Nas respostas da **API (JSON)** o serializador padrão tende a enviar os enums como **número** (0, 1, 2…). Se for necessário que a API devolva o nome do enum em texto (ex.: `"role": "Customer"`), basta configurar `JsonStringEnumConverter` no pipeline de serialização.

---

## FASE 8 — Testes

**Branch:** `feature/sales-unit-tests`
**Mergeada em:** `develop`
**Objetivo:** Testes unitários dos handlers de Sales (CRUD) com xUnit, NSubstitute e FluentAssertions.

---

### O que foi feito

- **SalesHandlerTestData:** dados de teste gerados usando o boilerplate de testes do .NET combinado com Bogus para `CreateSaleCommand` e `UpdateSaleCommand` (itens com ProductId, ProductName, Quantity, UnitPrice).
- **Handlers testados (fluxo CRUD):**
  - CreateSaleHandlerTests — comando válido retorna resultado e chama Repository + ReadRepository + Cache.Remove; comando inválido lança ValidationException.
  - UpdateSaleHandlerTests — comando válido atualiza e retorna; sale inexistente lança KeyNotFoundException.
  - CancelSaleHandlerTests — id válido cancela e retorna; id inexistente lança KeyNotFoundException.
  - CancelSaleItemHandlerTests — sale com item via AddItem; comando com ItemId cancela item; sale inexistente lança KeyNotFoundException.
  - GetSaleHandlerTests — id existente retorna resultado (cache miss); cache hit retorna do cache; id inexistente lança KeyNotFoundException.
  - ListSalesHandlerTests — query válida retorna resultado paginado; página inválida (Page=0) lança ValidationException.
- **Mocks:** NSubstitute para ISaleRepository, ISaleReadRepository, IDistributedCache, IMapper. Referência `Microsoft.Extensions.Caching.Abstractions` 10.0.5 no projeto Unit.
- Testes de regras de negócio (descontos, limites) ficaram para branch posterior.

---

### Arquivos criados

| Arquivo | Descrição |
|---------|-----------|
| `tests/.../Unit/Application/Sales/SalesHandlerTestData.cs` | Dados gerados com boilerplate .NET + Bogus para comandos |
| `tests/.../Unit/Application/Sales/CreateSaleHandlerTests.cs` | Testes do CreateSaleHandler |
| `tests/.../Unit/Application/Sales/UpdateSaleHandlerTests.cs` | Testes do UpdateSaleHandler |
| `tests/.../Unit/Application/Sales/CancelSaleHandlerTests.cs` | Testes do CancelSaleHandler |
| `tests/.../Unit/Application/Sales/CancelSaleItemHandlerTests.cs` | Testes do CancelSaleItemHandler |
| `tests/.../Unit/Application/Sales/GetSaleHandlerTests.cs` | Testes do GetSaleHandler |
| `tests/.../Unit/Application/Sales/ListSalesHandlerTests.cs` | Testes do ListSalesHandler |

---

### Commits da fase

| Tipo | Descrição |
|------|-----------|
| `feat(tests)` | Add unit tests for Sales CRUD handlers (Create, Update, Cancel, CancelItem, Get, List) |
| `chore(tests)` | Remove Domain.Validation tests (Email, Password, Phone, User validators) |

*(Merge em develop: feat(tests): merge feature/sales-unit-tests into develop)*

---

## FASE 9 — Documentação (setup e teste)

**Branch:** `feature/docs-setup-test`
**Mergeada em:** `develop`
**Objetivo:** Atender ao requisito do cliente: *"The repository must provide instructions on how to configure, execute and test the project"*.

---

### O que foi feito

- **.doc/setup-and-test.md:** guia com pré-requisitos (Docker, .NET 8 SDK opcional), como configurar (pasta `template/backend`, connection strings), como executar (Docker Compose ou dotnet run com bancos no Docker), como testar (Swagger, `dotnet test` com filtro Sales) e tabela-resumo.
- **README.md:** link adicionado após a lista de instruções do cliente: *"See [Setup and Test Guide](/.doc/setup-and-test.md) for how to configure, run and test the project."*

---

### Arquivos modificados / criados

| Arquivo | Tipo |
|---------|------|
| `.doc/setup-and-test.md` | Criado |
| `README.md` | Modificado (link para setup-and-test) |

---

### Commits da fase

| Tipo | Descrição |
|------|-----------|
| `docs` | add setup and test instructions |

*(Merge em develop: docs: merge feature/docs-setup-test into develop)*

---

## Fix — Eventos de venda (log)

**Branch:** `fix/sales-event-logs`
**Mergeada em:** `develop`
**Objetivo:** Implementar o diferencial do use case: log dos eventos SaleCreated, SaleModified, SaleCancelled, ItemCancelled (sem Message Broker, conforme README).

---

### O que foi feito

- Injeção de `ILogger<T>` nos quatro handlers de escrita de Sales.
- Após cada operação bem-sucedida, log em nível Information com o nome do evento e dados mínimos:
  - **CreateSaleHandler:** `SaleCreated: SaleId={SaleId}, SaleNumber={SaleNumber}`
  - **UpdateSaleHandler:** `SaleModified: SaleId={SaleId}, SaleNumber={SaleNumber}`
  - **CancelSaleHandler:** `SaleCancelled: SaleId={SaleId}, SaleNumber={SaleNumber}`
  - **CancelSaleItemHandler:** `ItemCancelled: SaleId={SaleId}, ItemId={ItemId}`

---

### Arquivos modificados

| Arquivo | Alteração |
|---------|-----------|
| `Application/Sales/CreateSale/CreateSaleHandler.cs` | ILogger + LogInformation SaleCreated |
| `Application/Sales/UpdateSale/UpdateSaleHandler.cs` | ILogger + LogInformation SaleModified |
| `Application/Sales/CancelSale/CancelSaleHandler.cs` | ILogger + LogInformation SaleCancelled |
| `Application/Sales/CancelSaleItem/CancelSaleItemHandler.cs` | ILogger + LogInformation ItemCancelled |

---

### Commits da fase

| Tipo | Descrição |
|------|-----------|
| `fix(sales)` | log SaleCreated, SaleModified, SaleCancelled, ItemCancelled |

*(Merge em develop: fix(sales): merge fix/sales-event-logs into develop)*

---

## Fix — List Sales incluir itens

**Branch:** `fix/sales-list-include-items`
**Objetivo:** Alinhar o endpoint GET /api/Sales (listagem) ao use case: a API deve poder informar, por venda, os itens com Products, Quantities, Unit prices, Discounts, Total amount for each item e Cancelled/Not Cancelled.

---

### O que foi feito

- **ListSaleItemResult:** propriedade `Items` adicionada como `List<ListSaleLineItemResult>`.
- **ListSaleLineItemResult:** novo DTO com Id, ProductId, ProductName, Quantity, UnitPrice, Discount, TotalAmount, IsCancelled.
- **ListSalesProfile:** mapeamento `SaleItem → ListSaleLineItemResult` e `Sale → ListSaleItemResult` com `ForMember(d => d.Items, o => o.MapFrom(s => s.Items))`.
- O repositório de leitura (MongoDB) já retorna `Sale` com `Items` populados; o handler e o cache continuam iguais.

---

### Arquivos modificados

| Arquivo | Alteração |
|---------|-----------|
| `Application/Sales/ListSales/ListSalesResult.cs` | Items em ListSaleItemResult + classe ListSaleLineItemResult |
| `Application/Sales/ListSales/ListSalesProfile.cs` | Mapeamento SaleItem → ListSaleLineItemResult e Sale.Items → ListSaleItemResult.Items |

---

### Commits da fase

| Tipo | Descrição |
|------|-----------|
| `fix(sales)` | include items in list response (products, quantities, prices, discounts, cancelled) |

*(Merge em develop: PR #10, commit `aef3128`.)*

---

## Conclusão de hoje (resumo)

- Organizamos as mudanças em duas branches separadas para facilitar revisão e merge: `fix/tests-coverage` (testes e cobertura) e `fix/architecture` (documentação e configuração de build).
- Atualizamos o guia `.doc/setup-and-test.md` com a observação de que o `coverage-report` do cliente é usado para acompanhar resultados e apoiar a evolução da cobertura.
- As duas branches foram publicadas no repositório remoto e estão prontas para abertura de PR e merge em `develop`.
- `fases.md` foi mantido fora dos commits anteriores por decisão de organização e agora recebeu este fechamento resumido.

---

## Preparação para perguntas técnicas (resumo rápido)

- **DDD e External Identities:** `Sale` referencia Customer/Branch/Product por Id + descrição desnormalizada para evitar acoplamento entre domínios.
- **Regras de negócio centrais:** desconto por quantidade (4-9 = 10%, 10-20 = 20%), sem desconto abaixo de 4 e bloqueio acima de 20 itens por produto.
- **Arquitetura de leitura/escrita:** escrita transacional no PostgreSQL (`ISaleRepository`) e leitura otimizada no MongoDB (`ISaleReadRepository`) com sincronização pelos handlers.
- **Cache e consistência:** Redis aplicado em consultas (`GetSale`/`ListSales`) com invalidação explícita após create/update/cancel/cancel item.
- **Eventos do use case (diferencial):** logs `SaleCreated`, `SaleModified`, `SaleCancelled`, `ItemCancelled` via `ILogger`, sem broker.
- **Qualidade e cobertura:** suíte unitária expandida (Auth, Users, Sales domain e validators) com apoio do `coverage-report` do cliente para direcionar evolução.

---

## Stack e ferramentas usadas no projeto

### Backend e arquitetura

- **.NET 8 / ASP.NET Core Web API:** base da aplicação e exposição dos endpoints REST.
- **DDD (Domain-Driven Design):** modelagem do domínio de Sales com agregados, entidades, eventos e regras de negócio.
- **CQRS + MediatR:** separação de comandos/queries com handlers dedicados.
- **AutoMapper:** mapeamento entre entidades de domínio, comandos/results e DTOs da API.
- **FluentValidation:** validação de comandos e objetos de domínio.
- **ILogger (Microsoft.Extensions.Logging):** registro dos eventos de negócio como diferencial do use case.

### Persistência, leitura e cache

- **Entity Framework Core:** persistência relacional da escrita.
- **PostgreSQL:** banco transacional principal (write model).
- **MongoDB (`MongoDB.Driver`):** read model desnormalizado para consultas.
- **Redis (`IDistributedCache`):** cache distribuído para endpoints de leitura e invalidação após escrita.

### Infra e execução

- **Docker / Docker Compose:** orquestração local da API e serviços (PostgreSQL, MongoDB, Redis).
- **Configuração central de build (`Directory.Build.props`):** padronização de geração de XML docs e warnings de build.

### Testes e qualidade

- **xUnit:** framework de testes unitários.
- **NSubstitute:** criação de mocks para dependências em testes.
- **FluentAssertions:** asserções legíveis e expressivas.
- **Boilerplate de testes do .NET + Bogus:** geração de dados fake consistentes para cenários de teste.
- **Coverlet + ReportGenerator:** coleta e geração de relatórios de cobertura.
- **`coverage-report` do cliente (`.bat`/`.sh`):** apoio para acompanhamento e evolução da cobertura.
