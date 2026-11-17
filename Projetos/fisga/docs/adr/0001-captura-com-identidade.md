# ADR 0001 — Captura com identidade própria

**Status:** aceito · **Data:** 2026-11-17

## Contexto

O troféu tem link próprio (`/trofeu/{pescaria}/{captura}`). Se a captura fosse apenas um item de
coleção sem ID, editar a pescaria poderia trocar a ordem e quebrar links já compartilhados. Também
precisamos de um alvo estável para foto, medidas e PDF.

## Decisão

- `Captura` é uma entidade do agregado `Pescaria`, com `Guid Id` explícito gerado no domínio.
- A edição recebe IDs opcionais: IDs existentes são atualizados; IDs desconhecidos são rejeitados;
  capturas sem ID são criadas; capturas ausentes são removidas.
- O link do troféu usa `shareToken` da pescaria + `capturaId`.

## Consequências

- Links de troféu continuam funcionando após editar medidas/fotos.
- O agregado controla a consistência das capturas.
- Excluir uma captura remove seu troféu, naturalmente.
