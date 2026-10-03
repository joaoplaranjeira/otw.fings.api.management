# otw.fings.api.management

Web API ASP.NET Core 10 para gestão financeira pessoal e familiar.

## Funcionalidades iniciais

- registo e autenticação passwordless por OTP;
- JWT Bearer válido durante um mês de calendário a partir da emissão;
- isolamento por agregado familiar;
- catálogo inicial de categorias e subcategorias criado automaticamente por agregado;
- orçamentos mensais válidos num intervalo de meses;
- criação automática de uma receita planeada por cada mês do orçamento;
- despesas com categoria obrigatória, subcategoria opcional, comerciante e NIF;
- dashboard mensal com orçamento, despesas e valor disponível;
- parsing transitório de talões com o SDK OpenAI, sem persistência da imagem ou do resultado.

## Configuração

Copiar as chaves de `.env.example` para variáveis de ambiente ou user-secrets. Segredos não devem ser colocados em `appsettings.json`.

Em desenvolvimento, `Otp__BypassEnabled=true` desativa o envio de email e permite autenticar utilizadores ativos com o código fixo `123123`. Esta opção deve permanecer desativada em produção.

```bash
dotnet restore
dotnet build --no-restore
dotnet run -- --migrate
dotnet run
```

As migrations `BackfillStandardCategories` e `AddNotApplicableCategories` acrescentam o catálogo inicial e as opções “Não aplicável” aos agregados já existentes, preservando categorias e subcategorias com o mesmo nome.

Swagger está disponível em `/swagger` em desenvolvimento e o health check em `/health`.

### Executar em Docker através do VS Code

1. Copiar `.env.example` para `.env` e preencher os segredos.
2. Garantir que `ConnectionStrings__DefaultConnection` é acessível a partir do container. Em Docker Desktop, uma base instalada no host deve usar `server=host.docker.internal` em vez de `server=localhost`.
3. Executar a task `docker-run-fings`.

A task remove um container anterior com o mesmo nome, constrói a imagem, aplica as migrations e inicia a API em `http://localhost:5015`. As tasks `docker-logs-fings` e `docker-stop-fings` permitem acompanhar e parar o container.

O contrato para o frontend está documentado em [`docs/frontend-integration.md`](docs/frontend-integration.md).

## Semântica do orçamento

`StartMonth` e `EndMonth` são inclusivos e normalizados para o primeiro dia do mês. Um orçamento de janeiro a junho com `MonthlyAmount = 1500` cria seis receitas planeadas de 1500 EUR. Períodos de orçamento ativos não podem sobrepor-se no mesmo agregado.

O valor disponível mensal é o orçamento aplicável menos as despesas confirmadas desse mês. Receitas criadas pelo orçamento são identificadas como `Planned` e `Budget`, pelo que continuam distinguíveis de receitas efetivamente recebidas.
# otw.fings.api.management
