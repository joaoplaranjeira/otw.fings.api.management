# Contexto financeiro pessoal e agregados partilhados

Este documento descreve a integração do frontend com os contextos financeiros do utilizador.

## Modelo

Cada utilizador tem exatamente um agregado do tipo `Personal`. Pode também pertencer a zero ou mais agregados do tipo `Shared`.

O JWT identifica apenas o utilizador. O agregado ativo não faz parte do token e não é necessário voltar a autenticar quando o utilizador muda de contexto. Todas as autorizações continuam a ser validadas pela API em cada pedido.

## Registo e utilizadores existentes

Qualquer novo registo cria automaticamente o agregado pessoal, respetiva associação como `Owner` e o catálogo inicial de categorias.

- sem `householdName` e sem convite: cria apenas o contexto pessoal;
- com `householdName`: cria o contexto pessoal e um agregado `Shared` adicional;
- com `invitationCode`: cria o contexto pessoal e associa o utilizador ao agregado do convite.

A migration cria o mesmo contexto pessoal para os utilizadores que já existiam. O frontend não precisa de executar qualquer operação de bootstrap.

## Listar contextos

Após autenticação, chamar:

```http
GET /api/households
Authorization: Bearer {token}
```

Exemplo de resposta:

```json
[
  {
    "id": "102aab27-2589-47f0-bf5f-20f85c58084e",
    "name": "Pessoal",
    "currency": "EUR",
    "timeZone": "Europe/Lisbon",
    "role": "Owner",
    "type": "Personal"
  },
  {
    "id": "fd0b66f4-f649-4d99-bcb2-f6ad8534f519",
    "name": "Família Silva",
    "currency": "EUR",
    "timeZone": "Europe/Lisbon",
    "role": "Administrator",
    "type": "Shared"
  }
]
```

Os valores possíveis de `type` são:

- `Personal`: contexto privado, com um único utilizador;
- `Shared`: agregado que pode ter vários membros e roles.

A API devolve o contexto pessoal primeiro e depois os contextos partilhados por nome.

## Contexto ativo

O frontend deve guardar um `activeHouseholdId`. Recomenda-se a seguinte escolha inicial:

1. reutilizar o último contexto selecionado, se ainda constar da resposta de `GET /api/households`;
2. caso contrário, selecionar o contexto com `type: "Personal"`;
3. se não existir, usar o primeiro contexto devolvido e apresentar um estado de recuperação.

A seleção pode ser persistida localmente, mas deve ser sempre validada contra a lista devolvida pela API após login ou refresh da sessão.

Não enviar o contexto ativo dentro do JWT nem assumir que a role guardada localmente continua válida.

## Pedidos financeiros

Todas as rotas financeiras continuam iguais. Ao mudar de contexto, substituir apenas o `householdId`:

```http
GET /api/households/{activeHouseholdId}/categories
GET /api/households/{activeHouseholdId}/budgets
GET /api/households/{activeHouseholdId}/incomes?from=2026-11-01&to=2026-11-30
GET /api/households/{activeHouseholdId}/expenses?from=2026-11-01&to=2026-11-30
GET /api/households/{activeHouseholdId}/recurring-expenses
GET /api/households/{activeHouseholdId}/dashboard/2026/11
POST /api/households/{activeHouseholdId}/receipts/parse
```

A API verifica em cada operação se o utilizador pertence ao agregado indicado. Um identificador alterado manualmente não concede acesso a outro contexto.

## Cache e mudança de contexto

Todas as chaves de cache financeiras devem incluir o `householdId`, por exemplo:

```ts
["categories", householdId]
["budgets", householdId]
["incomes", householdId, from, to]
["expenses", householdId, from, to]
["recurring-expenses", householdId]
["dashboard", householdId, year, month]
```

Quando o utilizador muda de contexto:

1. atualizar `activeHouseholdId`;
2. cancelar pedidos pendentes do contexto anterior;
3. limpar apenas estado de formulário que dependa do agregado;
4. carregar categorias e dashboard do novo contexto;
5. nunca apresentar dados do contexto anterior durante a transição.

## Interface por tipo e role

Para `Personal`:

- ocultar membros, convites e gestão de roles;
- manter disponíveis categorias, orçamentos, receitas, despesas, recorrências, talões e dashboard;
- considerar o utilizador sempre `Owner`.

Para `Shared`, apresentar ações segundo a `role`:

- `Owner`: leitura, escrita e gestão completa do agregado;
- `Administrator`: leitura, escrita e gestão limitada de membros;
- `Member`: leitura e escrita financeira;
- `Viewer`: apenas leitura.

`POST /api/households` cria sempre um contexto `Shared`. O contexto `Personal` é criado exclusivamente pela API durante o registo ou pela migration.

Se o frontend tentar gerir membros ou convites num contexto pessoal, a API responde `409 Conflict`.

## Tratamento de erros

- `401 Unauthorized`: token ausente, inválido ou expirado;
- `403 Forbidden`: o utilizador não pertence ao agregado ou não tem a role necessária;
- `409 Conflict`: operação incompatível com o tipo de contexto, incluindo membros ou convites em `Personal`;
- `404 Not Found`: agregado ou recurso inexistente.

Perante `403` ou `404` no contexto ativo, voltar a executar `GET /api/households`, selecionar um contexto válido e invalidar os dados em cache do contexto removido.
