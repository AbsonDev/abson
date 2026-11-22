---
operacao: atualizarPescaria
proposta: >
  Substitui os dados de uma pescaria existente e reconcilia a lista de capturas:
  o que vem com `id` é atualizado, o que vem sem `id` é criado e o que não vem é
  removido.
acesso: dono
caminho:
  id: 00000000-0000-4000-8000-000000000001
corpo: |
  {"local":"Represa de Guarapiranga","cidade":"São Paulo","estado":"SP","ambiente":"Represa","data":"2026-11-14","periodo":"Manha","mare":"Vazante","privacidadeDoLocal":"Cidade","capturas":[{"id":"00000000-0000-4000-8000-000000000002","especie":"Tucunaré","pesoKg":2.4,"comprimentoCm":47,"isca":"Hélice","solto":true,"fotoUrl":null,"observacao":null}]}
verificar: >
  O corpo traz a pescaria atualizada com `podeEditar: true` e `capturas` com a
  captura de exemplo. Contar os itens confirma a reconciliação.
relacionados: [obterPescariaDoDono, enviarMidia, obterPescariaPorToken]
erros:
  - status: 400
    codigos: [pescaria_sem_local, local_longo, pescador_longo, cidade_longa, corpo_ou_trecho_longo, estado_invalido, data_invalida, ambiente_invalido, periodo_invalido, condicao_invalida, observacoes_longas, privacidade_local_invalida, muitas_capturas, captura_inexistente, captura_duplicada, captura_sem_especie, especie_longa, captura_peso_invalido, captura_comprimento_invalido, captura_sem_medida, isca_longa, observacao_captura_longa, muitas_fotos, foto_url_longa, foto_url_invalida]
    quando: >
      Algum campo não passou no domínio. `captura_inexistente` é o caso comum de
      `id` de outra pescaria, e `captura_duplicada` aparece quando o mesmo `id`
      vem duas vezes. O `codigo` diz qual campo.
  - status: 404
    codigos: [pescaria_nao_encontrada]
    quando: >
      A pescaria não existe ou pertence a outra conta. A resposta é a mesma nos
      dois casos.
---
O corpo é o estado final, não um remendo: captura que existia e não veio na lista
é apagada. Para manter uma captura, mande o `id` dela junto com os campos; para
criar outra, omita o `id`.
