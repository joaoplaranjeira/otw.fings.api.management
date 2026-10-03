# Guia funcional e de integração frontend — Fings

Este documento descreve a funcionalidade atualmente implementada na API `otw.fings.api.management` e o contrato que o frontend deve usar. O código e o Swagger são a fonte final de verdade caso exista alguma divergência.

## 1. Funcionalidade disponível

A primeira versão permite:

- registar um utilizador e criar o seu primeiro agregado familiar;
- autenticar por código OTP enviado por email;
- gerir vários agregados por utilizador;
- criar categorias e subcategorias de despesa;
- definir um orçamento mensal aplicável durante um intervalo de meses;
- distribuir parte ou todo o orçamento por categorias;
- criar automaticamente uma receita planeada para cada mês abrangido pelo orçamento;
- registar e consultar despesas, incluindo nome e NIF do comerciante;
- configurar despesas recorrentes semanais, mensais, trimestrais ou anuais;
- consultar o orçamento disponível e a execução por categoria num mês;
- enviar a imagem de um talão para extração transitória através da OpenAI.

Não estão ainda disponíveis edição, cancelamento ou eliminação de registos, convites para outros membros, receitas manuais, projeções históricas avançadas, exportações ou gamificação.

## 2. Ambientes e convenções

| Ambiente | Base URL | Swagger | Health |
|---|---|---|---|
| Docker local | `http://localhost:5015` | `http://localhost:5015/swagger` | `http://localhost:5015/health` |
| `dotnet run` | Definida pelo ASP.NET Core | `{baseUrl}/swagger` | `{baseUrl}/health` |

Convenções:

- JSON em `camelCase`.
- Datas financeiras em `YYYY-MM-DD`, sem hora.
- GUIDs como strings.
- Valores monetários como números JSON, nunca strings formatadas.
- Moeda inicial `EUR`.
- Enums são serializados como números.
- Todos os endpoints, exceto registo, OTP e health check, exigem Bearer token.
- Não existe prefixo de versão; os endpoints funcionais começam em `/api`.

Headers habituais:

```http
Accept: application/json
Content-Type: application/json
Authorization: Bearer {token}
```

## 3. Executar o backend localmente

No diretório do projeto:

1. Copiar `.env.example` para `.env`.
2. Configurar MySQL, JWT, Loops e OpenAI.
3. Executar a task VS Code `docker-run-fings`.

A task:

- remove um container anterior chamado `otw.fings.api.management`;
- constrói a imagem `otw.fings.api.management:debug`;
- aplica as migrations;
- inicia a API na porta local `5015`.

Quando o MySQL corre diretamente no computador e é usado Docker Desktop, configurar a connection string com `server=host.docker.internal`. `localhost` dentro do container identifica o próprio container.

## 4. Autenticação OTP

### 4.1 Registar utilizador

`POST /api/users`

```json
{
  "name": "João Santos",
  "username": "joao.santos",
  "email": "joao@example.com",
  "householdName": "Família Santos"
}
```

Resposta `201 Created`:

```json
{
  "id": 1,
  "name": "João Santos",
  "username": "joao.santos",
  "email": "joao@example.com",
  "isActive": true
}
```

O registo cria automaticamente um agregado e associa o utilizador como `Owner`. Email e username duplicados devolvem `409`.

### 4.2 Pedir OTP

`POST /api/otp/send`

```json
{
  "email": "joao@example.com"
}
```

Resposta `200 OK`:

```json
{
  "success": true,
  "message": "OTP enviado com sucesso.",
  "expiresAt": "2026-10-01T18:10:00Z",
  "token": null
}
```

O código expira em 10 minutos. Existem limites de pedidos, tentativas falhadas e bloqueio temporário.

Em desenvolvimento, quando `Otp__BypassEnabled=true`, este endpoint não envia email nem cria um OTP. O frontend pode manter o mesmo fluxo e avançar para a validação com o código fixo `123123`. Esta opção deve estar sempre desativada em produção.

### 4.3 Validar OTP

`POST /api/otp/validate`

```json
{
  "email": "joao@example.com",
  "code": "123456"
}
```

Resposta `200 OK`:

```json
{
  "success": true,
  "message": "Autenticação concluída com sucesso.",
  "expiresAt": null,
  "token": "eyJ..."
}
```

O token expira um mês de calendário após a emissão. O frontend deve:

- enviá-lo em `Authorization: Bearer {token}`;
- limpar a sessão e regressar ao login em qualquer `401`;
- não assumir que esconder controlos substitui a autorização do backend;
- preferir armazenamento em memória ou outro mecanismo protegido pelo cliente, evitando exposição desnecessária a JavaScript de terceiros.

### 4.4 Utilizador atual

`GET /api/users/me`

Devolve o mesmo formato de `UserResponse` apresentado no registo.

### 4.5 Atualizar perfil

`PATCH /api/users/me`

```json
{
  "name": "João Silva",
  "username": "joao.silva"
}
```

Os campos são opcionais individualmente, mas o pedido tem de incluir pelo menos um deles. A resposta `200 OK` usa o formato de `UserResponse`. O `username` é normalizado para minúsculas; um username pertencente a outro utilizador devolve `409`. Valores vazios ou um pedido sem alterações devolvem `422`.

O email não pode ser alterado neste endpoint, uma vez que identifica o utilizador no fluxo de autenticação OTP.

## 5. Agregados familiares

### Listar agregados

`GET /api/households`

```json
[
  {
    "id": "8af810fc-f7f5-4fde-bb63-b530ae8fb4a1",
    "name": "Família Santos",
    "currency": "EUR",
    "timeZone": "Europe/Lisbon",
    "role": 1
  }
]
```

### Criar agregado adicional

`POST /api/households`

```json
{
  "name": "Finanças pessoais",
  "currency": "EUR",
  "timeZone": "Europe/Lisbon"
}
```

Resposta `201 Created` no mesmo formato da listagem.

A criação de um agregado também cria automaticamente o catálogo inicial de categorias e subcategorias de despesa. O catálogo pertence ao novo agregado e fica imediatamente disponível em `GET /api/households/{householdId}/categories`.

O frontend deve manter um `activeHouseholdId` e incluí-lo em todas as rotas financeiras. Nunca reutilizar dados em cache de um agregado depois de trocar o agregado ativo.

Papéis:

| Valor | Papel | Acesso atual |
|---:|---|---|
| `1` | Owner | Leitura e escrita |
| `2` | Administrator | Leitura e escrita |
| `3` | Member | Leitura e escrita |
| `4` | Viewer | Apenas leitura |

## 6. Categorias e subcategorias

### Criar categoria

`POST /api/households/{householdId}/categories`

```json
{
  "name": "Alimentação",
  "color": "#22C55E",
  "icon": "shopping-cart"
}
```

### Criar subcategoria

`POST /api/households/{householdId}/categories/{categoryId}/subcategories`

```json
{
  "name": "Supermercado"
}
```

### Listar

`GET /api/households/{householdId}/categories`

```json
[
  {
    "id": "973f4bd2-760b-4bb2-b455-d35e803c8c26",
    "name": "Alimentação",
    "color": "#22C55E",
    "icon": "shopping-cart",
    "isActive": true,
    "subcategories": [
      {
        "id": "8d60e81f-92fb-459f-9652-d68ad61ad8eb",
        "name": "Supermercado",
        "isActive": true
      }
    ]
  }
]
```

Ao escolher uma categoria numa despesa, o seletor de subcategoria deve apresentar apenas os elementos dessa categoria.

Todos os agregados incluem a categoria **Não aplicável** e todas as categorias incluem uma subcategoria **Não aplicável**. As categorias criadas pelo utilizador recebem também essa subcategoria automaticamente. O frontend não deve remover estas opções dos seletores.

## 7. Orçamentos por período

`POST /api/households/{householdId}/budgets`

```json
{
  "name": "Primeiro semestre",
  "startMonth": "2027-01-01",
  "endMonth": "2027-06-01",
  "monthlyAmount": 1500.00,
  "allocations": [
    {
      "categoryId": "973f4bd2-760b-4bb2-b455-d35e803c8c26",
      "monthlyAmount": 500.00
    }
  ]
}
```

Semântica:

- `startMonth` e `endMonth` são inclusivos;
- qualquer dia recebido é normalizado para o primeiro dia do mês;
- `monthlyAmount` é o valor disponível em cada mês, não o total de todo o período;
- a soma das alocações não pode ultrapassar o valor mensal;
- não podem existir dois orçamentos ativos com períodos sobrepostos;
- janeiro a junho com `monthlyAmount: 1500` cria seis receitas planeadas de 1500 EUR.

Resposta `201 Created`:

```json
{
  "id": "e935788c-d9e0-4678-9d92-e1bfb26786a1",
  "name": "Primeiro semestre",
  "startMonth": "2027-01-01",
  "endMonth": "2027-06-01",
  "monthlyAmount": 1500.00,
  "status": 1,
  "generatedIncomeCount": 6,
  "allocations": [
    {
      "categoryId": "973f4bd2-760b-4bb2-b455-d35e803c8c26",
      "categoryName": "Alimentação",
      "monthlyAmount": 500.00
    }
  ]
}
```

Listar: `GET /api/households/{householdId}/budgets`.

Estados do orçamento: `1 = Active`, `2 = Closed`, `3 = Cancelled`.

## 8. Receitas geradas

`GET /api/households/{householdId}/incomes?from=2027-01-01&to=2027-06-30`

```json
[
  {
    "id": "894476f7-b21a-4679-a32a-42118e6518e5",
    "date": "2027-01-01",
    "amount": 1500.00,
    "description": "Orçamento mensal — Primeiro semestre",
    "origin": 2,
    "status": 1,
    "budgetPeriodId": "e935788c-d9e0-4678-9d92-e1bfb26786a1"
  }
]
```

As receitas do orçamento são planeadas, não recebimentos bancários reais.

| Enum | Valores |
|---|---|
| `origin` | `1 = Manual`, `2 = Budget` |
| `status` | `1 = Planned`, `2 = Confirmed`, `3 = Cancelled` |

## 9. Despesas

### Criar

`POST /api/households/{householdId}/expenses`

```json
{
  "categoryId": "973f4bd2-760b-4bb2-b455-d35e803c8c26",
  "subcategoryId": "8d60e81f-92fb-459f-9652-d68ad61ad8eb",
  "date": "2027-01-08",
  "amount": 42.35,
  "description": "Compras semanais",
  "merchantName": "Supermercado Exemplo, S.A.",
  "merchantTaxNumber": "500000000",
  "origin": 1,
  "lines": null
}
```

Regras:

- valor superior a zero;
- categoria principal obrigatória quando `lines` é omitido; com parcelas explícitas pode ser omitida e é inferida da parcela de maior valor;
- subcategoria principal opcional, mas tem de pertencer à categoria principal;
- nome e NIF do comerciante são opcionais;
- uma criação manual deve usar `origin: 1`.
- `lines` é opcional; quando é omitido ou vazio, a API cria automaticamente uma parcela com a descrição, o valor e a classificação do movimento;
- quando são enviadas várias parcelas, a soma de `lines[].amount` tem de ser igual a `amount`;
- uma parcela pode ter `amount` e `unitPrice` negativos para representar um desconto, mas não pode ter valor zero; o total `amount` do movimento continua a ter de ser positivo;
- cada parcela pode ter a sua própria categoria e subcategoria;
- se `categoryId` for omitido num movimento com várias parcelas, a API usa como classificação principal a parcela de maior valor.

Origens: `1 = Manual`, `2 = Recurring`, `3 = Receipt`.

### Listar por intervalo

`GET /api/households/{householdId}/expenses?from=2027-01-01&to=2027-01-31`

```json
[
  {
    "id": "12214a2d-42a4-4d20-a8d4-745db5d59db0",
    "date": "2027-01-08",
    "amount": 42.35,
    "description": "Compras semanais",
    "categoryId": "973f4bd2-760b-4bb2-b455-d35e803c8c26",
    "categoryName": "Alimentação",
    "subcategoryId": "8d60e81f-92fb-459f-9652-d68ad61ad8eb",
    "subcategoryName": "Supermercado",
    "merchantName": "Supermercado Exemplo, S.A.",
    "merchantTaxNumber": "500000000",
    "origin": 1,
    "status": 2,
    "lines": [
      {
        "id": "b194f1d4-1fde-4b76-97b5-f7788cc6ace2",
        "description": "Compras semanais",
        "quantity": 1,
        "unitPrice": 42.35,
        "amount": 42.35,
        "categoryId": "973f4bd2-760b-4bb2-b455-d35e803c8c26",
        "categoryName": "Alimentação",
        "subcategoryId": "8d60e81f-92fb-459f-9652-d68ad61ad8eb",
        "subcategoryName": "Supermercado",
        "position": 1
      }
    ]
  }
]
```

### Editar um movimento

`PATCH /api/households/{householdId}/expenses/{expenseId}`

O pedido contém o estado completo dos dados gerais do movimento. `origin` não é editável. `lines` é opcional:

- numa despesa com a parcela automática, a omissão de `lines` atualiza também essa parcela;
- numa despesa com várias parcelas, pode omitir `lines` se o total não mudar;
- para alterar simultaneamente o total e as parcelas, deve enviá-las no mesmo pedido, garantindo que a soma coincide com `amount`.

```json
{
  "categoryId": null,
  "subcategoryId": null,
  "date": "2027-01-09",
  "amount": 45.00,
  "description": "Talão corrigido",
  "merchantName": "Supermercado Exemplo, S.A.",
  "merchantTaxNumber": "500000000",
  "lines": [
    {
      "categoryId": "973f4bd2-760b-4bb2-b455-d35e803c8c26",
      "subcategoryId": "8d60e81f-92fb-459f-9652-d68ad61ad8eb",
      "description": "Compras alimentares",
      "quantity": null,
      "unitPrice": null,
      "amount": 45.00
    }
  ]
}
```

### Substituir as parcelas

`PUT /api/households/{householdId}/expenses/{expenseId}/lines`

Substitui a lista completa, preservando o valor total e os restantes dados do movimento. A lista não pode estar vazia e a soma tem de coincidir com o total atual.

```json
{
  "lines": [
    {
      "categoryId": "973f4bd2-760b-4bb2-b455-d35e803c8c26",
      "subcategoryId": "8d60e81f-92fb-459f-9652-d68ad61ad8eb",
      "description": "Alimentação",
      "quantity": null,
      "unitPrice": null,
      "amount": 30.00
    },
    {
      "categoryId": "ea145b42-7489-4e95-a072-0063fc0a861e",
      "subcategoryId": null,
      "description": "Higiene",
      "quantity": null,
      "unitPrice": null,
      "amount": 15.00
    }
  ]
}
```

Ambos os endpoints devolvem o movimento completo no mesmo formato da listagem.

## 10. Despesas recorrentes

`POST /api/households/{householdId}/recurring-expenses`

```json
{
  "categoryId": "973f4bd2-760b-4bb2-b455-d35e803c8c26",
  "subcategoryId": null,
  "description": "Internet",
  "merchantName": "Operador Exemplo",
  "merchantTaxNumber": "500000000",
  "amount": 35.00,
  "frequency": 2,
  "startDate": "2027-01-10",
  "endDate": "2027-12-10"
}
```

Periodicidade: `1 = Weekly`, `2 = Monthly`, `3 = Quarterly`, `4 = Annual`.

Um worker cria automaticamente as despesas vencidas. A combinação regra/data é única, pelo que uma ocorrência não deve aparecer duplicada. Listar regras com `GET /api/households/{householdId}/recurring-expenses`.

## 11. Dashboard mensal

`GET /api/households/{householdId}/dashboard/2027/1`

```json
{
  "month": "2027-01-01",
  "budget": 1500.00,
  "plannedIncome": 1500.00,
  "confirmedExpenses": 342.35,
  "available": 1157.65,
  "categories": [
    {
      "categoryId": "973f4bd2-760b-4bb2-b455-d35e803c8c26",
      "categoryName": "Alimentação",
      "budget": 500.00,
      "spent": 142.35,
      "available": 357.65
    }
  ]
}
```

Fórmula atual:

```text
available = orçamento mensal - despesas confirmadas do mês
```

Uma categoria sem alocação tem `budget` e `available` a `null`, mas continua a apresentar o valor gasto.

Depois de criar uma despesa, o frontend deve invalidar as queries de despesas e do dashboard do mês correspondente.

## 12. Parsing de talões

`POST /api/households/{householdId}/receipts/parse`

Pedido `multipart/form-data` com um único campo chamado `file`:

```javascript
const form = new FormData();
form.append("file", selectedFile);

const response = await fetch(
  `${apiUrl}/api/households/${householdId}/receipts/parse`,
  {
    method: "POST",
    headers: { Authorization: `Bearer ${token}` },
    body: form
  }
);
```

Não definir manualmente `Content-Type`; o browser acrescenta o boundary correto.

São aceites JPEG, PNG e WebP até 10 MiB. A API valida o MIME e a assinatura binária. A imagem e o resultado não são guardados.

Resposta:

```json
{
  "parseId": "343d4f31-5a69-4ff1-9a11-e8fd9eb7f731",
  "merchantName": "Supermercado Exemplo, S.A.",
  "merchantTaxNumber": "500000000",
  "documentNumber": "FT 2027/123",
  "purchaseDate": "2027-01-08",
  "currency": "EUR",
  "subtotal": 39.95,
  "tax": 2.40,
  "total": 42.35,
  "linesTotal": 42.35,
  "lines": [
    {
      "description": "Leite",
      "quantity": 2,
      "unitPrice": 0.95,
      "amount": 1.90,
      "suggestedCategoryId": "973f4bd2-760b-4bb2-b455-d35e803c8c26",
      "suggestedCategoryName": "Alimentação",
      "suggestedSubcategoryId": "8d60e81f-92fb-459f-9652-d68ad61ad8eb",
      "suggestedSubcategoryName": "Supermercado",
      "confidence": 0.94
    }
  ],
  "warnings": []
}
```

Quando a OpenAI não identifica uma categoria com segurança, a API devolve **Não aplicável / Não aplicável** em vez de identificadores `null`. Quando identifica a categoria mas não uma subcategoria, mantém a categoria identificada e usa a respetiva subcategoria **Não aplicável**. A `confidence` original é preservada e estas linhas não contam como classificadas no histórico de qualidade.

Fluxo recomendado no frontend:

1. Mostrar loading durante o parse.
2. Apresentar todas as parcelas num ecrã de revisão editável.
3. Destacar confiança baixa e `warnings`.
4. Permitir corrigir descrição, valor e classificação.
5. Criar **uma única despesa** através de `POST /expenses`, usando o `total` como `amount`, `origin: 3` e enviando todas as linhas revistas em `lines`.
6. Considerar a operação concluída depois de esse movimento único ter sido criado.

Exemplo do pedido de criação após a revisão:

```json
{
  "categoryId": null,
  "subcategoryId": null,
  "date": "2027-01-08",
  "amount": 42.35,
  "description": "Talão FT 2027/123",
  "merchantName": "Supermercado Exemplo, S.A.",
  "merchantTaxNumber": "500000000",
  "origin": 3,
  "lines": [
    {
      "categoryId": "973f4bd2-760b-4bb2-b455-d35e803c8c26",
      "subcategoryId": "8d60e81f-92fb-459f-9652-d68ad61ad8eb",
      "description": "Leite",
      "quantity": 2,
      "unitPrice": 0.95,
      "amount": 1.90
    },
    {
      "categoryId": "973f4bd2-760b-4bb2-b455-d35e803c8c26",
      "subcategoryId": "8d60e81f-92fb-459f-9652-d68ad61ad8eb",
      "description": "Restantes produtos",
      "quantity": null,
      "unitPrice": null,
      "amount": 40.45
    }
  ]
}
```

O parse não é uma operação idempotente nem cria despesas automaticamente.

### Validar a qualidade de um parse

Cada parse cria um registo técnico pendente, identificado por `parseId`. A imagem e o conteúdo completo do talão continuam a não ser persistidos.

`PATCH /api/households/{householdId}/receipts/parses/{parseId}/validation`

```json
{
  "isValid": false,
  "notes": "Duas linhas foram associadas à categoria errada."
}
```

Owner, Administrator e Member podem validar. Viewer tem apenas acesso ao histórico.

### Histórico de qualidade

`GET /api/households/{householdId}/receipts/parses/history?limit=50`

```json
{
  "total": 10,
  "pending": 2,
  "valid": 6,
  "invalid": 2,
  "validRate": 75.00,
  "items": [
    {
      "parseId": "343d4f31-5a69-4ff1-9a11-e8fd9eb7f731",
      "parsedAtUtc": "2026-10-02T18:00:00Z",
      "model": "gpt-4o-mini",
      "lineCount": 5,
      "categorizedLineCount": 5,
      "averageConfidence": 0.86,
      "validationStatus": 3,
      "validatedAtUtc": "2026-10-02T18:02:00Z",
      "validationNotes": "Duas linhas foram associadas à categoria errada."
    }
  ]
}
```

Estados: `1 = Pending`, `2 = Valid`, `3 = Invalid`. `validRate` considera apenas parses já validados.

## 13. Erros HTTP

Erros transversais usam `application/problem+json`:

```json
{
  "type": "about:blank",
  "title": "Business validation failed",
  "status": 422,
  "detail": "A subcategoria não pertence à categoria indicada."
}
```

| Estado | Significado para o frontend |
|---:|---|
| `400` | Payload ou OTP inválido |
| `401` | Token ausente, inválido ou expirado; terminar sessão |
| `403` | Sem acesso ao agregado ou operação de escrita não permitida |
| `404` | Recurso não encontrado |
| `409` | Duplicado, período sobreposto ou conflito de persistência |
| `422` | Regra de negócio inválida; mostrar `detail` junto do formulário |
| `429` | Limite de pedidos; aguardar antes de repetir |
| `503` | Integração externa indisponível, nomeadamente parsing OpenAI |

Erros de validação automática do ASP.NET Core podem incluir um objeto `errors` por campo. O cliente deve suportar ambos os formatos.

## 14. CORS e configuração do frontend

A origem exata do frontend deve existir em `Cors:AllowedOrigins`. Para Vite local:

```text
Cors__AllowedOrigins__0=http://localhost:5173
Cors__AllowedOrigins__1=http://localhost:5175
```

Não usar barra final na origem. Uma configuração frontend típica:

```text
VITE_FINGS_API_URL=http://localhost:5015
```

## 15. Estado e cache recomendados

Chaves de query sugeridas:

```text
["me"]
["households"]
["categories", householdId]
["budgets", householdId]
["incomes", householdId, from, to]
["expenses", householdId, from, to]
["recurring-expenses", householdId]
["dashboard", householdId, year, month]
```

Ao trocar o agregado ativo, cancelar pedidos pendentes do agregado anterior e não misturar dados entre chaves. Após mutations, invalidar apenas as chaves afetadas e o dashboard do respetivo mês.
