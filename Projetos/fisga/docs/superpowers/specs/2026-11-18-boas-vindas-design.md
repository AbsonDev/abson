# Boas-vindas — uma tela, uma pergunta

**Data:** 2026-11-18
**Rota:** `/boas-vindas?novo=1`
**Status:** aprovado

## Contexto

Depois do cadastro (e do aceite dos termos), o Fisga leva a pessoa para
`/boas-vindas`. A tela atual é um wizard de dois passos: nome + newsletter no
primeiro, local no segundo, com barra de progresso, avatar genérico, header de
marketing e rodapé completo em volta. Antes de qualquer valor, ela pede três
coisas e repete informações que o produto já tem:

- o cadastro já exige o nome (`criar-conta.astro`);
- a newsletter já tem casa em Minha conta;
- o `/hoje` já tem um convite completo de primeiro local (busca, geolocalização
  e três exemplos), então quem pula em boas-vindas é perguntado de novo um
  segundo depois.

O próprio `DESIGN-UX.md` do Fisga define que onboarding e registrar são fluxos
focados, sem header, rodapé e tabbar. A tela implementada não segue isso.

## Objetivo

Levar a pessoa ao primeiro valor do produto — a janela de pesca de hoje — com
**uma pergunta e nenhum campo obrigatório**.

## Não-objetivos

- Coletar nome (já vem do cadastro; sem nome, a saudação usa “pescador”).
- Coletar opt-in de newsletter (fica em Minha conta).
- Criar um novo wizard, tour ou checklist de primeiro acesso.
- Alterar a API ou o modelo de dados.
- Mexer no `/hoje` além de receber a pessoa.

## Anti-objetivos

- Uma tela bonita que continua sendo um formulário antes do valor.
- Pedir o local duas vezes em telas consecutivas.
- Esconder a privacidade no rodapé ou atrás de um accordion.

## Abordagens consideradas

1. **Polir o wizard de 2 passos.** Mantém a duplicação com o Hoje e o pedido de
   marketing; continua lento. Descartada.
2. **Apagar a rota e cair direto no `/hoje`.** É o caminho mais curto, mas perde
   o momento de primeira impressão e a mensagem de privacidade. Fica como
   evolução possível, não como primeira entrega.
3. **Uma tela, uma pergunta (escolhida).** A tela paga o próprio custo porque faz
   a única coisa que desbloqueia o valor: escolher o local. O nome e a newsletter
   saem; a privacidade fica visível.

## A tela

Fluxo focado, sem header de marketing, rodapé ou tabbar. Barra superior mínima
com a marca Fisga à esquerda e “Agora não” discreto à direita.

```
┌ Fisga ───────────────────────────────── Agora não ┐
│                                                    │
│   PRIMEIRO ACESSO                                  │
│   Bem-vindo, Marina.                               │
│   Escolha onde você pesca para abrir a janela      │
│   de hoje e começar seu diário.                    │
│                                                    │
│   Onde você pesca?                                 │
│   [ 🔍 Cidade, praia ou represa             ]      │
│   [ Usar minha localização ]                       │
│                                                    │
│   Ou comece por:                                   │
│   ( Aracaju )  ( Ilhabela )  ( Campinas )          │
│                                                    │
│   🔒 Privado por padrão. Nada é publicado          │
│      até você compartilhar.  Como funciona         │
└────────────────────────────────────────────────────┘
```

### Copy

- Eyebrow: `Primeiro acesso`
- H1: `Bem-vindo, {primeiro nome}.` ou `Bem-vindo, pescador.`
- Subtítulo: `Escolha onde você pesca para abrir a janela de hoje e começar seu diário.`
- Label: `Onde você pesca?`
- Placeholder: `Cidade, praia ou represa`
- Secundário: `Usar minha localização`
- Sugestões: `Ou comece por` + Aracaju, Ilhabela, Campinas
- Privacidade: `Privado por padrão. Nada é publicado até você compartilhar.` + link `Como funciona` para `/privacidade`
- Pular: `Agora não`

### Comportamento

- Escolher sugestão, resultado da busca ou geolocalização salva o local em
  `POST /api/locais` com `padrao: true` e segue para o destino seguro
  (`sessionStorage['fisga:retorno']`, padrão `/hoje`).
- Busca começa com 2 caracteres, com debounce de 320 ms.
- “Agora não” segue para o destino sem salvar.
- Sem sessão → `/entrar?voltar=/hoje`.
- Aceite pendente → `/aceite?destino=<rota atual>`.
- Falha ao carregar a conta → mensagem em `role="status"`, sem UI morta.
- Salvar → “Abrindo seu dia…” e controles desabilitados contra duplo envio.
- Geolocalização negada → “Não conseguimos usar sua localização. Busque pelo nome.”
  e botão restaurado.

### Estados

padrão · buscando · resultados · nenhum resultado · erro de busca · salvando ·
localização negada · sem sessão · aceite pendente.

## Restrições

- pt-BR em toda a cópia.
- Tokens existentes (`--papel`, `--terracota`, `--tinta`, `--linha`, Fraunces,
  Inter); nenhuma estética paralela.
- Alvos de toque ≥ 44 px; label visível; `aria-live` no status; foco visível.
- Sem dependências novas, sem mudança de API.
- `#boas-pular` precisa continuar existindo: o E2E real de identidade usa esse id.

## O que fica para o construtor

- A composição visual final (respiro, proporção do título, detalhe de marca no
  fundo), guiada pelo artefato do OpenDesign.
- A decisão de manter o link “Como funciona” apontando para `/privacidade`.
- Ajustes de responsividade abaixo de 390 px.
