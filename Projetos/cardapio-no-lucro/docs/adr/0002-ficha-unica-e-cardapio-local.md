# ADR 0002 — Uma ficha por cálculo e “Meu cardápio” local

**Status:** aceito · **Data:** 2026-11-16

## Contexto

A visão completa é um workspace com catálogo de ingredientes, várias fichas e recálculo de todo o
cardápio quando um fornecedor muda o preço. Isso exige conta, permissões e um modelo de
estabelecimento — complexidade que atrasaria a validação da conta principal.

O MVP precisa provar que o dono entende o CMV por canal e usa o PDF na cozinha.

## Decisão

- Cada cálculo é uma `FichaTecnica` independente, com seus ingredientes e canais.
- O “Meu cardápio” do MVP é uma lista local no `localStorage`, formada pelas fichas salvas naquele
  navegador. Não há listagem pública por estabelecimento.
- O link público continua funcionando por `shareToken`; o `editToken` fica no navegador.
- A ficha de exemplo e os canais padrão vêm de um catálogo supporting em memória.

## Consequências

- Validação rápida sem login e sem workspace multiusuário.
- O dono precisa guardar o link/token para editar em outro dispositivo (limitação explícita).
- Catálogo compartilhado, recálculo em lote e histórico entram na próxima fase, com migração para
  um agregado `Estabelecimento`/`Cardapio` e autenticação.
