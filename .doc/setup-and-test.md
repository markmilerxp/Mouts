# Como configurar, executar e testar o projeto

Este documento descreve como configurar o ambiente, executar a API e rodar os testes.

---

## Pré-requisitos

- **Docker** e **Docker Compose** (para subir a API com PostgreSQL, MongoDB e Redis)
- Opcional: **.NET 8 SDK** (se quiser rodar a API ou os testes localmente sem Docker)

---

## Configurar

1. Clone o repositório (ou use o template já baixado).
2. Entre na pasta do backend:
   ```bash
   cd template/backend
   ```
3. As connection strings já vêm configuradas no `docker-compose.yml` para o ambiente em containers. Se for rodar a WebApi **fora** do Docker (com .NET SDK), use os mesmos valores em `src/Ambev.DeveloperEvaluation.WebApi/appsettings.Development.json` ou variáveis de ambiente:
   - **PostgreSQL:** `Host=localhost;Port=5432;Database=developer_evaluation;Username=developer;Password=ev@luAt10n`
   - **MongoDB:** `mongodb://developer:ev%40luAt10n@localhost:27017`
   - **Redis:** `localhost:6379,password=ev@luAt10n`

---

## Executar

### Opção 1 — Tudo com Docker (recomendado)

Na pasta `template/backend`:

```bash
docker compose up --build
```

A API sobe em:

- **HTTP:** http://localhost:8080  
- **HTTPS:** https://localhost:8081  

Os serviços de dados ficam em:

- PostgreSQL: `localhost:5432`
- MongoDB: `localhost:27017`
- Redis: `localhost:6379`

### Opção 2 — API com .NET SDK (bancos no Docker)

1. Suba só os bancos:
   ```bash
   cd template/backend
   docker compose up -d ambev.developerevaluation.database ambev.developerevaluation.nosql ambev.developerevaluation.cache
   ```
2. Rode a WebApi:
   ```bash
   dotnet run --project src/Ambev.DeveloperEvaluation.WebApi/Ambev.DeveloperEvaluation.WebApi.csproj
   ```
3. A API estará em http://localhost:5000 (ou nas portas indicadas no console).

---

## Testar

### Testar a API (Swagger)

1. Com a API em execução, abra no navegador:
   - **Com Docker:** http://localhost:8080/swagger  
   - **Com `dotnet run`:** http://localhost:5000/swagger (ou a URL exibida no terminal)
2. Use a interface do Swagger para chamar os endpoints (Sales, Auth, Users, etc.).

### Rodar os testes unitários

Na pasta `template/backend`:

```bash
dotnet test tests/Ambev.DeveloperEvaluation.Unit/Ambev.DeveloperEvaluation.Unit.csproj
```

Para rodar só os testes de Sales:

```bash
dotnet test tests/Ambev.DeveloperEvaluation.Unit/Ambev.DeveloperEvaluation.Unit.csproj --filter "FullyQualifiedName~Sales"
```

---

## Resumo rápido

| Ação        | Comando (em `template/backend`) |
|------------|-----------------------------------|
| Subir tudo | `docker compose up --build`        |
| Swagger    | http://localhost:8080/swagger     |
| Testes     | `dotnet test tests/Ambev.DeveloperEvaluation.Unit/Ambev.DeveloperEvaluation.Unit.csproj` |

[Voltar ao README](../README.md)
