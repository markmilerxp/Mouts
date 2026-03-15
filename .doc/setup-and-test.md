[# Developer Evaluation Project – Setup e Testes](../README.md)

## Como configurar, executar e testar o projeto

Este guia explica, de forma objetiva, como subir a API e rodar os testes, atendendo ao que o cliente pede no enunciado do desafio.

---

## Pré-requisitos

- **Docker** e **Docker Compose** (para subir API + bancos)
- Opcional: **.NET 8 SDK** (se quiser rodar a WebApi e os testes sem Docker)

---

## Configurar

1. Clone o repositório.
2. Entre na pasta do backend:

```bash
cd template/backend
```

3. As connection strings já estão configuradas no `docker-compose.yml` para rodar tudo em containers.  
   Se for rodar a WebApi **fora do Docker**, use estes valores (via `appsettings.Development.json` ou variáveis de ambiente):

- PostgreSQL:  
  `Host=localhost;Port=5432;Database=developer_evaluation;Username=developer;Password=ev@luAt10n`
- MongoDB:  
  `mongodb://developer:ev%40luAt10n@localhost:27017`
- Redis:  
  `localhost:6379,password=ev@luAt10n`

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

### Opção 2 — Bancos no Docker, WebApi com .NET SDK

1. Suba apenas os serviços de dados:

```bash
cd template/backend
docker compose up -d ambev.developerevaluation.database ambev.developerevaluation.nosql ambev.developerevaluation.cache
```

2. Rode a WebApi localmente:

```bash
dotnet run --project src/Ambev.DeveloperEvaluation.WebApi/Ambev.DeveloperEvaluation.WebApi.csproj
```

3. A URL padrão será algo como `http://localhost:5000` (ou a que aparecer no console).

---

## Testar

### Testar a API via Swagger

Com a API em execução:

- Se estiver usando Docker: acesse `http://localhost:8080/swagger`
- Se estiver usando `dotnet run`: acesse a URL mostrada no console (geralmente `http://localhost:5000/swagger`)

### Rodar os testes automatizados

Na pasta `template/backend`:

```bash
dotnet test tests/Ambev.DeveloperEvaluation.Unit/Ambev.DeveloperEvaluation.Unit.csproj
```

Para rodar apenas os testes relacionados a Sales:

```bash
dotnet test tests/Ambev.DeveloperEvaluation.Unit/Ambev.DeveloperEvaluation.Unit.csproj --filter "FullyQualifiedName~Sales"
```

---

## Resumo rápido

| Ação        | Comando (em `template/backend`)                                                        |
|------------|------------------------------------------------------------------------------------------|
| Subir tudo | `docker compose up --build`                                                              |
| Swagger    | `http://localhost:8080/swagger`                                                          |
| Testes     | `dotnet test tests/Ambev.DeveloperEvaluation.Unit/Ambev.DeveloperEvaluation.Unit.csproj` |

