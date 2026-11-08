# abson.tech

Portfólio profissional com **CMS próprio**: o conteúdo do site é editado em `/admin` e salvo no PostgreSQL, sem precisar alterar código ou refazer deploy.

## Arquitetura

```
Internet
├── abson.tech / www.abson.tech  → Astro SSR (Node 24)     → Coolify/Traefik
├── app.abson.tech               → mesmo site
├── api.abson.tech               → ASP.NET Core 10          → Coolify/Traefik
└── manager.abson.tech           → Coolify
        └── PostgreSQL 17 (rede interna)
```

O site Astro busca `GET /api/site` a cada requisição e renderiza o conteúdo atual. Se a API estiver indisponível, usa o JSON de fallback em `apps/web/src/data/site-content.json`.

## Estrutura

```
.
├── apps
│   ├── api                 # ASP.NET Core 10 + EF Core + Npgsql
│   │   ├── Data            # AppDbContext, SeedData
│   │   ├── Endpoints       # AdminEndpoints (auth, conteúdo, mensagens)
│   │   ├── Migrations
│   │   ├── Models
│   │   ├── Seed            # site-content.json inicial
│   │   └── Dockerfile
│   └── web                 # Astro SSR + painel admin
│       ├── public/admin    # painel (HTML/CSS/JS + jsoneditor)
│       ├── public/assets   # CSS/JS/PDF do portfólio
│       ├── src/data        # fallback do conteúdo
│       ├── src/pages       # index.astro, admin/index.astro, health.ts
│       └── Dockerfile
├── docker-compose.yml      # ambiente local
└── README.md
```

## Banco de dados

### `site_content`
Documento JSONB único (`Id = 1`) com todo o conteúdo editável:

| Seção | Conteúdo |
| --- | --- |
| `seo` | título, description, canonical, OG image |
| `theme` | tema padrão (dark/light) |
| `profile` | nome, marca, cargo, headline, lead, contato, LinkedIn, CV |
| `workCard` | ficha de trabalho e linhas |
| `about` | textos e princípios |
| `skills` | grupos e itens |
| `experience` | vagas, períodos, local, atividades |
| `projects` | cases, tags, contexto, decisão, resultado, stack |
| `education` | graduação e certificações |
| `process` | etapas e saídas |
| `contact` | textos do formulário, assuntos e mensagens de sucesso |
| `navigation` / `footerNavigation` | links do topo e rodapé |

Colunas: `Id`, `ContentJson` (jsonb), `UpdatedAt`, `UpdatedBy`.

### `admin_users`
Login do painel: `Id`, `Name`, `Email` (único), `PasswordHash`, `IsActive`, `CreatedAt`, `LastLoginAt`.

O primeiro admin é criado no startup a partir de `Admin__Email` / `Admin__Password`.

### `contact_messages`
Mensagens do formulário: `Id`, `Name`, `Email`, `Subject`, `Message`, `Status` (`new`, `read`, `replied`, `archived`), `ReadAt`, `CreatedAt`.

## Painel administrativo

- URL: **https://abson.tech/admin**
- Login com e-mail/senha (JWT de 8h, armazenado em `sessionStorage`)
- **Conteúdo do site:** editor de formulário gerado por schema (343 campos)
- **Mensagens:** inbox com filtro por status, marcar como lida/respondida/arquivada e excluir
- **Segurança:** troca de senha

Alterações de conteúdo aparecem no site na próxima visita, sem redeploy.

## API

### Públicos
- `GET /api/site` — conteúdo do site
- `POST /api/contact` — formulário (validação, honeypot, 5 envios/min por IP)
- `GET /health`, `GET /ready`

### Admin (Bearer JWT)
- `POST /api/admin/login`
- `GET /api/admin/me`
- `POST /api/admin/change-password`
- `GET/PUT /api/admin/content`
- `GET /api/admin/messages`
- `PATCH/DELETE /api/admin/messages/{id}`
- `GET /api/admin/stats`

## Desenvolvimento local

```bash
docker compose up --build
```

- Frontend: http://localhost:3000 (Astro SSR)
- API: http://localhost:8080
- Postgres: `localhost:5432` (`abson` / `abson` / `abson`)

Para rodar separado:

```bash
docker compose up -d postgres

cd apps/api
dotnet run

cd apps/web
npm install
npm run dev
```

## Variáveis de ambiente

### API

| Variável | Descrição |
| --- | --- |
| `ConnectionStrings__Default` | conexão PostgreSQL |
| `Cors__AllowedOrigins` | origens permitidas, separadas por vírgula |
| `Jwt__Secret` | segredo com pelo menos 32 caracteres |
| `Admin__Email` | e-mail do admin inicial |
| `Admin__Password` | senha do admin inicial |
| `Admin__Name` | nome exibido do admin |
| `Database__AutoMigrate` | aplica migrations no startup (padrão `true`) |

### Web

| Variável | Descrição |
| --- | --- |
| `API_URL` | URL da API usada no SSR (build) |
| `PUBLIC_API_URL` | URL pública da API |

## Deploy no Coolify

- **API:** build pack Dockerfile, base `/apps/api`, porta `8080`, health `/health`.
- **Web:** build pack Dockerfile, base `/apps/web`, porta `4321`, health `/health`.
- O site usa SSR; o container roda `node ./dist/server/entry.mjs`.
- Redirect `www` → raiz via Traefik (`/data/coolify/proxy/dynamic/abson-www-redirect.yaml`).
