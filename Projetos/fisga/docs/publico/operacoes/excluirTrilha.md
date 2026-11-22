---
operacao: excluirTrilha
proposta: >
  Apaga uma trilha privada da conta. O traçado some do mapa pessoal sem mexer nas
  pescarias.
acesso: dono
caminho:
  id: 00000000-0000-4000-8000-000000000001
verificar: >
  O `204` não tem corpo e `listarTrilhas` deixa de trazer o `id`. Repetir a
  chamada com o mesmo `id` devolve `404`.
relacionados: [listarTrilhas, atualizarTrilha]
erros:
  - status: 404
    codigos: [trilha_nao_encontrada]
    quando: >
      O `id` não corresponde a nenhuma trilha. Trilha de outra conta responde
      `403`, não `404`.
---
Trilhas são privadas por definição: não há token de compartilhamento e nenhuma
outra conta consegue lê-las. A exclusão não tem desfazer.
