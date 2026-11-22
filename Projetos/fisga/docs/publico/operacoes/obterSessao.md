---
operacao: obterSessao
proposta: >
  Descreve a sessão atual: se existe, de quem é, se falta aceitar os termos e se
  a conta tem senha e Google vinculados.
acesso: publico
verificar: >
  Sem cookie, o corpo traz `autenticado: false` e `usuario: null`. Com cookie,
  traz `usuario` preenchido, `consentimentoPendente`, `temSenha` e
  `googleConectado`.
relacionados: [entrarComSenha, registrarConsentimento]
---
Ausência de sessão aqui é `200`, não `401`. É a chamada certa para saber se o
visitante já pode usar as rotas de sessão antes de tentar qualquer uma delas.
