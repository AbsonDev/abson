---
operacao: sair
proposta: >
  Encerra a sessão do dispositivo atual. Com cookie, revoga a linha da sessão e
  apaga o cookie; sem cookie, não faz nada.
acesso: publico
verificar: >
  O `204` não tem corpo. Confira o `Set-Cookie` de remoção e, se quiser certeza,
  chame `obterSessao` depois: o corpo precisa voltar com `autenticado: false`.
relacionados: [sairDeTodos, obterSessao]
---
Chamar sem cookie também devolve `204`. A operação não erra por falta de sessão,
para que a interface possa encerrar o estado local sem tratar exceção.
