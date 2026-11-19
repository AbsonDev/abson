# Especificação — Comunidade do Pescador (19/11/2026)

## Intenção

Transformar o Fisga num lugar onde o pescador encontra gente como ele: compartilha capturas, faz perguntas e recebe respostas de quem pesca na mesma realidade. É o que torna a landing dinâmica e cria o motivo de voltar — além de ser o motor de GEO que a Wiki sozinha não entrega.

Inspiração: Reddit como **estrutura** (comunidade como unidade, tipos de post livres, flairs, regras visíveis, remoção com motivo), sem copiar a identidade visual. Ver `docs/referencias/reddit.md` (a escrever).

## Goals

1. Feed público de posts de pesca — texto, imagem e link — com títulos em forma de pergunta.
2. Landing `/` mostra 3–5 posts **renderizados no servidor**; `/comunidade` tem o feed completo com ordenação.
3. Comentários em árvore, flairs (Espécie, Técnica, Dúvida, Equipamento, Relato) e busca por assunto/região.
4. Composer livre com aviso de local aproximado e escolha explícita — nunca edição silenciosa, nunca bloqueio.
5. Moderação leve: regras curtas e numeradas, denúncia, remoção com motivo visível ao autor.

## Non-goals

Mensagens privadas, karma/gamificação, chat ao vivo, mods externos, crosspost, upload de vídeo, geolocalização exata, edição automática de texto, substituir `/hoje`.

## Anti-goals

Feed vazio com usuário falso; spot burning; conselho de defeso/espécie sem fonte passando como fato; posts renderizados só no cliente (perdem citação de IA).

## Constraints

Home já é SSR (`prerender=false`); API .NET 10 + Postgres; design system Fisga (sem cor nova, 44 px de toque, foco visível, copy pt-BR, "privado por padrão"); `/termos` e `/privacidade` já existem; `sitemap.xml`/`robots.txt`/helpers de JSON-LD já existem; spec de níveis de local é pré-requisito.

## Abordagens consideradas

- **Feed na landing + `/comunidade` (escolhida).** Dá dinamismo a quem chega e preserva o cockpit.
- **Feed substituindo `/hoje`.** Mais "Reddit", mas desloca "Vale a pena sair hoje?" — o motivo central do produto.
- **Comunidade em domínio separado.** Mais liberdade, mas perde o vínculo com diário, condições e o domínio que já tem autoridade.
- **Postagem:** aviso + escolha (escolhida) vs. edição automática (quebra confiança) vs. bloqueio (mata a proposta livre).

## Decisões

- Sem score público de upvotes no MVP: ordenar por *Novos*, *Mais comentados* e *Destaques*. Upvotes são sinal fraco para citação de IA e criam jogo de popularidade; comentários são o que importa.
- `/hoje` continua sendo o cockpit; o feed entra na landing e na rota própria, com bloco "Da comunidade perto de você" no fim do cockpit e atalho na navegação.
- Seed com perguntas reais derivadas da Wiki, flair "Time Fisga" e rótulo honesto ("A comunidade está começando; estas vieram da Wiki do Pescador"). Nenhum usuário falso; "Time Fisga" é conta oficial do sistema.
- EXIF de GPS removido no upload, sempre — é metadado, não conteúdo.
- Posts públicos e crawláveis, com `DiscussionForumPosting` + `BreadcrumbList`. Sem `QAPage` (exige resposta aceita, que não existe no MVP).
- Regras numeradas em três lugares: sidebar, composer e mensagem de remoção.
- Moderação inicial feita por nós: denúncia + remoção com motivo. Sem AutoMod ainda.
- Postar exige conta; byline nunca deriva do prefixo do e-mail. Exigir handle público.
- Exclusão de conta: anonimizar autor como "Pescador apagado"; a thread sobrevive.
- Termos v2 com re-aceite; assumir a interrupção temporária do `/api` até o aceite.

## Pré-requisito

`2026-11-19-niveis-de-detalhe-do-local-design.md`.

## Estado

Spec fechada; plano de implementação a expandir para passos TDD após o merge do pré-requisito. Draft privado no quadro: "Comunidade do Pescador: feed de capturas e perguntas (estilo Reddit)" — https://github.com/users/AbsonDev/projects/6
