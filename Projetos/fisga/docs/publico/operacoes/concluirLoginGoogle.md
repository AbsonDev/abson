---
operacao: concluirLoginGoogle
proposta: >
  Recebe o retorno do Google, valida a identidade e cria a sessão do Fisga,
  redirecionando o navegador para a próxima tela.
acesso: publico
verificar: >
  O `302` traz `Location` para `/boas-vindas?novo=1` no primeiro acesso, para
  `/entrar/confirmar?estado=ok` nos seguintes e para
  `/entrar/confirmar?estado=<codigo>` quando a validação falha. O cookie de
  sessão vai no mesmo `Set-Cookie`.
relacionados: [iniciarLoginGoogle, obterSessao]
---
O Google redireciona para cá: não chame esta rota direto. Sem o cookie externo
assinado pelo Google, o destino é `estado=google_sessao_externa_invalida`.
