---
operacao: excluirConta
proposta: >
  Apaga a conta, as pescarias e as fotos, encerra todas as sessões e responde
  `204`. A exclusão é imediata e não tem desfazer.
acesso: sessao
corpo: |
  {"confirmacao":"EXCLUIR"}
verificar: >
  O `204` não tem corpo e a resposta apaga o cookie de sessão. A conta deixa de
  existir: `entrarComSenha` passa a devolver `autenticado: false` e o e-mail fica
  livre para um novo cadastro.
relacionados: [exportarConta, sair]
erros:
  - status: 400
    codigos: [confirmacao_invalida]
    quando: >
      A `confirmacao` não é exatamente `EXCLUIR` em maiúsculas. Ausente, vazia ou
      com outra caixa devolve este código.
---
Exporte os dados antes: a exclusão remove também as fotos do armazenamento, sem
cópia de retenção acessível pela API.
