# Referência — documentação de API

**Estudado em:** 20/11/2026 · **Fontes:** `https://docs.stripe.com/api/errors`,
`https://docs.github.com/en/rest/using-the-rest-api/troubleshooting-the-rest-api`,
`https://developers.cloudflare.com/api/`, `https://docs.asaas.com/`
(índice e páginas de autenticação/sandbox).

## Por que estudar isto

O Fisga vai publicar 54 operações em `docs.fisga.abson.tech`
([ADR 0017](../adr/0017-portal-publico-da-api.md)). Antes de desenhar o portal,
vale ver o que as referências que integram de verdade fazem — e o que elas fazem
só porque têm escala que o Fisga não tem.

## O que cada uma faz

| Fonte | Modelo | O que entrega | Sinal de qualidade |
| --- | --- | --- | --- |
| **Stripe** | Referência por recurso, três colunas, exemplos em 8 linguagens | Página de erros com tabela de status, tipos de erro, catálogo de `code` e link `doc_url` por erro | O erro é documentado como contrato, não como exceção |
| **GitHub REST** | Referência por endpoint + guias de uso | Página inteira de "troubleshooting": rate limit, `404` proposital, JSON inválido, versão, `User-Agent` | Assume que o leitor vai errar e ensina a diagnosticar |
| **Cloudflare** | 100% gerada de um OpenAPI, uma página por método | Chips de SDK por linguagem, bloco "Security" e "Accepted Permissions" em cada operação, marca de `Deprecated` | A referência é a mesma fonte que gera os SDKs |
| **Asaas** | ReadMe-style, guias + referência, pt-BR | Tabela sandbox × produção, header `access_token` explicado, exemplo real de corpo de erro | Integrador brasileiro entende o ambiente antes do endpoint |

## O que funciona (princípio)

1. **Erro é conteúdo de primeira classe.** Stripe tem uma página só para erros e
   cada `code` tem link próprio. GitHub tem uma página só para diagnosticar.
   Nenhuma das duas esconde o `429` no rodapé.
2. **O comportamento inesperado é explicado, não omitido.** GitHub documenta que
   recurso privado sem autenticação responde `404`, não `403` — exatamente a
   decisão que o Fisga tomou nas rotas administrativas de newsletter.
3. **Segurança por operação, não por página de introdução.** Cloudflare lista os
   métodos de autenticação aceitos e as permissões exigidas em cada endpoint.
   Quem chega por busca entende o requisito sem voltar ao começo.
4. **Ambiente explícito.** Asaas abre a documentação com a tabela sandbox ×
   produção. O leitor não descobre a URL base errada no primeiro `404`.
5. **Recuperação, não só falha.** GitHub diz o que fazer com `Retry-After`,
   com `x-ratelimit-reset` e quando parar de tentar. O Fisga responde `429`
   sem corpo, com `Retry-After` em segundos (F1.8).
6. **Depreciação visível onde a operação aparece.** Cloudflare marca
   `Deprecated` no item de navegação e na página. Não existe rota velha
   silenciosa.
7. **Referência e SDK saem do mesmo documento.** Cloudflare e Stripe mostram a
   versão da biblioteca ao lado da operação. É o que o gerador do Fisga permite
   fazer sem esforço extra (F1.4 + F2.1).
8. **Exemplo executável, na língua do leitor.** Asaas usa `curl` com o header
   real; Stripe deixa trocar de linguagem sem sair da página.

## O que não copiar

- **Excesso de linguagens.** Stripe mantém oito; o Fisga precisa de `curl` e,
  no máximo, `fetch` e `HttpClient`. Cada linguagem é manutenção permanente.
- **Navegação por milhares de operações.** Cloudflare agrupa por conta, zona e
  produto porque tem mais de mil endpoints. O Fisga tem 54: 13 domínios e uma
  página por domínio bastam.
- **Console de escrita público.** Nenhuma das quatro permite operação destrutiva
  sem credencial do próprio usuário; o Fisga vai além e não oferece escrita nem
  com credencial (decisão 2 da F0).
- **"Try it" que consome a cota de quem lê.** Nenhuma das quatro repete chamada
  automaticamente. O console do Fisga também não.
- **Documentação que só existe com JavaScript.** Stripe e GitHub servem HTML
  legível sem JS. É o motivo de o Fisga gerar página por operação em vez de
  embutir um renderizador de OpenAPI.

## Decisões que estas referências confirmam

| Observado | Decisão no Fisga | Onde está |
| --- | --- | --- |
| Página dedicada a erros e a diagnóstico | `/referencia/erros` obrigatória e parágrafo próprio para os casos que surpreendem | [Política editorial](../api/politica-editorial.md#erros-o-que-sempre-dizer) |
| `404` proposital documentado | Explicar o `404` das rotas administrativas como decisão, não como bug | [F1.9](../api/backlog.md#f19--comportamentos-que-surpreendem) |
| Segurança por operação | `securityScheme` por operação no contrato e bloco "Acesso" em cada página | [F1.1](../api/backlog.md#f11--esquemas-de-segurança) |
| Referência gerada do mesmo documento que gera cliente | Gerador `openapi.json` → páginas; sem renderizador de terceiros | [Arquitetura do portal](../api/portal.md) |
| Sandbox explícito | Adiado para a F4.1, com aviso de que a v1 usa produção e credencial própria | [F4.1](../api/backlog.md#f41--sandbox) |
| Rate limit com recuperação | `Retry-After` em segundos desde a F1.8; guia de erros explica a janela de 1 minuto | [F1.8](../api/backlog.md#f18--429-com-retry-after) |
| Depreciação visível | Janela de 90 dias e aviso na página afetada | [F4.3](../api/backlog.md#f43--política-de-versão-e-depreciação) |

## O que ainda falta pesquisar

- Como Stripe mantém exemplos em oito linguagens sem divergir do contrato
  (geração ou revisão humana?) — relevante só se a F4.4 aprovar SDK.
- Se o Cloudflare publica o OpenAPI de origem e como versiona o documento
  publicado — referência para a política da F4.3.
- Prática de documentação de API em pt-BR fora do setor de pagamentos
  (saúde, logística) para calibrar vocabulário.
