---
operacao: alterarSenha
proposta: >
  Define ou troca a senha da conta. Quem já tem senha precisa informar a atual;
  quem entrou por link ou Google define a primeira sem ela.
acesso: sessao
corpo: |
  {"senhaAtual":"senha-de-exemplo","novaSenha":"outra-senha-de-exemplo"}
verificar: >
  O `204` não tem corpo e traz o `Set-Cookie` renovado da sessão atual. As demais
  sessões da conta somem de `listarSessoes`, e a sessão que fez a troca continua.
relacionados: [listarSessoes, sairDeTodos]
erros:
  - status: 400
    codigos: [senha_curta, senha_longa, senha_atual_incorreta, senha_invalida]
    quando: >
      A `novaSenha` fica fora de 10 a 128 caracteres; a `senhaAtual` não confere
      com a cadastrada; ou o provedor de identidade recusou a gravação.
---
`senhaAtual` é obrigatória para quem já tem senha: vazia ou ausente devolve
`senha_atual_incorreta`. Para conta sem senha, o campo é ignorado.
