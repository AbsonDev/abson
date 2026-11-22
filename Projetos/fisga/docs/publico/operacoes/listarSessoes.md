---
operacao: listarSessoes
proposta: >
  Lista os dispositivos com sessão aberta na conta, do uso mais recente para o
  mais antigo, e marca qual é o que está chamando.
acesso: sessao
verificar: >
  O corpo é uma lista de sessões. Cada item traz `id`, `userAgent`, `criadoEm`,
  `ultimoUsoEm`, `expiraEm` e `atual`; exatamente um item tem `atual: true`.
relacionados: [sairDeTodos, alterarSenha]
---
Sessões revogadas ou expiradas não aparecem. O `userAgent` ajuda a reconhecer o
dispositivo, mas pode vir `null` quando o cliente não manda o header.
