# Integração frontend — composição do agregado familiar

Este documento descreve a gestão dos elementos de um agregado familiar. Um elemento pode ter uma conta de utilizador associada ou existir apenas para representar a composição familiar.

## Conceitos

- `role` é o papel de acesso à aplicação (`Owner`, `Administrator`, `Member` ou `Viewer`). Só existe para elementos com utilizador.
- `relationship` é o grau familiar (`Esposa`, `Filho`, `Outro familiar`, etc.) e não concede permissões.
- `userId`, `username`, `email`, `isActive` e `role` são `null` quando o elemento não tem utilizador.
- Em associações anteriores ou criadas pelos fluxos de registo/convite, `relationship` e `birthDate` podem ser `null` até o elemento ser editado.
- Os enums são enviados como números e as datas seguem `YYYY-MM-DD`.

Todos os endpoints requerem `Authorization: Bearer {token}`.

## Catálogo de graus familiares

`GET /api/household-relationships`

Resposta `200 OK`:

```json
[
  { "value": 1, "name": "Próprio/a" },
  { "value": 2, "name": "Marido" },
  { "value": 3, "name": "Esposa" },
  { "value": 4, "name": "Companheiro/a" },
  { "value": 5, "name": "Filho" },
  { "value": 6, "name": "Filha" },
  { "value": 7, "name": "Filho/a" },
  { "value": 8, "name": "Pai" },
  { "value": 9, "name": "Mãe" },
  { "value": 10, "name": "Progenitor/a" },
  { "value": 11, "name": "Irmão" },
  { "value": 12, "name": "Irmã" },
  { "value": 13, "name": "Irmão/irmã" },
  { "value": 14, "name": "Avô" },
  { "value": 15, "name": "Avó" },
  { "value": 16, "name": "Avô/avó" },
  { "value": 17, "name": "Outro familiar" },
  { "value": 18, "name": "Outro" }
]
```

Usar este endpoint para preencher o seletor e apresentar `name`; não duplicar os rótulos no frontend.

## Listar elementos

`GET /api/households/{householdId}/members`

Qualquer utilizador pertencente ao agregado pode consultar a lista.

```json
[
  {
    "id": "f4d22575-3c32-4d3e-9bd7-0e415055aaba",
    "userId": 14,
    "name": "João Santos",
    "username": "joao.santos",
    "email": "joao@example.com",
    "isActive": true,
    "role": 1,
    "relationship": 1,
    "birthDate": "1988-02-03"
  },
  {
    "id": "2441d946-a56b-497d-92d5-b801855b0be5",
    "userId": null,
    "name": "Tomás Santos",
    "username": null,
    "email": null,
    "isActive": null,
    "role": null,
    "relationship": 5,
    "birthDate": "2018-07-09"
  }
]
```

## Criar um elemento

`POST /api/households/{householdId}/members`

Só `Owner` e `Administrator` podem criar elementos. `name`, `relationship` e `birthDate` são sempre obrigatórios.

Sem utilizador associado:

```json
{
  "name": "Tomás Santos",
  "relationship": 5,
  "birthDate": "2018-07-09",
  "email": null,
  "role": null
}
```

Com utilizador associado:

```json
{
  "name": "Maria Santos",
  "relationship": 3,
  "birthDate": "1990-04-12",
  "email": "maria@example.com",
  "role": 3
}
```

Quando `email` é preenchido, tem de corresponder a um utilizador ativo já registado e `role` torna-se obrigatório. Sem `email`, `role` tem de ser `null`. Um `Administrator` só pode atribuir `Member` (`3`) ou `Viewer` (`4`).

Resposta de sucesso: `201 Created`, com o elemento no mesmo formato da listagem.

## Editar um elemento

`PUT /api/households/{householdId}/members/{memberId}`

```json
{
  "name": "Maria Santos",
  "relationship": 3,
  "birthDate": "1990-04-12",
  "role": 3
}
```

- `name`, `relationship` e `birthDate` são obrigatórios.
- `birthDate` não pode estar no futuro.
- Omitir `role` ou enviar `null` mantém o papel atual num elemento com utilizador.
- Um elemento sem utilizador não pode receber `role`.
- A associação a uma conta (`userId`) não é alterada por este endpoint.
- Um `Administrator` não pode editar um `Owner` ou outro `Administrator`.
- O papel do único `Owner` não pode ser alterado para outro papel.

Resposta de sucesso: `200 OK`, com o elemento atualizado.

## Remover um elemento

`DELETE /api/households/{householdId}/members/{memberId}`

Resposta de sucesso: `204 No Content`.

Mantêm-se as proteções de acesso: ninguém remove a própria associação, um administrador não remove owners/administradores e o último owner não pode ser removido. Estas proteções não impedem a remoção normal de elementos sem utilizador.

## Erros a tratar

| Estado | Situação típica |
|---:|---|
| `400` | Payload ou formato de email/data inválido |
| `403` | Utilizador sem permissão para gerir o agregado |
| `404` | Elemento ou utilizador indicado pelo email não encontrado |
| `409` | Utilizador já associado ou tentativa de remover o único owner |
| `422` | Nome em branco, grau inválido, data futura ou combinação `email`/`role` inválida |

Os erros usam `ProblemDetails`; apresentar `detail` ao utilizador quando adequado.

## Fluxo de interface sugerido

1. Carregar em paralelo membros, graus familiares e papéis de acesso.
2. Mostrar um cartão por elemento, distinguindo visualmente “com conta” e “sem conta”.
3. Na criação, usar uma opção “Associar utilizador existente”. Só mostrar `email` e `role` quando estiver ativa.
4. Destacar registos com `relationship` ou `birthDate` nulos e pedir que sejam completados.
5. Depois de `POST`, `PUT` ou `DELETE`, invalidar a query da lista de membros.
6. Manter as restrições visuais de permissão, mas tratar sempre os erros devolvidos pela API.
