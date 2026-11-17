# Referência — Tábua de Marés (tabuademares.com)

**Estudado em:** 17/11/2026 · **URL:** `https://tabuademares.com/br/sergipe/aracaju`
· **Prints:** `/tmp/tabua-*.png` (desktop 1440 e mobile 390).

## O que é

Portal de marés e solunar para pesca, com cobertura global e app próprio (Nautide). Cada cidade
tem uma página com tempo, temperatura da água, ondas, marés, solunar, lugares de pesca e previsão
semanal. Monetização por anúncios e pelo app. Visual de portal de 2010, mas conteúdo funcional e
muito completo.

## Módulos da página

| Módulo | O que entrega |
| --- | --- |
| **O tempo** | Nebulosidade, chuva, vento/rajada/direção, temperatura/máx/mín/sensação, umidade, orvalho, visibilidade e pressão com barômetro interpretado (“muito bom / bom / mau para a pesca”) |
| **Temperatura da água** | Atual, média do dia e evolução horária, com aviso de que o algoritmo está em desenvolvimento |
| **Ondas** | Direção, altura significativa, período, vento costa/mar e gráfico |
| **Marés** | Estado atual (“subindo/descendo”), próxima preamar/baixa-mar com contagem regressiva, dial, curva e 4 marés do dia com altura |
| **Coeficiente** | Coeficiente da manhã/tarde, amplitude, altura máxima/mínima e evolução mensal |
| **Tabela de marés** | Mês inteiro, 4 marés por dia, coeficiente e atividade; impressão |
| **Solunar** | Saída/pôr da lua, trânsito lunar, períodos maiores/menores, atividade (peixes), fase/idade/iluminação e próximas fases |
| **Lugares** | Mapa e lugares próximos com distância (ex.: Aruana 12 km, Pirambu 30 km) |
| **Previsão semanal** | Subpáginas de marés, pescaria, sol, lua, UV, chuva, temperatura, umidade, visibilidade, pressão, vento, ondas e temperatura da água |
| **Conteúdo educativo** | Como se formam as marés, coeficiente, correntezas, marés nos rios, teoria solunar |

## O que funciona (princípio)

1. **Resposta antes do dado.** “O nível está descendo; a baixa-mar será em 4 h 06 min.” O usuário
   entende a situação sem interpretar gráfico.
2. **Três níveis de detalhe.** Dial (agora), curva (dia) e tabela (mês). O mesmo dado para decisões
   diferentes.
3. **Tabela de 4 marés com setas e cores.** Preamar/baixa-mar, hora e altura em uma linha. No
   celular continua legível.
4. **Coeficiente + atividade.** Explica *por que* o dia é bom ou ruim, não só quando.
5. **Solunar com motivo.** “Período maior 4:27–6:27, coincide com o amanhecer” — combina lua e sol
   em vez de despejar horários.
6. **Pressão interpretada.** Barômetro com três estados e o efeito esperado na atividade dos peixes.
7. **Tabela mensal + impressão.** Planejamento de viagem e uso offline.
8. **Aviso de limite.** “Não são válidas para navegação.” Honestidade que protege o produto.
9. **SEO profundo.** País → estado → cidade → mês → previsão, com lugares próximos e distância.

## O que evitar (anti-padrão)

- **Visual de 2010:** gradientes azuis, bevel, blocos amarelos/rosa, menus laterais, fontes
  pequenas.
- **Anúncios em excesso:** vídeo, sticky footer, in-content; no celular o primeiro fold é anúncio e
  instalação de app, não a maré.
- **App install antes do conteúdo.**
- **Texto repetido:** cada seção repete o que o gráfico já mostra.
- **Botões de compartilhamento por seção** (ruído).
- **Termos de Portugal:** “preia-mar” e “coeficiente de marés”. No Brasil: **preamar**,
  **baixa-mar**, **amplitude**.
- **CLS alto e dezenas de trackers** (a própria telemetria do site registra CLS ~0,39).

## De onde vêm os dados (importante)

A página explica o método: previsão por **análise harmônica** a partir de séries de marégrafos,
ajuste por mínimos quadrados e algoritmo de Foreman; portos secundários são corrigidos a partir de
um porto de referência. Ou seja: **maré boa não sai de um modelo genérico de oceano**; sai de
constantes harmônicas locais.

### Fontes possíveis para o Fisga

| Fonte | Cobertura | Custo | Precisão | Observação |
| --- | --- | --- | --- | --- |
| **CHM / Marinha do Brasil** | 44 portos, 2 ilhas oceânicas, 2 costeiras, 5 barras | Gratuito | Oficial | PDFs anuais; constantes harmônicas via BNDO; uso prioritário científico/pesquisa — confirmar termos para app gratuito |
| **Open-Meteo Marine** | Global | Grátis até 10 mil chamadas/dia (não comercial); planos pagos | Aproximada | Expõe `sea_level_height_msl` (modelo Copernicus/SMOC); sem extremos prontos |
| **Stormglass** | Global | €19/mês (500 req/dia) a €49/mês (comercial) | Aproximada | Extremos de maré; modelo global |
| **WorldTides** | Global | Créditos | Aproximada | Extremos e curva por lat/lon |

### Teste real: Aracaju, 16–18/11/2026

Comparação entre a tábua oficial e a curva da Open-Meteo, com deslocamento de datum de +0,95 m e
extremos interpolados:

| Dia | Oficial (CHM) | Open-Meteo + 0,95 m | Erro de horário |
| --- | --- | --- | --- |
| 16/11 | 2:26 0,7 · 9:05 1,5 · 14:54 0,8 · 21:17 1,6 | 1:36 0,69 · 7:37 1,43 · 13:45 0,85 · 19:56 1,54 | ~50–88 min adiantado |
| 17/11 | 3:41 0,7 · 10:20 1,5 · 16:15 0,8 · 22:34 1,6 | 2:43 0,69 · 9:07 1,43 · 15:00 0,86 · 21:15 1,55 | ~58–79 min adiantado |
| 18/11 | 5:00 0,7 · 11:26 1,6 · 17:32 0,8 · 23:41 1,7 | 4:00 0,64 · 10:23 1,50 · 16:22 0,79 · 22:20 1,61 | ~60–81 min adiantado |

Conclusão: as **alturas** ficam próximas (±0,1 m) depois do ajuste de datum, mas os **horários**
ficam ~70 min adiantados. Um deslocamento fixo por porto reduziria o erro para ~20 min — suficiente
para “maré aproximada”, insuficiente para vender precisão.

## Tradução para o Fisga

### Tela “Condições” (pública, sem login)

1. **Agora:** “Maré descendo · baixa às 16:15” + próxima janela solunar + vento/chuva.
2. **Janela de hoje:** bloco único com o melhor horário e o motivo (“maré enchendo + fim de tarde +
   vento fraco”).
3. **Próximos 7 dias:** faixa com nota de 1–5 e o melhor horário de cada dia.
4. **Maré:** dial + curva + 4 marés do dia; tabela mensal e impressão.
5. **Solunar:** períodos maiores/menores, fase e iluminação.
6. **Tempo:** vento/rajada, chuva, pressão com tendência, temperatura e água.
7. **Fonte e limite:** “Dados oficiais do CHM (Porto de Aracaju)” ou “Maré aproximada (modelo)” +
   “não use para navegação”.

### Regras de dados

- Fuso explícito (`America/Sao_Paulo`) e horário local.
- Datum explícito (nível de redução) quando vier do CHM.
- **Nunca** apresentar modelo como oficial.
- Local costeiro sem porto CHM: maré aproximada + edição manual.
- Local interior: sem maré, só vento/chuva/lua/solunar.
- Cache por porto/mês; previsão é determinística.
- Maré oficial: importar a tábua anual e validar contra a fonte.
- Vocabulário brasileiro: preamar, baixa-mar, amplitude, sizígia, quadratura.

### Decisão pendente

1. Confirmar com o CHM/BNDO se o uso dos PDFs e das constantes é permitido em app gratuito com
   atribuição.
2. Se não for: MVP com Open-Meteo + calibração por porto, rótulo “aproximada” e correção manual.
3. Não fazer scraping do tabuademares.com; a base é proprietária e não há API pública.

## Limites deste estudo

- Não testamos o app Nautide nem o fluxo de assinatura.
- A comparação cobre um porto e três dias; precisa de validação em mais portos e datas.
- O site muda com frequência; este documento vale para a data acima.
