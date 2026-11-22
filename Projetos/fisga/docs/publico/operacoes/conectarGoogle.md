---
operacao: conectarGoogle
proposta: >
  Começa o vínculo entre a conta com sessão e uma conta Google. Grava um cookie
  de intenção e manda o navegador para a tela de consentimento.
acesso: sessao
verificar: >
  O `302` traz `Location` para o Google e um `Set-Cookie` com `fisga_google_intencao`.
  Sem credenciais do Google, a resposta é `503` com `codigo` `google_indisponivel`.
relacionados: [concluirConexaoGoogle, obterSessao]
erros:
  - status: 503
    codigos: [google_indisponivel]
    quando: >
      As credenciais do Google não estão configuradas. A checagem vem antes da
      sessão, então até chamada sem cookie recebe `503`, não `401`.
---
O vínculo só se completa em `concluirConexaoGoogle`. Se o cookie de intenção se
perder no caminho, o retorno é `estado=google_intencao_invalida` e o processo
recomeça.
