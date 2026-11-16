# Cardápio no Lucro

Calculadora de **ficha técnica e precificação por canal** para restaurantes, lanchonetes, food
trucks, marmitarias e doces por encomenda.

O dono lança ingredientes com rendimento, embalagem, tempo, custos fixos, CMV alvo e as taxas de
cada canal. O produto mostra o **custo real por porção**, o **CMV realizado**, o **preço mínimo** e o
**preço sugerido no salão, balcão, delivery próprio, iFood e 99Food** — com PDF de ficha técnica.

> **Estado atual: 100% gratuito.** Produto em validação; sem checkout, assinatura ou cartão.
> Monetização futura prevista: R$ 29–49/mês por estabelecimento.

## O que já está implementado

- Ficha técnica com rendimento, perda, embalagem, mão de obra e custo fixo rateado.
- Canais configuráveis: comissão percentual + taxa fixa por pedido.
- Cálculo de preço por CMV alvo, preço mínimo por canal e preço sugerido.
- CMV real, margem de contribuição e lucro operacional por canal.
- Alerta quando o CMV alvo não cobre o custo total e o preço é elevado ao mínimo.
- PDF sem marca d'água com ficha de produção, ingredientes e tabela por canal.
- Link público com token + edição com token salvo no navegador.
- **Meu cardápio** local, listando as fichas salvas neste navegador.
- Modo gratuito e cinco canais com exemplos prontos.

## Stack

| Camada | Tecnologia |
| --- | --- |
| Domínio/aplicação | .NET 10 / C# 13 |
| API | ASP.NET Core 10, Minimal APIs, OpenAPI |
| Persistência | EF Core 10 + PostgreSQL 17 (SQLite no desenvolvimento) |
| PDF | QuestPDF + QRCoder |
| Web | Astro 7 SSR + TypeScript + CSS próprio |
| Infra local | Docker Compose |

## A conta

```
CustoIngredientesLote = Σ (quantidade × preçoUnitário / rendimento)
CustoIngredientesPorcao = CustoIngredientesLote / rendimentoEmPorcoes
CustoEmbalagemPorcao = Σ custos de embalagem por porção
CustoMaoDeObraPorcao = (minutos / 60 × valorHora) / rendimentoEmPorcoes
CustoFixoPorcao = custosFixosMensais / porcoesVendidasPorMes

CustoTotalPorcao = ingredientes + embalagem + mão de obra + custo fixo

PrecoPorCmv = CustoIngredientesPorcao / CMV alvo
PrecoMinimo = (CustoTotalPorcao + taxaFixaDoCanal) / (1 − imposto − comissãoDoCanal)
PrecoSugerido = maior entre PrecoPorCmv e PrecoMinimo, arredondado para cima

CMV real = CustoIngredientesPorcao / PrecoSugerido
MargemContribuicao = PrecoSugerido − impostos − comissão − taxaFixa
                      − ingredientes − embalagem
LucroOperacional = MargemContribuicao − mão de obra − custo fixo
```

Decisões deliberadas:

- **CMV** usa apenas o custo dos ingredientes, que é a definição usada na operação de food service.
  Embalagem entra como custo variável separado e aparece na margem.
- **Preço mínimo** inclui todos os custos, imposto e taxa do canal. Se o preço pelo CMV ficar abaixo
  dele, o preço sugerido é elevado ao mínimo e a ficha mostra um alerta.
- Dinheiro é `long` em **centavos**; percentuais são `int` em **pontos-base**.
- Arredondamento sempre para cima (centavo, R$ 0,50, R$ 1 ou R$ 5), para não perder margem.
- O rateio de custo fixo usa o volume total mensal do cardápio, não só a venda daquele prato.

## Rodando localmente

Pré-requisitos: **.NET 10 SDK** e **Node 24+**.

### API (SQLite, sem banco externo)

```bash
cd Projetos/cardapio-no-lucro/src/CardapioNoLucro.Api
dotnet run
```

API em `http://localhost:5182`. O banco SQLite é criado no primeiro start.

### Web

Em outro terminal:

```bash
cd Projetos/cardapio-no-lucro/web
npm install
npm run dev
```

Abra **`http://localhost:4322`**.

Fluxo de teste:

1. Abra `/calculadora` e veja o exemplo “Frango grelhado com arroz e legumes”.
2. Mude o CMV alvo e veja o preço e o CMV real mudarem.
3. Compare salão, delivery, iFood e 99Food na tabela do resultado.
4. Salve, baixe o PDF e abra `/meu-cardapio`.
5. Edite a ficha pelo botão que aparece no navegador em que ela foi criada.

### Docker Compose

```bash
cd Projetos/cardapio-no-lucro
docker compose up --build
```

- Web: `http://localhost:3001`
- API: `http://localhost:5182`
- Postgres: `localhost:5434` (`cardapio` / `cardapio` / `cardapio`)

## Testes

```bash
cd Projetos/cardapio-no-lucro
dotnet test
cd web && npm run check
```

Cobrem fórmula por canal, CMV, rendimento, custo fixo, arredondamento, validações, tokens e casos de
uso de criação/edição. Atualmente são **20 testes** de backend + checagem de tipos do Astro.

## API

| Método | Rota | Descrição |
| --- | --- | --- |
| `GET` | `/api/exemplo` | ficha de exemplo |
| `GET` | `/api/canais` | canais e taxas padrão |
| `POST` | `/api/fichas/preview` | calcula sem salvar |
| `POST` | `/api/fichas` | salva e devolve tokens |
| `GET` | `/api/fichas/{shareToken}` | visão pública |
| `PUT` | `/api/fichas/{id}` | edita com `X-Edit-Token` |
| `GET` | `/api/fichas/{shareToken}/pdf` | PDF da ficha |
| `GET` | `/health`, `/ready` | saúde/readiness |

## DDD

Contextos:

| Contexto | Tipo | Responsabilidade |
| --- | --- | --- |
| **Fichas Técnicas** | Core | `FichaTecnica`, ingredientes, canais, cálculo de CMV/preço |
| **Catálogo** | Supporting | canais padrão e ficha de exemplo |
| **Documentos** | Supporting | PDF e QR de compartilhamento |
| **Cardápio local** | Supporting | lista das fichas salvas no navegador (MVP) |
| **Assinatura** | Generic | futuro; não implementado no período gratuito |

Mapa, linguagem ubíqua, invariantes e decisões estão em [`docs/ddd.md`](docs/ddd.md).

## Limites do MVP e próximos passos

- Uma ficha por cálculo; o catálogo compartilhado de ingredientes e o recálculo do cardápio inteiro
  ficam para a próxima fase.
- “Meu cardápio” é local ao navegador; um workspace multiusuário exige conta/login.
- Taxas de iFood/99Food são editáveis; o produto não sincroniza contratos automaticamente.
- Sem cobrança no período gratuito. O contexto de assinatura será adicionado quando a validação
  justificar.
- Próximos passos: catálogo de insumos com atualização de preço em lote, comparação de pratos,
  importação de notas, login, plano Pro e domínio próprio.

## Privacidade

Fichas são acessíveis por link com token. O token de edição fica no `localStorage` do navegador e o
banco guarda apenas o hash. O “Meu cardápio” lê somente o armazenamento local; não há analytics de
terceiros.

## Licença

Uso privado do portfólio. Defina a licença comercial antes de publicar como produto independente.
