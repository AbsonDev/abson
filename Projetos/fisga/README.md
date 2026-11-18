# Fisga

**Diário de pesca e copiloto de condições para o pescador brasileiro.**

O Fisga ajuda antes, durante e depois da pescaria: mostra maré, vento, chuva, lua e a janela do dia;
guarda locais, pontos e trilhas privados; registra cada captura com fotos, condições e insights; e
transforma o dia em troféu, PDF e memória.

> **Estado atual: 100% gratuito.** Diário, condições, mapa, ferramentas, troféu e PDF liberados
> durante a validação, sem cartão ou assinatura. Nenhum provedor pago é obrigatório.

**No ar:** [fisga.abson.tech](https://fisga.abson.tech) · API em
[api.fisga.abson.tech](https://api.fisga.abson.tech/health).

[![CI do Fisga](https://github.com/AbsonDev/abson/actions/workflows/fisga-pipeline.yml/badge.svg?branch=main)](https://github.com/AbsonDev/abson/actions/workflows/fisga-pipeline.yml)
[![Deploy do Fisga](https://github.com/AbsonDev/abson/actions/workflows/fisga-deploy.yml/badge.svg?branch=main)](https://github.com/AbsonDev/abson/actions/workflows/fisga-deploy.yml)

---

## Visão geral

| Momento | Pergunta do pescador | O que o Fisga entrega |
| --- | --- | --- |
| **Antes** | “Vale a pena sair hoje?” | Cockpit com maré, corrente, vento, chuva, pressão, lua, solunar, janela e 7 dias |
| **Durante** | “Onde eu estou e o que funciona aqui?” | Mapa privado, pontos, trilhas, notas, camadas náuticas/batimetria e ferramentas offline |
| **Depois** | “O que aconteceu e o que funcionou?” | Diário, fotos, insights, comparação com o histórico, troféu, PDF e newsletter |

O Fisga é **privado por padrão**: o diário é da conta, o local pode ser ocultado e pontos/trilhas
nunca são publicados.

### Navegação

| Contexto | Itens principais |
| --- | --- |
| Desktop autenticado | **Hoje · Diário · Mapa · Aprender** + Registrar pescaria + conta |
| Desktop anônimo | **Hoje · Mapa · Aprender** + Entrar |
| Mobile autenticado | Bottom nav: Hoje, Diário, Registrar (central), Mapa, Aprender |
| Mobile anônimo | Bottom nav: Hoje, Mapa, Aprender |

- `/` é a home pública focada em “Vale a pena sair hoje?”; quem tem sessão vai para `/hoje`.
- `/hoje` é o cockpit do local padrão, com condições, janela, 7 dias e atalhos.
- `/aprender` agrupa **Ferramentas de campo + Wiki**.
- `/mapa` reúne **Locais · Pontos · Trilhas** no mesmo mapa privado.
- `/meu-diario` separa **Visão geral · Pescarias · Insights**.
- URLs antigas continuam funcionando: `/locais` → `/mapa?aba=locais` e `/diario` → `/meu-diario`.

---

## Funcionalidades

### Conta e acesso

- **Login por senha** para contas com senha (10 a 128 caracteres, sem exigir maiúscula/número) e
  **link mágico** de uso único (15 min) para quem prefere entrar sem senha.
- **Cadastro por senha** com confirmação de e-mail; o login por senha só é liberado depois da
  confirmação. O link mágico confirma o e-mail no primeiro acesso.
- **Google opcional**: o botão só aparece quando `Google__ClientId`/`Google__ClientSecret` estão
  configurados. Em `/conta` é possível vincular/entrar com Google sem trocar de usuário.
- Sessão de 30 dias no cookie `fisga_auth` da Identity (`HttpOnly`, `Secure`, `SameSite=Lax`),
  com uma linha de dispositivo por login. O cookie legado `fisga_sessao` é apagado e não autentica.
- **Aceite formal** dos Termos e da Política de Privacidade (`2026-11-17`) antes de acessar o app.
- **Boas-vindas** no primeiro acesso, com nome, preferência de e-mail e local padrão (busca ou GPS).
- **Minha conta**: senha, Google, sessões ativas (“sair desta” e “sair de todos”), exportação JSON,
  exclusão com confirmação `EXCLUIR` e alteração de nome/preferência de e-mail.
- **Recuperação**: confirmação de e-mail e redefinição de senha por link; o link de reset vale
  15 min e invalida as sessões antigas.
- Data Protection persistido em `/app/data/keys`; sem esse volume, todo deploy invalidaria os
  cookies e os links em circulação.
- **Conta obrigatória apenas para o diário e recursos privados**; condições, wiki e ferramentas são
  públicas.

### Diário e capturas

- Pescaria com **local, cidade/UF, ambiente, data, período e condições** (lua, maré, clima, água).
- **Várias capturas por pescaria**, cada uma com ID estável.
- Espécie, peso, comprimento, isca, destino (solto/capturado), observação e **até 4 fotos por
  captura**, com escolha da capa.
- Compressão de imagem no navegador (até 5 MB; JPG/PNG/WebP).
- **Privacidade do local por pescaria**: exato, apenas cidade/UF ou oculto. Vale para link público,
  troféu e PDF.
- Edição preservando IDs das capturas; links de troféu não quebram.
- Rascunho local do formulário para não perder o registro.

### Compartilhamento e memória

- **Link público por pescaria**, sem expor o restante do diário.
- **Card do troféu** em PNG (1080×1080) e **story** (1080×1920), gerados no navegador.
- **OG image** com a foto da captura para WhatsApp/Facebook.
- **PDF da pescaria** com condições, capturas, maior peixe, notas e QR.
- Leitura pública respeitando a privacidade do local e a galeria de fotos.

### Condições e cockpit

- **Página pública `/condicoes`**, sem login, com busca por cidade/praia e geolocalização.
- **`/hoje`** autenticado com o local padrão, condições agora, janela explicável, 7 dias e atalhos.
- **Maré aproximada** com curva, preamar/baixa-mar, altura e próximo evento.
- **Corrente oceânica real** do modelo Open-Meteo (`ocean_current_velocity`), em km/h e direção.
- Vento, rajadas, direção, chuva, temperatura, sensação, pressão e tendência.
- **Alerta de mudança brusca de pressão** (“queda” ou “subida”).
- Temperatura da água, altura e período de onda quando disponíveis.
- **Fase da lua, iluminação, idade e períodos solunares** (maiores/menores) com motivo.
- **Linha do tempo de 24 h** sincronizando maré, pressão, vento e faixas solunares.
- **Janela de pesca explicável**: nota 1–5 e motivos (solunar, maré, vento, chuva, luz do dia).
- **Próximos 7 dias** com nota e melhor horário.
- **Locais salvos**: até 5 por conta, com um padrão que abre primeiro.
- **Registro pré-preenchido** com local, data, período, maré, lua e clima.
- **Comparação com o histórico**: encontra um dia parecido usando lua, maré, clima e período; exige
  pelo menos 3 pescarias com captura.
- Fonte, fuso e aviso de limite sempre visíveis. A maré **não serve para navegação**.

### Mapa privado, pontos e trilhas

- **Leaflet** com mapa base, satélite, camada náutica e batimetria, sem token pago.
- **Pontos privados** com nome, tipo (ponto, poita, estrutura, trilha, rampa), notas e coordenadas.
- **Trilhas privadas** de corrico/deslocamento, com distância calculada.
- **Abas Locais/Pontos/Trilhas** no mesmo mapa, sem duplicar dados nem coordenadas.
- **Exportação GPX** de pontos e trilhas.
- Nenhum endpoint público de pontos ou trilhas; nenhuma coordenada é publicada.
- Cache local de pontos/trilhas e dos últimos azulejos visitados.
- Limites: 200 pontos e 100 trilhas por conta; 500 pontos por trilha.

### Diário inteligente

- **Estatísticas**: pescarias, capturas, espécies distintas, solturas, maior peso/comprimento,
  espécie/isca/local que mais aparecem e taxa de soltura.
- **Insights**: melhor mês, período, lua, maré, clima e água; recordes por espécie; isca que mais
  funcionou por espécie.
- **Abas Visão geral/Pescarias/Insights** para separar resumo, histórico e padrões.
- **Sinais comparativos**: “+X% de capturas com lua crescente”, com pelo menos 3 pescarias de cada
  lado, diferença mínima de 15% e aviso “sinal, não garantia”.
- **EXIF**: lê data/hora e GPS da foto para sugerir data, período e cidade/UF (GPS usado só para a
  sugestão; a cidade pode ser corrigida).
- **Lembrete de defeso**: ao preencher espécie e UF, mostra “confira tamanho mínimo e defeso de X em
  UF” com link para o órgão oficial.
- **Sugestão de espécie por foto**: porta plugável (`IReconhecedorDeEspecie`) com confirmação humana.
  Sem provedor configurado, o fluxo cai no preenchimento manual.

### Ferramentas offline

- **Nós e montagens**: oito nós com uso, dificuldade, passo a passo, diagrama e dica.
- **Checklists**: caiaque, costeira, embarcado e pesque-pague, com progresso salvo no aparelho.
- **Defeso por UF**: órgão ambiental, link oficial, checklist do que confirmar e aviso legal.
- **Guias e piloteiros**: roteiro para escolher, perguntas antes de fechar e diretório em curadoria.
- **Central `/aprender`** com busca local e selos de conteúdo disponível offline.

### Wiki e newsletter

- **Wiki do Pescador** com 11 espécies, 6 técnicas, 6 iscas, 5 regiões, defeso, iniciantes,
  segurança, glossário e fontes.
- Páginas estáticas com Astro Content Collections, busca Pagefind e leitura offline.
- **Newsletter mensal** personalizada: estatísticas do mês, leituras sazonais da wiki, lembrete de
  defeso e descadastro assinado por HMAC.
- Prévia HTML e endpoint administrativo protegido para envio.

### Offline e PWA

- Manifest, ícones, página offline e service worker.
- Cache de wiki, ferramentas, shell anônimo de `/condicoes` e `/`, pontos/trilhas e últimos 600 azulejos do mapa.
- Rascunho de pescaria em `localStorage`.
- Rotas privadas (`/hoje`, `/mapa`, `/conta`, `/meu-diario`, `/registrar`, `/trofeu`) não entram no
  cache de navegação.
- Não é um mapa offline completo: baixe a área antes de sair para o mar.

---

## Sprints entregues

| Sprint | Escopo | Status |
| --- | --- | --- |
| **Base** | Conta, diário, capturas, fotos, troféu, story, OG, PDF, PWA e wiki | No ar |
| **Sprint 1** | Privacidade do local, até 4 fotos com capa, insights e newsletter mensal | No ar |
| **Sprint 2** | Condições públicas, locais salvos, janela de pesca, prefill e comparação com histórico | No ar |
| **Sprint 3** | Cockpit de 24 h, corrente, alerta de pressão, mapa privado, pontos e cache offline | No ar |
| **Sprint 4** | Ferramentas offline, EXIF, defeso, sinais comparativos, sugestão de espécie, trilhas, GPX e cache de azulejos | No ar |
| **Sprint UX** | Hoje, Aprender, abas de Diário/Mapa, onboarding com local, bottom nav e Registrar em 4 etapas com revisão | No ar |

---

## Stack

| Camada | Tecnologia |
| --- | --- |
| Domínio/aplicação | .NET 10 / C# 13 |
| API | ASP.NET Core 10, Minimal APIs, OpenAPI |
| Persistência | EF Core 10 + PostgreSQL 17 (SQLite no desenvolvimento) |
| Autenticação | ASP.NET Core Identity: senha + link mágico + Google opcional; cookie `fisga_auth`; hashes SHA-256 nos tokens |
| E-mail | Resend (produção) e sender de console (desenvolvimento) |
| PDF | QuestPDF + QRCoder |
| Dados ambientais | Open-Meteo (tempo, ondas, corrente e nível do mar) + MET Norway (lua) |
| Geocodificação | Open-Meteo Geocoding + Nominatim/OSM (reverso) |
| Mapas | Leaflet + OpenStreetMap + Esri World Imagery + OpenSeaMap + GEBCO |
| Visão computacional | Porta `IReconhecedorDeEspecie` (Gemini opcional; manual por padrão) |
| Mídia | Upload local + arquivos estáticos com cache |
| Observabilidade | Sentry (erros, logs e traces) + health checks do Docker/Coolify |
| Card do troféu | Canvas API no navegador (quadrado + story) |
| PWA | Manifest, service worker, página offline e Cache Storage |
| Conteúdo | Astro Content Collections (Markdown tipado) |
| Busca | Pagefind (índice estático, offline) |
| Web | Astro 7 SSR + TypeScript + CSS próprio |
| CI/CD | GitHub Actions + Coolify |
| Testes | xUnit + Astro check + Playwright (mock no CI; suíte real via script) |
| Infra local | Docker Compose |

### Observabilidade

- **Sentry** captura erros, logs, traces e métricas da API e do web (SSR + navegador).
- Métricas de negócio com tags de baixa cardinalidade: pescarias, capturas, locais, condições,
  login, mídia e duração das rotas `/api`.
- Dados sensíveis são removidos antes do envio: cookies, headers, corpo, e-mail, nome,
  coordenadas e query string de local.
- **Session Replay** com texto, inputs e mídia mascarados/bloqueados; mapas e fotos do diário são
  bloqueados. Sem analytics de navegação.
- Source maps do web são enviados no build quando `SENTRY_AUTH_TOKEN` está presente; sem ele,
  stack traces de produção ficam minificados.
- `/health` verifica o processo e retorna o SHA do build; `/ready` verifica o banco. O Docker
  reinicia containers doentes.
- Logs continuam disponíveis no Coolify para investigação imediata.

---

## Arquitetura

`Pescaria` é o agregado central (local, condições e `Captura`s com identidade) e pertence
obrigatoriamente a um `Usuario`. O **Diário** é um read model privado que agrega pescarias e calcula
estatísticas.

Contextos e agregados:

- **Identidade e Acesso** — `Usuario`, `Sessao`, `TokenDeLogin`.
- **Pescarias** — `Pescaria`, `Captura`, condições e privacidade do local.
- **Locais** — `LocalDePesca`, limite de 5 e um padrão.
- **Condições** — agregação de Open-Meteo/MET Norway, solunar, janela e comparação.
- **Pontos Privados** — `PontoPrivado` e `TrilhaPrivada`, com ownership e sem projeção pública.
- **Diário** — read model de estatísticas, insights e sinais.
- **Ferramentas** — conteúdo estático de nós, checklists e defeso.
- **Visão** — porta de sugestão de espécie, com fallback manual.
- **Conhecimento** — Wiki do Pescador.
- **Newsletter** — digest mensal, opt-in e descadastro HMAC.

Migrações aplicadas: `Inicial`, `Contas`, `Sprint1`, `Sprint2`, `Sprint3`, `Sprint4`.

Detalhes em [`docs/ddd.md`](docs/ddd.md), [`docs/adr`](docs/adr) e
[`docs/referencias`](docs/referencias).

---

## Dados, fontes e atribuições

| Fonte | Uso | Custo/limite | Observação |
| --- | --- | --- | --- |
| **Open-Meteo** | Tempo, vento, chuva, pressão, ondas, corrente e nível do mar | Gratuito para uso não comercial; plano comercial se o Fisga cobrar | Atribuição necessária; modelos globais |
| **MET Norway** | Nascer, pôr, trânsito e fase da lua | Gratuito | CC BY 4.0/NLOD; atribuição |
| **Open-Meteo Geocoding** | Busca de cidade/praia | Gratuito | Resultados no Brasil |
| **Nominatim/OSM** | Cidade/UF a partir de GPS da foto | Gratuito, uso responsável | ODbL; cache e limite de requisições |
| **OpenStreetMap** | Mapa base | Gratuito | Atribuição © OpenStreetMap |
| **Esri World Imagery** | Satélite | Gratuito com atribuição | Tiles © Esri/Maxar |
| **OpenSeaMap** | Sinalização náutica | Gratuito | Atribuição © OpenSeaMap |
| **GEBCO** | Batimetria (WMS) | Gratuito | Atribuição GEBCO; modelo global |
| **Resend** | Link mágico e newsletter | Plano gratuito/limites | Domínio verificado |
| **Gemini** | Sugestão de espécie (opcional) | Depende da chave/conta | Sem chave, o fluxo é manual |

**Avisos:** a maré é aproximada por modelo global e **não serve para navegação**. O mapa não
substitui carta náutica oficial. O defeso é editorial e aponta para o órgão oficial; não é laudo.

---

## Rodando localmente

Pré-requisitos: **.NET 10 SDK** e **Node 24+**.

### API (SQLite)

```bash
cd Projetos/fisga/src/Fisga.Api
dotnet run
```

API em `http://localhost:5184`; o SQLite é criado no primeiro start. As fotos ficam em
`bin/Debug/net10.0/media`.

Em desenvolvimento, `/api/auth/solicitar` devolve `linkDesenvolvimento` e a tela `/entrar` informa
que o link foi “enviado”. Para testar confirmação de e-mail e reset sem provedor real, use o outbox:

```bash
cd Projetos/fisga/src/Fisga.Api
Email__Provider=Outbox Email__OutboxPath="$PWD/.outbox" dotnet run
```

Cada e-mail vira um JSON em `.outbox/` (destino, assunto e link). A pasta contém dados pessoais:
use apenas localmente e nunca a versione. Com o sender `Console` (padrão), nenhum link é gravado em
log — só domínio e assunto.

### Web

Em outro terminal:

```bash
cd Projetos/fisga/web
npm install
npm run dev
```

Abra **`http://localhost:4323`**.

### Fluxo de teste

1. Abra `/registrar`; o formulário aparece bloqueado.
2. Entre com um e-mail e abra o link de desenvolvimento.
3. Preencha o nome e escolha um local padrão (busca ou GPS) no primeiro acesso.
4. Confira `/hoje`: condições do local, janela com gráfico, 7 dias e atalhos.
5. Em `/mapa`, alterne Locais/Pontos/Trilhas, crie um ponto, desenhe uma trilha e exporte GPX.
6. Em `/registrar`, percorra as 4 etapas (pescaria, condições, capturas, revisar); envie uma foto com
   EXIF e confira data/cidade sugeridas e o lembrete de defeso.
7. Revise o resumo, salve a pescaria, abra o troféu e baixe a imagem/PDF.
8. Em `/meu-diario`, alterne Visão geral/Pescarias/Insights.
9. Em `/aprender`, busque “robalo” ou “nó” e confira os selos offline.
10. Em `/conta`, edite o perfil, crie/altere a senha, veja sessões, exporte os dados e teste a
    exclusão.

### Docker Compose

```bash
cd Projetos/fisga
docker compose up --build
```

- Web: `http://localhost:3002`
- API: `http://localhost:5184`
- Postgres: `localhost:5435` (`fisga` / `fisga` / `fisga`)
- Fotos: volume `fisga_media` em `/app/media`
- Chaves de Data Protection: volume `fisga_keys` em `/app/data/keys` (obrigatório)

---

## Configuração

Nunca commite chaves, tokens ou senhas. Use variáveis de ambiente do provedor.

> **Produção exige `ASPNETCORE_ENVIRONMENT=Production`.** Nunca exponha o perfil Development: ele
> devolve `linkDesenvolvimento` em `/api/auth/solicitar`, um vetor de tomada de conta.

### Banco e API

| Variável | Descrição |
| --- | --- |
| `Database__Provider` | `Postgres` (produção) ou `Sqlite` (dev) |
| `Database__ConnectionString` | conexão do banco |
| `Cors__AllowedOrigins` | origens do site separadas por vírgula |
| `Web__Url` | URL pública do site usada em links |
| `Media__Path` | pasta de mídia |
| `Auth__LinkBaseUrl` | URL pública da API no link mágico |
| `Auth__WebUrl` | URL pública do site após confirmar |
| `Auth__CookieSecure` | `true` em produção |
| `Auth__ValidadeDoLinkEmMinutos` | padrão 15 |
| `Auth__DuracaoDaSessaoEmDias` | padrão 30 |
| `Auth__LegacyCookieName` | nome do cookie obsoleto a apagar (padrão `fisga_sessao`) |
| `DataProtection__KeysPath` | pasta persistente das chaves (padrão `data/keys`; produção `/app/data/keys`) |
| `Google__ClientId` | Client ID OAuth (opcional; sem ele o Google fica oculto) |
| `Google__ClientSecret` | Client Secret OAuth (opcional) |
| `Google__CallbackPath` | caminho do callback (padrão `/signin-google`) |
| `Legal__TermsVersion` | versão vigente dos Termos (padrão `2026-11-17`) |
| `Legal__PrivacyVersion` | versão vigente da Privacidade (padrão `2026-11-17`) |
| `RateLimiting__Auth` | limite por minuto/IP da autenticação (padrão 8; só testes elevam) |
| `SENTRY_DSN` | DSN do projeto `fisga-api` |
| `SENTRY_ENVIRONMENT` | `production` ou `development` |
| `SENTRY_RELEASE` | não usado diretamente pela API; o release vem de `GIT_SHA`/`SOURCE_COMMIT` |
| `SENTRY_TRACES_SAMPLE_RATE` | amostragem de traces (padrão 0.1 em produção) |
| `SOURCE_COMMIT` | commit passado pelo Coolify no build; vira `GIT_SHA` e release do Sentry |

### Web (build/SSR)

| Variável | Descrição |
| --- | --- |
| `PUBLIC_API_URL` | URL pública da API usada pelo navegador |
| `PUBLIC_SITE_URL` | URL pública do site/canonical/sitemap |
| `API_URL` | URL da API usada pelo SSR do Astro |
| `SENTRY_DSN` | DSN do projeto `fisga-web` no SSR |
| `PUBLIC_SENTRY_DSN` | DSN do navegador (build) |
| `SENTRY_ENVIRONMENT` | `production` ou `development` (build e runtime) |
| `SENTRY_RELEASE` | runtime; o Dockerfile define a partir de `GIT_SHA` |
| `PUBLIC_SENTRY_RELEASE` | release do navegador (build arg; fallback para `SENTRY_RELEASE`) |
| `SENTRY_AUTH_TOKEN` | token de upload de source maps; usar BuildKit secret `sentry_auth_token`, nunca ARG/ENV |

### E-mail e newsletter

| Variável | Descrição |
| --- | --- |
| `Email__Provider` | `Console` (dev), `Outbox` (E2E) ou `Resend` (produção) |
| `Email__From` | remetente verificado |
| `Email__ResendApiKey` | chave do Resend |
| `Email__ReplyTo` | resposta opcional |
| `Email__OutboxPath` | pasta dos JSONs de e-mail quando `Provider=Outbox` (somente teste) |
| `Newsletter__AdminToken` | token do endpoint administrativo |
| `Newsletter__Secret` | segredo HMAC do descadastro |
| `Newsletter__MaximoPorEnvio` | limite por execução |

### Condições e visão

| Variável | Descrição |
| --- | --- |
| `Condicoes__Fuso` | padrão `America/Sao_Paulo` |
| `Condicoes__CacheMinutos` | validade do cache (padrão 20; mínimo 1, máximo 20) |
| `Vision__Provider` | `Manual` (padrão) ou `Gemini` |
| `Vision__ApiKey` | chave do provedor de visão |
| `Vision__Model` | padrão `gemini-2.5-flash` |

Exemplo para ligar a sugestão de espécie:

```env
Vision__Provider=Gemini
Vision__ApiKey=sua-chave
Vision__Model=gemini-2.5-flash
```

Sem chave, a API responde `disponivel: false` e o formulário pede confirmação manual — sem quebrar o
fluxo.

O domínio `abson.tech` está verificado no Resend (DKIM + CNAMEs de envio + DMARC) e o remetente de
produção é `Fisga <acesso@abson.tech>`. O cron mensal da newsletter fica no servidor e chama
`POST /api/newsletter/enviar` com `X-Admin-Token`.

---

## Testes

```bash
cd Projetos/fisga
dotnet test
cd web
npm run check && npm run build
npx playwright test
```

Atualmente: **250 testes de backend** (36 de domínio + 48 de aplicação + 166 de API) e checagem de
tipos do Astro. Cobrem:

- criação/edição de pescaria, IDs de captura, validações e isolamento entre usuários;
- link mágico, senha, confirmação de e-mail, reset, sessões, logout e exclusão de conta;
- consentimento, `/conta`, exportação e Google indisponível;
- exportação, locais salvos, condições, lua/solunar, janela e comparação com histórico;
- pontos e trilhas privadas, com ownership;
- fallback manual da sugestão de espécie;
- resolução da versão exposta em `/health`.

O fluxo completo também é validado com Playwright: **61 testes mockados** (UI e estados) e
**6 testes com backend real** para a migração de identidade. Os reais rodam com SQLite, chaves e
outbox temporários:

```bash
cd web
./scripts/e2e-real.sh
```

Eles cobrem cadastro por senha → confirmação → consentimento → app; link mágico de usuário existente
→ consentimento → diário com dados preservados; reset de senha; “sair desta” e “sair de todos”;
`/conta` (senha, perfil/newsletter, exportação) e Google indisponível. O fluxo OAuth real e a
migration em PostgreSQL ficam no runbook de rollout, condicionados a credenciais e backup.

Build da web (wiki + Pagefind + precache offline):

```bash
cd web
npm run build   # astro build + pagefind + precache
```

---

## CI/CD

O Fisga tem dois workflows no GitHub Actions: `fisga-pipeline.yml` (CI) e `fisga-deploy.yml` (CD).
O CI roda em todo PR e em push para `main`, detecta mudanças em `Projetos/fisga/**` e executa:

- `api`: restore locked, `dotnet format --verify-no-changes`, build `-warnaserror` e os 250 testes
  xUnit.
- `web`: `npm ci`, `astro check`, `npm run build` e `npm run test:e2e` (61 testes Playwright
  mockados).
- `docker`: build das imagens da API e do web sem push (em PR e `workflow_dispatch`).
- `gate`: job obrigatório; falha se qualquer job necessário falhar.

No push para `main`, quando o `fisga-pipeline` conclui verde, o `fisga-deploy` roda por
`workflow_run`: dispara o `fisga-api`, depois o `fisga-web`, acompanha o deployment, aguarda o
`/health` retornar o SHA do commit e roda smoke tests. O `workflow_dispatch` na `main` roda o CI e,
ao concluir, o deploy. Validação ponta a ponta: 18/11/2026, commit `725afd5`, deploy `44338998771`;
revalidada no deploy dos docs em `f50560a` (`44348792718`).

O check obrigatório da `main` é o job **`gate`** (a UI pode exibir `fisga-pipeline / gate`). Confirme
o contexto exato com `gh api repos/AbsonDev/abson/commits/<sha>/check-runs`.

Segredos e variáveis necessários no GitHub: `COOLIFY_URL`, `COOLIFY_TOKEN` (read+deploy),
`COOLIFY_WEBHOOK_FISGA_API`, `COOLIFY_WEBHOOK_FISGA_WEB` e a variável `FISGA_PUBLIC_SENTRY_DSN`.
Os webhooks e o `COOLIFY_URL` precisam ser HTTPS.

Runbook completo, configuração do Coolify e rollback: [docs/ci-cd.md](docs/ci-cd.md).

---

## API

| Método | Rota | Acesso | Descrição |
| --- | --- | --- | --- |
| `GET` | `/health`, `/ready` | Público | Saúde/readiness |
| `GET` | `/api/catalogo` | Público | Espécies, iscas, opções e exemplo |
| `POST` | `/api/midia` | Sessão | Upload de foto (`multipart`, campo `arquivo`) |
| `POST` | `/api/auth/solicitar` | Público | Envia link mágico (resposta genérica; `linkDesenvolvimento` em dev) |
| `GET` | `/api/legal/versoes` | Público | Versões vigentes de Termos e Privacidade |
| `GET` | `/api/auth/confirmar?token=` | Público | Confirma link, cria sessão e redireciona |
| `POST` | `/api/auth/registrar` | Público | Cadastro por nome/e-mail/senha e envio de confirmação |
| `POST` | `/api/auth/entrar` | Público | Login por senha (exige e-mail confirmado) |
| `POST` | `/api/auth/confirmar-email` | Público | Confirma e-mail com `uid` + `token` |
| `POST` | `/api/auth/esqueci-senha` | Público | Envia link de reset (resposta genérica) |
| `POST` | `/api/auth/redefinir-senha` | Público | Troca a senha, invalida sessões e o token |
| `GET` | `/api/auth/google/disponivel` | Público | Se o Google está configurado |
| `GET` | `/api/auth/google/iniciar` | Público | Inicia OAuth de login (503 sem credenciais) |
| `GET` | `/api/auth/google/concluir` | Público | Callback pós-Google de login |
| `GET` | `/api/auth/google/conectar` | Sessão | Inicia vínculo do Google em `/conta` |
| `GET` | `/api/auth/google/conectar/concluir` | Sessão | Callback do vínculo do Google |
| `GET` | `/api/auth/me` | Público | Usuário, consentimento, Google e senha |
| `POST` | `/api/auth/sair` | Público (idempotente) | Encerra a sessão atual e apaga `fisga_sessao` legado |
| `POST` | `/api/auth/sair-de-todos` | Sessão | Encerra todas as sessões/dispositivos |
| `PATCH` | `/api/auth/perfil` | Sessão | Nome e preferência de e-mail |
| `POST` | `/api/conta/consentimento` | Sessão | Registra aceite das versões vigentes |
| `POST` | `/api/conta/senha` | Sessão | Cria/alterar senha e revoga outras sessões |
| `GET` | `/api/conta/sessoes` | Sessão | Sessões ativas |
| `GET` | `/api/conta/exportar` | Sessão | Exportação JSON da conta/diário |
| `DELETE` | `/api/conta` | Sessão | Exclui conta, credencial Identity, tokens, diário, sessões e fotos (`confirmacao: "EXCLUIR"`) |
| `POST` | `/api/newsletter/enviar` | Admin | Envia o digest mensal (`X-Admin-Token`) |
| `GET` | `/api/newsletter/preview` | Admin | Prévia HTML do digest |
| `GET` | `/api/newsletter/descadastrar?token=` | Token | Descadastra via link assinado |
| `POST` | `/api/pescarias` | Sessão | Cria pescaria |
| `GET` | `/api/pescarias/{shareToken}` | Público | Visão pública |
| `GET` | `/api/pescarias/{shareToken}/permissao` | Público | Se o visitante pode editar |
| `GET` | `/api/pescarias/{id}/editar` | Dono | Dados privados para edição |
| `PUT` | `/api/pescarias/{id}` | Dono | Edita pescaria |
| `GET` | `/api/pescarias/{shareToken}/pdf` | Público | PDF da pescaria |
| `GET` | `/api/diario` | Sessão | Diário, estatísticas, insights e sinais |
| `GET` | `/api/condicoes?lat=&lon=` | Público | Condições, maré aproximada, cockpit e janela |
| `GET` | `/api/condicoes/comparar?lat=&lon=` | Sessão | Dia parecido com o histórico |
| `GET` | `/api/locais` | Sessão | Locais salvos |
| `POST` | `/api/locais` | Sessão | Cria local (máximo 5) |
| `PUT` | `/api/locais/{id}` | Dono | Edita local |
| `POST` | `/api/locais/{id}/padrao` | Dono | Define local padrão |
| `DELETE` | `/api/locais/{id}` | Dono | Exclui local |
| `GET` | `/api/locais/buscar?q=` | Público | Busca cidade/praia |
| `GET` | `/api/locais/reverso?lat=&lon=` | Público | Cidade/UF a partir de coordenadas |
| `GET` | `/api/pontos` | Sessão | Pontos privados |
| `POST` | `/api/pontos` | Sessão | Cria ponto (máximo 200) |
| `PUT` | `/api/pontos/{id}` | Dono | Edita ponto |
| `DELETE` | `/api/pontos/{id}` | Dono | Exclui ponto |
| `GET` | `/api/trilhas` | Sessão | Trilhas privadas |
| `POST` | `/api/trilhas` | Sessão | Cria trilha (máximo 100) |
| `PUT` | `/api/trilhas/{id}` | Dono | Edita trilha |
| `DELETE` | `/api/trilhas/{id}` | Dono | Exclui trilha |
| `POST` | `/api/especies/sugerir` | Sessão | Sugestão de espécie por foto |

Cada pescaria tem `shareToken` público. O diário é acessado pela conta; não existe token anônimo no
navegador. Sessões e links mágicos são armazenados apenas como hash SHA-256.

---

## Privacidade e segurança

- O diário completo é privado; cada pescaria tem um link público próprio e opcional.
- O local pode ser exato, aproximado (cidade/UF) ou oculto; a escolha vale para link, troféu e PDF.
- Locais salvos, pontos e trilhas são privados e não têm endpoint público.
- Links mágicos são de uso único, valem 15 minutos e só o mais recente funciona.
- Login por senha exige e-mail confirmado; a senha tem 10–128 caracteres e o reset invalida sessões.
- Sessões duram 30 dias no cookie `fisga_auth` da Identity (`HttpOnly`/`Secure`/`SameSite=Lax`), com
  uma linha por dispositivo e encerramento individual ou total. O cookie legado `fisga_sessao` é
  apagado e não autentica.
- As chaves de Data Protection ficam em volume persistente (`/app/data/keys`); perdê-las invalida
  cookies e links em circulação, não dados.
- Termos e Privacidade têm versão (`2026-11-17`) e o aceite é registrado com data/hora por usuário.
- Google é opcional e só é habilitado quando as duas credenciais existem; sem elas, o botão não
  aparece e o endpoint responde `disponivel: false`.
- Fotos são reencodadas no navegador e têm EXIF removido do arquivo enviado. O EXIF é lido antes,
  localmente, apenas para sugerir data/período/cidade; o GPS não é armazenado sem ação do usuário.
- A sugestão de espécie nunca tem autoridade: o pescador confirma ou corrige. Sem provedor, o fluxo
  é manual.
- Defeso e tamanho mínimo são um lembrete editorial com link para o órgão oficial, não um laudo.
- Rotas privadas não entram no cache de navegação do service worker; `/condicoes` autenticado também não
  é cacheado offline (só o shell anônimo, com `Cache-Control: public`, entra). O logout e a sessão
  expirada apagam `fisga-private-*` e o `localStorage` privado, mas preservam `fisga-public-*` e os
  tiles para manter `/offline`, wiki, ferramentas e Pagefind funcionando.
- Exportação JSON e exclusão de conta (LGPD) estão em `/conta`.
- O mapa usa camadas de terceiros com atribuição e não substitui carta náutica oficial.

---

## Offline e limites do offline

**Funciona offline depois da primeira visita:**

- wiki e busca Pagefind;
- ferramentas (nós, checklists, defeso, guias);
- condições do shell anônimo de `/condicoes` e `/` (a versão autenticada não é cacheada offline);
- pontos e trilhas já carregados (`localStorage`);
- rascunho de pescaria;
- últimos 600 azulejos de mapa visitados.

**Não funciona offline:**

- consultar condições de um local novo;
- sincronizar uma pescaria criada sem sinal (fila/IndexedDB ainda não existe);
- baixar uma área inteira do mapa;
- sugestão de espécie por IA;
- mapa completo de uma região desconhecida.

---

## Limites conhecidos

- **Maré aproximada**: modelo global, sem tábua oficial do CHM; pode variar ~1 h em relação ao porto.
- **Janela de pesca é heurística**: explainable, não um modelo treinado nem garantia.
- **IA de espécie depende de provedor**: sem chave, o fluxo é manual.
- **Defeso é editorial**: não mantemos tabela própria de tamanhos mínimos por espécie/UF.
- **Diretório de guias em curadoria**: sem cadastro automático nem pagamento.
- **Sem PostGIS/clustering**: raio, bacia e inteligência coletiva anonimizada ainda não existem.
- **Sem mapa offline por área**: só cache de azulejos visitados.
- **Cache de condições em memória**: múltiplas réplicas exigem cache distribuído. Cada entrada é limitada a 256 KB e a chave a 200 caracteres; não há teto próprio de quantidade no HybridCache — o crescimento fica limitado pela cardinalidade (lat/lon com 2–3 casas, termo de busca normalizado a 80) e pelo rate limit de leitura.
- **Newsletter sem idempotência por mês**: o cron deve rodar uma vez.
- **Mídia em volume local**: escala maior pede object storage/CDN.

---

## Próximos passos

1. **Maré oficial do CHM** para os 44 portos, com importação anual e atribuição.
2. **Provedor de visão** para ativar a sugestão automática de espécie.
3. **PostGIS + inteligência coletiva anonimizada** por macrorregião, com opt-in e amostra mínima.
4. **Fila offline de pescarias** em IndexedDB e sincronização.
5. **Mapas offline por área** baixada.
6. **Diretório de guias** com perfis verificados.
7. **Alertas** de janela boa e mudança brusca de pressão.

Roadmap completo em [`docs/roadmap.md`](docs/roadmap.md).

---

## Documentação

- [`docs/ddd.md`](docs/ddd.md) — contextos, agregados, invariantes e limites.
- [`docs/roadmap.md`](docs/roadmap.md) — módulos, fases, dados e métricas.
- [`docs/ux-audit.md`](docs/ux-audit.md) — auditoria de UX, arquitetura de informação e prioridades.
- [`docs/superpowers/runbooks/2026-11-17-identity-rollout.md`](docs/superpowers/runbooks/2026-11-17-identity-rollout.md)
  — rollout, backup, migration, Google, rollback e evidências da identidade.
- [`docs/adr`](docs/adr) — decisões arquiteturais (0001–0012).
- [`docs/referencias`](docs/referencias) — estudos de referência (Fishing World Guide, Tábua de Marés).
- [`docs/ddd.md#testes`](docs/ddd.md#testes) — estratégia de testes.

---

## Licença

Uso privado do portfólio. Defina a licença comercial antes de publicar como produto independente.
