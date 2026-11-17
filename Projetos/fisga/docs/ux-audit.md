# Auditoria de UX e arquitetura de informação

**Data:** 17/11/2026 · **Escopo:** home, navegação desktop/mobile, onboarding, condições, diário,
mapa, ferramentas, wiki, registro e conta.

## Método

- Inventário de todas as rotas em `web/src/pages`.
- Leitura do header (`Layout.astro`), menu de conta (`ContaMenu.astro`) e regras de responsividade.
- Prints em desktop (1440) e mobile (390) da home, condições, mapa e ferramentas.
- Teste do primeiro acesso: link mágico → onboarding → diário vazio.
- Teste do header logado e do header anônimo.
- Comparação com os trabalhos reais do pescador: antes, durante e depois da pescaria.

## O que o produto é hoje

| Momento | Trabalho | Features |
| --- | --- | --- |
| **Antes** | “Vale a pena sair hoje?” | Condições, cockpit, janela, 7 dias, locais salvos, defeso |
| **Durante** | “Onde estou e o que funciona aqui?” | Mapa, pontos, trilhas, camadas, ferramentas offline |
| **Depois** | “O que aconteceu e o que funcionou?” | Diário, fotos, insights, comparação, troféu, PDF |
| **Aprender** | “Como faço melhor?” | Wiki, nós, checklists, defeso, guias |

O produto cresceu de “diário” para “copiloto”. A apresentação ainda conta a história antiga.

## Inventário de rotas

| Rota | Trabalho | Acesso |
| --- | --- | --- |
| `/` | Marketing do diário | Público |
| `/condicoes` | Cockpit público e condições | Público |
| `/mapa` | Pontos, trilhas e camadas | Público (salvar exige conta) |
| `/locais` | Locais salvos | Conta |
| `/ferramentas` | Hub de nós, checklists, defeso e guias | Público |
| `/wiki` | Conteúdo de referência | Público |
| `/registrar` | Registro da pescaria | Conta (formulário visível) |
| `/meu-diario` | Estatísticas, insights e histórico | Conta |
| `/conta` | Perfil, sessões, exportação e exclusão | Conta |
| `/entrar`, `/boas-vindas` | Acesso e primeiro uso | Público |

## Achados

### P0 — quebram a descoberta ou o primeiro uso

1. **Usuário logado perde Condições, Mapa e Ferramentas do header.**
   - `ContaMenu` esconde todos os links `.nav-publico` ao autenticar.
   - `Condições`, `Mapa` e `Ferramentas` usam essa classe.
   - Resultado no header logado: “Wiki · Meu diário · Meus locais · Registrar · Conta”.
   - As três features mais novas ficam invisíveis justamente para quem tem conta.

2. **No mobile não há navegação principal.**
   - Abaixo de 760 px, todos os links que não são botão somem.
   - Sobram logo, “Registrar pescaria” e “Entrar”.
   - Condições, Mapa, Ferramentas e Wiki só são alcançáveis pelo rodapé.
   - O botão “Registrar pescaria” ocupa ~270 px e quebra em duas linhas no header de 390 px.

3. **O onboarding não apresenta o novo valor.**
   - O primeiro acesso pede nome e newsletter e redireciona para `/meu-diario`.
   - Não pergunta onde o pescador pesca, não salva local padrão e não mostra condições.
   - O usuário cai em um diário vazio com estatísticas zeradas.

4. **A home ainda vende só o diário.**
   - Hero: “Cada peixe tem uma história”; CTA “Registrar pescaria”.
   - Não há busca de condições, exemplo de janela, mapa ou ferramentas.
   - O principal motivo de retorno diário (“vale a pena sair hoje?”) não aparece na porta de entrada.

### P1 — organização e vocabulário

5. **Header sobrecarregado e duplicado.**
   - Anônimo: 8 links + CTA + Entrar.
   - Logado: “Meu diário”, “Meus locais” e “Registrar” competem com o CTA.
   - “Como funciona” e “É grátis” são marketing, não ferramenta de campo.

6. **Local, ponto e trilha se confundem.**
   - `LocalDePesca` é a área usada nas condições.
   - `PontoPrivado` é uma coordenada exata.
   - `TrilhaPrivada` é um caminho.
   - O formulário de ponto ainda oferece o tipo “Trilha” para um único ponto.
   - `/locais` vive separado de `/mapa`, embora ambos sejam “meus lugares”.

7. **Ferramentas e Wiki são dois menus para o mesmo trabalho de aprender.**
   - Nós, checklists, defeso e guias ficam em `/ferramentas`.
   - Espécies, técnicas, iscas e regiões ficam em `/wiki`.
   - O usuário não sabe qual abrir para “aprender a pescar melhor”.

8. **Estados vazios não puxam para o próximo passo certo.**
   - Diário vazio mostra quatro zeros e “Registrar primeira pescaria”.
   - Não oferece “ver condições de hoje”, “salvar meu local” ou “abrir o mapa”.

9. **Registro é longo e não tem etapa de revisão.**
   - Já há prefill, EXIF, defeso e sugestão de espécie, mas eles aparecem como banners no meio do
     formulário.
   - Falta um resumo final: local, data, condições, capturas, fotos, avisos e o que será salvo.

10. **Nomes inconsistentes.**
    - “Condições” vs “Hoje”; “Meu diário” vs “Histórico”; “Mapa” vs “Mapa privado”;
      “Meus locais” vs “Mapa”; “Registrar” vs “Registrar pescaria”.

### P2 — polimento

- Não há busca global para “ir para um lugar”.
- Não há destaque de alerta (pressão/defeso) fora das páginas específicas.
- Não há indicação de “offline disponível” nas ferramentas.
- O header mobile não tem menu, hambúrguer ou bottom bar.
- O CTA “Registrar” não é fixo no mobile.

## Proposta de arquitetura de informação

### Navegação principal (desktop)

```
[Fisga]   Hoje   Diário   Mapa   Aprender        [Registrar]   [Conta]
```

- **Hoje**: cockpit, condições do local padrão, janela, 7 dias, última pescaria e atalhos.
- **Diário**: visão geral, pescarias e insights.
- **Mapa**: locais, pontos e trilhas em abas.
- **Aprender**: hub com Wiki + Ferramentas (nós, checklists, defeso, guias).
- **Registrar**: ação primária, sempre visível.
- **Conta**: avatar, perfil, sessões, exportação e exclusão.
- **Marketing** (“Como funciona”, “É grátis”, “Sobre”) vai para o rodapé e para a home anônima.

### Navegação principal (mobile)

Bottom tab bar com cinco espaços:

```
Hoje     Diário     [Registrar]     Mapa     Aprender
```

- “Registrar” em destaque no centro, com ícone de peixe/mais.
- Conta no topo direito (avatar), não na barra.
- A barra resolve a descoberta sem depender do rodapé.
- Em telas de formulário, a barra pode sumir para dar espaço.

### Navegação pública (sem conta)

```
[Fisga]   Hoje   Mapa   Aprender        [Entrar]   [Registrar]
```

- “Hoje” leva a `/condicoes`.
- “Mapa” e “Aprender” são públicos.
- “Registrar” leva ao login com retorno.
- Home anônima continua sendo a página de venda, mas com busca de condições no hero.

### Rotas e redirecionamentos

| Hoje | Proposta |
| --- | --- |
| `/` | Home adaptativa: marketing para anônimo, dashboard “Hoje” para logado |
| `/hoje` | Dashboard logado (ou redireciona para `/` quando logado) |
| `/condicoes` | Página pública detalhada (mantém SEO e links compartilhados) |
| `/mapa` | Abas Locais · Pontos · Trilhas |
| `/locais` | Redireciona para `/mapa?aba=locais` |
| `/ferramentas` e `/wiki` | Mantidos; `/aprender` vira o hub e a entrada de navegação |
| `/meu-diario` | “Diário”; pode ganhar abas internas |
| `/registrar` | Ação primária; retorno para `/hoje` após salvar |

### Vocabulário

| Termo | Significado | Onde aparece |
| --- | --- | --- |
| **Local** | Área de pesca (cidade, praia, represa) usada nas condições | Hoje, Mapa · Locais |
| **Ponto** | Coordenada exata (poita, laje, estrutura) | Mapa · Pontos |
| **Trilha** | Caminho de corrico/deslocamento | Mapa · Trilhas |
| **Pescaria** | Um dia registrado no diário | Diário, Registrar |
| **Condições / Hoje** | Cockpit do dia | Hoje |
| **Aprender** | Wiki + ferramentas de campo | Aprender |

Ajuste imediato: renomear o tipo de ponto “Trilha” para **“Marco”** ou **“Referência”** e reservar
“Trilha” para o caminho.

## Proposta por tela

### Home anônima

- Hero com a pergunta “Vale a pena sair hoje?”.
- Busca de cidade/praia + “Usar minha localização” + exemplo Aracaju.
- Três cartões de trabalho: **Antes** (condições), **Durante** (mapa/ferramentas), **Depois**
  (diário/troféu).
- O troféu continua como prova visual, mas abaixo do valor principal.
- CTA primário: **Ver condições**. Secundário: **Registrar pescaria**.

### Hoje (logado)

- Saudação curta + local padrão.
- Resumo “agora”: maré, vento, chuva, lua.
- Janela de pesca em destaque + 7 dias.
- Ações rápidas: **Registrar**, **Abrir mapa**, **Checklist**.
- Última pescaria e um insight/sinal.
- Alerta de defeso ou pressão quando existir.

### Condições

- Manter a página pública como está em conteúdo, com breadcrumb “Hoje > Aracaju”.
- No topo, deixar claro que é pública e que salvar/comparar pede conta.
- Evitar duplicar a home logada; a página detalhada pode ser o “drill-down” de Hoje.

### Diário

- Abas: **Visão geral**, **Pescarias**, **Insights**.
- Estado vazio com três caminhos: ver condições, salvar local, registrar primeira pescaria.
- Sinais comparativos depois de 3+3 pescarias, com o aviso atual.

### Mapa

- Abas: **Locais**, **Pontos**, **Trilhas**.
- Locais: área + “Abrir condições” + padrão.
- Pontos: waypoints com notas.
- Trilhas: caminhos com distância e GPX.
- Um único botão “Adicionar” que muda conforme a aba.
- Manter o aviso de privacidade e as camadas.

### Aprender

- Hub com duas colunas: **Ferramentas de campo** (nós, checklists, defeso, guias) e **Wiki**
  (espécies, técnicas, iscas, regiões).
- Badge “offline” nas ferramentas.
- Busca Pagefind global dentro do hub.

### Registrar

- Manter as três etapas atuais.
- Adicionar uma etapa **Revisar** antes de salvar: local, data, condições, capturas, fotos, EXIF,
  defeso e sugestão de espécie.
- Ações: “Salvar”, “Voltar”, “Descartar rascunho”.
- Após salvar: “Ver pescaria”, “Baixar troféu”, “Voltar para Hoje”.

### Onboarding

1. Nome (opcional).
2. **Onde você pesca?** Busca/geolocalização + “Salvar como padrão” + “Agora não”.
3. Preferência de newsletter.
4. Fim: abrir **Hoje** com as condições do local, não um diário vazio.

## Priorização

### Quick wins (1–2 dias)

| # | Mudança | Impacto |
| --- | --- | --- |
| 1 | Corrigir o header logado para manter Condições/Mapa/Ferramentas | P0 |
| 2 | Adicionar bottom nav mobile (ou menu hambúrguer) | P0 |
| 3 | Onboarding: passo de local padrão e destino `/hoje` | P0 |
| 4 | Home: busca de condições no hero + CTA “Ver condições” | P0 |
| 5 | Remover “Meus locais” do header; entrar por Mapa | P1 |
| 6 | Renomear tipo de ponto “Trilha” para “Marco” | P1 |
| 7 | Estado vazio do diário com “Ver condições” e “Salvar local” | P1 |
| 8 | Unificar rótulos “Meu diário” → “Diário”, “Registrar” → “Registrar pescaria” | P2 |

### Estrutural (UX Sprint 5)

1. Criar `/hoje` (ou home adaptativa) com o cockpit do local padrão.
2. Criar `/aprender` e agrupar Wiki + Ferramentas na navegação.
3. Transformar `/mapa` em abas Locais/Pontos/Trilhas e aposentar `/locais`.
4. Reescrever a home anônima em torno de “Vale a pena sair hoje?”.
5. Adicionar etapa “Revisar” no registro.
6. Prototipar tudo no OpenDesign antes de codar.

## Métricas para validar

- **Ativação:** % de novos usuários que salvam um local e veem condições na primeira sessão.
- **Primeiro registro:** % que registram a primeira pescaria em 24 h.
- **Retorno semanal:** visitas a Hoje/condições por usuário ativo.
- **Adoção:** mapa, trilhas, ferramentas, EXIF e defeso.
- **Tempo até registrar:** menos campos digitados e menosabandono.
- **Mobile:** cliques na bottom nav e conclusão de fluxo em 390 px.

## Recomendação

Não adicionar mais features antes de arrumar a porta de entrada. A ordem recomendada é:

1. Corrigir os P0 de navegação e onboarding.
2. Prototipar a nova home/Hoje e a bottom nav no OpenDesign.
3. Implementar a IA nova em uma sprint curta, mantendo URLs antigas com redirect.
4. Só depois voltar para IA, CHM oficial, PostGIS e mapas offline.

O produto já tem profundidade. O que falta é **uma entrada que conte a história certa e uma
navegação que não esconda o que foi construído**.

## Status de implementação — Sprint UX

Implementado em 17/11/2026, depois da validação do protótipo `fisga-ux` no OpenDesign.

### P0 — entrada e navegação

- [x] `Layout.astro` com **Hoje · Diário · Mapa · Aprender**, CTA Registrar e conta.
- [x] Bottom nav mobile com 5 itens (Hoje, Diário, Registrar central, Mapa, Aprender) e 3 no modo
  anônimo.
- [x] `ContaMenu` alterna `.nav-auth-only`/`.nav-anon-only` via `body[data-auth]`.
- [x] `/hoje` com gate de sessão, local padrão, condições agora, janela, 7 dias, atalhos e última
  pescaria.
- [x] Fluxo de primeiro local em `/hoje`, com busca e GPS.
- [x] Home pública reescrita em torno de “Vale a pena sair hoje?”, com busca e três trabalhos.
- [x] Onboarding em 2 etapas (nome/newsletter → local), terminando em `/hoje`.
- [x] Altura de maré em `/hoje` usa o ponto atual da curva, não o primeiro evento do dia.
- [x] Saudação dinâmica (Bom dia/Boa tarde/Boa noite).

### P1 — Diário, Mapa e Aprender

- [x] `/mapa` com abas **Locais · Pontos · Trilhas**, `?aba=` e os mesmos dados privados.
- [x] `/meu-diario` com abas **Visão geral · Pescarias · Insights**.
- [x] `/aprender` agrupando Ferramentas de campo + Wiki, com busca local e selos offline.
- [x] Empty state do diário com links para condições, mapa e registro.
- [x] `/locais` → `/mapa?aba=locais` e `/diario` → `/meu-diario`.

### P2 — Registrar

- [x] Quatro etapas: **A pescaria · As condições · As capturas · Revisar**.
- [x] Navegação por etapa, validação de local/data e retorno preservando dados.
- [x] Revisão com local, data, condições, capturas e fotos.
- [x] Anotações movidas para a revisão.
- [x] Formulário deixou de abrir preenchido com o exemplo (Campinas/Tucunaré/Tilápia); agora abre
  vazio, com data de hoje e uma linha de captura.

### Verificação

- Protótipo e aplicação sem overflow em 360, 390, 768, 1024, 1280, 1440 e 1920 px.
- Fluxo real testado: link mágico → onboarding com local → `/hoje` → mapa com abas → `/aprender` →
  registrar em 4 etapas → revisão → pescaria salva → diário com abas.
- Bottom nav autenticada validada em 390 px, com Diário e Registrar visíveis.
- `astro check`, build, 75 testes .NET e testes Playwright passando.

### Diferença em relação ao protótipo

O protótipo mostra 3 passos de onboarding (nome → local → newsletter). A implementação mantém 2
passos: nome/newsletter na primeira tela e local na segunda, para reduzir fricção sem perder o
local padrão.
