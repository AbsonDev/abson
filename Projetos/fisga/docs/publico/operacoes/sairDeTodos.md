---
operacao: sairDeTodos
proposta: >
  Revoga todas as sessões da conta, inclusive a atual, e apaga o cookie do
  dispositivo que fez a chamada.
acesso: sessao
verificar: >
  O `204` não tem corpo. Os outros dispositivos perdem acesso na chamada
  seguinte; confirme com `obterSessao`, que precisa voltar `autenticado: false`.
relacionados: [sair, listarSessoes]
---
Diferente de `sair`, esta rota exige sessão válida: sem cookie ou com cookie
expirado a resposta é `401`. Ela também rotaciona o carimbo de segurança da
conta, então cookies antigos deixam de autenticar mesmo antes de expirarem.
