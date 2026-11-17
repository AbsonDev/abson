# Fisga

**Diário de pesca com troféus, estatísticas e relatório em PDF.**

O pescador registra a pescaria, as condições do dia e cada captura — espécie, peso, comprimento,
isca, foto e se o peixe foi solto. O Fisga monta um diário privado por conta, mostra padrões e gera um
card de troféu compartilhável + PDF da pescaria.

> **Estado atual: 100% gratuito.** Diário, card do troféu e PDF liberados durante a validação, sem
> cartão ou assinatura. Monetização futura prevista: R$ 9–19/mês.

**No ar:** [fisga.abson.tech](https://fisga.abson.tech) · API em
[api.fisga.abson.tech](https://api.fisga.abson.tech/health).

## Funcionalidades

- **Conta por link mágico**: o pescador informa o e-mail, recebe um link de uso único (15 min) e
  entra sem senha. A sessão dura 30 dias em cookie `HttpOnly`.
- **Diário privado por usuário**: pescarias, capturas, condições, fotos, estatísticas, exportação
  JSON e exclusão de conta. O diário acompanha o pescador em qualquer aparelho.
- **Pescaria com local, cidade/UF, ambiente, data, período e condições** (lua, maré, clima, água).
- **Várias capturas por pescaria**, com ID estável.
- **Espécie, peso, comprimento, isca, destino (solto/capturado), observação e até 4 fotos por captura**, com escolha da capa.
- **Privacidade do local por pescaria**: local exato, apenas cidade/UF ou local oculto. Vale para link público, troféu e PDF.
- Upload de foto com compressão no navegador (até 5 MB; JPG/PNG/WebP).
- **Estatísticas**: pescarias, capturas, espécies distintas, peixes soltos, maior peso e
  comprimento, espécie/isca/local que mais aparecem, taxa de soltura.
- **Insights**: melhor mês, período, lua, maré, clima e água; recordes por espécie; isca que mais
  funcionou por espécie.
- **Link público por pescaria**, sem expor o restante do diário; troféu e PDF também são públicos.
- **Card do troféu** em PNG, gerado no navegador com foto, espécie, peso, local e data.
- **Story card 1080×1920** para Instagram/WhatsApp Status, além do card quadrado 1080×1080.
- **Prévia social (OG)** com a foto da captura para o link render bonito no WhatsApp/Facebook.
- **PDF da pescaria** com condições, capturas, maior peixe, notas e QR.
- **PWA instalável** com manifest, ícones, página offline e rascunho local do formulário.
- **Wiki do Pescador** com 11 espécies, 6 técnicas, 6 iscas, 5 regiões, defeso, iniciantes,
  segurança, glossário e fontes. Páginas estáticas, busca Pagefind e leitura offline.
- **Newsletter mensal** personalizada: estatísticas do mês, leituras da wiki, lembrete de defeso e
  descadastro assinado.
- **Condições automáticas** por local: maré aproximada, vento, chuva, pressão, lua e janela de
  pesca. Consulta pública, sem login.
- **Locais salvos**: até 5 por conta, com um padrão que abre primeiro e pré-preenche o registro.
- **Janela de pesca explicável**: nota de 1–5 com os motivos (solunar, maré, vento, chuva e luz do
  dia), em vez de um número mágico.
- **Comparação com o histórico**: encontra um dia parecido usando lua, maré, clima e período; só
  mostra padrão quando há pelo menos 3 pescarias com captura.
- **Registro pré-preenchido**: local, data, período, maré, lua e clima entram a partir das condições.
- **Cockpit sincronizado**: linha do tempo de 24 h com maré, pressão, vento e solunar; corrente
  oceânica real e alerta de mudança brusca de pressão.
- **Mapa privado**: Leaflet com mapa, satélite, náutico e batimetria; pontos, poitas, estruturas e
  rampas privados, com notas e cache local.
- **Trilhas privadas**: corrico e deslocamento desenhados no mapa, distância calculada e exportação
  GPX.
- **Ferramentas offline**: oito nós passo a passo, checklists por modalidade, defeso por UF e guia
  para escolher piloteiro.
- **Diário inteligente**: leitura de EXIF (data e GPS) para sugerir data, período e cidade; lembrete
  de defeso por espécie/UF; insights comparativos com amostra mínima.
- **Sugestão de espécie por foto**: porta plugável (Gemini opcional) com confirmação humana e
  fallback manual quando não há provedor configurado.
- Edição de pescaria preservando os IDs das capturas (links de troféu não quebram).
- **Boas-vindas no primeiro acesso**, com nome e preferência de e-mail.
- **Conta**: perfil, troca de e-mail via novo link, sessões, exportação e exclusão com confirmação.
- Modo gratuito, sem checkout.

## Stack

| Camada | Tecnologia |
| --- | --- |
| Domínio/aplicação | .NET 10 / C# 13 |
| API | ASP.NET Core 10, Minimal APIs, OpenAPI |
| Persistência | EF Core 10 + PostgreSQL 17 (SQLite no desenvolvimento) |
| Autenticação | Link mágico + sessão em cookie; tokens com hash SHA-256 |
| E-mail | Resend (produção) e sender de console (desenvolvimento) |
| PDF | QuestPDF + QRCoder |
| Dados ambientais | Open-Meteo (tempo, ondas, corrente e nível do mar) + MET Norway (lua) |
| Mapas | Leaflet + OpenStreetMap + Esri World Imagery + OpenSeaMap + GEBCO (batimetria) |
| Visão computacional | Porta `IReconhecedorDeEspecie` (Gemini opcional; manual por padrão) |
| Mídia | upload local + arquivos estáticos com CORS |
| Card do troféu | Canvas API no navegador (quadrado + story) |
| PWA | manifest, service worker e página offline |
| Conteúdo | Astro Content Collections (Markdown tipado) |
| Busca | Pagefind (índice estático, funciona offline) |
| Web | Astro 7 SSR + TypeScript + CSS próprio |
| Infra local | Docker Compose |

## DDD em uma frase

`Pescaria` é o agregado central (local, condições e `Captura`s com identidade) e pertence
obrigatoriamente a um `Usuario`. O **Diário** é um read model privado por usuário que agrega
pescarias e calcula estatísticas. **Identidade e Acesso** é o contexto genérico de conta, sessão e
link mágico. Catálogo, mídia e documentos são contextos de apoio.

Detalhes em [`docs/ddd.md`](docs/ddd.md), [`docs/adr`](docs/adr) e
[`docs/referencias`](docs/referencias).

## Rodando localmente

Pré-requisitos: **.NET 10 SDK** e **Node 24+**.

### API (SQLite)

```bash
cd Projetos/fisga/src/Fisga.Api
dotnet run
```

API em `http://localhost:5184`; o SQLite é criado no primeiro start. As fotos ficam em
`bin/Debug/net10.0/media`.

Em desenvolvimento, o link mágico é impresso no log da API **e** aparece um botão
“Abrir link de acesso (desenvolvimento)” na tela `/entrar`. Nenhum e-mail real é enviado.

### Web

Em outro terminal:

```bash
cd Projetos/fisga/web
npm install
npm run dev
```

Abra **`http://localhost:4323`**.

Fluxo de teste:

1. Abra `/registrar`; o formulário aparece bloqueado.
2. Clique em “Entrar com e-mail”, informe um e-mail e abra o link de desenvolvimento.
3. No primeiro acesso, preencha o nome e escolha receber dicas (opcional).
4. Registre a pescaria, adicione uma foto em uma captura e salve.
5. Abra o card do troféu e baixe a imagem/PDF.
6. Volte ao `/meu-diario` e confira as estatísticas.
7. Saia pelo menu da conta e entre de novo com o mesmo e-mail.
8. Em `/conta`, edite o perfil, veja as sessões, exporte os dados e teste a exclusão.

### Docker Compose

```bash
cd Projetos/fisga
docker compose up --build
```

- Web: `http://localhost:3002`
- API: `http://localhost:5184`
- Postgres: `localhost:5435` (`fisga` / `fisga` / `fisga`)
- Fotos: volume `fisga_media` em `/app/media`

## Configuração de e-mail

| Variável | Descrição |
| --- | --- |
| `Email__Provider` | `Console` (dev) ou `Resend` (produção) |
| `Email__From` | remetente verificado, ex. `Fisga <acesso@abson.tech>` |
| `Email__ResendApiKey` | API key do Resend (nunca commitar) |
| `Email__ReplyTo` | resposta opcional |
| `Newsletter__AdminToken` | token do endpoint administrativo de envio |
| `Newsletter__Secret` | segredo HMAC dos links de descadastro |
| `Newsletter__MaximoPorEnvio` | limite de destinatários por execução |
| `Condicoes__Fuso` | fuso das condições (padrão `America/Sao_Paulo`) |
| `Condicoes__CacheMinutos` | validade do cache de condições (padrão 20) |
| `Vision__Provider` | `Manual` (padrão) ou `Gemini` |
| `Vision__ApiKey` | chave do provedor de visão (nunca commitar) |
| `Vision__Model` | modelo de visão (padrão `gemini-2.5-flash`) |
| `Auth__LinkBaseUrl` | URL pública da API usada no link mágico |
| `Auth__WebUrl` | URL pública do site para redirecionar após confirmar |
| `Auth__CookieSecure` | `true` em produção (HTTPS) |

O domínio `abson.tech` está verificado no Resend (DKIM + CNAMEs de envio + DMARC) e o remetente de
produção é `Fisga <acesso@abson.tech>`. Se trocar de provedor, mantenha a porta `IEmailSender` e
configure o novo adaptador.

## Testes

```bash
cd Projetos/fisga
dotnet test
cd web && npm run check
```

Atualmente: **75 testes de backend** (25 de domínio + 50 de aplicação) e checagem de tipos do
Astro. Cobrem criação/edição de pescaria, IDs de captura, validações, isolamento entre usuários,
link mágico, expiração, uso único, sessão, logout, exclusão de conta, exportação, locais salvos,
condições, lua/solunar, comparação com o histórico, pontos e trilhas privadas, além do fallback
manual da sugestão de espécie. O fluxo completo também é validado com Playwright.

O build da web gera as páginas da wiki e roda o Pagefind automaticamente:

```bash
cd web
npm run build   # astro build + pagefind + precache offline
```

## API

| Método | Rota | Descrição |
| --- | --- | --- |
| `GET` | `/api/catalogo` | espécies, iscas, opções e exemplo |
| `POST` | `/api/midia` | upload de foto (`multipart`, campo `arquivo`); exige sessão |
| `POST` | `/api/auth/solicitar` | envia link mágico (resposta genérica) |
| `GET` | `/api/auth/confirmar?token=` | confirma o link, cria sessão e redireciona |
| `GET` | `/api/auth/me` | usuário da sessão atual (ou `{ autenticado: false }`) |
| `POST` | `/api/auth/sair` | encerra a sessão atual |
| `POST` | `/api/auth/sair-de-todos` | encerra todas as sessões |
| `PATCH` | `/api/auth/perfil` | nome e preferência de e-mail |
| `GET` | `/api/conta/sessoes` | sessões ativas da conta |
| `GET` | `/api/conta/exportar` | exportação JSON da conta e do diário |
| `DELETE` | `/api/conta` | exclui conta, diário, sessões e fotos (`confirmacao: "EXCLUIR"`) |
| `POST` | `/api/newsletter/enviar` | envia o digest mensal; exige `X-Admin-Token` |
| `GET` | `/api/newsletter/preview` | prévia HTML do digest; exige `X-Admin-Token` |
| `GET` | `/api/newsletter/descadastrar?token=` | descadastra via link assinado |
| `POST` | `/api/pescarias` | cria pescaria; exige sessão |
| `GET` | `/api/pescarias/{shareToken}` | visão pública |
| `GET` | `/api/pescarias/{shareToken}/permissao` | se o visitante pode editar |
| `GET` | `/api/pescarias/{id}/editar` | dados privados para edição; exige dono |
| `PUT` | `/api/pescarias/{id}` | edita; exige dono |
| `GET` | `/api/pescarias/{shareToken}/pdf` | PDF da pescaria (público) |
| `GET` | `/api/diario` | diário + estatísticas; exige sessão |
| `GET` | `/api/condicoes?lat=&lon=` | condições públicas, maré aproximada e janela |
| `GET` | `/api/condicoes/comparar?lat=&lon=` | dia parecido com o histórico; exige sessão |
| `GET` | `/api/locais` | locais salvos; exige sessão |
| `POST` | `/api/locais` | cria local (máximo 5); exige sessão |
| `PUT` | `/api/locais/{id}` | edita local; exige dono |
| `POST` | `/api/locais/{id}/padrao` | define o local padrão |
| `DELETE` | `/api/locais/{id}` | exclui local |
| `GET` | `/api/locais/buscar?q=` | busca cidade/praia (geocodificação) |
| `GET` | `/api/pontos` | pontos privados do mapa; exige sessão |
| `POST` | `/api/pontos` | cria ponto privado (máximo 200); exige sessão |
| `PUT` | `/api/pontos/{id}` | edita ponto; exige dono |
| `DELETE` | `/api/pontos/{id}` | exclui ponto; exige dono |
| `GET` | `/api/trilhas` | trilhas privadas; exige sessão |
| `POST` | `/api/trilhas` | cria trilha (máximo 100); exige sessão |
| `PUT` | `/api/trilhas/{id}` | edita trilha; exige dono |
| `DELETE` | `/api/trilhas/{id}` | exclui trilha; exige dono |
| `GET` | `/api/locais/reverso?lat=&lon=` | cidade/UF a partir de coordenadas (Nominatim) |
| `POST` | `/api/especies/sugerir` | sugestão de espécie por foto; exige sessão |
| `GET` | `/health`, `/ready` | saúde/readiness |

Cada pescaria tem `shareToken` público. O acesso ao diário é pela conta; não existe mais token
anônimo no navegador. Sessões e links mágicos são armazenados apenas como hash SHA-256.

## Privacidade e limites

- O diário completo fica na conta do pescador; cada pescaria tem um link público próprio.
- O local pode ser exato, aproximado (cidade/UF) ou oculto. A escolha vale para link público,
  troféu e PDF.
- O e-mail é a chave de acesso; não há senha para vazar ou esquecer.
- Links mágicos são de uso único, valem 15 minutos e só o mais recente funciona.
- Sessões duram 30 dias e podem ser encerradas no dispositivo ou em todos.
- Fotos são reencodadas no navegador antes do upload, o que remove metadados EXIF do arquivo.
- O local da pescaria aparece no link público daquela pescaria; o restante do diário não.
- Locais salvos, pontos e trilhas do mapa são privados. As condições públicas usam apenas as
  coordenadas da URL e não expõem a conta de quem consulta.
- O mapa usa camadas de terceiros (OSM, Esri, OpenSeaMap, GEBCO) com atribuição; não substitui carta
  náutica oficial. Pontos, trilhas e últimos azulejos ficam em cache local; dados privados são
  apagados quando a sessão expira (401).
- EXIF de foto é usado apenas para sugerir data, período e cidade. O GPS não é armazenado sem ação
  do usuário; a cidade sugerida pode ser corrigida.
- A sugestão de espécie é opcional e nunca tem autoridade: o pescador confirma ou corrige. Sem
  provedor configurado, o fluxo cai no preenchimento manual.
- Defeso e tamanho mínimo são um lembrete editorial com link para o órgão oficial, não um laudo.
- A maré é uma **aproximação por modelo global** e é rotulada como tal. Não serve para navegação.
- O card do troféu é gerado localmente no navegador; o Fisga não publica em rede social.
- Exportação JSON e exclusão de conta (LGPD) já estão disponíveis em `/conta`.
- Identificação de espécie, mapa de locais, dados oficiais do CHM e comunidade ficam para as
  próximas fases.

## Próximos passos

- **Sincronização offline real**: fila de pescarias no IndexedDB e envio quando o sinal voltar.
- **Mais espécies, técnicas e regiões**, com revisão por pescadores locais.
- **Validação de defeso/tamanho mínimo** no formulário por espécie e UF.
- **Maré oficial do CHM** para os 44 portos, com importação anual e atribuição.
- **Provedor de visão** para ativar a sugestão automática de espécie (Gemini ou outro).
- **PostGIS e inteligência coletiva anonimizada** por macrorregião, com opt-in e amostra mínima.
- **Tábua oficial do CHM** para os 44 portos.
- **Diretório de guias** com curadoria e perfis verificados.
- **Mapas offline por área** baixada (tiles em Cache Storage/IndexedDB).
- Roadmap completo em [`docs/roadmap.md`](docs/roadmap.md).
- **Múltiplas fotos por captura** e escolha da capa.
- **OG image dinâmica** renderizada no servidor, além da foto atual.
- Previsão de maré/lua/clima com APIs e sugestão de janela.
- Catálogo de locais, mapa privado e identificação de espécie por foto.
- Feed/comunidade, desafios e assinatura após validação.

## Licença

Uso privado do portfólio. Defina a licença comercial antes de publicar como produto independente.
