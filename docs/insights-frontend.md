# Integração frontend — Insights financeiros

## Objetivo

A API transforma o histórico financeiro do agregado em cartões explicáveis. Cada cartão inclui a conclusão, as métricas que a suportam, a dimensão da amostra, um nível de confiança e, quando aplicável, um filtro para abrir os movimentos de origem.

Os insights são calculados deterministicamente. Não é usado texto gerado por IA e não são persistidos resultados. Uma nova leitura reflete imediatamente alterações a despesas, orçamentos ou recorrências.

## Endpoint

```http
GET /api/households/{householdId}/insights/{year}/{month}
Authorization: Bearer {token}
```

Exemplo:

```http
GET /api/households/6c5966fc-96f0-4ff3-9dbe-cdb5b7c74101/insights/2027/1
```

O utilizador autenticado tem de pertencer ao agregado. `year` deve estar entre 1 e 9999 e `month` entre 1 e 12.

Para o mês corrente, os cálculos consideram apenas os dias decorridos na zona horária do agregado. Para meses passados, consideram o mês completo. Um mês futuro não produz conclusões baseadas em despesas.

## Contrato da resposta

```json
{
  "month": "2027-01-01",
  "generatedAtUtc": "2027-01-20T12:00:00+00:00",
  "currency": "EUR",
  "historyMonths": 3,
  "insights": [
    {
      "id": "budget-pace",
      "type": "BudgetPace",
      "severity": "Critical",
      "title": "92% do orçamento utilizado",
      "explanation": "Decorreram 64,5% dos dias do mês. A este ritmo, a projeção ultrapassa o orçamento em 170 EUR.",
      "confidence": 0.9,
      "sampleSize": 7,
      "metrics": [
        {
          "key": "spent",
          "label": "Gasto confirmado",
          "value": 368,
          "unit": "EUR"
        },
        {
          "key": "projected",
          "label": "Projeção",
          "value": 570.4,
          "unit": "EUR"
        }
      ],
      "expenseFilter": {
        "from": "2027-01-01",
        "to": "2027-01-20",
        "categoryId": null,
        "subcategoryId": null,
        "merchantName": null,
        "maximumAmount": null,
        "origin": null
      }
    }
  ]
}
```

### Campos de topo

| Campo | Tipo | Descrição |
|---|---|---|
| `month` | `date` | Primeiro dia do mês analisado. |
| `generatedAtUtc` | `datetime` | Instante em que a resposta foi calculada. |
| `currency` | `string` | Moeda configurada no agregado. Também é usada como `unit` nas métricas monetárias. |
| `historyMonths` | `number` | Número de meses anteriores com despesas confirmadas, até ao máximo de três. |
| `insights` | `Insight[]` | Apenas conclusões suportadas pelos dados disponíveis. |

### Insight

| Campo | Tipo | Descrição |
|---|---|---|
| `id` | `string` | Identificador estável dentro do tipo. Pode ser usado como React `key`. Uma despesa anómala inclui o ID da despesa. |
| `type` | `InsightType` | Determina ícone, componente e ação do cartão. |
| `severity` | `InsightSeverity` | Importância visual, não um diagnóstico financeiro. |
| `title` | `string` | Conclusão curta, pronta para apresentação. |
| `explanation` | `string` | Explicação legível da comparação efetuada. |
| `confidence` | `decimal` | Valor entre `0` e `1`. Reflete a robustez da amostra, não uma probabilidade. |
| `sampleSize` | `number` | Número de movimentos, regras ou meses usados, conforme o tipo. |
| `metrics` | `InsightMetric[]` | Valores estruturados para gráficos, badges ou acessibilidade. |
| `expenseFilter` | `InsightExpenseFilter \| null` | Filtro sugerido para mostrar os movimentos relacionados. |

### Métricas

`unit` pode conter:

- o código da moeda do agregado, por exemplo `EUR`;
- `percent`;
- `count`;
- `days`.

O frontend deve formatar `value` com `Intl.NumberFormat`, usando a unidade, e não extrair valores numéricos de `title` ou `explanation`.

## Tipos de insight

| `type` | Quando é devolvido | Informação principal |
|---|---|---|
| `BudgetPace` | Existe orçamento no mês. | Percentagem já usada, dias decorridos e projeção linear até ao fim do mês. |
| `SpendingTrend` | Existem pelo menos dois meses anteriores comparáveis. | Variação face à média dos últimos meses. Num mês em curso compara o mesmo número de dias. |
| `SpendingDriver` | Existe histórico suficiente e alguma categoria aumentou. | Categoria que mais contribuiu para o aumento e principal subcategoria atual. |
| `UnusualExpense` | Um comerciante tem pelo menos três compras anteriores e uma compra atual é materialmente superior ao padrão robusto. | Compra atual, mediana histórica e diferença percentual. |
| `SmallExpenses` | Existem pelo menos três movimentos positivos inferiores a 10 unidades monetárias. | Quantidade, total acumulado e peso na despesa. |
| `MerchantConcentration` | Existem despesas com comerciante identificado. | Peso dos três comerciantes com maior valor acumulado. |
| `RecurringCommitments` | Existem regras recorrentes aplicáveis ao mês. | Equivalente mensal e peso no orçamento. |
| `PotentialSubscriptions` | Um comerciante aparece em pelo menos três meses, aproximadamente a cada 25–35 dias, com variação de valor até 15%. | Número de padrões e estimativa mensal. É sempre uma sugestão a confirmar. |
| `BudgetAccuracy` | Uma categoria excedeu a alocação em pelo menos três meses avaliados ou existe orçamento não distribuído. | Recorrência do desvio ou montante por distribuir. |
| `DataQuality` | Existem despesas confirmadas no período. | Peso sem classificação útil e movimentos sem comerciante. |

Não se deve reservar espaço para todos os tipos. A ausência de um cartão significa que os dados não sustentam essa conclusão.

## Severidade

| Valor | Uso visual sugerido |
|---|---|
| `Information` | Neutro; azul/cinza. |
| `Positive` | Evolução favorável ou boa cobertura; verde. |
| `Warning` | Desvio que merece atenção; âmbar. |
| `Critical` | Projeção de orçamento mais de 15% acima do limite; vermelho. |

Não depender apenas da cor. Mostrar também ícone e texto acessível.

## Confiança e amostra

Sugestão de apresentação:

- `confidence >= 0.85`: não é necessário badge;
- `0.70 <= confidence < 0.85`: mostrar “Baseado em histórico limitado” no detalhe;
- `confidence < 0.70`: mostrar “Estimativa inicial”.

`sampleSize` deve aparecer no detalhe ou tooltip como contexto. Não o interpretar de forma uniforme: em `BudgetAccuracy` representa meses e em `RecurringCommitments` representa regras.

## Abrir os movimentos relacionados

O objeto `expenseFilter` usa os seguintes campos opcionais:

```ts
type InsightExpenseFilter = {
  from: string;
  to: string;
  categoryId: string | null;
  subcategoryId: string | null;
  merchantName: string | null;
  maximumAmount: number | null;
  origin: "Manual" | "Recurring" | "Receipt" | null;
};
```

Atualmente `GET /expenses` aceita diretamente apenas `from` e `to`. O frontend deve:

1. pedir `GET /api/households/{householdId}/expenses?from={from}&to={to}`;
2. aplicar localmente os restantes campos presentes no filtro;
3. ao filtrar por categoria ou subcategoria, considerar `expense.lines` quando existirem, tal como o dashboard.

`maximumAmount` usa comparação estrita (`amount < maximumAmount`), coerente com o insight de pequenas despesas.

## Exemplo React Query

```ts
export type InsightType =
  | "BudgetPace"
  | "SpendingTrend"
  | "SpendingDriver"
  | "UnusualExpense"
  | "SmallExpenses"
  | "MerchantConcentration"
  | "RecurringCommitments"
  | "PotentialSubscriptions"
  | "BudgetAccuracy"
  | "DataQuality";

export function useHouseholdInsights(
  householdId: string,
  year: number,
  month: number,
) {
  return useQuery({
    queryKey: ["insights", householdId, year, month],
    queryFn: () =>
      api.get<HouseholdInsightsResponse>(
        `/api/households/${householdId}/insights/${year}/${month}`,
      ),
    enabled: Boolean(householdId),
  });
}
```

Invalidar `['insights', householdId]` depois de criar, editar ou eliminar:

- uma despesa;
- um orçamento;
- uma alocação de orçamento;
- uma regra recorrente.

## Ordem e layout recomendados

A API devolve os cartões nesta ordem:

1. ritmo do orçamento;
2. evolução da despesa;
3. causa da variação;
4. despesa anómala;
5. pequenas despesas;
6. concentração por comerciante;
7. compromissos recorrentes;
8. possíveis subscrições;
9. qualidade do orçamento;
10. qualidade dos dados.

O frontend pode preservar esta ordem. Em ecrã pequeno, mostrar primeiro `Critical` e `Warning`, mantendo a ordem relativa dentro de cada severidade.

Estado vazio sugerido:

> Ainda não existe histórico suficiente para gerar insights. Continua a registar despesas e volta a consultar esta área.

## Regras e limitações

- Apenas despesas com estado `Confirmed` entram nos cálculos.
- Despesas canceladas e planeadas não são tratadas como gasto realizado.
- `CreatedByUserId` não é usado para inferir quem efetuou a compra.
- Rendimentos planeados gerados por orçamento não são usados para calcular poupança real.
- A projeção do orçamento é linear e deve ser apresentada como estimativa, não como previsão garantida.
- Uma possível subscrição é apenas um padrão de periodicidade e valor. O utilizador deve confirmá-la.
- Um talão com várias linhas é distribuído pelas categorias das linhas; o total do movimento é contado apenas uma vez nos totais globais.
