# Integração frontend — listas de compras

Este módulo permite manter um catálogo parametrizável de tipos de produto por agregado, várias listas de compras ativas, adicionar produtos manualmente ou a partir de uma fotografia e rever produtos encontrados numa nova despesa antes de os marcar como comprados.

## Conceitos e estados

- Uma lista tem estado `Active` ou `Archived`.
- Um produto tem estado `Active`, `Purchased` ou `Removed`.
- Uma correspondência entre uma parcela de despesa e um produto tem estado `Pending`, `Approved` ou `Rejected`.
- Um tipo de produto, por exemplo `Arroz`, `Água` ou `Champô`, pertence obrigatoriamente a uma categoria e pode pertencer a uma subcategoria.
- Marca, quantidade, unidade, preço expectável, variante e notas pertencem ao item concreto da lista, não ao tipo.
- Um item criado manualmente referencia obrigatoriamente um tipo através de `productTypeId`.
- Um item criado por fotografia não exige pré-seleção; o backend tenta associar automaticamente o tipo reconhecido e pode devolver `productTypeId: null` quando ainda não existir no catálogo.
- Aprovar uma correspondência desconta a quantidade comprada. O produto só passa para `Purchased` quando a quantidade em falta chega a zero; numa compra parcial permanece `Active` com a quantidade restante.
- Rejeitar uma correspondência mantém o produto `Active`.
- Nunca é removido um produto automaticamente. A decisão final é sempre do utilizador.

Os enums são serializados pelo nome, não pelo respetivo valor numérico.

## Tipos de produto parametrizados

Os tipos são configurados por agregado, tal como as categorias. Cada novo agregado recebe automaticamente 17 tipos comuns associados às categorias e subcategorias standard: leite, pão, ovos, arroz, massa, água, fruta, legumes, carne, peixe, azeite, café, papel higiénico, detergente da roupa, detergente da loiça, champô e pasta de dentes.

- No registo de um utilizador, os tipos são criados no agregado pessoal e também no agregado partilhado criado nesse registo, quando aplicável.
- As migrações inserem os mesmos tipos em todos os agregados já existentes.
- Aceitar um convite não duplica tipos no agregado partilhado, porque o catálogo pertence ao agregado e não a cada membro.

### Listar tipos

```http
GET /api/households/{householdId}/product-types?includeInactive=false
```

Resposta:

```json
[
  {
    "id": "e2499734-98aa-44c9-aa64-a788e91103bd",
    "name": "Leite",
    "categoryId": "3c13d98f-f350-4742-9363-632d378095dc",
    "categoryName": "Alimentação",
    "subcategoryId": "ab517de1-8a09-4b63-bde3-334ed170ea53",
    "subcategoryName": "Supermercado",
    "isActive": true
  }
]
```

Por omissão são devolvidos apenas tipos ativos. Use `includeInactive=true` no ecrã de gestão de parametrizações.

### Criar um tipo

```http
POST /api/households/{householdId}/product-types
Content-Type: application/json

{
  "name": "Iogurte",
  "categoryId": "3c13d98f-f350-4742-9363-632d378095dc",
  "subcategoryId": "ab517de1-8a09-4b63-bde3-334ed170ea53"
}
```

`name` e `categoryId` são obrigatórios. A subcategoria é opcional e, quando indicada, tem de pertencer à categoria selecionada. Não podem existir dois tipos com o mesmo nome normalizado no mesmo agregado.

### Alterar, ativar ou desativar

```http
PATCH /api/households/{householdId}/product-types/{productTypeId}
Content-Type: application/json

{
  "name": "Iogurte",
  "categoryId": "3c13d98f-f350-4742-9363-632d378095dc",
  "subcategoryId": "ab517de1-8a09-4b63-bde3-334ed170ea53",
  "isActive": true
}
```

Tipos inativos deixam de poder ser escolhidos manualmente para novas entradas, mas os itens existentes mantêm os dados e a referência histórica.

## Rotas

Todas as rotas exigem `Authorization: Bearer <token>` e usam o agregado atualmente selecionado.

### Listar listas

```http
GET /api/households/{householdId}/shopping-lists?activeOnly=true&includeCompletedItems=false
```

- `activeOnly` tem valor predefinido `true`.
- `includeCompletedItems` tem valor predefinido `false`. Quando é `false`, cada lista inclui apenas produtos `Active`.

Resposta:

```json
[
  {
    "id": "a19a4708-2275-4e50-a856-6682f35ba995",
    "name": "Compras semanais",
    "status": "Active",
    "createdAtUtc": "2026-11-11T09:30:00Z",
    "activeItemCount": 2,
    "items": [
      {
        "id": "f964e70a-dd9f-43ef-bf42-15119779e50c",
        "productTypeId": "e2499734-98aa-44c9-aa64-a788e91103bd",
        "productTypeName": "Leite",
        "brand": "Mimosa",
        "variant": null,
        "quantity": 2,
        "unit": "un",
        "expectedPrice": 1.29,
        "notes": null,
        "source": "Manual",
        "detectionConfidence": null,
        "status": "Active",
        "createdAtUtc": "2026-11-11T09:31:00Z",
        "completedAtUtc": null
      }
    ]
  }
]
```

Para obter uma lista individual:

```http
GET /api/households/{householdId}/shopping-lists/{listId}?includeCompletedItems=true
```

### Criar e alterar uma lista

```http
POST /api/households/{householdId}/shopping-lists
Content-Type: application/json

{
  "name": "Compras semanais"
}
```

Retorna `201 Created` com a lista criada.

```http
PATCH /api/households/{householdId}/shopping-lists/{listId}
Content-Type: application/json

{
  "name": "Compras do fim de semana",
  "status": "Active"
}
```

Arquivar uma lista:

```http
DELETE /api/households/{householdId}/shopping-lists/{listId}
```

Retorna `204 No Content`. O arquivo é lógico e mantém os produtos e o histórico.

### Adicionar um produto manualmente

O primeiro passo da interface deve ser a escolha obrigatória do tipo. Depois podem ser apresentados os restantes campos opcionais:

```http
POST /api/households/{householdId}/shopping-lists/{listId}/items
Content-Type: application/json

{
  "productTypeId": "e2499734-98aa-44c9-aa64-a788e91103bd",
  "brand": "Mimosa",
  "quantity": 2,
  "unit": "un",
  "expectedPrice": 1.29,
  "notes": "Meio gordo"
}
```

`brand`, `quantity`, `unit`, `expectedPrice` e `notes` são opcionais. Quando indicada, `quantity` tem de ser superior a zero e `expectedPrice` não pode ser negativo. O preço usa a moeda configurada no agregado. Retorna `201 Created`.

O payload mínimo é:

```json
{
  "productTypeId": "e2499734-98aa-44c9-aa64-a788e91103bd"
}
```

Um `productTypeId` inativo, inexistente ou pertencente a outro agregado é rejeitado. A adição manual não aceita nomes livres: novos tipos devem ser criados primeiro na gestão de parametrizações.

### Adicionar através de fotografia

```http
POST /api/households/{householdId}/shopping-lists/{listId}/items/from-image
Content-Type: multipart/form-data

file=<JPEG, PNG ou WebP>
```

O campo multipart chama-se obrigatoriamente `file`. O limite atual é 10 MiB. Não é enviado `productTypeId`: a API valida a imagem, pede à OpenAI o tipo genérico e tenta associá-lo automaticamente ao catálogo do agregado. A fotografia continua válida quando não existir correspondência; nesse caso o item é criado com `productTypeId: null` e com o nome reconhecido.

Resposta `201 Created`:

```json
{
  "item": {
    "id": "f964e70a-dd9f-43ef-bf42-15119779e50c",
    "productTypeId": "e2499734-98aa-44c9-aa64-a788e91103bd",
    "productTypeName": "Leite",
    "brand": "Mimosa",
    "variant": "Meio gordo",
    "quantity": 1,
    "unit": "L",
    "expectedPrice": null,
    "notes": null,
    "source": "ProductPhoto",
    "detectionConfidence": 0.96,
    "status": "Active",
    "createdAtUtc": "2026-11-11T09:31:00Z",
    "completedAtUtc": null
  },
  "recognition": {
    "productTypeName": "Leite",
    "brand": "Mimosa",
    "variant": "Meio gordo",
    "suggestedQuantity": 1,
    "suggestedUnit": "L",
    "confidence": 0.96
  }
}
```

UX recomendada:

1. Abrir a câmara com `accept="image/*"` e `capture="environment"` quando suportado.
2. Mostrar estado de processamento durante o upload.
3. Após o `201`, mostrar o produto num formulário editável, sobretudo quando `detectionConfidence < 0.8`.
4. Usar o endpoint de alteração abaixo caso o utilizador corrija o resultado.

### Alterar ou remover um produto

```http
PATCH /api/households/{householdId}/shopping-lists/{listId}/items/{itemId}
Content-Type: application/json

{
  "productTypeId": "e2499734-98aa-44c9-aa64-a788e91103bd",
  "brand": "Mimosa",
  "quantity": 2,
  "unit": "un",
  "expectedPrice": 1.49,
  "notes": null
}
```

Apenas produtos `Active` podem ser alterados.

```http
DELETE /api/households/{householdId}/shopping-lists/{listId}/items/{itemId}
```

Retorna `204 No Content` e altera o produto para `Removed`.

## Correspondências com despesas

Ao criar uma despesa em:

```http
POST /api/households/{householdId}/expenses
```

o backend compara as descrições de `lines` com todos os produtos ativos de todas as listas ativas do agregado. Cada correspondência suficientemente segura é guardada como `Pending`.

Para esta funcionalidade ser eficaz, as despesas de talão devem ser criadas com as parcelas devolvidas por `POST /receipts/parse`. Uma despesa manual com apenas uma descrição genérica, sem produtos em `lines`, normalmente não produzirá correspondências.

### Obter decisões pendentes

```http
GET /api/households/{householdId}/shopping-lists/expense-matches?status=Pending
```

Resposta:

```json
[
  {
    "id": "bb706951-e240-4e94-9d7b-ddda187a0546",
    "shoppingListId": "a19a4708-2275-4e50-a856-6682f35ba995",
    "shoppingListName": "Compras semanais",
    "shoppingListItemId": "f964e70a-dd9f-43ef-bf42-15119779e50c",
    "productTypeName": "Leite",
    "expenseId": "c5e99483-32ec-4385-a267-0ae5b2b37fda",
    "expenseLineId": "00110c07-ecaa-4725-ab66-487062767755",
    "expenseLineDescription": "LEITE MIMOSA MG 1L",
    "requestedQuantityAtDetection": 3,
    "detectedPurchasedQuantity": 1,
    "approvedPurchasedQuantity": null,
    "remainingQuantity": null,
    "confidence": 0.82,
    "status": "Pending",
    "createdAtUtc": "2026-11-11T12:00:00Z",
    "reviewedAtUtc": null
  }
]
```

O parâmetro `status` também aceita `Approved` e `Rejected`. Sem query string, o valor predefinido é `Pending`.

### Aprovar ou rejeitar

Aprovar usando a quantidade detetada na parcela da despesa:

```http
PATCH /api/households/{householdId}/shopping-lists/expense-matches/{matchId}
Content-Type: application/json

{
  "decision": "Approved"
}
```

O frontend pode corrigir ou preencher explicitamente a quantidade comprada:

```json
{
  "decision": "Approved",
  "purchasedQuantity": 1
}
```

Regras de quantidade:

- `detectedPurchasedQuantity` vem de `quantity` na parcela da despesa.
- `purchasedQuantity`, quando enviada, prevalece sobre a quantidade detetada.
- Se o produto da lista tiver quantidade e a despesa não indicar uma quantidade válida, `purchasedQuantity` é obrigatória e a API devolve `422` quando estiver ausente.
- Se a quantidade comprada for inferior à quantidade da lista, a resposta contém `remainingQuantity` e o produto continua `Active` com esse valor.
- Se a quantidade comprada for igual ou superior à quantidade em falta, o produto passa para `Purchased`.
- Quando o produto da lista não tem quantidade, a aprovação marca-o diretamente como `Purchased`; `purchasedQuantity` continua opcional.

Rejeitar e manter o produto na lista:

```json
{
  "decision": "Rejected"
}
```

Uma correspondência só pode ser revista uma vez. Uma segunda tentativa retorna `409 Conflict`.

UX recomendada após criar uma despesa:

1. Atualizar/invalidate a query de correspondências `Pending`.
2. Se existirem resultados para a despesa acabada de criar, abrir uma folha/modal de confirmação.
3. Mostrar lista, produto pedido, quantidade pedida e quantidade encontrada no talão.
4. Permitir corrigir a quantidade comprada antes da aprovação.
5. Disponibilizar ações explícitas “Confirmar compra” (`Approved`) e “Manter” (`Rejected`).
6. Após cada decisão, atualizar as queries das listas e das correspondências pendentes.

## Permissões e erros

- Qualquer membro do agregado pode consultar listas e correspondências.
- `Viewer` não pode criar, alterar, arquivar, remover nem rever correspondências (`403`).
- Recursos que não pertençam ao agregado devolvem `404` ou `403`, conforme o controlo falhe na associação ou no recurso.
- Lista arquivada, produto já concluído ou correspondência já revista devolvem `409`.
- Erros de validação de negócio (quantidade, imagem ou decisão inválida) devolvem `422`; erros de model binding podem devolver `400`.
- OpenAI não configurada ou indisponível devolve `503` através do formato Problem Details já usado pela API.

## Configuração do backend

As variáveis relevantes são:

```text
OpenAI__ApiKey
OpenAI__ProductRecognitionModel
OpenAI__MaximumProductImageBytes
```

Os valores predefinidos de desenvolvimento para modelo e tamanho máximo estão em `appsettings.json`. A chave nunca deve ser exposta ao frontend; a imagem é enviada apenas para esta API.
