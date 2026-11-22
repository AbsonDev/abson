---
operacao: excluirPonto
proposta: >
  Apaga um ponto privado da conta. O ponto sai do mapa e da lista sem afetar as
  pescarias já registradas.
acesso: dono
caminho:
  id: 00000000-0000-4000-8000-000000000001
verificar: >
  O `204` não tem corpo e `listarPontos` deixa de trazer o `id`. Excluir de novo o
  mesmo `id` passa a devolver `404`.
relacionados: [listarPontos, atualizarPonto]
erros:
  - status: 404
    codigos: [ponto_nao_encontrado]
    quando: >
      O `id` não corresponde a nenhum ponto. Ponto de outra conta responde `403`,
      não `404`.
---
A exclusão é definitiva e não há lixeira. Nenhuma pescaria guarda o `id` do
ponto: a ligação entre os dois é só geográfica, então o registro da pescaria
continua intacto.
