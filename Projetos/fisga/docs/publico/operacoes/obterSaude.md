---
operacao: obterSaude
proposta: >
  Informa se a API está no ar e qual versão está publicada.
acesso: publico
verificar: >
  O corpo traz `status` igual a `ok` e `version` com a versão publicada.
relacionados: [obterProntidao]
---
Esta rota não consulta o banco nem o cache. É a primeira chamada de qualquer
monitoramento e a que menos custa ao servidor.
