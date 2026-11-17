# ADR 0011 — Mapa privado e pontos do pescador

**Status:** aceito · **Data:** 2026-11-17

## Contexto

Apps de pesca costumam transformar o ponto secreto do pescador em conteúdo público. Isso destrói a
confiança e expõe estruturas sensíveis. Ao mesmo tempo, o pescador precisa de um caderno de pontos
para poitas, lajes, canais, manguezais e rampas, com notas que só ele vê.

A plataforma também precisa de camadas de mapa sem depender de um token pago nesta fase.

## Decisão

- **Pontos são privados por padrão.** `PontoPrivado` pertence a um usuário, tem nome, tipo, notas,
  latitude e longitude. Não existe endpoint público de pontos e nenhuma projeção pública é gerada.
- Limite de **200 pontos por conta**, com ownership em toda operação.
- Tipos iniciais: `Ponto`, `Poita`, `Estrutura`, `Trilha`, `Rampa`. Nesta fase, “trilha” é um ponto
  marcado; a linha (LineString) fica para a fase geoespacial.
- **Mapa com Leaflet** e camadas gratuitas:
  - OpenStreetMap para o mapa base;
  - Esri World Imagery para satélite;
  - OpenSeaMap para sinalização náutica;
  - GEBCO WMS para batimetria.
- **Sem token** de Mapbox/MapTiler nesta fase. A troca para MapLibre/Mapbox fica possível se houver
  necessidade de vetor, offline de tiles ou estilo próprio.
- **Offline local:** os pontos são guardados em `localStorage` e exibidos com aviso quando a API não
  responde. O mapa em si depende de conexão para os azulejos; mapas offline por área ficam para a
  próxima fase.
- **Privacidade no service worker:** rotas privadas (`/locais`, `/conta`, `/meu-diario`,
  `/registrar`, `/trofeu`) não entram no cache de navegação.
- **Inteligência coletiva** só será feita por macrorregião/quadrante, opt-in, com amostra mínima e
  nunca com a coordenada exata.

## Consequências

- O diferencial competitivo é explícito: o mapa é um caderno pessoal, não um feed.
- Dependemos de políticas de uso das camadas gratuitas (OSM, Esri, OpenSeaMap, GEBCO); precisamos
  atribuir e evitar uso abusivo.
- Sem PostGIS, consultas por raio e clustering ficam limitadas; a migração entra quando a
  inteligência coletiva ou as trilhas forem implementadas.
- Cache local de pontos em dispositivo compartilhado exige cuidado: ao detectar logout (401), o
  cache é apagado.
- O mapa não substitui carta náutica oficial nem dados de segurança; a UI deve deixar isso claro.
