---
operacao: excluirLocal
proposta: >
  Apaga um local salvo. Se ele era o padrão, o local atualizado mais recentemente
  assume o posto.
acesso: dono
caminho:
  id: 00000000-0000-4000-8000-000000000001
verificar: >
  O `204` não tem corpo. `listarLocais` deixa de trazer o `id` e, quando o local
  era o padrão, mostra outro com `padrao: true`.
relacionados: [listarLocais, definirLocalPadrao]
erros:
  - status: 404
    codigos: [local_nao_encontrado]
    quando: >
      O `id` não corresponde a nenhum local. Local de outra conta responde `403`,
      não `404`.
---
A promoção do próximo padrão vale só para o local padrão. Excluir um local
comum não mexe na escolha do padrão.
