# abson.tech

Monorepo com frontend **Astro + React** (estático), API **.NET 10** e **PostgreSQL**, preparado para deploy no **Coolify**.

## Arquitetura

```
Internet
├── app.abson.tech      → Astro + React (estático)   → Nginx
├── api.abson.tech      → ASP.NET Core 10            → Kestrel :8080
└── manager.abson.tech  → Coolify
        └── PostgreSQL 17 (rede interna do Coolify)
```

## Estrutura

```
.
├── apps
│   ├── api                 # ASP.NET Core 10 + EF Core + Npgsql
│   │   ├── Data            # AppDbContext
│   │   ├── Migrations      # migrations do EF Core
│   │   ├── Models          # entidades
│   │   └── Dockerfile
│   └── web                 # Astro + React (SSG)
│       ├── src
│       └── Dockerfile
├── docker-compose.yml      # ambiente local (Postgres + API + web)
└── README.md
```

## Desenvolvimento local

Pré-requisitos: Node.js 22+, .NET SDK 10 e Docker (opcional).

### Tudo com Docker

```bash
docker compose up --build
```

- Frontend: http://localhost:3000
- API: http://localhost:8080
- Postgres: `localhost:5432` (`abson` / `abson` / `abson`)

### Rodando separado

```bash
# banco
docker compose up -d postgres

# API (aplica migrations automaticamente)
cd apps/api
dotnet run

# frontend
cd apps/web
npm install
npm run dev
```

## Variáveis de ambiente

### API

| Variável | Exemplo | Descrição |
| --- | --- | --- |
| `ConnectionStrings__Default` | `Host=postgres;Port=5432;Database=abson;Username=abson;Password=...` | Conexão com o PostgreSQL |
| `Cors__AllowedOrigins` | `https://app.abson.tech` | Origens permitidas (separadas por vírgula) |
| `Database__AutoMigrate` | `true` | Aplica migrations no startup (padrão `true`) |

### Frontend

| Variável | Exemplo | Descrição |
| --- | --- | --- |
| `PUBLIC_API_URL` | `https://api.abson.tech` | URL pública da API (inlinada no build) |

## Endpoints da API

- `GET /health` — liveness (usado pelo Coolify)
- `GET /ready` — readiness + conexão com o banco
- `GET /api/hello` — hello world
- `GET /api/todos` — lista tarefas
- `POST /api/todos` — cria tarefa
- `PATCH /api/todos/{id}` — atualiza tarefa
- `DELETE /api/todos/{id}` — remove tarefa
- `GET /openapi/v1.json` — OpenAPI (apenas em Development)

## Deploy no Coolify

1. Crie um projeto `abson` (environment `production`).
2. Crie um **PostgreSQL 17** e guarde host/porta/usuário/senha internos.
3. Crie a aplicação da API a partir deste repositório:
   - Base Directory: `apps/api`
   - Build Pack: `Dockerfile`
   - Porta: `8080`
   - Domínio: `https://api.abson.tech`
   - Health check: `/health`
   - Env: `ConnectionStrings__Default`, `Cors__AllowedOrigins=https://app.abson.tech`
4. Crie a aplicação do frontend:
   - Base Directory: `apps/web`
   - Build Pack: `Dockerfile`
   - Porta: `80`
   - Domínio: `https://app.abson.tech`
   - Build arg: `PUBLIC_API_URL=https://api.abson.tech`
   - Health check: `/health`
5. Faça o deploy na ordem: Postgres → API → frontend.

As migrations são aplicadas automaticamente no startup da API (com retry enquanto o banco sobe).
