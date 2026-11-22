---
operacao: obterProntidao
proposta: >
  Confirma que a API consegue falar com o banco e está pronta para atender.
acesso: publico
verificar: >
  O corpo traz `status` igual a `ready`. O `503` sai sem corpo, então monitore o
  código, não o JSON.
relacionados: [obterSaude]
---
Cada chamada abre uma conexão com o banco. Para monitoramento de alta frequência,
use `obterSaude`, que não toca no banco.
