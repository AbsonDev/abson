---
operacao: concluirConexaoGoogle
proposta: >
  Fecha o vínculo com a conta Google a partir do retorno do provedor. Cria só o
  vínculo, sem trocar de usuário nem emitir cookie de sessão.
acesso: sessao
verificar: >
  O `302` traz `Location` para `/conta?estado=google_conectado` no sucesso e para
  `/conta?estado=<codigo>` nas falhas, inclusive
  `google_conexao_nao_autenticado` quando o cookie de sessão não chegou.
relacionados: [conectarGoogle, obterSessao]
---
O cookie `fisga_google_intencao` é apagado em todos os desfechos, sucesso ou
falha: uma segunda tentativa sempre recomeça em `conectarGoogle`.
