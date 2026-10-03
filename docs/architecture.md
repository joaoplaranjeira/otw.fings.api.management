# Arquitetura

## Fluxo

```text
HTTP → Controller → Service → Repository → FingsDbContext → MySQL
                     ├── Token/OTP/Loops
                     ├── RecurringExpenseMaterializer
                     └── OpenAiReceiptParser
```

- Controllers tratam binding e códigos HTTP.
- Services aplicam regras de negócio e isolamento por agregado.
- Repositories concentram EF Core, queries e transações.
- Entidades nunca são devolvidas diretamente no contrato HTTP.
- `ApiExceptionHandler` converte erros de aplicação em Problem Details.

## Orçamento e receitas

Um `BudgetPeriod` define um valor mensal constante entre `StartMonth` e `EndMonth`, inclusive. Os meses são normalizados para o primeiro dia. Não são permitidos períodos ativos sobrepostos.

A criação é transacional e insere uma `Income` planeada por mês. A relação `BudgetPeriodId + Date` é única. As alocações por categoria são mensais e a soma não pode ultrapassar `MonthlyAmount`.

## Despesas recorrentes

O worker interno executa no arranque e depois a cada seis horas. Materializa ocorrências vencidas de regras semanais, mensais, trimestrais ou anuais. O índice único `RecurringExpenseId + Date` impede duplicados.

## Talões

O endpoint lê a imagem para memória, valida tamanho, MIME e assinatura e avalia localmente resolução útil, nitidez, exposição e deteção do papel. Abaixo do limiar configurado, interrompe o fluxo antes da OpenAI, salvo quando o utilizador aceita explicitamente o risco. Antes da extração, deteta localmente a área de papel e, nos talões compridos, cria recortes verticais sobrepostos. Esses recortes seguem juntos na mesma chamada, aumentando o tamanho efetivo do texto sem introduzir uma segunda chamada de OCR; se a deteção não for segura, é usada a imagem original. A primeira chamada à OpenAI usa `OpenAI:ExtractionModel` e faz apenas extração factual/OCR. A segunda usa `OpenAI:Model` para classificar o comerciante e as linhas extraídas através de uma lista fechada de chaves, em que cada chave representa um par válido de categoria e subcategoria do agregado. Ambas usam amostragem determinística e Structured Outputs. Guardrails determinísticos corrigem combinações manifestamente incoerentes em contextos conhecidos. A imagem e o resultado completo não são persistidos. Existe uma segunda verificação experimental de valores, controlada por `OpenAI:VerifyReceiptValues`, mas permanece desativada por omissão porque aumenta significativamente a latência sem benefício consistente nos talões reais testados.

Após cada parse é persistido apenas um registo técnico de qualidade: agregado, utilizador, modelo, número de linhas, número de linhas classificadas, confiança média e estado de validação. A imagem, os artigos, os dados do comerciante e o resultado completo não são guardados. O utilizador pode marcar o parse como válido ou inválido e consultar o histórico agregado.

## Segurança

- OTP expira em 10 minutos, com limitação de pedidos, tentativas e bloqueio temporário.
- JWT usa HMAC SHA-256 e expira um mês de calendário após a emissão.
- Todas as operações financeiras confirmam a associação entre utilizador e agregado.
- `Viewer` tem acesso apenas de leitura.
- Segredos são fornecidos por ambiente ou user-secrets.
