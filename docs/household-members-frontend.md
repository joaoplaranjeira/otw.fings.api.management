# Integração frontend — membros dos agregados familiares

Este documento descreve o contrato da API para listar os tipos de membro e gerir os utilizadores associados a um agregado familiar.

## Autenticação e formato

Todos os endpoints exigem o token JWT:

```http
Authorization: Bearer {token}
Accept: application/json
```

Pedidos com corpo devem também enviar `Content-Type: application/json`. Os enums são serializados como números.

## Papéis disponíveis

`GET /api/household-roles`

Resposta `200 OK`:

```json
[
  { "value": 1, "name": "Owner" },
  { "value": 2, "name": "Administrator" },
  { "value": 3, "name": "Member" },
  { "value": 4, "name": "Viewer" }
]
```

| Valor | Nome | Permissões |
|---:|---|---|
| `1` | `Owner` | Leitura, escrita e gestão de todos os membros |
| `2` | `Administrator` | Leitura, escrita e gestão de `Member` e `Viewer` |
| `3` | `Member` | Leitura e escrita, sem gestão de membros |
| `4` | `Viewer` | Apenas leitura |

O frontend deve obter esta lista da API para preencher seletores, sem duplicar os valores no código. Ao adicionar membros, um `Administrator` só pode selecionar `Member` ou `Viewer`.

## Listar membros

`GET /api/households/{householdId}/members`

Qualquer membro do agregado pode consultar a lista, incluindo um `Viewer`.

Resposta `200 OK`:

```json
[
  {
    "id": "f4d22575-3c32-4d3e-9bd7-0e415055aaba",
    "userId": 14,
    "name": "João Santos",
    "username": "joao.santos",
    "email": "joao@example.com",
    "isActive": true,
    "role": 1
  },
  {
    "id": "2441d946-a56b-497d-92d5-b801855b0be5",
    "userId": 27,
    "name": "Maria Santos",
    "username": "maria.santos",
    "email": "maria@example.com",
    "isActive": true,
    "role": 3
  }
]
```

O campo `id` identifica a associação entre o utilizador e o agregado. É este valor, e não `userId`, que deve ser enviado ao endpoint de remoção.

## Adicionar um membro

`POST /api/households/{householdId}/members`

Apenas `Owner` e `Administrator` podem executar esta operação. O utilizador identificado pelo email tem de estar previamente registado e ativo.

```json
{
  "email": "maria@example.com",
  "role": 3
}
```

Resposta `201 Created`:

```json
{
  "id": "2441d946-a56b-497d-92d5-b801855b0be5",
  "userId": 27,
  "name": "Maria Santos",
  "username": "maria.santos",
  "email": "maria@example.com",
  "isActive": true,
  "role": 3
}
```

Após sucesso, o frontend deve invalidar ou atualizar a query da lista de membros do agregado.

Erros relevantes:

| Estado | Situação |
|---:|---|
| `400` | Email inválido ou payload malformado |
| `403` | Sem permissão; inclui um administrador tentar atribuir `Owner` ou `Administrator` |
| `404` | Não existe um utilizador ativo com o email indicado |
| `409` | O utilizador já pertence ao agregado |
| `422` | O valor de `role` não corresponde a um papel suportado |

## Remover um membro

`DELETE /api/households/{householdId}/members/{memberId}`

Apenas `Owner` e `Administrator` podem executar esta operação. Um `Administrator` só pode remover utilizadores com os papéis `Member` ou `Viewer`. Nenhum utilizador pode remover a sua própria associação por este endpoint.

Resposta de sucesso: `204 No Content`, sem corpo.

Erros relevantes:

| Estado | Situação |
|---:|---|
| `403` | Sem permissão ou administrador a tentar remover `Owner`/`Administrator` |
| `404` | Associação não encontrada nesse agregado |
| `409` | A operação deixaria o agregado sem `Owner` |
| `422` | O utilizador tentou remover a sua própria associação |

Após sucesso, remover o item da cache ou invalidar a query da lista de membros.

## Sugestão de fluxo de interface

1. Ao abrir a gestão do agregado, pedir em paralelo os papéis e os membros.
2. Mostrar o botão “Adicionar membro” apenas a `Owner` e `Administrator`.
3. Para `Administrator`, ocultar as opções `Owner` e `Administrator` no seletor.
4. Não mostrar a ação “Remover” no próprio utilizador autenticado.
5. Para `Administrator`, não mostrar “Remover” em owners ou administradores.
6. Confirmar a remoção antes do `DELETE` e atualizar a lista apenas depois de receber `204`.

As restrições de interface melhoram a experiência, mas o frontend deve continuar a tratar respostas `403`, `404`, `409` e `422`, porque a API aplica todas as regras novamente.
