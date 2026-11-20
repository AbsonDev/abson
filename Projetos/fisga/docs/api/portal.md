# Arquitetura do portal de documentação

Como o portal `docs.fisga.abson.tech` é
construído, publicado e mantido. Decisão registrada na
[ADR 0017](../adr/0017-portal-publico-da-api.md); regras de escrita na
[política editorial](politica-editorial.md).

## Princípios

1. **O contrato é a espinha.** A referência nasce de
   [`openapi.json`](openapi.json). Nenhuma página de operação é escrita à mão.
2. **Prosa ao lado do contrato, no mesmo PR.** Guias, visão geral e glossário
   vivem no repositório, em Markdown, e passam pelo `markdownlint` e pelo
   `lychee` que já existem.
3. **Estático por inteiro.** HTML gerado no build, sem servidor de aplicação,
   sem dependência de CDN, sem runtime de terceiros.
4. **Nada de segredo e nada de escrita.** O portal não fala com o banco, não
   recebe token de serviço e não oferece operação de escrita.

## Mapa de URL

Português, sem barra final, com âncora estável por operação.

| URL | Conteúdo | Fonte |
| --- | --- | --- |
| `/` | Visão geral, estado da API, atalhos | `docs/publico/visao-geral.md` |
| `/guia-rapido` | Primeira chamada em 3 passos | `docs/publico/guia-rapido.md` |
| `/guias/autenticacao` | Cookie, link mágico, Google, sessão | `docs/publico/guias/autenticacao.md` |
| `/guias/pescarias` | Ciclo de vida da pescaria e compartilhamento | `docs/publico/guias/pescarias.md` |
| `/guias/locais` | Locais, pontos, trilhas e níveis de detalhe | `docs/publico/guias/locais.md` |
| `/guias/condicoes` | Condições do mar, maré e comparação | `docs/publico/guias/condicoes.md` |
| `/referencia` | Índice por domínio, gerado | `openapi.json` |
| `/referencia/sistema` | Health, ready, versões legais | `openapi.json` |
| `/referencia/catalogo` | Catálogo de espécies, iscas e técnicas | `openapi.json` |
| `/referencia/autenticacao` | 16 operações de sessão | `openapi.json` |
| `/referencia/conta` | Consentimento, senha, sessões, exportação, exclusão | `openapi.json` |
| `/referencia/pescarias` | CRUD, permissão, PDF e visão pública | `openapi.json` |
| `/referencia/diario` | Read model do diário | `openapi.json` |
| `/referencia/condicoes` | Condições e comparação | `openapi.json` |
| `/referencia/locais` | Reverso, busca e locais salvos | `openapi.json` |
| `/referencia/pontos` | Pontos privados | `openapi.json` |
| `/referencia/trilhas` | Trilhas privadas | `openapi.json` |
| `/referencia/midia` | Upload de foto | `openapi.json` |
| `/referencia/especies` | Sugestão de espécie por foto | `openapi.json` |
| `/referencia/newsletter` | Envio, preview e descadastro | `openapi.json` |
| `/referencia/erros` | Catálogo de `codigo` | `docs/publico/referencia/erros.md` |
| `/limites` | Rate limit, cache e CORS | `docs/publico/limites.md` |
| `/changelog` | Mudanças do contrato | gerado do histórico do spec |
| `/glossario` | Pescaria, local, ponto, trilha, troféu | `docs/publico/glossario.md` |
| `/privacidade` | O que é público, o que é do dono | `docs/publico/privacidade.md` |
| `/status` | Última versão publicada e data | `openapi.json` + build |

Operação individual: `/referencia/<dominio>#<operationId>`. Âncora por
`operationId`, não por posição, para link externo não quebrar quando a ordem
muda. O `operationId` é congelado no contrato; renomear rota exige atualizar o
mapa versionado.

## Três camadas de conteúdo

| Camada | Onde vive | Quem edita | Validação |
| --- | --- | --- | --- |
| Contrato | `docs/api/openapi.json` | build do projeto | `gerar-openapi.sh` + comparação no job `api` |
| Override por operação | `docs/publico/operacoes/<operationId>.md` | quem implementa | gerador exige todos os campos |
| Prosa de conceito | `docs/publico/**/*.md` | quem escreve | `markdownlint` + `lychee` |

O override é curto e obrigatório. Frontmatter:

```yaml
---
operacao: criarPescaria         # tem que existir no contrato
acesso: sessao                  # publico | sessao | dono | admin | token
consentimento: true
limites: escrita                # politica de rate limit
cache: no-store
verificar: "GET /api/pescarias/{shareToken} devolve a pescaria criada."
relacionados: [obterPescariaPorToken, atualizarPescaria]
---
```

Corpo: propósito em 1–3 frases, notas de comportamento e a tabela de erros.

Erros no corpo, em tabela obrigatória:

```markdown
| Status | codigo | Quando acontece |
| --- | --- | --- |
| 400 | `dados_invalidos` | ... |
```

## Pipeline de build

```text
openapi.json ──┐
               ├─► gerar-referencia.mjs ─► content collection ─► astro build ─► dist/ + pagefind
docs/publico ──┘            │
                            └─ falha o build se:
                               • operação do contrato sem override
                               • override sem operação no contrato
                               • status declarado no spec sem linha de erro
                               • erro documentado que o spec não declara
                               • schema de request sem exemplo
```

Regras do gerador:

- Lê o spec e **não** o reescreve. Saída vai para `docs-site/src/content/gerado/`,
  que é ignorado pelo git.
- Nome de arquivo e âncora vêm do mapa de `operationId` versionado.
- Ordena domínios e operações por uma lista canônica, não pela ordem do spec.
- Falha com o nome da operação e o campo faltando; nunca gera página incompleta.

Checagens extras no job: `astro check`, `lychee` no `dist/` (link interno
quebrado é erro de build), e um teste que abre cada página gerada e confirma
`h1`, título, âncora e presença dos blocos obrigatórios.

## Console "Experimente"

- **v1: só `GET` público, sem credencial.** `fetch` com
  `credentials: 'omit'`, método e URL travados pelo spec. Nenhuma escrita.
- **Rotas de sessão e de dono:** mostra o `curl` com `--cookie`, mas não executa.
  O leitor roda na máquina dele.
- **Rotas administrativas:** documentadas e marcadas
  `Execução não disponível neste console.` O `404` proposital é explicado.
- **Pré-requisito de CORS:** o console precisa de
  `https://docs.fisga.abson.tech` em `Cors:AllowedOrigins` da API. Isso é
  mudança de configuração de produção e entra na F3, com o mesmo cuidado do
  CSRF: sem `AllowCredentials` no v1, um `GET` anônimo não carrega cookie.
- **Limite de uso:** o console respeita 1 chamada por clique, sem repetição
  automática, para não consumir o rate limit por IP do leitor.
- **Timeout e erro:** mostra status, headers relevantes e os primeiros 4 KB do
  corpo; erro de rede tem mensagem própria, nunca "algo deu errado".

Console autenticado (ler dados da própria conta pelo navegador) fica para a F4,
com opt-in explícito, aviso de que o cookie `fisga_auth` passa a ser enviado e
revisão de CORS com credencial.

## Insumo do OpenDesign

O portal foi prototipado no projeto **Fisga Docs Portal**
(`fisga-docs-portal-ef4c`) antes de qualquer código, como exige o processo do
estúdio. O artefato `index.html` cobre quatro telas — home, referência de
autenticação, guia rápido e uma moldura de 390px com busca sem resultado — e
serve só para validar decisões, não para virar o site.

O que o protótipo confirmou:

- Três colunas na referência funcionam com 1120px: navegação, conteúdo e
  "Nesta página" cabem sem coluna apertada.
- O console desabilitado precisa de aviso próprio, e não de botão cinza: o
  texto "Console desabilitado para escrita" comunica mais que o `disabled`.
- A faixa "Leitura pública. Escrita exige sua conta." na home resolve a dúvida
  de permissão antes do primeiro clique.
- Busca precisa mostrar sugestão quando não acha ("Tente 'condições' ou
  'tábua'"), não uma página vazia.
- O guia rápido só funciona com três passos; quatro já exige rolagem no mobile.

O que o protótipo errou e não se repete: dados técnicos inventados (token no
corpo do login, `api.fisga.app`, `/api/versoes-legais`, `429` com corpo). É a
razão de a referência publicada ser gerada do contrato, e não escrita à mão.

## Hospedagem

- App estático no Coolify, build a partir do repositório, servindo `dist/`.
- `docs.fisga.abson.tech` com TLS do próprio Coolify (mesmo padrão de `api` e
  `fisga`).
- Headers:

| Header | Valor |
| --- | --- |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` |
| `Content-Security-Policy` | `default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; connect-src 'self'; frame-ancestors 'none'` |
| `X-Content-Type-Options` | `nosniff` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` |

- Cache: HTML com `max-age=60, stale-while-revalidate=600`; assets com hash,
  `immutable` por um ano; `sitemap.xml`, `robots.txt` e `/health.txt` na raiz.
- `/health.txt` contém versão do contrato e SHA do build — é o alvo do smoke.

**CSP e o console.** A CSP acima é a da v1, sem console: `connect-src 'self'`
fecha a possibilidade de o navegador chamar a API antes de existir código que
faça isso. A F3.1 libera `connect-src 'self' https://api.fisga.abson.tech` no
mesmo PR que implementa o console, junto com o CORS da API. Sem esse ajuste o
`fetch` do console falharia no navegador sem erro visível no servidor — por isso
a liberação é parte do critério de aceite da F3.1, não um passo separado.

## CI/CD

Duas mudanças no pipeline atual, sem tocar no comportamento da API.

### `fisga-pipeline.yml`

1. O job `changes` ganha o output `portal`, alimentado por
   `Projetos/fisga/docs-site/**`, `Projetos/fisga/docs/publico/**`,
   `Projetos/fisga/docs/api/**` e os dois workflows. `docs-site/` também sai de
   `CODIGO` — hoje `^Projetos/fisga/` sem `docs/` casaria com ele e dispararia
   `dotnet build` à toa.
2. Novo job `docs-site` (`needs: changes`, roda em PR quando `portal == true` e
   em push na `main` quando `portal == true`): `npm ci`, geração da referência,
   `astro check`, `astro build`, `lychee` no `dist/`.
3. `gate` passa a depender de `docs-site`. Job que falha bloqueia o merge.

### `fisga-deploy.yml`

1. `changes` ganha o mesmo output `portal`.
2. Novo job `deploy-docs`: valida `COOLIFY_WEBHOOK_FISGA_DOCS`, dispara o
   deploy, acompanha o `deployment_uuid` e faz smoke em
   `https://docs.fisga.abson.tech/health.txt` conferindo o SHA do build.
3. **Ordem obrigatória: API primeiro, portal depois.** `deploy-docs` depende de
   `deploy-api` e só roda se ele terminou em sucesso. Sem isso, um PR que muda
   contrato e portal junto publicaria a documentação de um contrato que ainda
   não está no ar — o leitor veria operação documentada e `404` na API. Quando a
   mudança é só de prosa, `deploy-api` fica `skipped` por falta de mudança de
   código; o job de docs usa `if: always()` e falha apenas se `deploy-api`
   terminou em `failure` ou `cancelled`. Custo: alguns minutos a mais em PR que
   mexe em contrato e portal ao mesmo tempo — barato perto de documentação
   mentindo por minutos.

Novo secret: `COOLIFY_WEBHOOK_FISGA_DOCS`. O `COOLIFY_TOKEN` também precisa de
`read` + `deploy` no app `fisga-docs`, porque o job acompanha o
`deployment_uuid` pela API — ou um token dedicado, se preferir não ampliar o
existente. O escopo novo entra em `docs/ci-cd.md` junto com o webhook.

## Versionamento e changelog

- O rodapé de cada página mostra a versão do contrato (`info.version`) e o SHA
  curto do build.
- `/changelog` é gerado comparando o `openapi.json` do commit atual com o do
  commit anterior na `main`: operação adicionada, removida, campo novo, status
  novo. Entrada manual só para explicar impacto.
- Mudança que quebra integração ganha aviso no topo da página de referência
  afetada até a janela de convivência terminar. Política proposta: 90 dias de
  aviso e `/v2` coexistindo — a aprovar na F2.

## Busca, desempenho e acessibilidade

- Pagefind no pós-build: índice estático, busca client-side, sem serviço.
- Orçamento: JS total ≤ 60 KB comprimido por página; nenhuma fonte de terceiro
  (Fraunces e Inter servidas pelo próprio domínio, subset latin).
- Acessibilidade AA verificada no job: contraste dos tokens, foco visível,
  landmarks, `skip link`, um `h1`, tabela com cabeçalho, bloco de código com
  rótulo e cópia acessível por teclado.

## Observabilidade

- Sentry próprio do portal (projeto `fisga-docs`), só para erro de JS do
  console e falha de build não capturada no CI. Sem `replay` e sem PII.
- Sem analytics de terceiro no v1. Contagem de acesso, se necessária, usa o
  proxy do próprio domínio e é decidida na F4.

## Definition of done do portal

- [ ] Toda operação do contrato tem página, com os 11 campos do template.
- [ ] `docs-site` verde em PR e em `main`, e `gate` dependendo dele.
- [ ] Deploy automático com smoke de `/health.txt` conferindo o SHA.
- [ ] Nenhum link quebrado no `dist/` e nenhuma página órfã.
- [ ] Guia rápido executado do zero por alguém de fora do time.
- [ ] `docs/ci-cd.md` e `AGENTS.md` do Fisga atualizados com o novo job.
