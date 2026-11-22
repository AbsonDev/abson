---
operacao: confirmarLink
proposta: >
  Consome o token do link de acesso, abre a sessão e redireciona o navegador para
  a próxima tela. É uma chamada de navegador, não uma API de consulta.
acesso: capacidade
consulta:
  token: tokenDeExemplo
verificar: >
  O `302` traz `Location` para `/boas-vindas?novo=1` no primeiro acesso, para
  `/entrar/confirmar?estado=ok` nos seguintes e para
  `/entrar/confirmar?estado=<codigo>` quando o link falha. O cookie de sessão vai
  no mesmo `Set-Cookie`.
relacionados: [solicitarLink, obterSessao]
---
O token é de uso único: recarregar o link abre a página de erro, não a sessão. A
falha chega como `302` com o código na URL, nunca como `4xx`.
