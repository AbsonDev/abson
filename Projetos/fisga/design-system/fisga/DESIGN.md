# Fisga

> Category: Lifestyle & Outdoors
> A Brazilian fishing diary: warm sand paper, petrol ink, Fraunces + Inter.
> An editorial field notebook — private by default, useful before the trip,
> during the day on the water, and after the catch.

## Visual Theme & Atmosphere

Fisga should feel like a well-kept paper logbook that happens to be digital.
Warm sand paper (`#F8F4EA`) is the ground; deep petrol (`#174E57`) is the ink
that carries actions; clay (`#D9773F`) appears only as a signal — a warning, a
destructive accent, the trophy detail. Fraunces gives headlines the cadence of
handwriting; Inter keeps the interface fast to read in sunlight.

The product is used outdoors, often on a phone, sometimes with wet hands and
bad signal. Calm and legible beats decorative. Never a SaaS console, never a
dashboard of cards for their own sake, never a stock photo of a fisherman.

## Color Palette & Roles

| Token | Value | Role |
| --- | --- | --- |
| `--bg` | `#F8F4EA` | Page ground (sand paper) |
| `--surface` | `#FFFDF8` | Cards, inputs, menus |
| `--surface-warm` | `#F1E9DA` | Secondary surface, hover rows |
| `--fg` | `#172A2D` | Body and headings |
| `--muted` | `#657277` | Secondary copy, metadata |
| `--border` | `#E3D8C8` | Hairline borders and dividers |
| `--accent` | `#174E57` | Primary actions, links, focus |
| `--accent-hover` | `#10383F` | Hover / pressed accent |
| `--accent-on` | `#FFF9F0` | Label on accent |
| `--success` | `#56745C` | Released fish, success text |
| `--warn` | `#E6A15C` | Attention, trophy detail |
| `--danger` | `#D9773F` | Warning borders, destructive signal |

Rules:

- Petrol is the only large action color. Clay is never a large fill and never
  body text at its base value.
- Success and warning are never color-only: pair them with a word and an icon.
- Do not invent new hex values. If a screen needs a tint, use `color-mix()` on
  an existing token and keep the contrast honest.
- Pure black and pure white are not part of the palette; use `--fg` and
  `--surface`.

## Typography Rules

- **Display:** Fraunces 500/600, optical sizing on, tracking `-0.025em`.
  Italic is reserved for the user's name and a single emphatic word — it is a
  greeting, not decoration.
- **Body:** Inter 400/500/600, 16px base, line-height 1.6.
- **Labels / eyebrows:** 11.5px, uppercase, 600, tracking `0.13em`–`0.16em`.
- Scale: 11.5 · 13.5 · 16 · 20 · 26 · 40 · 56 · 84 px.
- Headlines are sentence case in pt-BR: “Onde você pesca?”, not “Onde Você Pesc”.
- Numbers that matter (tide height, wind, temperatura, peso) can use Fraunces
  for emphasis, but keep units on the same line and never fake precision.

## Spacing, Layout & Composition

- 4/8 rhythm; the page container is `1120px` with `20px` side gutters.
- Section rhythm: `112px` desktop, `76px` tablet, `42px` phone.
- Cards use `--radius-md` (20px); controls and chips use `--radius-sm` (12px);
  pills use `--radius-pill`.
- One idea per screen. The first screen of a flow gets a single decision, not a
  form with three.
- Focused flows (welcome, register, auth) hide the marketing header, footer and
  tabbar. The user is inside a task, not browsing the site.
- On mobile, content starts at the top; do not vertically center a form that
  the thumb needs to reach. Keep the primary action above the fold.

## Components & Interaction States

- **Buttons.** Petrol fill with cream label for the primary action; outlined
  petrol for the secondary; quiet text for the escape hatch. Minimum 44px on
  touch. Hover darkens; disabled drops to 55% and loses motion.
- **Fields.** Visible sentence-case label (15px, 600), 48px input, 12px radius,
  1px `--border`; focus is a 3px petrol ring around the field. Errors appear
  inline, in pt-BR, with an icon and a word.
- **Chips / suggestions.** 44px, 12px radius, hairline border. Selecting one is
  an action, not a filter: it should save and move the user forward.
- **Result rows.** 44px, name left, UF right in muted text, hover on
  `--surface-warm`, keyboard reachable.
- **Privacy line.** Lock icon + “Privado por padrão. Nada é publicado até você
  compartilhar.” plus a quiet “Como funciona” that opens an in-context dialog.
  Privacy is a feature, not fine print.
- **Dialogs.** Native `<dialog>`, paper surface, 12px radius, petrol scrim;
  focus moves to the primary button, Escape and backdrop close, and clicking the
  dialog's own padding does not close it.
- **Empty / loading / error.** Every state has a sentence a person can act on:
  “Nenhum lugar encontrado. Tente o nome da cidade.”, “Não foi possível buscar
  agora. Tente de novo.”, “Abrindo seu dia…”.

## Motion

- 150ms for hover and micro-states, 200ms for general state changes, 260ms for
  sheets, 340ms for screen entrances.
- Use transform and opacity; never animate layout properties.
- Strong ease-out: `cubic-bezier(0.23, 1, 0.32, 1)`.
- `prefers-reduced-motion` removes stagger, lift and spinner rotation. The copy
  still explains the wait.
- No parallax, no looping background motion, no confetti.

## Accessibility

- 4.5:1 for body text and 3:1 for large text against the actual background.
- Every control has a visible `:focus-visible` ring; never remove focus without
  replacing it.
- 44px minimum touch target on mobile, including quiet links and dialog actions.
- One visible `h1` per route; a skip link; semantic landmarks; `aria-current` on
  the active nav item.
- Async results are announced in a `role="status"` live region, including the
  count of search results and the locating/saving state.
- Status is never color-only. Release/keep carries the word, not just a dot.
- Forms keep the label visible; placeholders are examples, never labels.

## Voice & Copy

- pt-BR, direct, warm, a little weathered. Talk like a fishing companion, not a
  brand manifesto.
- Use “você”, short sentences, and concrete nouns: maré, vento, chuva, lua,
  janela, diário, troféu, local.
- Explain before asking. Show the value, then request the one thing you need.
- Never shame the user for skipping. “Agora não” is a valid path.
- Privacy language is plain: “Ninguém vê seu diário até você compartilhar.”
- Examples of the tone:
  - “Bem-vindo, Marina.”
  - “Escolha onde você pesca para abrir a janela de hoje.”
  - “Seu diário nasce privado.”
  - “Não conseguimos usar sua localização. Busque pelo nome.”
- Avoid hype, exclamation marks, emoji, “revolucione”, “descubra o segredo”,
  and English UI words.

## Screens & Flows

- `/` — public landing; the argument for the diary, conditions and privacy.
- `/entrar`, `/criar-conta`, `/esqueci-senha`, `/redefinir-senha` — auth.
- `/aceite` — terms and privacy consent, versioned.
- `/boas-vindas` — one question only: “Onde você pesca?”. No name field, no
  newsletter, no wizard. Saves a default location and hands off to `/hoje`.
- `/hoje` — the day's window, conditions, next 7 days, shortcuts, last trip.
  A new account without a location becomes a single “choose your first place”
  invitation.
- `/meu-diario` — overview, trips, insights. Empty state invites the first trip.
- `/registrar` — focused flow: trip, conditions, catches, review.
- `/mapa` — private places, points and trails; privacy strip.
- `/conta` — email, password, Google, newsletter, sessions, export, delete.
- `/aprender` and `/wiki` — field guides and knots, readable offline.

## Anti-patterns

- No two-step welcome wizard, no progress “1 de 2” for a single decision.
- No newsletter opt-in before the first value; it belongs in Minha conta.
- No dark patterns, fake urgency, pre-checked consent or hidden unsubscribe.
- No pure black/white, no gradient washes, no glassmorphism, no neon.
- No emoji in product copy; no stock photography of fishermen.
- No 40px touch targets, no placeholder-as-label, no color-only status.
- No lorem ipsum and no “demonstration data” without a clear label.
- No shipping the OpenDesign reviewer switcher or review chrome to production.

## Agent Prompt Guide

When generating a Fisga artifact:

1. Start from this `DESIGN.md` and `tokens.css`; do not invent a parallel
   palette. If a value is missing, extend with `color-mix()` on a token.
2. Write real pt-BR copy for the actual state, including loading, empty, error
   and success. No lorem ipsum.
3. Prefer one decision per screen. If the flow needs three fields, ask whether
   two can be inferred or moved to a later, contextual step.
4. Design mobile first at 390px, then desktop at 1440px. Check the 44px targets
   and the focus ring.
5. Preserve the product's privacy posture: the diary is private until the user
   shares a specific trip.
6. Treat the OpenDesign preview as a review surface; never ship its state
   switcher, device frames or notes into the app.
