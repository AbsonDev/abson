# Roadmap de produto — Fisga

Este documento organiza os quatro módulos pedidos e separa o que já está no ar, o que vem agora e
o que depende de decisão externa. A regra é entregar valor utilizável a cada fase, sem prometer
precisão que os dados ainda não sustentam.

## Módulo A — Cockpit do Pescador

**Status:** núcleo no ar.

- Linha do tempo sincronizada de 24 h com maré, pressão, vento e períodos solunares.
- Maré aproximada (Open-Meteo/Copernicus), com preamar/baixa-mar, altura e datum do modelo.
- Corrente oceânica real do modelo (`ocean_current_velocity`), em km/h e direção.
- Vento, rajadas, chuva, pressão com tendência e alerta de mudança brusca.
- Solunar calculado com MET Norway (trânsito, anti-trânsito, nascer/pôr e fase).
- Janela de pesca explicável, nota 1–5 e motivos.
- Próximos 7 dias com melhor horário.

**Próximo:**

1. Importar a tábua oficial do CHM para os 44 portos (anual, com atribuição e validação).
2. Validar a janela contra o histórico real e ajustar pesos.
3. Camada de corrente no mapa e estimativa de corrente em rios/estuários.
4. Alertas push/WhatsApp para “janela boa” e mudança brusca de pressão.

## Módulo B — Mapa Náutico e Caderno de Pontos

**Status:** núcleo no ar.

- Leaflet + OpenStreetMap (mapa), Esri World Imagery (satélite), OpenSeaMap (náutico) e GEBCO
  (batimetria), sem token pago.
- Pontos privados com nome, tipo (ponto, poita, estrutura, trilha, rampa), notas e coordenadas.
- CRUD completo, limite de 200 pontos, ownership por conta.
- Nenhum endpoint público de pontos; nenhuma coordenada é publicada.
- Cache local dos pontos para consulta offline; o mapa em si precisa de conexão para os azulejos.

**Próximo:**

1. **Trilhas reais** (LineString) para corrico e deslocamento, não apenas pontos do tipo “trilha”.
2. **PostGIS** para consultas por raio, polígonos de bacia e clustering.
3. **Inteligência coletiva anonimizada** por macrorregião/quadrante de 5 km, opt-in, com mínimo de
   amostras e nunca a coordenada exata.
4. **Mapas offline** por área baixada (tiles em IndexedDB/Cache Storage).
5. Importação GPX/KML e exportação dos pontos do usuário.

## Módulo C — Diário Inteligente + IA

**Status:** diário, condições e comparação já existem; IA ainda não.

- Registro com condições, fotos, espécie, isca, soltura e insights.
- Comparação com o histórico por lua, maré, clima e período.
- Pré-preenchimento por condições do local.

**Próximo:**

1. **EXIF**: ler GPS e horário da foto, com consentimento, e anexar condições daquele minuto.
2. **Reconhecimento de espécie** por foto: sugestão com confiança, confirmação humana, link para a
   wiki e nenhuma autoridade automática.
3. **Defeso/tamanho mínimo** por espécie e UF, com fonte oficial e alerta no registro.
4. **Insights comparativos**: “68% mais capturas na maré enchente com pressão acima de 1015 hPa”,
   com amostra mínima e intervalo de confiança.
5. **Vídeo**: extrair 2–3 frames para sugestão, sem armazenar o vídeo inteiro no início.

**Bloqueios:** provedor de visão computacional, custo por imagem, política de confirmação e
privacidade das fotos.

## Módulo D — Ferramentas, Defeso e Guias

**Status:** não iniciado.

1. **Biblioteca de nós e montagens** offline, com diagramas passo a passo por situação.
2. **Legislação e defeso** por bacia/estado: piracema, cotas, tamanhos mínimos e fontes.
3. **Diretório de guias/piloteiros** com perfil, região, modalidade e contato.
4. **Checklists** por modalidade (caiaque, costeira, embarcado, pesqueiro), editáveis e offline.

**Próximo:** começar por defeso/tamanho mínimo e checklists, que são conteúdo e regra de negócio;
guia comercial exige moderação e acordo com parceiros.

## Dados e arquitetura

| Camada | Escolha atual | Evolução |
| --- | --- | --- |
| Clima, vento, ondas, corrente e nível do mar | Open-Meteo (grátis) | Plano comercial se o Fisga cobrar |
| Lua/solunar | MET Norway + cálculo local | Mantém |
| Maré oficial | Aproximada (modelo) | CHM/Marinha para 44 portos |
| Mapas | Leaflet + OSM/Esri/OpenSeaMap/GEBCO | MapLibre/Mapbox se precisar de vetor e offline |
| Geoespacial | latitude/longitude | PostgreSQL + PostGIS para raio, bacia e clustering |
| Offline | PWA + service worker + localStorage | IndexedDB para fila de pescarias e tiles |
| IA de espécie | — | Provedor com confirmação humana e limite por usuário |

## Sequência recomendada

1. **Agora:** cockpit + mapa privado (entregue).
2. **Sprint 4:** EXIF + defeso/tamanho mínimo + reconhecimento por foto (se houver provedor).
3. **Sprint 5:** trilhas + PostGIS + inteligência coletiva anonimizada + mapas offline.
4. **Sprint 6:** nós/montagens, checklists, guias e alertas.

## Métricas

- Tempo até a primeira resposta útil (“vale a pena sair?”).
- Locais salvos por usuário ativo e retorno semanal.
- Pescarias registradas com condições preenchidas.
- Taxa de confirmação/correção da sugestão de espécie.
- Uso offline e sincronizações pendentes.
- Retenção em 30 dias e pescarias por usuário.
