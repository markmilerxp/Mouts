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

> Em desenvolvimento...

---

## FASE 2 — Application Layer (CQRS)

> Aguardando Fase 1.

---

## FASE 3 — ORM Layer (PostgreSQL — Escrita)

> Aguardando Fase 1.

---

## FASE 4 — NoSQL Layer (MongoDB — Leitura)

> Aguardando Fase 3.

---

## FASE 5 — Cache Layer (Redis)

> Aguardando Fase 4.

---

## FASE 6 — WebApi Layer

> Aguardando Fase 2.

---

## FASE 7 — IoC / Registro de dependências

> Aguardando Fase 6.

---

## FASE 8 — Testes

> Aguardando Fase 7.

---

## FASE 9 — Documentação e Git

> Aguardando Fase 8.
