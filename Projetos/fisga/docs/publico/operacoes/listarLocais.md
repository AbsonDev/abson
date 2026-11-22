---
operacao: listarLocais
proposta: >
  Lista os locais salvos da conta, com o local padrão na frente e o restante do
  mais recente para o mais antigo.
acesso: sessao
verificar: >
  O corpo é uma lista de locais. O primeiro item com `padrao: true` é o que a
  interface deve pré-selecionar. Conta sem local recebe lista vazia.
relacionados: [criarLocal, definirLocalPadrao]
---
A ordem é estável: padrão primeiro, depois `atualizadoEm` decrescente. No máximo
um item traz `padrao: true`.
