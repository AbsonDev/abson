# Fisga — Usage

## Read Order

1. `tokens.css` — bind these semantic names; do not invent hex values.
2. `DESIGN.md` — atmosphere, type, components, voice and anti-patterns.
3. `Projetos/fisga/docs/DESIGN-UX.md` — route-level UX and state inventory.
4. `Projetos/fisga/web/src/styles/global.css` — production token aliases and
   component rules; the app is the source of truth for what shipped.

## Design Highlights

- Warm sand paper + petrol ink; clay only as a signal.
- Fraunces for headlines and the user's name in italic; Inter for the interface.
- One decision per screen; privacy visible, never buried.
- Focused flows hide marketing chrome.
- 44px targets, 3px focus, live regions for async state.

## Do

- Write real pt-BR copy for every state.
- Use `--accent` for the primary action and keep it rare.
- Pair status colors with a word and an icon.
- Test 390px first; then 1440px.
- Keep the diary private until the user shares a specific trip.

## Avoid

- Wizards, progress bars and marketing forms before the first value.
- Newsletter opt-in on the welcome screen.
- Pure black/white, gradient washes, glassmorphism, neon.
- Emoji, stock photos and English UI words.
- Reviewer chrome (state switcher, device frames, notes) in production.
