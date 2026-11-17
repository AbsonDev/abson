# Referência — Fishing World Guide

**Estudado em:** 17/11/2026 · **URLs:** `https://www.fishingworldguide.com/pt/brasil`,
`/pt/fish`, `/pt/fish/tucunare`, `/pt/solunar`, `/pt/catches`, `/pt/license-comparison`, `/pt/trips`
· **Prints:** `/tmp/fwg-*.png` (desktop 1440 e mobile 390).

## O que é

Diretório global de pesca esportiva: 203 países, 194 espécies, técnicas, tipos de água, calendário
solunar, comparação de licenças, viagens/charters e um feed de capturas. Conteúdo em vários idiomas
(tradução automática), monetizado com afiliados (FishingBooker, GetYourGuide, Amazon, Kiwi, Saily) e
anúncios (Mediavine).

## Inventário por rota

| Rota | O que entrega | Padrões interessantes | Problemas observados |
| --- | --- | --- | --- |
| `/pt/brasil` | Hub do país: veredito, época, licença, spots, espécies, capturas, FAQ | Resposta em 10 s no topo; números-chave; spots com mapa; “quatro coisas a saber”; solunar grátis | Copy traduzida, generalizações, afiliados |
| `/pt/fish` | Lista de 194 espécies | Busca + filtro por país/ordem/tamanho; linha editorial com ilustração, nome, água, tamanho e nº de países; CTA “identifica um peixe a partir de uma foto” acima da busca | Zero filtro por água/troféu/iniciante apesar do texto prometer; 194 imagens AVIF sem lazy agressivo; linha muito alta no mobile |
| `/pt/fish/tucunare` | Ficha de espécie | Fatos-chave em tabela; prosa curta; chips de técnica; chips de país; **estatísticas de capturas** (horário, sazonalidade, tamanho, lua); tours e equipamento | Estatísticas com N=4 (gráficos vazios); dados duvidosos (recorde, “comestível”); tradução literal |
| `/pt/solunar` | Calendário de pesca | “Melhores dias” com estrelas; **relógio de 24 h** com períodos principais/secundários, dia/noite, lua e próximo horário de mordida; geolocalização; abas de mês; países por continente | Depende de localização aproximada; sem maré; copy densa |
| `/pt/catches` | Feed de capturas | Cards com foto, espécie, medida, local, data, selo “solto”; mapa/bússola/ranking/insights; aviso honesto de capturas de demonstração | Cold start resolvido com dados de IA (rotulados); 18 relatos reais; imagens quebradas/ambíguas |
| `/pt/license-comparison` | Licenças de 203 países | Busca + chips “sem licença/simples/complexo”; tabela com status colorido, obrigatoriedade e link oficial | Tabela longa; sem contexto local (estados, rios, espécies) |
| `/pt/trips` | Marketplace de charters | Cards com preço “a partir de”, avaliações, capacidade, comprimento, chips de espécie | Imagens quebradas; dependência total de afiliado |

## O que funciona (princípio)

1. **Resposta antes do clique.** O hub do país começa com veredito, época, licença e custo. O usuário
   entende o valor em segundos, sem cadastro.
2. **Taxonomia forte.** País → espécie → técnica → água → época. Tudo se liga por links; cada página
   vira porta de entrada.
3. **Ilustração como identidade.** As espécies usam ilustrações consistentes em vez de fotos
   aleatórias. É bonito, leve e resolve o problema de licenciamento de imagem.
4. **Números com significado.** “100–170 cm · 55 países” comunica tamanho e alcance numa linha.
5. **Decisão, não dados.** O solunar não mostra só lua e hora: mostra “melhores dias” com estrelas e
   “próximo horário de mordida”. Transforma dado em recomendação.
6. **Estatísticas de capturas.** Horário, sazonalidade, tamanho e lua agregados por espécie — o tipo
   de conteúdo que só quem tem diário consegue produzir.
7. **App-like no mobile.** Barra inferior com Buscar, Horários, Publicar, Capturas, Perfil. Um site
   de conteúdo que se comporta como ferramenta.
8. **Transparência no cold start.** O feed avisa que as capturas de demonstração foram geradas por
   IA e não contam para ranking. Honestidade vira confiança.
9. **Tabela de licenças com filtros.** “Sem licença / simples / complexo” resolve uma pergunta cara
   em uma tela.
10. **Tipografia editorial.** Geist 300, títulos de 48–60 px, labels de 11 px em caixa alta,
    hairlines, fundo creme `#f5f0e8`, texto `rgba(0,0,0,.62)`. Sensação de revista, não de SaaS.

## O que evitar (anti-padrões)

- **Tradução automática sem revisão.** “Licenca”, “CALENDARIO”, “INSCREVER-SE”, “Quarto crescente”
  para 52% de iluminação. Erros assim destroem autoridade.
- **Conteúdo raso e repetido.** As fichas de espécie são curtas e genéricas; várias não respondem ao
  que o pescador brasileiro precisa (defeso, tamanho mínimo por UF, iscas locais).
- **Estatísticas com amostra minúscula.** Quatro capturas geram gráficos vazios e conclusões falsas.
- **Modal de saída agressivo.** “ANTES DE SAIR…” com blur na página inteira; parece desespero.
- **Anúncio no meio da leitura.** Bloco “PUBLICIDADE” quebra a ficha da espécie.
- **Imagens quebradas e dados inconsistentes.** “Truta-arco-íris 20 cm · 15 kg”; cards sem foto.
- **Afiliado antes de confiança.** Tours e Amazon aparecem antes de o site provar que entende o
  peixe e a região.
- **Prometer filtros que não existem.** O texto fala em “água doce/salgada, troféu, iniciantes”, mas
  a interface só tem busca, país e ordenação.
- **Ignorar o contexto local.** Licença por país não resolve defeso por estado, bacia ou espécie.

## O que o Fisga deve levar

| Padrão da referência | Tradução para o Fisga | Prioridade |
| --- | --- | --- |
| Resposta antes do clique | Abrir com “vale a pena sair hoje?” para o local salvo, sem login | Sprint 2 |
| Relógio solunar + melhores dias | “Janela de pesca” com maré, vento, chuva, lua e melhor horário | Sprint 2 |
| Estatísticas de capturas | “Seus padrões” por espécie, horário, lua, maré e isca (dados do próprio diário) | Já iniciado; evoluir |
| Taxonomia espécie → técnica → água → região | Wiki já tem; falta ligar condições e capturas a essas páginas | Sprint 2/3 |
| Ficha de espécie com fatos-chave | Já temos; adicionar tamanho mínimo/defeso por UF e “onde pescar” | Sprint 3 |
| CTA “identifica um peixe por foto” | ID por foto com confirmação humana, integrado ao formulário | Sprint 3 |
| Feed de capturas com selo “solto” | Mural público opt-in, começando pelos links já compartilhados | Sprint 4 |
| Transparência de cold start | Nunca inventar capturas; mostrar “seja o primeiro a registrar” | Sempre |
| App-like no mobile | PWA já existe; adicionar atalho “Condições” e “Registrar” | Sprint 2 |
| Tipografia editorial | Fisga já tem Fraunces + Inter; manter e não reprimir a identidade | Sempre |

## Decisões de produto sugeridas

1. **Condições como produto público.** `/condicoes/{local}` com maré, vento, chuva, lua, janela e
   “melhores dias”. Login só para salvar local e comparar com o histórico.
2. **Locais salvos.** 2–5 locais por usuário; abrir o app já mostra o local padrão e o botão
   “Registrar pescaria” pré-preenchido.
3. **Comparação com o histórico.** “Hoje está parecido com seu melhor dia de tucunaré” usando lua,
   maré, vento, clima e água. É o diferencial que a referência não tem.
4. **Nunca mostrar estatística de comunidade com N baixo.** Para o Fisga, o equivalente é o diário
   pessoal: com 1 captura, mostrar “primeiro registro”, não “melhor lua”.
5. **Defeso e tamanho mínimo por UF** como tabela filtrável, no espírito da comparação de licenças,
   mas com dados brasileiros e fonte oficial.
6. **ID por foto como entrada, não como autoridade.** Sugestão com confiança, link para a wiki e
   confirmação obrigatória.
7. **Sem feed público aberto no Sprint 2.** Primeiro condições e retenção; depois mural opt-in.

## Evidência visual

- Desktop: `/tmp/fwg-fish-desktop-1.png`, `/tmp/fwg-fish-tucunare-desktop-1.png`,
  `/tmp/fwg-solunar-desktop-1.png`, `/tmp/fwg-catches-desktop-1.png`,
  `/tmp/fwg-licenses-desktop-1.png`.
- Mobile: `/tmp/fwg-fish-mobile-1.png`, `/tmp/fwg-solunar-mobile-1.png`,
  `/tmp/fwg-catches-mobile-1.png`.
- Modal de saída (anti-padrão): `/tmp/fwg-fish-desktop-1.png` na primeira captura.

## Limites deste estudo

- Não testamos criação de conta, pagamento, compra de charter nem o fluxo de publicação.
- As estatísticas de capturas foram observadas, não validadas quanto à metodologia.
- O site muda com frequência; este documento vale para a data acima.
