# Endpoints iniciais

## Acesso público

- `POST /api/users` — regista utilizador e primeiro agregado.
- `POST /api/otp/send` — envia OTP para um utilizador ativo; em modo bypass não envia email.
- `POST /api/otp/validate` — valida OTP e devolve JWT Bearer; com `Otp__BypassEnabled=true`, aceita o código fixo `123123` para utilizadores ativos.
- `GET /health` — health check técnico.

## Autenticado

- `GET /api/users/me`
- `PATCH /api/users/me` — atualiza o nome e/ou username do utilizador autenticado.
- `GET/POST /api/households`
- `GET/POST /api/households/{householdId}/categories`
- `POST /api/households/{householdId}/categories/{categoryId}/subcategories`
- `GET/POST /api/households/{householdId}/budgets`
- `GET /api/households/{householdId}/incomes?from=YYYY-MM-DD&to=YYYY-MM-DD`
- `GET/POST /api/households/{householdId}/expenses` — cada despesa é um movimento total com uma ou mais parcelas em `lines`; na ausência de `lines`, é criada uma parcela automaticamente.
- `GET /api/households/{householdId}/expenses/suggestions` — devolve as 10 combinações de comerciante, NIF, categoria e subcategoria mais frequentes entre hoje e os 29 dias anteriores; exclui despesas canceladas e registos sem comerciante.
- `PATCH /api/households/{householdId}/expenses/{expenseId}` — edita o movimento e, opcionalmente, substitui as parcelas na mesma operação.
- `PUT /api/households/{householdId}/expenses/{expenseId}/lines` — substitui todas as parcelas, mantendo o total atual.
- `GET/POST /api/households/{householdId}/recurring-expenses`
- `POST /api/households/{householdId}/recurring-expenses/{recurringExpenseId}/materialize` — cria imediatamente todas as ocorrências vencidas da regra, sem duplicar movimentos existentes.
- `GET /api/households/{householdId}/dashboard/{year}/{month}`
- `POST /api/households/{householdId}/receipts/parse` — campo multipart `file`; não persiste o ficheiro.

Todas as rotas autenticadas esperam `Authorization: Bearer {token}`.
