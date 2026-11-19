# Especificação — Níveis de detalhe do local compartilhado (19/11/2026)

## Intenção

Permitir compartilhar a história da pescaria sem entregar o ponto. É a base da futura comunidade, sem coletar nenhum dado sensível novo agora.

## Goals

1. Expandir `PrivacidadeDoLocal` para cinco níveis: `Exato`, `Trecho`, `CorpoAgua`, `Cidade`, `Oculto`.
2. Mudar o padrão de `Exato` para `Cidade` (privacidade por padrão).
3. A regra vale para `/p/[token]`, card de troféu e PDF — todos já passam por `LocalPublico`.
4. Nenhuma coordenada em DTO público (continua como é hoje).

## Non-goals

- Raio em metros (500 m / 1 km / 2 km) — Fase 2, só como agregado por bacia, nunca por pescaria individual.
- O feed/comunidade em si (spec separada).
- Captura de lat/long no registro.

## Anti-goals

- Mostrar "Local aproximado" enquanto o nome do ponto ainda aparece no texto (ex.: nível `CorpoAgua` com `Local = "Poita do Zé"`).
- Falso rigor: "a ~2 km" sem coordenada.

## Estado real que condiciona

- `PrivacidadeDoLocal` é persistido como **string** (`HasConversion<string>()`, `HasMaxLength(16)`) — adicionar valores é seguro; nomes de enum com ≤16 caracteres.
- `Pescaria.LocalPublico` já centraliza a decisão; `Dtos.ParaEntradaPublica` já entrega `LocalPublico/CidadePublica/EstadoPublico`.
- `Local` é texto livre (≤120) e não tem coordenada; as reais estão em `PontoPrivado`.
- Select em `registrar.astro` com default `'Exato'`; união em `web/src/lib/tipos.ts`; badge em `/p/[token]`.
- Testes existentes: `PescariaTests`, `ServicoDePescariasTests`, `identity-e2e.spec.ts`.

## Decisões de implementação

- Novo campo opcional `CorpoOuTrecho` (≤80): "Rio, lagoa ou trecho" · placeholder "Ex.: Rio Sergipe, trecho centro".
- Regras de exibição: `Exato` → `Local`; `Trecho` → `CorpoOuTrecho` inteiro; `CorpoAgua` → texto até a primeira vírgula (ou inteiro se não houver); `Cidade` → `Cidade · UF`; `Oculto` → "Local não divulgado". Se `CorpoOuTrecho` estiver vazio, `Trecho`/`CorpoAgua` caem para `Cidade` — nunca para `Local`.
- Badge do link público passa a ter rótulo por nível, em vez do `Cidade ? ... : ...` atual.
- UI segue o design system Fisga: label visível, helper "Vale para o link público, o troféu e o PDF. O diário continua privado.", 44px de alvo, sem cor nova. A variação da tela é gerada no OpenDesign antes da implementação.
- Risco residual fora desta spec: `Observacoes` e EXIF das fotos seguem públicos no link — sinalizar no copy e tratar numa passagem separada.

## Fora de escopo

- Raio em metros, captura de lat/long, feed/comunidade.

## Referências

- Draft privado no quadro: "Níveis de detalhe do local no link público (Exato / Trecho / Rio / Cidade / Oculto)" — https://github.com/users/AbsonDev/projects/6
