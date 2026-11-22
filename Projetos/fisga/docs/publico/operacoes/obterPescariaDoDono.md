---
operacao: obterPescariaDoDono
proposta: >
  Carrega a pescaria completa para edição, com as observações e o local exato
  como o dono registrou.
acesso: dono
caminho:
  id: 00000000-0000-4000-8000-000000000001
verificar: >
  O corpo é o mesmo de `atualizarPescaria`: `entrada` com os campos privados,
  `capturas`, `criadoEm`, `atualizadoEm` e `podeEditar: true`.
relacionados: [atualizarPescaria, obterPescariaPorToken]
erros:
  - status: 404
    codigos: [pescaria_nao_encontrada]
    quando: >
      A pescaria não existe ou pertence a outra conta. A resposta é a mesma nos
      dois casos, de propósito: não revela pescaria de outra pessoa.
---
Devolve o local exato mesmo quando `privacidadeDoLocal` é `Oculto`; a leitura
pública esconde esse dado. Use o `shareToken` do corpo para montar o link.
