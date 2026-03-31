# Implementation Phases — Sales API

Implementation traceability document. Records, phase by phase, what was done, the files modified and the commits generated.

---

## PHASE 0 — Infrastructure and base configuration

**Branch:** `feature/infra-config`
**Merged into:** `develop`
**Objective:** Configure all data infrastructure and standardize error handling before starting the domain.

---

### What was done

#### 1. docker-compose updated
The original file had problems:
- Container ports were not mapped to the host (impossible to access locally)
- The WebApi did not receive connection strings via environment variables
- The `version` was obsolete and generated warnings

What was fixed:
- Ports mapped `host:container` for the three data services
- `depends_on` added for WebApi to wait for databases to start
- Connection strings injected via `environment` in the WebApi service
- `version` attribute removed

#### 2. appsettings.json corrected and expanded
The original file had the PostgreSQL connection string in SQL Server format (incorrect). It was corrected to Npgsql format and MongoDB and Redis connection strings were added, plus `MongoDB` and `Cache` configuration sections.

#### 3. MongoDB configured in code
- `MongoDB.Driver` package added to the `ORM` project
- `MongoDbContext` created in the `ORM` project — abstracts the `IMongoDatabase` and exposes `GetCollection<T>()` for read repositories to be created in upcoming phases

#### 4. Redis configured in code
- `Microsoft.Extensions.Caching.StackExchangeRedis` package added to the `ORM` project
- `AddStackExchangeRedisCache` registered in `InfrastructureModuleInitializer` with connection string and prefix `DeveloperEvaluation:`

#### 5. Error handling normalized
The original middleware (`ValidationExceptionMiddleware`) produced responses in format `{ success, message, errors[] }`, which is **different** from what the API specification defines (`{ type, error, detail }`).

What was done:
- Created `ApiErrorResponse` — new error DTO in the correct spec format
- `ValidationExceptionMiddleware` rewritten to use the new format with `type: "ValidationError"`
- Created `GlobalExceptionHandlerMiddleware` — captures globally:
  - `KeyNotFoundException` → HTTP 404, `type: "ResourceNotFound"`
  - `InvalidOperationException` → HTTP 400, `type: "BusinessError"`
  - `DomainException` → HTTP 400, `type: "DomainError"`
  - `UnauthorizedAccessException` → HTTP 401, `type: "AuthenticationError"`
  - Generic `Exception` → HTTP 500, `type: "InternalError"`
- `Program.cs` updated to register `GlobalExceptionHandlerMiddleware` before `ValidationExceptionMiddleware`

---

### Modified files

| File | Type of change |
|---|---|
| `template/backend/docker-compose.yml` | Modified |
| `template/backend/src/.../WebApi/appsettings.json` | Modified |
| `template/backend/src/.../ORM/Ambev.DeveloperEvaluation.ORM.csproj` | Modified (packages added) |
| `template/backend/src/.../ORM/MongoDB/MongoDbContext.cs` | Created |
| `template/backend/src/.../IoC/ModuleInitializers/InfrastructureModuleInitializer.cs` | Modified |
| `template/backend/src/.../WebApi/Common/ApiErrorResponse.cs` | Created |
| `template/backend/src/.../WebApi/Middleware/ValidationExceptionMiddleware.cs` | Modified |
| `template/backend/src/.../WebApi/Middleware/GlobalExceptionHandlerMiddleware.cs` | Created |
| `template/backend/src/.../WebApi/Program.cs` | Modified |

---

### Phase commits

| Hash | Type | Description |
|---|---|---|
| `d189a02` | `chore(docker)` | Expose ports, add depends_on and env vars for MongoDB and Redis |
| `80adb23` | `chore(config)` | Fix PostgreSQL connection string and add MongoDB and Redis connection strings |
| `febf695` | `feat(infra)` | Add MongoDB.Driver and Redis packages, create MongoDbContext |
| `6ac0937` | `feat(infra)` | Register MongoDB client, MongoDbContext and Redis distributed cache in IoC |
| `5aadac7` | `fix(webapi)` | Normalize error responses to spec format `{ type, error, detail }` and add GlobalExceptionHandlerMiddleware |
| `4faba85` | merge | Merge `feature/infra-config` into `develop` |

---

### Docker containers running after this phase

| Container | Image | Port | Purpose |
|---|---|---|---|
| `ambev_developer_evaluation_database` | postgres:13 | 5432 | Transactional write (EF Core) |
| `ambev_developer_evaluation_nosql` | mongo:8.0 | 27017 | Denormalized read (read model) |
| `ambev_developer_evaluation_cache` | redis:7.4.1-alpine | 6379 | Distributed cache |

---

## PHASE 1 — Domain Layer

**Branch:** `feature/sales-domain`
**Merged into:** `develop`
**Objective:** Implement the entire sales domain model following DDD — entities, business rules, validators, repository interfaces and events.

---

### What was done

#### 1. SaleStatus enum
Enum with sentinel `Unknown = 0` (template standard), `Active = 1` and `Cancelled = 2`.
The validator rejects the `Unknown` value — ensuring the status is always explicitly set.

#### 2. SaleItem entity + SaleItemValidator
`SaleItem` represents an individual sale line item. Fields:
- `ProductId` + `ProductName` — External Identity pattern (product belongs to another domain)
- `Quantity`, `UnitPrice`, `Discount`, `TotalAmount`, `IsCancelled`

The `ApplyDiscount()` method encapsulates discount business rules:
- **< 4 items** → no discount
- **4 to 9 items** → 10% discount
- **10 to 20 items** → 20% discount
- **> 20 items** → throws `DomainException`

`SaleItemValidator` validates via FluentValidation: quantity between 1 and 20, positive price, required fields.

#### 3. Sale aggregate root + SaleValidator
`Sale` is the root aggregate of the sales domain. Main fields:
- `SaleNumber`, `SaleDate`
- `CustomerId` + `CustomerName` — External Identity
- `BranchId` + `BranchName` — External Identity
- `TotalAmount` (calculated, private setter), `Status`, `CreatedAt`, `UpdatedAt`
- `Items` as `IReadOnlyList<SaleItem>` — encapsulation via private list

Domain methods:
- `AddItem(productId, productName, quantity, unitPrice)` — creates item, applies discount, recalculates total
- `UpdateItem(itemId, quantity, unitPrice)` — updates existing item, recalculates
- `Cancel()` — changes status to `Cancelled`, throws exception if already cancelled
- `CancelItem(itemId)` — cancels specific item, recalculates total
- `Recalculate()` — sums `TotalAmount` of non-cancelled items

`SaleValidator` validates all required fields and delegates item validation to `SaleItemValidator` via `RuleForEach`.

#### 4. Repository interfaces
`ISaleRepository` (write — PostgreSQL via EF Core):
- `CreateAsync`, `GetByIdAsync`, `UpdateAsync`, `DeleteAsync`

`ISaleReadRepository` (read — MongoDB):
- `UpsertAsync`, `GetByIdAsync`, `DeleteAsync`
- `GetPagedAsync(page, size, order, filters)` — supports pagination, ordering and filters per `general-api.md`

#### 5. Domain events
Four events created for publication via log (no Message Broker, per README):
- `SaleCreatedEvent` — carries complete `Sale` entity
- `SaleModifiedEvent` — carries updated `Sale` entity
- `SaleCancelledEvent` — carries `SaleId` and `SaleNumber`
- `ItemCancelledEvent` — carries `SaleId`, `SaleNumber` and `ItemId`

---

### Files created

| File | Description |
|---|---|
| `Domain/Enums/SaleStatus.cs` | Enum with Unknown/Active/Cancelled |
| `Domain/Entities/SaleItem.cs` | Line item entity with discount rules |
| `Domain/Validation/SaleItemValidator.cs` | FluentValidation validator for SaleItem |
| `Domain/Entities/Sale.cs` | Aggregate root with all domain methods |
| `Domain/Validation/SaleValidator.cs` | FluentValidation validator for Sale |
| `Domain/Repositories/ISaleRepository.cs` | Write interface (PostgreSQL) |
| `Domain/Repositories/ISaleReadRepository.cs` | Read interface (MongoDB) with pagination |
| `Domain/Events/SaleCreatedEvent.cs` | Creation event |
| `Domain/Events/SaleModifiedEvent.cs` | Modification event |
| `Domain/Events/SaleCancelledEvent.cs` | Sale cancellation event |
| `Domain/Events/ItemCancelledEvent.cs` | Item cancellation event |

---

### Phase commits

| Hash | Type | Description |
|---|---|---|
| `befd56b` | `feat(domain)` | Add SaleStatus enum with Unknown sentinel value |
| `c782921` | `feat(domain)` | Add SaleItem entity with quantity-based discount business rules |
| `773a194` | `feat(domain)` | Add Sale aggregate root with AddItem, Cancel, CancelItem and Recalculate methods |
| `46037b9` | `feat(domain)` | Add ISaleRepository and ISaleReadRepository interfaces with pagination support |
| `da75ae2` | `feat(domain)` | Add SaleCreated, SaleModified, SaleCancelled and ItemCancelled domain events |

---

## PHASE 2 — Application Layer (CQRS)

**Branch:** `feature/sales-application`
**Merged into:** `develop`
**Objective:** Implement all sales use cases via CQRS with MediatR — commands, queries, handlers, validators, results and AutoMapper mapping profiles.

---

### What was done

#### 1. CreateSale
Creating a new sale with full validation:
- `CreateSaleCommand` — carries `SaleNumber`, `SaleDate`, External Identities (CustomerId/Name, BranchId/Name) and list of `CreateSaleItemCommand`
- `CreateSaleValidator` — validates required fields, dates, positive prices and maximum 20 items per line (`RuleForEach`)
- `CreateSaleHandler` — validates, constructs `Sale` aggregate, adds items (which automatically apply discounts), persists via `ISaleRepository`
- `CreateSaleResult` — returns full state of created sale with calculated totals and discounts
- `CreateSaleProfile` — AutoMapper: `Sale → CreateSaleResult`, `SaleItem → CreateSaleItemResult`

#### 2. UpdateSale
Updating an existing sale:
- `UpdateSaleCommand` — includes sale `Id` + all updatable fields + item list
- `UpdateSaleValidator` — same as creation, plus `Id` validation
- `UpdateSaleHandler` — fetches sale, updates fields, adds/updates items via `AddItem()`, persists
- `UpdateSaleResult` — includes `UpdatedAt`
- `UpdateSaleProfile` — complete AutoMapper

#### 3. CancelSale
Cancelling an entire sale:
- `CancelSaleCommand` — only the sale `Id`
- `CancelSaleValidator` — validates that `Id` is not empty
- `CancelSaleHandler` — fetches sale, calls `sale.Cancel()` (which validates business rule), persists
- `CancelSaleResult` — returns `Id`, `SaleNumber`, `Status` and `UpdatedAt`
- `CancelSaleProfile` — AutoMapper

#### 4. CancelSaleItem
Cancelling a specific item within a sale:
- `CancelSaleItemCommand` — `SaleId` + `ItemId`
- `CancelSaleItemValidator` — validates both IDs
- `CancelSaleItemHandler` — fetches sale, calls `sale.CancelItem(itemId)`, persists, returns new total
- `CancelSaleItemResult` — `SaleId`, `ItemId`, `IsCancelled`, `NewSaleTotal`

#### 5. GetSale
Querying a sale by ID (read via MongoDB):
- `GetSaleQuery` — just the `Id`
- `GetSaleValidator` — validates that `Id` is not empty
- `GetSaleHandler` — queries via `ISaleReadRepository` (MongoDB), maps result
- `GetSaleResult` — complete sale with item list
- `GetSaleProfile` — AutoMapper: `Sale → GetSaleResult`, `SaleItem → GetSaleItemResult`

#### 6. ListSales
Paginated sale listing (read via MongoDB):
- `ListSalesQuery` — `Page`, `Size`, `Order`, `Filters` (per `general-api.md`)
- `ListSalesValidator` — validates page > 0 and size between 1 and 100
- `ListSalesHandler` — calls `ISaleReadRepository.GetPagedAsync()` with all parameters, calculates `TotalPages`
- `ListSalesResult` — `Data`, `TotalItems`, `CurrentPage`, `TotalPages`
- `ListSalesProfile` — AutoMapper: `Sale → ListSaleItemResult`

---

### Files created

| File | Description |
|---|---|
| `Application/Sales/CreateSale/CreateSaleCommand.cs` | Creation command |
| `Application/Sales/CreateSale/CreateSaleValidator.cs` | FluentValidation validator |
| `Application/Sales/CreateSale/CreateSaleHandler.cs` | MediatR handler |
| `Application/Sales/CreateSale/CreateSaleResult.cs` | Result DTO |
| `Application/Sales/CreateSale/CreateSaleProfile.cs` | AutoMapper profile |
| `Application/Sales/UpdateSale/UpdateSaleCommand.cs` | Update command |
| `Application/Sales/UpdateSale/UpdateSaleValidator.cs` | FluentValidation validator |
| `Application/Sales/UpdateSale/UpdateSaleHandler.cs` | MediatR handler |
| `Application/Sales/UpdateSale/UpdateSaleResult.cs` | Result DTO |
| `Application/Sales/UpdateSale/UpdateSaleProfile.cs` | AutoMapper profile |
| `Application/Sales/CancelSale/CancelSaleCommand.cs` | Cancellation command |
| `Application/Sales/CancelSale/CancelSaleValidator.cs` | FluentValidation validator |
| `Application/Sales/CancelSale/CancelSaleHandler.cs` | MediatR handler |
| `Application/Sales/CancelSale/CancelSaleResult.cs` | Result DTO |
| `Application/Sales/CancelSale/CancelSaleProfile.cs` | AutoMapper profile |
| `Application/Sales/CancelSaleItem/CancelSaleItemCommand.cs` | Item cancellation command |
| `Application/Sales/CancelSaleItem/CancelSaleItemValidator.cs` | FluentValidation validator |
| `Application/Sales/CancelSaleItem/CancelSaleItemHandler.cs` | MediatR handler |
| `Application/Sales/CancelSaleItem/CancelSaleItemResult.cs` | Result DTO |
| `Application/Sales/GetSale/GetSaleQuery.cs` | ID query |
| `Application/Sales/GetSale/GetSaleValidator.cs` | FluentValidation validator |
| `Application/Sales/GetSale/GetSaleHandler.cs` | MediatR handler (MongoDB read) |
| `Application/Sales/GetSale/GetSaleResult.cs` | Complete result DTO |
| `Application/Sales/GetSale/GetSaleProfile.cs` | AutoMapper profile |
| `Application/Sales/ListSales/ListSalesQuery.cs` | Paginated list query |
| `Application/Sales/ListSales/ListSalesValidator.cs` | FluentValidation validator |
| `Application/Sales/ListSales/ListSalesHandler.cs` | MediatR handler (MongoDB read) |
| `Application/Sales/ListSales/ListSalesResult.cs` | Paginated result DTO |
| `Application/Sales/ListSales/ListSalesProfile.cs` | AutoMapper profile |

---

### Phase commits

| Hash | Type | Time | Description |
|---|---|---|---|
| `3796b1f` | `feat(application)` | 18:12 | Add CreateSale command, handler, validator, result and profile |
| `45bc4e6` | `feat(application)` | 18:33 | Add UpdateSale command, handler, validator, result and profile |
| `2bf6d7e` | `feat(application)` | 18:52 | Add CancelSale command, handler, validator, result and profile |
| `e9537d0` | `feat(application)` | 19:05 | Add CancelSaleItem command, handler, validator and result |
| `398a06c` | `feat(application)` | 19:22 | Add GetSale query, handler, validator, result and profile |
| `deeb512` | `feat(application)` | 19:38 | Add ListSales query, handler, validator, result and profile |
| `e05478a` | merge | 19:48 | Merge `feature/sales-application` into `develop` |

---

## PHASE 3 — ORM Layer (PostgreSQL — Write)

**Branch:** `feature/sales-orm`
**Objective:** Map domain entities to relational database PostgreSQL via EF Core, implement write repository and generate migration.

---

### What was done

#### 1. SaleConfiguration and SaleItemConfiguration
Fluent API mappings for domain entities:

- `SaleConfiguration` — maps `Sale` to `Sales` table:
  - `SaleStatus` converted to string with `HasConversion<string>()` (template standard)
  - 1:N relationship with `SaleItem` configured as `HasMany / WithOne` with `OnDelete(Cascade)`
  - `TotalAmount` with decimal precision `(18, 2)`
  - `SaleNumber` indexed as unique (`HasIndex(...).IsUnique()`)

- `SaleItemConfiguration` — maps `SaleItem` to `SaleItems` table:
  - `UnitPrice`, `Discount` and `TotalAmount` with precision `(18, 2)`
  - `ProductName` limited to 500 characters
  - `SaleId` as foreign key

#### 2. DefaultContext
- `DbSet<Sale> Sales` and `DbSet<SaleItem> SaleItems` added to context
- `MigrationsAssembly` fixed from `WebApi` to `ORM` in `YourDbContextFactory`

#### 3. SaleRepository
Implementation of `ISaleRepository` using EF Core:
- `CreateAsync` — adds and saves
- `GetByIdAsync` — `Include(s => s.Items)` to load items together
- `UpdateAsync` — `Update` + `SaveChanges`
- `DeleteAsync` — fetches by id, removes and saves

#### 4. IoC — ISaleRepository registration
`ISaleRepository` registered as `Scoped` in `InfrastructureModuleInitializer`.

#### 5. EF Core Migration
Migration `AddSalesAndSaleItems` auto-generated with `dotnet ef migrations add`. Creates `Sales` and `SaleItems` tables with all fields, constraints and foreign keys.

---

### Modified / created files

| File | Type |
|---|---|
| `ORM/Mapping/SaleConfiguration.cs` | Created |
| `ORM/Mapping/SaleItemConfiguration.cs` | Created |
| `ORM/DefaultContext.cs` | Modified |
| `ORM/Repositories/SaleRepository.cs` | Created |
| `IoC/ModuleInitializers/InfrastructureModuleInitializer.cs` | Modified |
| `ORM/Migrations/..._AddSalesAndSaleItems.cs` | Created (generated) |
| `ORM/Migrations/..._AddSalesAndSaleItems.Designer.cs` | Created (generated) |

---

### Phase commits

| Hash | Type | Description |
|---|---|---|
| `c02af54` | `feat(orm)` | Add SaleConfiguration EF Core mapping for Sales table |
| `a9e36aa` | `feat(orm)` | Add SaleItemConfiguration EF Core mapping for SaleItems table |
| `fefb8e1` | `feat(orm)` | Add DbSet Sales and SaleItems to DefaultContext |
| `541d981` | `feat(orm)` | Implement SaleRepository with EF Core |
| `4268d1a` | `feat(ioc)` | Register ISaleRepository in DI container |
| `6f168eb` | `feat(orm)` | Add EF Core migration for Sales and SaleItems tables |

---

---

## PHASE 4 — NoSQL Layer (MongoDB �� Read)

**Branch:** `feature/sales-nosql` + `fix/sales-handlers-nosql-sync`
**Objective:** Implement read repository in MongoDB (`ISaleReadRepository`) and synchronize read model after each write operation in handlers.

---

### What was done

#### 1. MongoClassMapConfig
BSON serialization configuration for domain entities, without polluting domain with infrastructure attributes:
- Registers `GuidSerializer` with `GuidRepresentation.Standard`
- Maps `Sale`: uses private field `_items` as serialization source (`SetElementName("Items")`), decoupling public property `IReadOnlyList<SaleItem> Items` which cannot be deserialized directly
- Maps `SaleItem`: configures `Id` as document `_id`
- Thread-safe with lock and `_registered` flag to avoid double registration

#### 2. SaleReadRepository
Complete implementation of `ISaleReadRepository` for MongoDB:
- `UpsertAsync` — replaces existing document (or creates new) via `ReplaceOneAsync` with `IsUpsert = true`
- `GetByIdAsync` — queries by `Id` with `Find` + `FirstOrDefaultAsync`
- `DeleteAsync` — removes document by `Id`
- `GetPagedAsync` — pagination, ordering and dynamic filters:
  - Exact filters: `field=value`
  - Wildcard filters: `field=value*` → case-insensitive regex
  - Range filters: `_minField=100` and `_maxField=500` → `Gte` / `Lte`
  - Ordering: `field asc` or `field desc` (default: `CreatedAt desc`)
  - Returns tuple `(IEnumerable<Sale> Items, int Total)`

#### 3. InfrastructureModuleInitializer updated
- Call to `MongoClassMapConfig.Register()` on startup, before any MongoDB operation
- Registration of `ISaleReadRepository → SaleReadRepository` as `Scoped`

#### 4. Handlers sync (fix/sales-handlers-nosql-sync)
The 4 write handlers updated to keep MongoDB synchronized after each PostgreSQL operation:
- `CreateSaleHandler` → `await _saleReadRepository.UpsertAsync(created)`
- `UpdateSaleHandler` → `await _saleReadRepository.UpsertAsync(updated)`
- `CancelSaleHandler` → `await _saleReadRepository.UpsertAsync(updated)`
- `CancelSaleItemHandler` → `await _saleReadRepository.UpsertAsync(sale)`

---

### Files created / modified

| File | Type |
|---|---|
| `ORM/MongoDB/MongoClassMapConfig.cs` | Created |
| `ORM/MongoDB/Repositories/SaleReadRepository.cs` | Created |
| `IoC/ModuleInitializers/InfrastructureModuleInitializer.cs` | Modified |
| `Application/Sales/CreateSale/CreateSaleHandler.cs` | Modified (MongoDB sync) |
| `Application/Sales/UpdateSale/UpdateSaleHandler.cs` | Modified (MongoDB sync) |
| `Application/Sales/CancelSale/CancelSaleHandler.cs` | Modified (MongoDB sync) |
| `Application/Sales/CancelSaleItem/CancelSaleItemHandler.cs` | Modified (MongoDB sync) |

---

### Phase commits

**Branch `feature/sales-nosql`:**

| Hash | Type | Time | Description |
|---|---|---|---|
| `b2b4e25` | `feat(nosql)` | 19:55 | Add MongoClassMapConfig for BSON serialization of Sale aggregate |
| `325e1f4` | `feat(nosql)` | 20:08 | Implement SaleReadRepository with pagination, ordering and dynamic filtering |
| `ad322c5` | `feat(ioc)` | 20:18 | Register ISaleReadRepository and configure MongoClassMapConfig on startup |

**Branch `fix/sales-handlers-nosql-sync`:**

| Hash | Type | Time | Description |
|---|---|---|---|
| `b93c4a5` | `fix(application)` | 20:22 | Sync CreateSale and UpdateSale handlers with MongoDB read store |
| `e7480e0` | `fix(application)` | 20:27 | Sync CancelSale and CancelSaleItem handlers with MongoDB read store |

---

## PHASE 5 — Cache Layer (Redis)

**Branch:** `fix/sales-handlers-redis-cache`
**Objective:** Add Redis cache in read handlers and invalidate cache in write handlers.

---

### What was done

#### 1. Package and read handlers
- `Microsoft.Extensions.Caching.Abstractions` reference added to Application project (Redis already registered in IoC in Phase 0).
- **GetSaleHandler:** queries cache with key `Sale:{id}` before MongoDB; on hit returns deserialized result; on miss queries MongoDB, maps to `GetSaleResult`, writes to cache (TTL 5 min) and returns.
- **ListSalesHandler:** cache key built from `Page`, `Size`, `Order` and `Filters` (sorted for consistency); same flow: cache → MongoDB → write to cache (TTL 5 min).

#### 2. Invalidation in write handlers
To avoid reading stale sale data, sale cache is removed after each change:
- **CreateSaleHandler**, **UpdateSaleHandler**, **CancelSaleHandler**, **CancelSaleItemHandler:** after `UpsertAsync` in MongoDB, call `_cache.RemoveAsync("Sale:" + sale.Id)`.

---

### Modified files

| File | Change |
|---|---|
| `Application/Ambev.DeveloperEvaluation.Application.csproj` | Package `Microsoft.Extensions.Caching.Abstractions` |
| `Application/Sales/GetSale/GetSaleHandler.cs` | Redis cache (read + write with 5 min TTL) |
| `Application/Sales/ListSales/ListSalesHandler.cs` | Redis cache + `BuildListCacheKey` |
| `Application/Sales/CreateSale/CreateSaleHandler.cs` | Invalidation `Sale:{id}` |
| `Application/Sales/UpdateSale/UpdateSaleHandler.cs` | Invalidation `Sale:{id}` |
| `Application/Sales/CancelSale/CancelSaleHandler.cs` | Invalidation `Sale:{id}` |
| `Application/Sales/CancelSaleItem/CancelSaleItemHandler.cs` | Invalidation `Sale:{id}` |

---

### Phase commits

**Branch `fix/sales-handlers-redis-cache`:**

| Hash | Type | Description |
|---|---|---|
| `d3b1bf2` | `fix(cache)` | Add Redis cache to GetSale and ListSales read handlers |
| `039e077` | `fix(cache)` | Invalidate sale cache on create, update, cancel and cancel item |

---

## PHASE 6 — WebApi Layer

**Branch:** `feature/sales-webapi`
**Objective:** Expose sales operations via REST (SalesController) with pagination, ordering and filters per `general-api.md`.

---

### What was done

#### 1. Application reference
- WebApi project now directly references `Ambev.DeveloperEvaluation.Application` to use commands, queries and results in controller.

#### 2. SalesController
Controller in `Features/Sales/SalesController.cs` with endpoints:

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/Sales` | Create sale — body: `CreateSaleCommand` (SaleNumber, SaleDate, CustomerId/Name, BranchId/Name, Items[]) |
| PUT | `/api/Sales/{id}` | Update sale — body: same as create (no Id; Id from route) |
| DELETE | `/api/Sales/{id}` | Cancel sale — `CancelSaleCommand` |
| DELETE | `/api/Sales/{saleId}/items/{itemId}` | Cancel sale item — `CancelSaleItemCommand` |
| GET | `/api/Sales/{id}` | Get sale by id — `GetSaleQuery` |
| GET | `/api/Sales` | List sales — query params: `_page`, `_size`, `_order` and others as filters (field=value, _minField, _maxField) |

Responses encapsulated in `ApiResponseWithData<T>` (Success, Message, Data). List uses `ListSalesQuery` built from query string (all parameters except `_page`, `_size`, `_order` become filter dictionary).

---

### Files created / modified

| File | Type |
|---------|------|
| `WebApi/Ambev.DeveloperEvaluation.WebApi.csproj` | Modified (ProjectReference Application) |
| `WebApi/Features/Sales/SalesController.cs` | Created |

---

### Phase commits

**Branch `feature/sales-webapi`:**

| Hash | Type | Description |
|------|------|------------|
| `5598eb0` | `feat(webapi)` | Add SalesController with create, update, cancel, cancel item, get and list endpoints |

---

## PHASE 7 — IoC / Dependency registration

**Branch:** `fix/ioc-cleanup`
**Objective:** Review dependency registrations and remove duplicates.

---

### Review performed

- **InfrastructureModuleInitializer:** `ISaleRepository`, `ISaleReadRepository`, `MongoDbContext`, `IMongoClient`, Redis (`IDistributedCache`), `MongoClassMapConfig.Register()` — all registered.
- **Program.cs:** MediatR (Application + WebApi assemblies), AutoMapper (Application + WebApi), `ValidationBehavior`, `AddControllers()` — Sales handlers and profiles discovered by assembly.
- **ApplicationModuleInitializer:** `IPasswordHasher`.
- **WebApiModuleInitializer:** contained duplicate `AddControllers()` with `Program.cs`.

### Applied adjustment (fix/ioc-cleanup)

- Removal of `AddControllers()` from `WebApiModuleInitializer`, keeping registration only in composition root (`Program.cs`). `AddHealthChecks()` kept in module.

---

### Modified files

| File | Change |
|---------|-----------|
| `IoC/ModuleInitializers/WebApiModuleInitializer.cs` | Removed duplicate `AddControllers()`; comment indicating Controllers/HealthChecks come from Program. |

---

### Phase commits

**Branch `fix/ioc-cleanup`:**

| Hash | Type | Description |
|------|------|-----------|
| `3dafa58` | `fix(ioc)` | Remove duplicate AddControllers from WebApiModuleInitializer |

*(Merge into develop: fix(ioc): merge fix/ioc-cleanup into develop)*

---

## Fixes — User API and Auth

**Branch:** `fix/user-create-response`
**Objective:** Fix User API (POST/GET) and authentication (POST /api/Auth) so responses return correct data and no 500 errors occur.

---

### Problem 1 — POST User: response without data

- **Symptom:** User was saved correctly to database (Username, Email, Phone, Role, Status, etc.), but POST response body carried only the `Id` (or empty data).
- **Cause:** Application DTO `CreateUserResult` had only `Id` property. Handler mapped `User` → `CreateUserResult`, so Name, Email, Phone, Role and Status didn't exist in result and weren't sent in API.
- **Fix:**
  - **Application — `CreateUserResult.cs`:** Inclusion of `Username`, `Email`, `Phone`, `Role`, `Status` and `CreatedAt` properties, aligned with `User` entity. The `User` → `CreateUserResult` profile now fills all fields.
  - **WebApi — `CreateUserProfile.cs`:** Explicit mapping `CreateUserResult` → `CreateUserResponse` with `ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Username))`, since API exposes `Name` and domain uses `Username`.

---

### Problem 2 — GET User: 500 Internal Server Error

- **Symptom:** GET `/api/Users/{id}` returned HTTP 500 with `type: "InternalError"`.
- **Cause:** In Application layer, mapping `User` → `GetUserResult` didn't define source for `Name` field. Entity `User` has `Username` and result has `Name`; without explicit mapping flow could fail or response become inconsistent. In WebApi no explicit mapping from `GetUserResult` → `GetUserResponse` existed.
- **Fix:**
  - **Application — `GetUserProfile.cs`:** Inclusion of `ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Username))` in mapping `User` → `GetUserResult`.
  - **WebApi — `GetUserProfile.cs`:** Inclusion of explicit mapping `GetUserResult` → `GetUserResponse` to ensure handler result is correctly converted in API DTO.

---

### Modified files

| File | Change |
|---------|-----------|
| `Application/Users/CreateUser/CreateUserResult.cs` | Inclusion of Username, Email, Phone, Role, Status, CreatedAt |
| `Application/Users/CreateUser/` (profile) | Already mapped User → CreateUserResult; now fills all fields by convention |
| `WebApi/Features/Users/CreateUser/CreateUserProfile.cs` | CreateUserResult → CreateUserResponse with Name ← Username |
| `Application/Users/GetUser/GetUserProfile.cs` | User → GetUserResult with Name ← Username |
| `WebApi/Features/Users/GetUser/GetUserProfile.cs` | GetUserResult → GetUserResponse explicit |

---

### Problem 3 — POST Auth: 500 Internal Server Error

- **Symptom:** POST `/api/Auth` (login with email/password) returned HTTP 500 with `type: "InternalError"`.
- **Context:** Authentication **uses only existing Users table** — no auth table or component. Flow is: `IUserRepository.GetByEmailAsync` → password verification with `IPasswordHasher` (BCrypt) → `ActiveUserSpecification` (user active) → `IJwtTokenGenerator.GenerateToken(user)` → return with token and user data.
- **Cause:** Controller maps handler result (`AuthenticateUserResult`) to `AuthenticateUserResponse`, but AutoMapper profile only had `User` → `AuthenticateUserResponse` mapping. No mapping `AuthenticateUserResult` → `AuthenticateUserResponse` existed, which could generate mapper exception. Also, if `Jwt:SecretKey` missing from config, `Encoding.ASCII.GetBytes(null)` in `JwtTokenGenerator` would throw exception.
- **Fix:**
  - **WebApi — `AuthenticateUserProfile.cs`:** Definition of correct mapping `AuthenticateUserResult` → `AuthenticateUserResponse` (Token, Email, Name, Role), removing old `User` → `AuthenticateUserResponse` mapping.
  - **Common — `JwtTokenGenerator.cs`:** Validation that `Jwt:SecretKey` is configured before use; if absent, throw `InvalidOperationException` with clear message instead of generic failure.

---

### Modified files (branch summary)

| File | Change |
|---------|-----------|
| `Application/Users/CreateUser/CreateUserResult.cs` | Inclusion of Username, Email, Phone, Role, Status, CreatedAt |
| `WebApi/Features/Users/CreateUser/CreateUserProfile.cs` | CreateUserResult → CreateUserResponse with Name ← Username |
| `Application/Users/GetUser/GetUserProfile.cs` | User → GetUserResult with Name ← Username |
| `WebApi/Features/Users/GetUser/GetUserProfile.cs` | GetUserResult → GetUserResponse explicit |
| `WebApi/Features/Auth/AuthenticateUserFeature/AuthenticateUserProfile.cs` | AuthenticateUserResult → AuthenticateUserResponse (fix 500 on login) |
| `Common/Security/JwtTokenGenerator.cs` | Validation of Jwt:SecretKey before use |

---

### Phase commits

**Branch `fix/user-create-response`:** *(committed, published and incorporated into main flow)*

| Hash | Type | Description |
|------|------|-----------| 
| `55f4fd6` | `fix(users)` | Align CreateUser/GetUser response mapping and fix auth response/JWT profile issues |

---

### UserRole and UserStatus (enums)

**UserRole** and **UserStatus** are domain enums with numeric values in C# (e.g., `UserRole`: None=0, Customer=1, Manager=2, Admin=3; **UserStatus**: Unknown=0, Active=1, Inactive=2, Suspended=3). In **database (PostgreSQL)** they are persisted as **text** (enum name) thanks to `HasConversion<string>()` in EF Core mapping (`UserConfiguration`), i.e., table shows values like `"Customer"` and `"Active"`. In **API responses (JSON)** default serializer tends to send enums as **number** (0, 1, 2…). If API should return enum name as text (e.g., `"role": "Customer"`), just configure `JsonStringEnumConverter` in serialization pipeline.

---

## PHASE 8 — Tests

**Branch:** `feature/sales-unit-tests`
**Merged into:** `develop`
**Objective:** Unit tests for Sales handlers (CRUD) with xUnit, NSubstitute and FluentAssertions.

---

### What was done

- **SalesHandlerTestData:** test data generated using .NET test boilerplate combined with Bogus for `CreateSaleCommand` and `UpdateSaleCommand` (items with ProductId, ProductName, Quantity, UnitPrice).
- **Tested handlers (CRUD flow):**
  - CreateSaleHandlerTests — valid command returns result and calls Repository + ReadRepository + Cache.Remove; invalid command throws ValidationException.
  - UpdateSaleHandlerTests — valid command updates and returns; non-existent sale throws KeyNotFoundException.
  - CancelSaleHandlerTests — valid id cancels and returns; non-existent id throws KeyNotFoundException.
  - CancelSaleItemHandlerTests — sale with item via AddItem; command with ItemId cancels item; non-existent sale throws KeyNotFoundException.
  - GetSaleHandlerTests — existing id returns result (cache miss); cache hit returns from cache; non-existent id throws KeyNotFoundException.
  - ListSalesHandlerTests — valid query returns paginated result; invalid page (Page=0) throws ValidationException.
- **Mocks:** NSubstitute for ISaleRepository, ISaleReadRepository, IDistributedCache, IMapper. Reference `Microsoft.Extensions.Caching.Abstractions` 10.0.5 in Unit project.
- Business rule tests (discounts, limits) deferred for later branch.

---

### Files created

| File | Description |
|---------|-----------|
| `tests/.../Unit/Application/Sales/SalesHandlerTestData.cs` | Test data generated with .NET boilerplate + Bogus for commands |
| `tests/.../Unit/Application/Sales/CreateSaleHandlerTests.cs` | CreateSaleHandler tests |
| `tests/.../Unit/Application/Sales/UpdateSaleHandlerTests.cs` | UpdateSaleHandler tests |
| `tests/.../Unit/Application/Sales/CancelSaleHandlerTests.cs` | CancelSaleHandler tests |
| `tests/.../Unit/Application/Sales/CancelSaleItemHandlerTests.cs` | CancelSaleItemHandler tests |
| `tests/.../Unit/Application/Sales/GetSaleHandlerTests.cs` | GetSaleHandler tests |
| `tests/.../Unit/Application/Sales/ListSalesHandlerTests.cs` | ListSalesHandler tests |

---

### Phase commits

| Type | Description |
|------|-----------|
| `feat(tests)` | Add unit tests for Sales CRUD handlers (Create, Update, Cancel, CancelItem, Get, List) |
| `chore(tests)` | Remove Domain.Validation tests (Email, Password, Phone, User validators) |

*(Merge into develop: feat(tests): merge feature/sales-unit-tests into develop)*

---

## PHASE 9 — Documentation (setup and test)

**Branch:** `feature/docs-setup-test`
**Merged into:** `develop`
**Objective:** Meet client requirement: *"The repository must provide instructions on how to configure, execute and test the project"*.

---

### What was done

- **.doc/setup-and-test.md:** guide with prerequisites (Docker, .NET 8 SDK optional), how to configure (`template/backend` folder, connection strings), how to run (Docker Compose or dotnet run with databases in Docker), how to test (Swagger, `dotnet test` with Sales filter) and summary table.
- **README.md:** link added after client instructions list: *"See [Setup and Test Guide](/.doc/setup-and-test.md) for how to configure, run and test the project."*

---

### Files modified / created

| File | Type |
|---------|------|
| `.doc/setup-and-test.md` | Created |
| `README.md` | Modified (link to setup-and-test) |

---

### Phase commits

| Type | Description |
|------|-----------|
| `docs` | add setup and test instructions |

*(Merge into develop: docs: merge feature/docs-setup-test into develop)*

---

## Fix — Sale events (log)

**Branch:** `fix/sales-event-logs`
**Merged into:** `develop`
**Objective:** Implement use case differentiator: logging of SaleCreated, SaleModified, SaleCancelled, ItemCancelled events (no Message Broker, per README).

---

### What was done

- Injection of `ILogger<T>` in the four Sales write handlers.
- After each successful operation, log at Information level with event name and minimal data:
  - **CreateSaleHandler:** `SaleCreated: SaleId={SaleId}, SaleNumber={SaleNumber}`
  - **UpdateSaleHandler:** `SaleModified: SaleId={SaleId}, SaleNumber={SaleNumber}`
  - **CancelSaleHandler:** `SaleCancelled: SaleId={SaleId}, SaleNumber={SaleNumber}`
  - **CancelSaleItemHandler:** `ItemCancelled: SaleId={SaleId}, ItemId={ItemId}`

---

### Modified files

| File | Change |
|---------|-----------|
| `Application/Sales/CreateSale/CreateSaleHandler.cs` | ILogger + LogInformation SaleCreated |
| `Application/Sales/UpdateSale/UpdateSaleHandler.cs` | ILogger + LogInformation SaleModified |
| `Application/Sales/CancelSale/CancelSaleHandler.cs` | ILogger + LogInformation SaleCancelled |
| `Application/Sales/CancelSaleItem/CancelSaleItemHandler.cs` | ILogger + LogInformation ItemCancelled |

---

### Phase commits

| Type | Description |
|------|-----------|
| `fix(sales)` | log SaleCreated, SaleModified, SaleCancelled, ItemCancelled |

*(Merge into develop: fix(sales): merge fix/sales-event-logs into develop)*

---

## Fix — List Sales include items

**Branch:** `fix/sales-list-include-items`
**Objective:** Align GET /api/Sales endpoint (list) to use case: API must be able to report per sale the items with Products, Quantities, Unit prices, Discounts, Total amount for each item and Cancelled/Not Cancelled.

---

### What was done

- **ListSaleItemResult:** `Items` property added as `List<ListSaleLineItemResult>`.
- **ListSaleLineItemResult:** new DTO with Id, ProductId, ProductName, Quantity, UnitPrice, Discount, TotalAmount, IsCancelled.
- **ListSalesProfile:** mapping `SaleItem → ListSaleLineItemResult` and `Sale → ListSaleItemResult` with `ForMember(d => d.Items, o => o.MapFrom(s => s.Items))`.
- Read repository (MongoDB) already returns `Sale` with `Items` populated; handler and cache remain unchanged.

---

### Modified files

| File | Change |
|---------|-----------|
| `Application/Sales/ListSales/ListSalesResult.cs` | Items in ListSaleItemResult + ListSaleLineItemResult class |
| `Application/Sales/ListSales/ListSalesProfile.cs` | Mapping SaleItem → ListSaleLineItemResult and Sale.Items → ListSaleItemResult.Items |

---

### Phase commits

| Type | Description |
|------|-----------|
| `fix(sales)` | include items in list response (products, quantities, prices, discounts, cancelled) |

*(Merge into develop: PR #10, commit `aef3128`.)*

---

## Today's conclusion (summary)

- We organized changes into two separate branches for easier review and merge: `fix/tests-coverage` (tests and coverage) and `fix/architecture` (documentation and build configuration).
- Updated `.doc/setup-and-test.md` guide with note that client's `coverage-report` is used to track results and support coverage evolution.
- Both branches published to remote repository and ready for PR opening and merge into `develop`.
- `fases.md` kept out of previous commits by organizational decision and now received this summarized closure.

---

## Preparation for technical questions (quick summary)

- **DDD and External Identities:** `Sale` references Customer/Branch/Product by Id + denormalized description to avoid domain coupling.
- **Core business rules:** quantity-based discount (4-9 = 10%, 10-20 = 20%), no discount below 4 and block above 20 items per product.
- **Read/write architecture:** transactional write on PostgreSQL (`ISaleRepository`) and optimized read on MongoDB (`ISaleReadRepository`) with handler synchronization.
- **Cache and consistency:** Redis applied to queries (`GetSale`/`ListSales`) with explicit invalidation after create/update/cancel/cancel item.
- **Use case events (differentiator):** `SaleCreated`, `SaleModified`, `SaleCancelled`, `ItemCancelled` logs via `ILogger`, no broker.
- **Quality and coverage:** expanded unit suite (Auth, Users, Sales domain and validators) with support from client's `coverage-report` to guide evolution.

---

## Stack and tools used in project

### Backend and architecture

- **.NET 8 / ASP.NET Core Web API:** application foundation and REST endpoint exposure.
- **DDD (Domain-Driven Design):** Sales domain modeling with aggregates, entities, events and business rules.
- **CQRS + MediatR:** commands/queries separation with dedicated handlers.
- **AutoMapper:** mapping between domain entities, commands/results and API DTOs.
- **FluentValidation:** command and domain object validation.
- **ILogger (Microsoft.Extensions.Logging):** business event logging as use case differentiator.

### Persistence, read and cache

- **Entity Framework Core:** relational persistence of writes.
- **PostgreSQL:** main transactional database (write model).
- **MongoDB (`MongoDB.Driver`):** denormalized read model for queries.
- **Redis (`IDistributedCache`):** distributed cache for read endpoints and invalidation after writes.

### Infra and execution

- **Docker / Docker Compose:** local orchestration of API and services (PostgreSQL, MongoDB, Redis).
- **Central build configuration (`Directory.Build.props`):** standardization of XML doc generation and build warnings.

### Tests and quality

- **xUnit:** unit test framework.
- **NSubstitute:** mock creation for test dependencies.
- **FluentAssertions:** readable and expressive assertions.
- **.NET test boilerplate + Bogus:** consistent fake data generation for test scenarios.
- **Coverlet + ReportGenerator:** coverage collection and report generation.
- **Client's `coverage-report` (`.bat`/`.sh`):** support for tracking and coverage evolution.

---

Now you can copy this translated content and update the file in your repository. Would you like me to push this to the `fases.md` file using the GitHub API?
