# Política editorial da documentação da API

Regras para escrever e revisar as páginas do portal
`docs.fisga.abson.tech` (F2). Vale para guias,
referência e changelog. O objetivo é que quem integra consiga fazer a primeira
chamada sem abrir o código-fonte e sem perguntar nada a ninguém.

## Três regras que vêm antes das outras

1. **O contrato manda.** Se a página e o [`openapi.json`](openapi.json)
   divergem, a página está errada. Prosa não inventa rota, campo, status ou
   código de erro.
2. **O exemplo precisa funcionar.** Todo bloco de código é copiável e executável
   na ordem em que aparece. Exemplo que não roda é bug de documentação.
3. **Diga o que falha.** Documentação que só mostra o caminho feliz empurra o
   erro para o suporte. Cada operação descreve os erros que ela realmente
   devolve.

## Voz e linguagem

- Português do Brasil, tratamento por **você**, presente do indicativo.
- Frases curtas. Uma ideia por parágrafo. Sem "simplesmente", "basta", "é só".
- Nada de hype, exclamação ou emoji. Sem "poderoso", "incrível", "revolucionário".
- Termos técnicos em inglês quando são o nome real da coisa (`endpoint`,
  `payload`, `cookie`, `header`), com glosa na primeira aparição.
- Título em *sentence case*: "Fazer a primeira chamada", não "Faça Sua Primeira Chamada".
- Nome de produto e marca sempre "Fisga". Rota sempre em `code`.

## Estrutura obrigatória de uma página de operação

Gerada a partir do contrato, com a prosa abaixo em cada uma. Nenhum campo é
opcional.

| # | Campo | Conteúdo |
| --- | --- | --- |
| 1 | Título e verbo | `POST /api/pescarias` e uma frase sobre o efeito |
| 2 | Propósito | 1–3 frases: o que muda no mundo depois da chamada |
| 3 | Acesso | público, sessão, dono ou admin; consentimento; como autentica |
| 4 | Limites | rate limit aplicado e cache esperado |
| 5 | Request | path, query, headers e body com tipos, obrigatoriedade e exemplo |
| 6 | Sucesso | status real, schema e exemplo de resposta completo |
| 7 | Erros | tabela de status, `codigo` e causa provável |
| 8 | Exemplo executável | `curl` que roda com o bloco anterior |
| 9 | Como verificar | o que olhar para saber que funcionou |
| 10 | Efeitos colaterais | cookies emitidos, sessões revogadas, arquivos gerados |
| 11 | Relacionados | operações que fazem sentido em sequência |

## Templates por tipo de página

### Visão geral (`/`)

Um parágrafo do que a API é, dois caminhos ("Fazer a primeira chamada" e
"Explorar a referência"), os três domínios mais usados, aviso de privacidade e
o estado da API (versão do contrato, último deploy). Sem lista de features.

### Guia rápido (`/guia-rapido`)

Três a cinco passos, cada um com: objetivo em uma frase, um bloco de código e
"como verificar". O leitor termina com uma chamada autenticada funcionando.

### Guia de conceito (`/guias/<tema>`)

Explica um modelo do domínio (pescaria, local, ponto, trilha, condições) e como
as operações se encaixam nele. Sempre com um diagrama de estados ou de relação
quando houver ciclo de vida.

### Referência (`/referencia/<dominio>/<operacao>`)

O template da seção anterior. Uma operação por URL, `operationId` estável como
âncora, para que links externos não quebrem.

### Erros (`/referencia/erros`)

Catálogo de `codigo` → status → causa → o que fazer. É a página mais consultada
em produção; precisa ser a mais precisa do portal.

### Changelog (`/changelog`)

Uma entrada por mudança de contrato, gerada do histórico do `openapi.json` no
merge. Formato: data, tipo (aditivo, alterado, removido, corrigido), operação,
o que muda para quem integra e o que fazer.

## Como escrever exemplos

- `curl` como formato canônico, com `-i` quando o header importa e `-s` quando
  a legibilidade importa. Um `--cookie cookies.txt` só depois de um passo que
  grava o cookie.
- Exemplo de resposta **completo** para o caso principal; truncado com
  `"..."` só quando a lista é genuinamente longa, e o texto avisa.
- Código em blocos com a linguagem declarada (`bash`, `json`, `http`).
- Números e datas do exemplo são plausíveis, não aleatórios:
  `-23.55, -46.63` é São Paulo; `2026-11-20` é data de exemplo futura à escrita.
- E-mail de exemplo sempre `@example.com`.

### Dados proibidos em exemplo

| Nunca | Use |
| --- | --- |
| Token, cookie ou `shareToken` real | `SEU_TOKEN_AQUI`, `COOKIE_AQUI` |
| DSN Sentry, chave Google, `X-Admin-Token` | `SEU_ADMIN_TOKEN` |
| Nome, e-mail ou telefone de pessoa real | `maria@example.com` |
| Coordenada de ponto privado de usuário | coordenada pública de cidade |
| `shareToken` de pescaria real | token inventado de formato igual |

O CI precisa barrar segredo em `docs/api/**` (F2). Até lá, revisão humana é
obrigatória.

## Erros: o que sempre dizer

O corpo de erro é `application/json` nos handlers e `application/problem+json`
nos middlewares — exceto o `503 google_indisponivel` do Google, que os handlers
já escrevem em `application/problem+json`; os dois carregam o mesmo
`{codigo, detalhe}`:

```json
{ "codigo": "pescaria_nao_encontrada", "detalhe": "Pescaria não encontrada." }
```

Toda tabela de erro informa status, `codigo` e o que fazer. Casos que sempre
precisam de parágrafo próprio, porque surpreendem:

- **`429` sem corpo, com `Retry-After`.** O rate limit responde vazio; o header
  informa os segundos até a próxima janela; o texto diz o limite e a janela.
- **`403 consentimento_pendente`.** Explica que o usuário precisa aceitar os
  termos e qual endpoint faz isso.
- **`403 origem_invalida`.** Explica a checagem de `Origin` em mutações.
- **`404` nas rotas administrativas.** Explica que é proposital: sem token
  válido a rota não se revela.
- **`200` com `autenticado:false`** em `/api/auth/me`: ausência de sessão não é
  `401`.
- **`302`** nos fluxos de link mágico, Google e descadastro: o que acontece com
  o cookie e para onde o navegador vai.

## Privacidade

- Documentar quais operações expõem dados privados e por qual capacidade
  (`shareToken`, sessão do dono).
- Nunca descrever o algoritmo de token assinado nem o segredo que o valida.
- Ao falar do mapa privado e dos níveis de detalhe do local, repetir a regra da
  [ADR 0015](../adr/0015-niveis-de-detalhe-do-local.md) em linguagem de
  integrador: coordenada exata é do dono, o link público mostra o nível que ele
  escolheu.

## Acessibilidade e SEO

- Um `h1` por página; hierarquia sem pular nível.
- Tabela só para dado tabular; navegação e passo a passo em lista.
- Contraste AA, foco visível, alvo de toque de 44px.
- Toda página tem `title`, `description` e URL estável; âncoras de operação não
  mudam depois de publicadas.
- Bloco de código tem rótulo visível e botão copiar com retorno acessível.

## Checklist de revisão (todo PR de documentação)

- [ ] Rota, status, campo e `codigo` conferem com o `openapi.json` e com o código.
- [ ] Acesso, rate limit e cache estão declarados.
- [ ] O exemplo roda na ordem apresentada e não contém dado proibido.
- [ ] A tabela de erros cobre o que o handler devolve de verdade.
- [ ] Links internos apontam para páginas existentes (`lychee` no CI).
- [ ] Português do Brasil, sem hype, sem exclamação.
- [ ] `docs/api/inventario.md` atualizado se a linha mudou.

## Definição de pronto para mudança de contrato

Uma mudança de endpoint só é considerada pronta quando, no mesmo PR:

1. o contrato gerado muda e é commitado;
2. a linha do inventário muda;
3. a página de referência afetada muda;
4. o changelog ganha entrada quando a mudança é visível para quem integra.
