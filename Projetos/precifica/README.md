# Precifica

Calculadora de precificação para **confeiteiros, artesãos, impressão 3D e esteticistas**.

O usuário lança custo de material, tempo, custos fixos, impostos, taxas e a margem que quer. O
Precifica mostra o preço sugerido, o lucro real e o preço mínimo para não vender no prejuízo — e
gera um **PDF compartilhável** com a conta aberta, que funciona como material de marketing.

> **Estado atual: 100% gratuito.** Calculadora, PDF sem marca d’água e link compartilhável estão
> liberados para todos durante o período de validação. O código de cobrança (R$ 29 avulso / R$ 9/mês)
> continua no projeto, mas fica desligado pela flag `Monetizacao:Ativa=false`.

**No ar:** [precifica.abson.tech](https://precifica.abson.tech) · API em
[api.precifica.abson.tech](https://api.precifica.abson.tech/health).

## O que já está implementado

- Cálculo completo com margem sobre o preço final, impostos, taxa do cartão, taxa fixa, perda de
  material e rateio de custos fixos.
- Quatro nichos com exemplo, dica e PDF próprios.
- Salvar cálculo, link público com token e edição com token privado (sem login/senha).
- PDF com QR Code e link; no período gratuito, o PDF sai **sem marca d’água**.
- Checkout Mercado Pago (preference e preapproval) + **modo simulado**, pronto para reativar depois.
- Webhooks idempotentes e validação de assinatura HMAC quando o segredo está configurado.
- Front Astro responsivo com calculadora em tempo real, página compartilhável e planos.
- Testes de domínio e aplicação; build Docker para API, web e Postgres.

## Stack

| Camada | Tecnologia |
| --- | --- |
| Domínio e aplicação | .NET 10 / C# 13 |
| API | ASP.NET Core 10, Minimal APIs, OpenAPI |
| Persistência | EF Core 10 + PostgreSQL 17 (SQLite no desenvolvimento) |
| PDF | QuestPDF + QRCoder |
| Pagamentos | Mercado Pago (Checkout Pro e preapproval) |
| Web | Astro 7 SSR + TypeScript + CSS próprio |
| Infra local | Docker Compose |

## Arquitetura DDD

O projeto é um **monólito modular** com quatro contextos e dependências apontando para dentro:

```
Precifica.Domain          ← regras e linguagem, sem dependências
   ▲
Precifica.Application     ← casos de uso e portas
   ▲
Precifica.Infrastructure  ← EF Core, Mercado Pago, QuestPDF
   ▲
Precifica.Api             ← composição, HTTP, webhooks
```

| Contexto | Tipo | Responsabilidade |
| --- | --- | --- |
| **Precificação** | Core | Agregado `Calculo`, fórmula, invariantes, eventos |
| **Catálogo de Nichos** | Supporting | Modelos, exemplos e dicas por nicho (estático no MVP) |
| **Documentos** | Supporting | Renderização do PDF e compartilhamento |
| **Pagamentos/Assinaturas** | Generic | `Pedido`, `Assinatura`, acesso premium, gateway |

O mapa completo, a linguagem ubíqua, as invariantes e as decisões estão em
[`docs/ddd.md`](docs/ddd.md). Decisões pontuais estão em [`docs/adr`](docs/adr).

### A conta

```
C = materiais × (1 + perda)
  + (minutos / 60) × valorHora
  + custosFixos × (minutos / (horasProdutivas × 60))
  + custosExtras

P = (C + taxaFixa) / (1 − margem − imposto − taxaCartão)
Pmin = (C + taxaFixa) / (1 − imposto − taxaCartão)
```

- Percentuais incidem sobre o **preço final**, não sobre o custo (margem, não markup).
- Dinheiro é `long` em **centavos**; percentuais são `int` em **pontos-base** (1% = 100).
- O preço é arredondado **para cima** (centavo, R$ 0,50, R$ 1 ou R$ 5), para nunca perder margem.
- A margem efetiva é recalculada depois do arredondamento.
- A soma `margem + imposto + taxa do cartão` precisa ser menor que 100%.

## Rodando localmente

Pré-requisitos: **.NET 10 SDK** e **Node 24+**.

### 1. API (SQLite, sem banco externo)

```bash
cd Projetos/precifica/src/Precifica.Api
dotnet run
```

A API sobe em `http://localhost:5180`, com `appsettings.Development.json` apontando para SQLite
(`precifica.db`). O banco é criado no primeiro start. Em desenvolvimento o pagamento começa no
**modo simulado** — não precisa de credenciais do Mercado Pago.

Health checks: `GET /health` e `GET /ready`.

### 2. Web

Em outro terminal:

```bash
cd Projetos/precifica/web
npm install
npm run dev
```

Para checar os tipos do front (Astro/TypeScript):

```bash
npm run check
```

Abra **`http://localhost:4321`** (use `localhost`, não `127.0.0.1`). O redirecionamento do checkout
simulado usa `Web:Url=http://localhost:4321`; manter o mesmo host preserva o token de edição no
`localStorage`.

Fluxo de teste completo:

1. Abra `/calculadora` e veja o exemplo de confeitaria.
2. Salve e vá para a página `/c/{token}`.
3. Baixe o PDF: ele já sai sem marca d’água.
4. Compartilhe o link ou o QR Code com o cliente.
5. Confira `/precos` e a página inicial: não há cobrança, checkout ou assinatura ativa.

### 3. Docker Compose (PostgreSQL)

```bash
cd Projetos/precifica
docker compose up --build
```

- Web: `http://localhost:3000`
- API: `http://localhost:5180`
- Postgres: `localhost:5433` (`precifica` / `precifica` / `precifica`)

O compose usa `ASPNETCORE_ENVIRONMENT=Development` e `Pagamentos__Modo=Simulado`. As migrations do
Postgres são aplicadas no startup; no SQLite o schema é criado com `EnsureCreated`.

### 4. Testes

```bash
cd Projetos/precifica
dotnet test
```

Cobrem a fórmula (margem, impostos, perda, custos fixos, preço mínimo, arredondamento), invariantes,
tokens, idempotência de pagamento/assinatura, regras de acesso ao PDF premium e validação HMAC do
webhook do Mercado Pago (39 testes).

## Configuração

### API (`appsettings.json` / variáveis de ambiente)

| Chave | Descrição |
| --- | --- |
| `Database:Provider` | `Postgres` (produção) ou `Sqlite` (desenvolvimento) |
| `Database:ConnectionString` | conexão do provider escolhido |
| `Cors:AllowedOrigins` | origens do front, separadas por vírgula |
| `Web:Url` | URL pública do front (links e retorno do checkout) |
| `Api:PublicUrl` | URL pública da API (webhook) |
| `Monetizacao:Ativa` | `false` libera tudo; `true` reativa o paywall |
| `Pagamentos:Modo` | `MercadoPago` ou `Simulado` (este só em Development) |
| `MercadoPago:AccessToken` | token de produção/sandbox |
| `MercadoPago:WebhookSecret` | segredo para validar `x-signature` |
| `MercadoPago:BaseUrl` | padrão `https://api.mercadopago.com` |

> Nunca comite `AccessToken`, `WebhookSecret` ou senhas. Use variáveis do ambiente/Coolify.
> Em produção, `Pagamentos:Modo=Simulado` é rejeitado no startup.

### Web (build)

| Variável | Descrição |
| --- | --- |
| `PUBLIC_API_URL` | URL da API usada pelo navegador (embutida no build) |
| `API_URL` | URL da API usada pelo SSR (runtime; no Docker, `http://api:8080`) |
| `PUBLIC_SITE_URL` | URL canônica do site (links/SEO) |

## Monetização

O produto está em período gratuito. Com `Monetizacao:Ativa=false` (padrão):

- todo cálculo recebe `premium=true` no endpoint de acesso;
- o PDF sem marca é liberado para qualquer pessoa;
- os endpoints de compra/assinatura respondem 503 (o front nem exibe os botões);
- a UI não mostra preços, planos ou checkout.

Para reativar a cobrança no futuro, defina `Monetizacao:Ativa=true`, configure o Mercado Pago e
redeploy. O restante do fluxo (pedido, assinatura, webhook, PDF pago) já está implementado.

### Mercado Pago (opcional)

1. Crie uma aplicação no [Mercado Pago Developers](https://www.mercadopago.com.br/developers).
2. Copie o **Access Token** de teste/produção.
3. Configure o webhook público `https://api.SEU_DOMINIO/api/webhooks/mercadopago` para os eventos:
   - `payment` (compra avulsa);
   - `subscription_preapproval` (assinatura).
4. Defina `MercadoPago__WebhookSecret` com o segredo do webhook para validar a assinatura HMAC.
5. Configure `Web__Url` com a URL pública do front (usada em `back_urls`).
6. Troque `Pagamentos__Modo` para `MercadoPago`.

O adaptador está em `src/Precifica.Infrastructure/Payments/MercadoPagoGateway.cs`. O domínio e a
aplicação não conhecem DTOs do provedor (anti-corruption layer).

**Modo simulado:** cria um checkout local em `/checkout/simulado/{id}` com botão de confirmação.
Nenhum valor é cobrado. Esse endpoint só é mapeado em `Development`.

## API

### Público

| Método | Rota | Descrição |
| --- | --- | --- |
| `GET` | `/api/nichos` | modelos dos quatro nichos |
| `GET` | `/api/planos` | preços vigentes |
| `POST` | `/api/calculos/preview` | calcula sem salvar |
| `POST` | `/api/calculos` | salva e devolve `shareToken` + `editToken` |
| `GET` | `/api/calculos/{shareToken}` | visão pública do cálculo |
| `PUT` | `/api/calculos/{id}` | edita com header `X-Edit-Token` |
| `GET` | `/api/calculos/{shareToken}/acesso` | status de acesso premium |
| `GET` | `/api/calculos/{shareToken}/pdf` | PDF com marca d’água |
| `GET` | `/api/calculos/{shareToken}/pdf/premium?email=` | PDF sem marca, se houver acesso |
| `POST` | `/api/calculos/{shareToken}/comprar` | checkout avulso (503 no modo gratuito) |
| `POST` | `/api/assinaturas` | checkout da assinatura (503 no modo gratuito) |
| `POST` | `/api/webhooks/mercadopago` | webhook idempotente |

Erros usam `application/problem+json` com `{ codigo, detalhe }`. Limites por IP: 180 leituras, 40
escritas e 30 PDFs por minuto.

### Desenvolvimento

| Método | Rota | Descrição |
| --- | --- | --- |
| `GET` | `/api/dev/checkout/{id}` | consulta o checkout simulado |
| `POST` | `/api/dev/checkout/{id}/confirmar` | confirma o pagamento simulado |

## Estrutura

```
Projetos/precifica/
├── src/
│   ├── Precifica.Domain/
│   │   ├── Common/            # Dinheiro, Percentual, DomainException, AggregateRoot
│   │   ├── Precificacao/      # Calculo, DadosDoCalculo, ResultadoDaPrecificacao
│   │   └── Pagamentos/        # Pedido, Assinatura, eventos
│   ├── Precifica.Application/
│   │   ├── Calculos/          # casos de uso, DTOs, mapper
│   │   ├── Catalogo/          # modelos por nicho e planos
│   │   ├── Documentos/        # caso de uso do PDF
│   │   └── Pagamentos/        # checkout, webhook, acesso
│   ├── Precifica.Infrastructure/
│   │   ├── Persistence/       # DbContext, configurações, repositórios, migrations
│   │   ├── Pdf/               # QuestPDF + fontes Lato (OFL)
│   │   └── Payments/          # Mercado Pago e gateway simulado
│   └── Precifica.Api/         # Program, endpoints, middleware, contratos
├── tests/
│   ├── Precifica.Domain.Tests/
│   └── Precifica.Application.Tests/
├── web/                       # Astro SSR
├── docs/                      # DDD e ADRs
├── docker-compose.yml
└── README.md
```

## Deploy

Sugestão de subdomínios:

- `precifica.abson.tech` → web Astro
- `api.precifica.abson.tech` → API .NET
- `PostgreSQL` gerenciado ou container interno

Checklist:

1. Crie o Postgres e configure `Database__Provider=Postgres` + `Database__ConnectionString`.
2. Suba a API com `ASPNETCORE_ENVIRONMENT=Production` e as variáveis acima.
3. Suba a web com `PUBLIC_API_URL=https://api.precifica.abson.tech`, `API_URL=http://api:8080` e
   `PUBLIC_SITE_URL=https://precifica.abson.tech`.
4. Libere CORS apenas para o domínio do front.
5. Opcional: configure o Mercado Pago e o webhook público (só se reativar a cobrança).
6. Rode `dotnet ef database update` (ou deixe o startup aplicar as migrations).
7. Teste `GET /health`, `GET /ready`, um cálculo e um checkout sandbox.

## Decisões e limites do MVP

- **Sem login:** o cálculo tem `shareToken` público e `editToken` privado (hash no banco). O token de
  edição fica só no navegador. Para o Pro, o e-mail é a chave de acesso.
- **Catálogo estático:** os nichos vivem em memória; quando houver curadoria, viram tabela sem
  alterar o cálculo.
- **Assinatura:** o código existe, mas fica desligado no período gratuito. A ativação/renovação
  depende dos webhooks do Mercado Pago quando a monetização for reativada.
- **E-mail:** não há envio transacional no MVP; o acesso é liberado na própria página e no PDF.
- **Privacidade:** e-mails são armazenados apenas para vincular pagamento/assinatura. Não há
  analytics de terceiros. Considere política de retenção e LGPD antes de escalar.
- **PDF:** QuestPDF com licença Community (permitida para receita anual abaixo de US$ 1M). Fonte
  Lato sob OFL; veja `src/Precifica.Infrastructure/Pdf/Fonts/OFL.txt`.

### Próximos passos

- Histórico de cálculos e comparação de cenários.
- Marca própria no PDF (logo, cores) no plano Pro.
- Envio do PDF por e-mail/WhatsApp e lembrete de orçamento.
- Cupons, afiliados e página de indicação.
- Outbox para eventos (e-mail, analytics, CRM).
- Mais nichos e templates editáveis.

## Licença

Uso privado do portfólio. Defina a licença comercial antes de publicar como produto independente.
