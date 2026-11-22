---
operacao: enviarMidia
proposta: >
  Recebe uma foto e devolve o endereço público do arquivo, para usar em
  `fotoUrl` e `fotos` de uma captura.
acesso: sessao
arquivos:
  arquivo: foto.jpg
verificar: >
  O corpo traz `url` começando com `/media/`. Esse caminho é o que
  `atualizarPescaria` aceita em `fotoUrl`; qualquer outro endereço cai em
  `foto_url_invalida`.
relacionados: [atualizarPescaria, sugerirEspecies]
erros:
  - status: 400
    codigos: [midia_sem_formulario, midia_vazia, midia_grande, midia_tipo_invalido]
    quando: >
      O corpo não é multipart; o campo `arquivo` falta ou está vazio; o arquivo
      passa de 5 MB; ou o tipo não é JPG, PNG ou WebP.
---
O tipo é decidido pelo `Content-Type` declarado no multipart, não pela extensão.
O campo do formulário precisa se chamar `arquivo`.
