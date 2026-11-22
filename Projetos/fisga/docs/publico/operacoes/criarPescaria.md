---
operacao: criarPescaria
proposta: >
  Registra uma pescaria no diário da conta e devolve o identificador, o token
  público e o endereço da página de compartilhamento.
acesso: sessao
corpo: |
  {"local":"Represa de Guarapiranga","cidade":"São Paulo","estado":"SP","ambiente":"Represa","data":"2026-11-14","periodo":"Manha","mare":"Vazante","privacidadeDoLocal":"Cidade"}
verificar: >
  A resposta é `201` com `id`, `shareToken` e `url`; o header `Location` aponta
  para `/api/pescarias/{shareToken}`. Abrir esse `url` no navegador já mostra a
  pescaria.
relacionados: [obterPescariaPorToken, atualizarPescaria, enviarMidia]
erros:
  - status: 400
    codigos: [pescaria_sem_local, local_longo, pescador_longo, cidade_longa, corpo_ou_trecho_longo, estado_invalido, data_invalida, ambiente_invalido, periodo_invalido, condicao_invalida, observacoes_longas, privacidade_local_invalida, muitas_capturas, captura_inexistente, captura_sem_especie, especie_longa, captura_peso_invalido, captura_comprimento_invalido, captura_sem_medida, isca_longa, observacao_captura_longa, muitas_fotos, foto_url_longa, foto_url_invalida]
    quando: >
      Algum campo do corpo não passou no domínio. Os casos mais comuns: `local`
      vazio ou acima de 120 caracteres; `data` ausente; `ambiente` ou `periodo`
      fora do enum; observações acima de 1.000 caracteres; mais de 100 capturas;
      captura sem espécie ou sem peso e comprimento. O `codigo` diz qual campo.
---
O `shareToken` nasce com a pescaria e é a credencial do link público; não há
como trocá-lo depois. O exemplo não manda `capturas`: na criação, captura com
`id` preenchido cai em `captura_inexistente`, então o caminho é criar a pescaria
e acrescentar as capturas em `atualizarPescaria`.
