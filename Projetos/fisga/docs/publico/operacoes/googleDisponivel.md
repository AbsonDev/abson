---
operacao: googleDisponivel
proposta: >
  Diz se o login com Google está configurado nesta instalação, sem iniciar o
  fluxo de autenticação.
acesso: publico
verificar: >
  O corpo traz `disponivel`. Quando é `false`, `iniciarLoginGoogle` responde
  `503` e o botão de login com Google deve ficar escondido na interface.
relacionados: [iniciarLoginGoogle]
---
Serve para a interface decidir se mostra o botão do Google antes de o usuário
clicar. A configuração vem do ambiente, então o valor pode mudar de uma
publicação para outra.
