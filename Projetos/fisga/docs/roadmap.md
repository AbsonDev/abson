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
- Trilhas de corrico/deslocamento como linha, com distância e exportação GPX.
- Cache dos últimos 600 azulejos visitados no service worker.

**Próximo:**

1. **PostGIS** para consultas por raio, polígonos de bacia e clustering.
2. **Inteligência coletiva anonimizada** por macrorregião/quadrante de 5 km, opt-in, com mínimo de
   amostras e nunca a coordenada exata.
3. **Mapas offline** por área baixada (além do cache de azulejos visitados).
4. Importação GPX/KML (a exportação já existe).

## Módulo C — Diário Inteligente + IA

**Status:** diário, condições e comparação já existem; IA ainda não.

- Registro com condições, fotos, espécie, isca, soltura e insights.
- Comparação com o histórico por lua, maré, clima e período.
- Pré-preenchimento por condições do local.
- EXIF de data/hora e GPS para sugerir data, período e cidade.
- Lembrete de defeso por espécie/UF com link oficial.
- Insights comparativos com amostra mínima (3 pescarias de cada lado).

**Próximo:**

1. **EXIF**: ler GPS e horário da foto, com consentimento, e anexar condições daquele minuto.
2. **Reconhecimento de espécie** por foto: porta plugável já implementada (Gemini opcional) com
   confiança, confirmação humana e fallback manual. Falta configurar o provedor.
3. **Defeso/tamanho mínimo** por espécie e UF, com fonte oficial e alerta no registro.
4. **Insights comparativos**: “68% mais capturas na maré enchente com pressão acima de 1015 hPa”,
   com amostra mínima e intervalo de confiança.
5. **Vídeo**: extrair 2–3 frames para sugestão, sem armazenar o vídeo inteiro no início.

**Bloqueios:** provedor de visão computacional, custo por imagem, política de confirmação e
privacidade das fotos.

## Módulo D — Ferramentas, Defeso e Guias

**Status:** núcleo no ar.

1. **Biblioteca de nós e montagens** offline, com oito nós e diagramas esquemáticos.
2. **Defeso por UF** com órgão oficial, checklist e lembrete no registro.
3. **Checklists** por modalidade, salvas no aparelho.
4. **Diretório de guias** em curadoria, com roteiro de escolha e contato.

**Próximo:** ampliar a biblioteca de nós com fotos/vídeos, adicionar espécies/regras específicas
quando houver fonte oficial estável e abrir o diretório de guias com perfis verificados.

## Dados e arquitetura

| Camada | Escolha atual | Evolução |
| --- | --- | --- |
| Clima, vento, ondas, corrente e nível do mar | Open-Meteo (grátis) | Plano comercial se o Fisga cobrar |
| Lua/solunar | MET Norway + cálculo local | Mantém |
| Maré oficial | Aproximada (modelo) | CHM/Marinha para 44 portos |
| Mapas | Leaflet + OSM/Esri/OpenSeaMap/GEBCO | MapLibre/Mapbox se precisar de vetor e offline |
| Geoespacial | latitude/longitude | PostgreSQL + PostGIS para raio, bacia e clustering |
| Offline | PWA + service worker + localStorage | IndexedDB para fila de pescarias e tiles |
| IA de espécie | Porta plugável com fallback manual | Gemini ou outro provedor, com confirmação humana |

## Sequência recomendada

1. **Agora:** cockpit + mapa privado (entregue).
2. **Sprint 4:** EXIF + defeso + ferramentas + trilhas (entregue; IA depende de provedor).
3. **Sprint 5:** PostGIS + inteligência coletiva anonimizada + mapas offline por área.
4. **Sprint 6:** guias verificados, alertas e ampliação da biblioteca de nós.

## Métricas

- Tempo até a primeira resposta útil (“vale a pena sair?”).
- Locais salvos por usuário ativo e retorno semanal.
- Pescarias registradas com condições preenchidas.
- Taxa de confirmação/correção da sugestão de espécie.
- Uso offline e sincronizações pendentes.
- Retenção em 30 dias e pescarias por usuário.
