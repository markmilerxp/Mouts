[Voltar ao README](../README.md)

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

Para acompanhar os resultados e apoiar a evolução da cobertura, usamos também o `coverage-report` disponibilizado pelo cliente no projeto (`coverage-report.bat`/`coverage-report.sh`).

---

## Dados fake para testes (Bogus)

Nos testes unitários usamos a biblioteca **Bogus** para gerar dados fake de forma consistente e realista, evitando massa de teste manual fixa.

### Como aplicamos no projeto

- Helper central de dados para Sales: `tests/Ambev.DeveloperEvaluation.Unit/Application/Sales/SalesHandlerTestData.cs`
- Geração de comandos válidos para cenários de teste:
  - `CreateValidCreateCommand(...)`
  - `CreateValidUpdateCommand(...)`
- Uso principal: montar payloads com produtos, quantidades, preços e dados de cliente/filial para testes dos handlers.

Essa abordagem facilita a manutenção dos testes e reduz duplicação de setup.

---

## Gerar documentação XML (summaries)

Além da execução da API e dos testes, o projeto está configurado para gerar documentação XML a partir dos comentários `/// summary` durante o build.

### Como está configurado

- Configuração central em `template/backend/Directory.Build.props`.
- Propriedade usada: `GenerateDocumentationFile=true` para projetos não teste.
- Isso faz o compilador gerar o arquivo XML da documentação para cada assembly.

### Como gerar

Na pasta `template/backend`:

```bash
dotnet build Ambev.DeveloperEvaluation.sln --no-restore
```

### Onde os arquivos são gerados

Os arquivos `.xml` ficam ao lado dos `.dll` em cada projeto compilado, por exemplo:

- `src/Ambev.DeveloperEvaluation.Application/bin/Debug/net8.0/Ambev.DeveloperEvaluation.Application.xml`
- `src/Ambev.DeveloperEvaluation.Domain/bin/Debug/net8.0/Ambev.DeveloperEvaluation.Domain.xml`
- `src/Ambev.DeveloperEvaluation.WebApi/bin/Debug/net8.0/Ambev.DeveloperEvaluation.WebApi.xml`

Essa abordagem segue a recomendação da propriedade `GenerateDocumentationFile` do SDK .NET.

---

## Resumo rápido

| Ação        | Comando (em `template/backend`)                                                        |
|------------|------------------------------------------------------------------------------------------|
| Subir tudo | `docker compose up --build`                                                              |
| Swagger    | `http://localhost:8080/swagger`                                                          |
| Testes     | `dotnet test tests/Ambev.DeveloperEvaluation.Unit/Ambev.DeveloperEvaluation.Unit.csproj` |
| Doc XML    | `dotnet build Ambev.DeveloperEvaluation.sln --no-restore`                               |

---

## Nota do Desenvolvedor

- Durante a implementação, foram usados como referência alguns modelos e padrões de classes já consolidados em cursos e treinamentos de DDD que fiz durante minha carreira. 
- Isso acelerou a aplicação dos padrões arquiteturais em cada arquivo, sempre adaptando o conteúdo para a especificação deste projeto. 
- A experiência prévia com .NET e arquitetura também contribuiu para maior velocidade e consistência técnica.

---

## Stack e ferramentas usadas

### Backend e arquitetura

- .NET 8 / ASP.NET Core Web API
- DDD (Domain-Driven Design)
- CQRS + MediatR
- AutoMapper
- FluentValidation
- ILogger (logs de eventos de venda)

### Persistência e dados

- Entity Framework Core + PostgreSQL (write model)
- MongoDB (`MongoDB.Driver`) para read model
- Redis (`IDistributedCache`) para cache de leitura

### Testes e qualidade

- xUnit
- NSubstitute
- FluentAssertions
- Bogus (dados fake para testes)
- Coverlet + ReportGenerator
- `coverage-report` do cliente (`coverage-report.bat` / `coverage-report.sh`)

