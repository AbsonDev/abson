---
operacao: registrarConsentimento
proposta: >
  Registra o aceite dos termos de uso e da política de privacidade nas versões
  vigentes, liberando as demais rotas de sessão da conta.
acesso: sessao
corpo: |
  {"termosVersao":"2026-11-17","privacidadeVersao":"2026-11-17"}
verificar: >
  O `204` não tem corpo. Depois dele, `obterSessao` precisa trazer
  `consentimentoPendente: false`.
relacionados: [versoesLegais, obterSessao]
erros:
  - status: 400
    codigos: [consentimento_versao_invalida]
    quando: >
      Uma das versões enviadas não é exatamente a vigente, ou vem vazia. Copie os
      valores de `versoesLegais`, não a data de hoje.
---
É a rota que libera as demais: até o aceite, as outras rotas de dados respondem
`403` com `codigo` `consentimento_pendente`. `sair` e `obterSessao` continuam
acessíveis para o usuário encerrar ou consultar a sessão.
