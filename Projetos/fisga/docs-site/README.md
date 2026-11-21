# Portal da API do Fisga

Site estático em Astro que publica a documentação da API do Fisga em
`https://docs.fisga.abson.tech`. A home é **provisória** nesta fase (F2.1): o
template editorial completo, com os domínios mais usados e o estado da API,
entra na F2.2.

As páginas de referência nascem de `docs/api/openapi.json` mais um arquivo
Markdown por operação, em `docs/publico/operacoes/`. Nenhum dado de rota,
status, `codigo` ou schema é escrito à mão aqui.

## Comandos

Na raiz deste diretório:

```bash
npm ci               # instala a partir do package-lock.json (o que o CI usa)
npm run dev          # servidor de desenvolvimento em http://localhost:4321
npm run build        # gera dist/ com o site estático
npm run test:unit    # testes do gerador e do contrato real
npm run test:paginas # verificação pós-build: rode depois de npm run build
```

Nesta fase (esqueleto) a home é a única página e não há overrides ainda; os
comandos `npm run gerar`, `test:unit` e `test:paginas` passam a valer quando o
gerador e os testes entrarem, nas próximas etapas da F2.1.

Os scripts `predev`, `prebuild`, `precheck` e `pretest:unit` rodam
`npm run preparar`, que copia `design-system/fisga/tokens.css` para
`src/styles/gerado/tokens.css` (gitignorado). Editar um token no design system
reflete no site no próximo comando, sem cópia manual.

`npm run check` roda o `astro check` (`.astro` e `.ts`); a correção dos scripts
`.mjs` é responsabilidade do `node --test`, porque ligar `checkJs` num gerador
JS puro transformaria todo `any` implícito em erro.

## Como adicionar uma operação

1. Escreva o override em `docs/publico/operacoes/<operationId>.md`. O nome do
   arquivo é o `operationId` do contrato; qualquer outro nome falha no build.
2. Rode `npm run gerar` para validar e gerar `src/data/referencia.json`.
3. Rode `npm run build` e `npm run test:paginas`.

O override declara só o que o contrato cala: `proposta`, `acesso`,
`verificar`, e conforme a operação `caminho`, `consulta`, `corpo`, `arquivos`,
`relacionados` e `erros`. Título, método, rota, `security`, limites, cache,
parâmetros e schemas vêm do contrato.

## Estrutura

| Caminho | O quê |
| --- | --- |
| `src/pages/index.astro` | Home (provisória na F2.1) |
| `src/components/Base.astro` | Documento base: skip link, header, `main`, footer |
| `src/styles/portal.css` | Estilos do portal, só sobre os tokens |
| `src/styles/fontes.css` | Os dois `@font-face` self-hosted (Fraunces e Inter) |
| `scripts/copiar-tokens.mjs` | Copia o `tokens.css` canônico para o site |
