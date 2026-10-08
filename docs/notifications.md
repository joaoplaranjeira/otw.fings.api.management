# Notificações

O backend usa uma outbox transacional. Alterações a despesas e movimentos criados por recorrências guardam o evento no mesmo `SaveChanges`; dois workers independentes transformam depois esses eventos em notificações e entregam-nas por Web Push ou In-App.

## Configuração

Configure VAPID apenas através de secrets/variáveis de ambiente em produção:

```text
WebPush__Subject=mailto:geral@fings.pt
WebPush__PublicKey=...
WebPush__PrivateKey=...
```

A chave privada nunca é exposta pela API. A migração `AddGenericNotifications` cria as tabelas, templates `pt-PT` e uma regra de nova despesa. As regras de sistema predefinidas notificam no cruzamento ascendente de 50%, 80% e 100% do orçamento mensal.

## Endpoints autenticados

```text
GET    /api/push/public-key
PUT    /api/push/subscriptions
GET    /api/push/subscriptions
DELETE /api/push/subscriptions/{id}

GET    /api/notification-rules
POST   /api/notification-rules
GET    /api/notification-rules/{id}
PATCH  /api/notification-rules/{id}
DELETE /api/notification-rules/{id}
POST   /api/notification-rules/{id}/test

GET    /api/notification-preferences
PUT    /api/notification-preferences

GET    /api/notifications?unreadOnly=true
POST   /api/notifications/{id}/read
```

O `PUT /api/push/subscriptions` associa sempre a subscrição ao utilizador do JWT. Não recebe `userId`. A eliminação desativa a subscrição para preservar histórico.

## Regras

Condições disponíveis:

- `Always`
- `BudgetThresholdCrossed`: `thresholdPercentage`, `direction` (`up`/`down`) e `categoryId` opcional
- `ExpenseAmountAbove`: `amount`
- `CategoryExpenseThreshold`: `categoryId` e `amount`
- `MonthlyExpenseThreshold`: `amount` e `categoryId` opcional

Políticas de destinatários:

- `AllHouseholdMembers`
- `HouseholdMembersExceptActor`
- `HouseholdOwners`
- `HouseholdManagers`
- `SpecificUsers` com `{ "userIds": [1, 2] }`
- `ActorOnly`

As regras de agregado só podem ser geridas por owners/administradores desse agregado. Regras de utilizador só podem pertencer ao próprio utilizador; regras de sistema exigem `User.Role == "Admin"`.

## Operação

Execute as migrações antes de iniciar a aplicação:

```bash
dotnet run -- --migrate
```

Os workers aplicam locks transacionais, deduplicação e cinco tentativas com backoff. Respostas Web Push `404`/`410` desativam o destino; `408`, `429` e `5xx` são repetidas. O payload enviado contém apenas título, corpo, ícone, URL de ação e tag de deduplicação.
