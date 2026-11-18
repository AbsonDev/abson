# Fisga — Design de Caching e Plano de Rollout

**Status:** rascunho para aprovação  
**Data:** 2026-11-18  
**Stack:** Astro 7.4.12 (SSR Node standalone) + ASP.NET Core 10 + PostgreSQL 17 + Traefik/Coolify  
**Escopo:** cache de runtime (browser, CDN, Astro, API, service worker). Não inclui cache de build/CI, que já está resolvido.

---

## 1. Decisão central

O Fisga vai usar **cache em camadas com separação explícita entre público e privado**:

> **CDN/browser para conteúdo público → Astro Server Islands para personalização → HybridCache no .NET para dados → `no-store` para tudo que é privado.**

O objetivo não é “cachear tudo”, e sim:

1. não chamar a origem quando o conteúdo é público e reutilizável;
2. não pagar o custo do Open-Meteo/Nominatim duas vezes pelo mesmo dado;
3. nunca colocar diário, locais, pontos, trilhas, fotos ou HTML autenticado em cache compartilhado;
4. manter o comportamento correto quando o cache falha ou expira.

**O que não será feito nesta fase:** Redis, CDN e cache de página inteira para usuário logado. Esses itens entram apenas quando houver mais de uma réplica ou tráfego que justifique.

---

## 2. Contexto e evidências

A análise de 18/11/2026 mediu a produção:

| Medição | Resultado |
|---|---|
| `GET /api/condicoes` com cache frio | 0,87 s a 3,08 s |
| `GET /api/condicoes` com cache quente | 0,14 s a 0,18 s |
| `GET /api/locais/buscar?q=Paraty` — 1ª | 1,09 s |
| `GET /api/locais/buscar?q=Paraty` — seguintes | 0,36 s (sem cache de servidor) |
| Geração de PDF (12 capturas, Mac local) | 119 ms na 1ª; 3,5–5,9 ms nas seguintes |
| `/_astro/*` | `Cache-Control: public, max-age=31536000, immutable` |
| API pública | sem `Cache-Control` e sem `ETag` |
| Wiki/ícones/OG | `public, max-age=0` + ETag (revalida sempre) |
| Compressão | Traefik entrega br, gzip e zstd; HTTP/2 e HTTP/3 ativos |
| Borda | DNS direto para o VPS; sem CDN |
| Open-Meteo free | 10.000 chamadas/dia, 5.000/h, 600/min |
| Nominatim | máximo 1 req/s e cache obrigatório |

O cache de condições em memória já é o maior acerto: 20 minutos por latitude/longitude arredondadas em 2 casas (~1,1 km). O problema é que ele é um `ConcurrentDictionary` sem single-flight, sem `stale-while-revalidate` e que cacheia falha parcial do mar por 20 minutos.

---

## 3. Objetivos e não-objetivos

### Objetivos

- Eliminar o cold tail de 0,87–3 s quando o cache de condições expira.
- Reduzir chamadas ao Open-Meteo e ao Nominatim.
- Cachear a busca de geocoding, hoje sem cache.
- Padronizar headers HTTP e ETag.
- Corrigir o cache privado do service worker.
- Deixar o caminho pronto para CDN e múltiplas réplicas sem reescrever a aplicação.

### Não-objetivos

- Cachear HTML de `/hoje`, `/conta`, `/meu-diario`, `/mapa` ou qualquer página autenticada.
- Cachear diário, locais, pontos, trilhas, comparação ou fotos em camada compartilhada.
- Trocar o adapter do Astro ou migrar DNS para CDN nesta fase.
- Cachear PDF para economizar CPU (o custo medido é baixo).
- Cachear tiles de mapa no servidor (políticas de uso e complexidade).
- Cache de sessão/consentimento nesta fase.

---

## 4. Invariantes de segurança e privacidade

Estas regras valem para qualquer fase:

1. **Privado nunca entra em cache compartilhado.** Endpoint autenticado ou HTML personalizado responde `Cache-Control: private, no-store`.
2. **Cache público só com opt-in explícito.** O padrão da API é `no-store`; um endpoint só ganha `public`/`private, max-age` se declarar isso.
3. **CDN só depois dos headers privados.** Nenhuma regra de “cache everything” antes de `no-store` em `/api/locais`, `/api/diario`, `/api/pontos`, `/api/trilhas`, `/api/conta/*`, `/api/auth/*`.
4. **Service worker não cacheia HTML privado.** A decisão é pelo header da resposta, não só pelo path.
5. **Coordenadas não vão para cache compartilhado sem decisão explícita.** `/api/locais/reverso` e `/api/condicoes` usam `private` no browser; CDN só com bypass de cookie e revisão de privacidade.
6. **Fotos são privadas por padrão.** `/media/*` continua imutável no browser; não entra em CDN compartilhado sem URL assinada.
7. **Logout limpa o que é privado.** Cache Storage e chaves `fisga:*` do `localStorage` são apagados; o cache de mídia da API pode ser limpo com `Clear-Site-Data`.
8. **Entrada externa não vira chave crua.** Termos de busca são normalizados, limitados e versionados; coordenadas são arredondadas.

---

## 5. Arquitetura alvo

```text
Browser
  ├─ HTTP cache
  │    ├─ _astro/*, ícones, OG: public, immutable
  │    ├─ /wiki/*: public, max-age + SWR
  │    └─ API pública: private, max-age curto
  ├─ Service Worker
  │    ├─ cache público: wiki, ferramentas, Pagefind, offline, tiles
  │    └─ sem HTML privado; limpa no logout
  └─ CDN (fase futura)
       ├─ assets imutáveis
       ├─ HTML público (/wiki, shell anônimo de /condicoes)
       └─ API pública selecionada
            │
            └─ Astro SSR (Node)
                 ├─ route cache (memoryCache) para páginas públicas sem cookie
                 ├─ Server Islands para blocos personalizados
                 └─ cliente da API
                      │
                      └─ API ASP.NET Core 10
                           ├─ HybridCache L1 (memória)
                           ├─ HybridCache L2 (Redis, fase futura)
                           ├─ OutputCache para endpoints públicos estáveis
                           └─ PostgreSQL / Open-Meteo / MET Norway / Nominatim
```

---

## 6. Camada 1 — Browser, HTTP e CDN

### 6.1 Assets e conteúdo estático

| Recurso | Header | Observação |
|---|---|---|
| `/_astro/*` | `public, max-age=31536000, immutable` | Já correto; nomes com hash. |
| `/media/*` | `public, max-age=31536000, immutable` | Browser only; não compartilhar em CDN sem URL assinada. |
| `/favicon.svg`, ícones, OG | `public, max-age=86400` ou nome versionado | Hoje estão `max-age=0`; ganho pequeno, mas fácil. |
| `/wiki/*` | `public, max-age=3600, stale-while-revalidate=86400` | Conteúdo muda só no deploy; ETag já existe. |
| `/pagefind/*` | `public, max-age=86400` | Índice gerado no build; versionar no deploy. |
| `/sw.js` | `no-cache` | Precisa atualizar; hoje `max-age=0` já funciona. |
| `/manifest.webmanifest` | `public, max-age=86400` | Ícones versionados. |

### 6.2 HTML

| Rota | Header | Estratégia |
|---|---|---|
| `/hoje`, `/conta`, `/meu-diario`, `/mapa`, `/registrar`, `/trofeu`, `/aceite`, `/boas-vindas` | `private, no-store` | Nunca cachear em CDN/SW. |
| `/` anônimo | `public, max-age=60, s-maxage=300, stale-while-revalidate=600` | Shell público; login é client-side. |
| `/` autenticado | `private, no-store` | Redireciona para `/hoje`. |
| `/condicoes` anônimo | `public, max-age=60, s-maxage=300, stale-while-revalidate=600` | Dados públicos por coordenada; shell cacheável. |
| `/condicoes` autenticado | `private, no-store` | Contém locais salvos e comparação. |
| `/p/[token]` | `private, max-age=30` ou ETag | `podeEditar` varia por visitante; não cachear publicamente sem separar esse campo. |

**Fase 1:** apenas headers e `private/no-store`; `/condicoes` continua SSR sem CDN.  
**Fase 2:** separar o shell público de `/condicoes` dos blocos privados (Server Islands) e só então habilitar cache de borda.

### 6.3 API

| Endpoint | Header | Cache de servidor |
|---|---|---|
| `GET /api/condicoes` | `private, max-age=300, stale-while-revalidate=600` | HybridCache 20 min |
| `GET /api/locais/buscar` | `private, max-age=3600` | HybridCache 24 h + negativo 10 min |
| `GET /api/locais/reverso` | `private, max-age=86400` | HybridCache 12 h + throttle 1 req/s |
| `GET /api/catalogo` | `public, max-age=86400` + ETag | OutputCache 1 dia |
| `GET /api/legal/versoes` | `public, max-age=86400` + ETag | OutputCache 1 dia |
| `GET /api/auth/me` | `private, no-store` | — |
| `GET /api/locais`, `/api/diario`, `/api/pontos`, `/api/trilhas`, `/api/conta/*` | `private, no-store` | — |
| `GET /api/pescarias/{token}` | `private, max-age=30` + ETag | — |
| `GET /api/pescarias/{token}/pdf` | `private, max-age=300` + ETag | LRU opcional de bytes |
| `POST/PUT/DELETE` | `no-store` | — |
| `/health`, `/ready` | `no-store` | — |

**Regra de implementação:** o default da API é `no-store`. Endpoints públicos precisam de um marcador explícito (por exemplo, `IPublicCacheable` ou um endpoint filter `CachePublico`) para ganhar `public`/`max-age`. Isso evita esquecer um endpoint privado.

**ETag:**
- PDF e share: `ETag` derivado de `shareToken + AtualizadoEm.Ticks`; devolver `304` em `If-None-Match`.
- Catálogo/legal: ETag de versão/conteúdo; `304` quando aplicável.
- Condições: **não** usar ETag; a resposta muda com o relógio (`faltamHoras`, janela, agora). TTL curto é melhor.

**`Vary`:**
- `Accept-Encoding` para compressão.
- `Origin` para CORS.
- Evitar `Vary: Cookie` como estratégia principal; ele praticamente zera o cache. Para páginas personalizadas, usar `private/no-store` ou separar o conteúdo público.

### 6.4 CDN (fase futura)

Só habilitar depois de:

1. todos os endpoints privados responderem `private, no-store`;
2. o SW ignorar respostas privadas;
3. regras de bypass de cookie para `/`, `/condicoes`, `/p/*` e API;
4. preservação do IP real (`X-Forwarded-For`/`CF-Connecting-IP`) para o rate limit;
5. `Vary: Origin` e `Vary: Accept-Encoding` respeitados;
6. purge por tag (`Cache-Tag`) documentado.

Com CDN, usar:

```http
Cache-Control: public, max-age=60
CDN-Cache-Control: public, s-maxage=300, stale-while-revalidate=600
Cache-Tag: condicoes, local:<hash>
```

O Cloudflare exige mudança de nameservers; é um projeto operacional separado. O ganho principal não é compressão (Traefik já entrega br/zstd/HTTP3), e sim cache de borda e WAF.

---

## 7. Camada 2 — Astro

### 7.1 Route cache

O Astro 7.4.12 tem route caching nativo:

```js
// astro.config.mjs
import { defineConfig, memoryCache } from 'astro/config';
import node from '@astrojs/node';

export default defineConfig({
  output: 'server',
  adapter: node({ mode: 'standalone' }),
  cache: { provider: memoryCache({ max: 1000 }) },
  routeRules: {
    '/p/[token]': { maxAge: 60, swr: 300 },
  },
});
```

Uso por página:

```astro
---
if (Astro.cache.enabled && !Astro.request.headers.get('cookie')) {
  Astro.cache.set({ maxAge: 60, swr: 300, tags: ['condicoes'] });
}
---
```

**Não aplicar `routeRules` em `/` ou `/condicoes` enquanto a página ramificar por cookie.** O provider de memória:

- cacheia por URL + query;
- não faz bypass porque a requisição tem `Cookie`;
- trata `Vary: Cookie` como não-cacheável.

O resultado de aplicar cegamente seria servir a página anônima para um usuário logado — ou, pior, cachear a página logada.

**Fase 1:** não habilitar route cache em páginas personalizadas.  
**Fase 2:** usar route cache em `/p/[token]` (público) e, depois da refatoração com Server Islands, no shell de `/condicoes`.

### 7.2 Server Islands

Para `/condicoes`, o padrão alvo é:

- **shell público cacheável**: condições, maré, vento, lua, janela e 7 dias;
- **ilhas privadas** (`server:defer`): “Seus locais”, comparação com o histórico e qualquer bloco que dependa de sessão;
- o shell pode ser cacheado no CDN mesmo para usuários logados, porque não contém dado privado;
- as ilhas respondem `private, no-store` e são buscadas depois.

Cuidados:

- props de ilha são criptografadas; em CDN/rolling deploy, definir `ASTRO_KEY` no build;
- ilhas usam GET; POST não é cacheado;
- manter props pequenas (limite prático de URL de 2 KB);
- a ilha não vê `Astro.url` da página; usar `Referer` ou props explícitas.

**Fase 1:** não refatorar. O SSR atual com API cacheada já resolve o custo principal.  
**Fase 2:** refatorar `/condicoes` e habilitar cache de borda.

---

## 8. Camada 3 — .NET 10

### 8.1 HybridCache para dados

Adicionar o pacote `Microsoft.Extensions.Caching.Hybrid` (linha 10.x) e registrar:

```csharp
builder.Services.AddHybridCache(options =>
{
    // Sem L2 na fase 1, o cache local precisa durar o mesmo que a entrada.
    options.MaximumPayloadBytes = 256 * 1024;
    options.MaximumKeyLength = 200;
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(20),
        LocalCacheExpiration = TimeSpan.FromMinutes(20),
    };
});
```

**Chaves versionadas e normalizadas:**

| Dado | Chave | TTL |
|---|---|---|
| Condições | `condicoes:v1:{lat:F2}:{lon:F2}:{dia:yyyy-MM-dd}` | 20 min (parcial: 2–5 min) |
| Lua | `lua:v1:{lat:F2}:{lon:F2}:{dia:yyyy-MM-dd}` | 12 h |
| Busca de local | `geocode:search:v1:{termo-normalizado}` | 24 h (negativo: 10 min) |
| Reverso | `geocode:reverse:v1:{lat:F3}:{lon:F3}` | 12 h (negativo: 10 min) |

**Condições:**
- usar `GetOrCreateAsync` com o TTL completo já na criação; o HybridCache garante single-flight por chave na instância;
- buscar clima e mar em paralelo; lua em cache separado;
- se o mar falhar, **não** cachear o resultado parcial por 20 min: reaplicar a política (2 min) com `SetAsync` sempre que o valor devolvido for parcial, inclusive quando quem executou a factory foi cancelado;
- se o clima falhar, cachear um sentinela de indisponibilidade por 30 s para não martelar o provedor;
- a reaplicação usa `CancellationToken.None` (contabilidade de cache) e calcula o tempo restante pela idade do cálculo; assim uma leitura não estende o TTL curto indefinidamente;
- incluir `diaLocal` na chave para não servir lua de ontem depois da meia-noite;
- remover o `CacheEmMemoriaDeCondicoes` e o dicionário estático do geocodificador.

**Busca de geocoding:**
- normalizar termo (trim, lower-case, colapsar espaços, limite de 80 caracteres sem cortar par substituto);
- não criar chave quando o termo normalizado tiver menos de 2 caracteres;
- cachear resultados vazios por 10 min, reaplicando a política pelo valor devolvido (inclusive com factory de outro chamador cancelada);
- limitar o tamanho por entrada (`MaximumPayloadBytes = 256 KB`) e a chave (`MaximumKeyLength = 200`); o número de entradas não tem teto próprio no HybridCache e fica limitado pela cardinalidade da chave (lat/lon com 2–3 casas, termo normalizado a 80) e pelo rate limit de leitura. Revisitar com L2/Redis, que traz política de eviction;
- não usar o termo cru sem normalização.

**Reverso:**
- manter 12 h, mas adicionar `SemaphoreSlim`/rate limiter para respeitar 1 req/s do Nominatim;
- cachear `null` por 10 min;
- não usar cache compartilhado/CDN para coordenadas.

**Multi-réplica:**
- HybridCache L1 é por instância; o L2 Redis entra quando houver mais de uma réplica;
- a coordenação de stampede do HybridCache **não** se estende entre instâncias; para esse caso, avaliar FusionCache com backplane ou um lock Redis;
- não adicionar Redis agora.

### 8.2 OutputCache

Usar para respostas públicas e estáveis:

```csharp
builder.Services.AddOutputCache(options =>
{
    options.SizeLimit = 32 * 1024 * 1024;
    options.AddPolicy("catalogo", p => p
        .Expire(TimeSpan.FromDays(1))
        .Tag("catalogo"));
});
```

- `/api/catalogo` e `/api/legal/versoes`: `CacheOutput("catalogo")`.
- `/api/condicoes`: **não** usar OutputCache na fase 1. O cache de dados já elimina o custo do Open-Meteo; cachear a resposta completa por URL com `nome` livre fragmenta o cache. Se for usado depois, variar só por `lat`, `lon` e `ambiente`, com limite de tamanho.
- OutputCache respeita o padrão de não cachear resposta autenticada nem `Set-Cookie`; não desligar essa proteção.
- Para múltiplas réplicas, usar `AddStackExchangeRedisOutputCache` (não `IDistributedCache` direto).

### 8.3 Sessão e consentimento

Hoje cada requisição autenticada faz 3–4 leituras (sessão, usuário, 1–2 consentimentos), e o `ContaMenu` chama `/api/auth/me` em toda página.

**Não cachear na fase 1.** Se o volume justificar, na fase 3:

- cache de 30–60 s por `sessao_id`/`usuario_id`;
- invalidação em logout, “sair de todos”, troca de senha e aceite;
- Redis se houver múltiplas réplicas;
- teste explícito de revogação: sessão revogada não pode continuar autenticada além do TTL.

---

## 9. Camada 4 — Service Worker

### 9.1 Problema atual

O `sw.js` tem:

```js
const VERSAO = 'fisga-v8';
const CASCA = [
  '/', '/entrar', '/meu-diario', '/registrar', '/condicoes', ...
];

const privada = ['/locais', '/conta', '/meu-diario', '/registrar', '/trofeu'];
```

Problemas:

- `/hoje` e `/mapa` não estão em `privada`, embora README/ADR 0011 digam que estão;
- `/condicoes` não está em `privada`; quando renderizada com sessão, contém “Seus locais” e comparação;
- `CASCA` inclui `/` e `/meu-diario`; `/` pode resolver para `/hoje` quando há sessão;
- não há limpeza de Cache Storage no logout;
- o cache same-origin cresce sem limite.

### 9.2 Desenho

Separar caches:

| Cache | Conteúdo | Limpeza |
|---|---|---|
| `fisga-public-vN` | wiki, ferramentas, Pagefind, offline, shell público aprovado por `podeCachear` | troca de versão; **sobrevive ao logout** |
| `fisga-tiles-v1` | até 600 tiles | LRU; **sobrevive ao logout** |
| `fisga-private-vN` | **não usar** para HTML; se existir, só dados efêmeros | logout/401 |

Regras:

1. **Precache apenas público:** remover `/`, `/meu-diario` e `/mapa` do `CASCA`; manter wiki, ferramentas, offline, manifest e ícones. O install busca cada URL sem credenciais e só grava se `podeCachear` aprovar (não `cache.addAll`).
2. **Navegação:** network-first; cachear somente se a resposta tiver `Cache-Control` público e não tiver `Set-Cookie`.
3. **Rotas privadas:** `['/hoje','/mapa','/conta','/meu-diario','/registrar','/trofeu','/aceite','/boas-vindas']` — sempre `fetch` direto, sem `cache.put`.
4. **`/condicoes`:** não decidir pelo path; ler o header. Anônimo (`public`) pode cachear; autenticado (`private`) não.
5. **Logout/expiração:** apagar `fisga-private-*` e caches legados `fisga-*` que não sejam `fisga-public-*`/`fisga-tiles-v1`, e remover chaves `fisga:*` do `localStorage`. **Preservar** `fisga-public-*` e `fisga-tiles-v1`: só contêm conteúdo aprovado por `podeCachear`, e mantê-los é o que faz `/offline`, wiki, ferramentas e Pagefind continuarem funcionando depois de sair. A limpeza é a mesma na sessão expirada (200 anônimo/401), via `encerrarSessaoExpirada`; quando a tela continua pública, como `/mapa` anônimo, basta `limparCachePrivado`.
6. **Tamanho:** limite de entradas/bytes por cache; remover os mais antigos.
7. **Versão:** bump explícito de `VERSAO` em cada deploy que mude conteúdo não hasheado; documentar no checklist de release.

### 9.3 Teste de privacidade

Adicionar um teste Playwright que:

1. faz login;
2. visita `/hoje` e `/condicoes`;
3. verifica que o Cache Storage **não** contém essas respostas;
4. faz logout;
5. verifica que `fisga-private-*` e `localStorage` privados foram limpos, e que `fisga-public-*`/`fisga-tiles-v1` continuam presentes;
6. recarrega offline e confirma que `/offline`, wiki e ferramentas continuam servindo do cache público.

---

## 10. Observabilidade

Adicionar métricas de baixa cardinalidade (Sentry/OpenTelemetry):

- `cache.conditions.hit`, `.miss`, `.stale`, `.partial`;
- `cache.geocode.hit`, `.miss`, `.negative`;
- `cache.output.hit`, `.miss`;
- `upstream.openmeteo.weather.latency`, `.errors`;
- `upstream.openmeteo.marine.latency`, `.errors`;
- `upstream.metno.moon.latency`, `.errors`;
- `upstream.nominatim.reverse.latency`, `.errors`;
- `http.304` por rota;
- chamadas Open-Meteo/dia vs. limite de 10.000.

Não enviar coordenadas, termos de busca, e-mail, cookie ou token para o Sentry. O scrubber existente deve continuar cobrindo query string, corpo e URL, com `q`, `busca` e `termo` na lista de sensíveis. Para reduzir o risco de um parâmetro novo vazar, a query usa allowlist: chave desconhecida é filtrada por padrão.

**Critério de sucesso:** hit ratio de condições > 80% após warm-up; hit ratio de geocoding > 80% para termos repetidos; chamadas Open-Meteo/dia < 50% do limite no tráfego atual.

---

## 11. Fases de rollout

### Fase 0 — Privacidade e headers (P0)

- Default `no-store` na API; opt-in público.
- Headers nas páginas Astro (`private/no-store` nas privadas; `public` nas públicas).
- SW: lista de rotas, leitura de `Cache-Control`, remoção de `/` e `/meu-diario` do precache, limpeza no logout, limite de tamanho.
- Testes Playwright de Cache Storage e headers.
- **Não muda performance;** corrige o risco.

### Fase 1 — Dados externos (P1)

- `HybridCache` para condições, lua, busca e reverso.
- Single-flight/stampede; TTL curto para parcial; chave com data.
- Remover `CacheEmMemoriaDeCondicoes` e dicionário estático.
- Métricas de hit/miss e upstream.
- **Maior ganho de performance e cota.**

### Fase 2 — HTTP, ETag e Astro (P1/P2)

- ETag/304 em PDF e share.
- Headers de catálogo/legal, wiki, ícones e OG.
- OutputCache em catálogo/legal.
- `routeRules` em `/p/[token]`.
- Opcional: refatorar `/condicoes` com Server Islands e habilitar cache de borda.

### Fase 3 — Escala (P2/P3)

- Redis como L2 do HybridCache e store do OutputCache.
- Cache de sessão/consentimento com invalidação.
- CDN (Cloudflare/Fastly) com bypass de cookie, `CDN-Cache-Control`, `Cache-Tag` e preservação de IP.
- Mídia em object storage + URL assinada, se necessário.

Cada fase é independente e pode ser revertida sem as seguintes.

---

## 12. Critérios de aceite

- [ ] `/api/auth/me`, `/api/locais`, `/api/diario`, `/api/pontos`, `/api/trilhas`, `/api/conta/*` respondem `Cache-Control: private, no-store`.
- [ ] Respostas de erro (`>= 400`, inclusive 404 e 429) respondem `private, no-store` mesmo em endpoint com política positiva; o mesmo vale para o HTML do Astro.
- [ ] `/api/catalogo` e `/api/legal/versoes` respondem `public, max-age=86400` e ETag.
- [ ] `/api/condicoes` responde `private, max-age=300, stale-while-revalidate=600`.
- [ ] PDF responde ETag e `304` em `If-None-Match`.
- [ ] `/hoje` e `/condicoes` autenticados não aparecem no Cache Storage após logout; `fisga-public-*` e tiles sobrevivem para manter o offline.
- [ ] `/condicoes` anônimo pode ser cacheado; autenticado, não — inclusive quando a sessão expira em `/meu-diario`, `/mapa`, `/registrar` ou `/boas-vindas`.
- [ ] Cache de condições não perde maré por falha transitória do provedor marítimo.
- [ ] Busca de local repetida não chama Open-Meteo novamente dentro de 24 h.
- [ ] Nenhuma chamada ao Nominatim acima de 1 req/s.
- [ ] 250 testes de backend e 61 Playwright continuam passando; novos testes cobrem headers, SW e cache.
- [ ] Chamadas Open-Meteo/dia medidas e abaixo de 50% do limite.

---

## 13. Riscos e trade-offs

| Risco | Mitigação |
|---|---|
| Dado stale de condições por 20 min | Rotular “atualizado em”; TTL curto; SWR; nunca prometer tempo real. |
| Falha do Open-Meteo | TTL curto para parcial; sentinela negativo; fallback futuro com FusionCache. |
| Cache de HTML privado | `private/no-store` + SW por header + teste Playwright. |
| Coordenadas em CDN/logs | `private` no browser; CDN só com bypass/revisão; não logar coordenadas. |
| Astro memoryCache por instância | Uma réplica; Redis/CDN provider quando escalar. |
| Server Islands adicionam requisição | Só para personalização; shell público cacheado; fallback visível. |
| Redis adiciona operação | Só na fase 3, com métricas e necessidade comprovada. |
| CDN adiciona DNS/TLS/purge | Projeto separado, depois dos headers privados. |
| SW stale | Versionamento explícito; network-first para navegação. |

---

## 14. Questões abertas

1. **Escopo do cookie em produção:** o código não define `Domain` no cookie `fisga_auth`. Se ele for host-only da API, o SSR de `/hoje`/`/condicoes` não o vê. Verificar no DevTools antes da fase 2; isso muda o alcance da personalização e do cache.
2. **Staleness aceitável** para condições: 20 min é o atual; 10 min é viável se a cota permitir.
3. **Volume de tráfego** para justificar CDN e Redis; medir por 2–4 semanas.
4. **Fail-safe:** usar HybridCache + fallback manual ou adotar FusionCache para `stale-on-failure` e eager refresh.
5. **Mídia:** continuar em volume local ou migrar para object storage + URL assinada.
6. **`/condicoes` autenticado:** separar com Server Islands ou manter SSR privado sem cache de página.

---

## 15. Referências

- Astro 7 — Route caching: https://docs.astro.build/en/guides/caching/
- Astro 7 — Server islands: https://docs.astro.build/en/guides/server-islands/
- Astro 7 — Cache Provider API: https://docs.astro.build/en/reference/cache-provider-reference/
- .NET 10 — HybridCache: https://learn.microsoft.com/aspnet/core/performance/caching/hybrid?view=aspnetcore-10.0
- .NET 10 — Output caching: https://learn.microsoft.com/aspnet/core/performance/caching/output?view=aspnetcore-10.0
- .NET 10 — Caching overview: https://learn.microsoft.com/aspnet/core/performance/caching/overview?view=aspnetcore-10.0
- FusionCache: https://github.com/ZiggyCreatures/FusionCache
- Open-Meteo terms/limits: https://open-meteo.com/en/terms
- Nominatim usage policy: https://operations.osmfoundation.org/policies/nominatim/
- Evidências medidas: análise de caching de 2026-11-18 (produção `f50560a`).

---

## 16. Próximo passo

Este documento é o **design**. Depois da aprovação, o próximo artefato é o **plano de implementação** por fase, com tarefas TDD pequenas, arquivos exatos e critérios de teste. A ordem recomendada é **Fase 0 (privacidade) → Fase 1 (dados externos) → Fase 2 (HTTP/ETag/Astro) → Fase 3 (escala)**.
