---
operacao: sugerirEspecies
proposta: >
  Analisa uma foto e sugere até três espécies de peixe, da mais provável para a
  menos provável, com um grau de confiança em cada uma.
acesso: sessao
arquivos:
  arquivo: foto.jpg
verificar: >
  O corpo traz `disponivel`, `mensagem` e `sugestoes`. `disponivel: false`
  significa que o reconhecimento não está configurado: a resposta é `200` com
  lista vazia, não erro.
relacionados: [enviarMidia, atualizarPescaria]
erros:
  - status: 400
    codigos: [sugestao_sem_formulario, sugestao_sem_foto, sugestao_foto_grande, sugestao_tipo_invalido]
    quando: >
      O corpo não é multipart; o campo `arquivo` falta ou está vazio; a foto passa
      de 5 MB; ou o tipo não é JPG, PNG ou WebP.
---
A sugestão é um palpite para o usuário confirmar, não um dado de pescaria: nada é
gravado. Use `enviarMidia` para guardar a foto e a sugestão em seguida.
