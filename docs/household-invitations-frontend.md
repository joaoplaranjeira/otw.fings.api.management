# Integração frontend — registo sem agregado e convites para agregados

Este documento define o contrato funcional proposto para permitir:

- registar um utilizador sem criar automaticamente um agregado familiar;
- convidar uma pessoa para um agregado por email;
- atribuir a role do futuro membro no momento do convite;
- aceitar o convite com um código ou através de um link que transporta esse código;
- mostrar o código e o link ao utilizador que cria o convite, para que os possa partilhar por mensagem.

> Esta funcionalidade está implementada no backend. Este documento constitui o contrato para a integração do frontend; o código e o Swagger são a fonte final de verdade em caso de divergência.

## 1. Conceitos e regras

Um convite pertence a um agregado, destina-se a um email e guarda a role que será atribuída quando for aceite.

- O código é gerado pelo backend, é único, não deve ser previsível e não é o código OTP de autenticação.
- O email usado para aceitar o convite tem de coincidir com o email do convite, depois de normalizado.
- A associação ao agregado e a marcação do convite como aceite devem ocorrer na mesma transação.
- Um convite só pode ser aceite uma vez e deixa de poder ser usado quando expira ou é revogado.
- O backend devolve sempre o link completo; o frontend não deve construir o URL a partir do código.
- O código completo e o link são devolvidos ao gestor na criação ou regeneração. Devem ser tratados como um segredo de utilização única.
- Sugestão de validade: 7 dias. A data efetiva é sempre indicada em `expiresAt`.
- Criar ou regenerar um convite nunca envia email automaticamente.
- O email só é enviado através do endpoint próprio, depois de uma ação explícita do utilizador no frontend.

Estados do convite:

| Valor | Nome | Significado |
|---:|---|---|
| `1` | `Pending` | Pode ser aceite |
| `2` | `Accepted` | Já foi utilizado |
| `3` | `Expired` | Ultrapassou a validade |
| `4` | `Revoked` | Foi cancelado pelo agregado |

## 2. Roles

O frontend deve continuar a obter as roles através de:

`GET /api/household-roles`

| Valor | Nome | Pode ser atribuída por `Owner` | Pode ser atribuída por `Administrator` |
|---:|---|:---:|:---:|
| `1` | `Owner` | Sim | Não |
| `2` | `Administrator` | Sim | Não |
| `3` | `Member` | Sim | Sim |
| `4` | `Viewer` | Sim | Sim |

Estas regras são validadas pelo backend. Ocultar opções sem permissão no frontend serve apenas para melhorar a experiência.

## 3. Registar um utilizador sem criar agregado

`POST /api/users`

O campo atual `householdName` passa a ser opcional. O novo campo `invitationCode` também é opcional.

### 3.1 Registo independente

```json
{
  "name": "Maria Santos",
  "username": "maria.santos",
  "email": "maria@example.com"
}
```

Resposta `201 Created`:

```json
{
  "id": 27,
  "name": "Maria Santos",
  "username": "maria.santos",
  "email": "maria@example.com",
  "isActive": true
}
```

Este registo não cria nenhum agregado nem nenhuma associação. Depois de autenticar, `GET /api/households` pode devolver `[]`. Nesse estado, o frontend deve apresentar as opções "Criar agregado" e "Entrar com convite" em vez de assumir que existe um agregado ativo.

### 3.2 Registo com criação de agregado

O fluxo atual continua disponível:

```json
{
  "name": "João Santos",
  "username": "joao.santos",
  "email": "joao@example.com",
  "householdName": "Família Santos"
}
```

O utilizador é associado ao novo agregado como `Owner`.

### 3.3 Registo através de convite

```json
{
  "name": "Maria Santos",
  "username": "maria.santos",
  "email": "maria@example.com",
  "invitationCode": "FINGS-7K4M-92QP"
}
```

O utilizador é criado e associado ao agregado com a role guardada no convite. O frontend não envia a role durante o registo.

`householdName` e `invitationCode` são mutuamente exclusivos. Se ambos forem enviados, a API responde `422 Unprocessable Entity`.

Erros adicionais do registo com convite:

| Estado | Situação |
|---:|---|
| `404` | Código desconhecido |
| `409` | Convite já aceite, utilizador já associado ao agregado, email ou username já registado |
| `410` | Convite expirado ou revogado |
| `422` | Email diferente do destinatário ou combinação inválida de campos |

Se o registo ou a associação falhar, não deve ficar criado um utilizador parcial.

## 4. Criar um convite

`POST /api/households/{householdId}/invitations`

Requer Bearer token e permissão de `Owner` ou `Administrator`.

```json
{
  "email": "maria@example.com",
  "role": 3
}
```

Resposta `201 Created`:

```json
{
  "id": "3a36f468-6128-48e4-a260-35571816490e",
  "householdId": "8af810fc-f7f5-4fde-bb63-b530ae8fb4a1",
  "householdName": "Família Santos",
  "email": "maria@example.com",
  "role": 3,
  "code": "FINGS-7K4M-92QP",
  "invitationUrl": "https://app.fings.pt/register?invitationCode=FINGS-7K4M-92QP",
  "emailSent": false,
  "status": 1,
  "expiresAt": "2026-10-11T14:30:00Z",
  "createdAt": "2026-10-04T14:30:00Z",
  "acceptedAt": null
}
```

O backend cria apenas o convite e o respetivo código. Não envia qualquer email neste endpoint.

Depois do sucesso, o frontend apresenta um modal com o código e o link, ambos com ação de copiar. O código deve permanecer visível para poder ser partilhado por SMS, WhatsApp ou outra mensagem. O modal pode também apresentar a opção "Enviar por email", que chama explicitamente o endpoint descrito na secção seguinte.

Erros relevantes da criação:

| Estado | Situação |
|---:|---|
| `400` | Email inválido ou payload malformado |
| `403` | Sem permissão ou `Administrator` a tentar atribuir `Owner`/`Administrator` |
| `404` | Agregado não encontrado |
| `409` | O email já pertence ao agregado ou já tem um convite pendente |
| `422` | Role inválida |

## 5. Enviar o convite por email

`POST /api/households/{householdId}/invitations/{invitationId}/send-email`

Requer `Owner` ou `Administrator` e envia por email o código já associado ao convite. Não cria um convite novo e não altera o código nem a validade.

Resposta `200 OK`:

```json
{
  "id": "3a36f468-6128-48e4-a260-35571816490e",
  "email": "maria@example.com",
  "emailSent": true
}
```

O email contém:

- nome do agregado;
- role que será atribuída;
- data de expiração;
- código;
- botão ou link para aceitar.

Se o fornecedor de email falhar, a API responde `200 OK` com `emailSent: false`. O convite continua ativo e o frontend deve informar que o email não foi entregue. O utilizador pode tentar novamente ou partilhar o código por outro canal.

Erros relevantes do envio:

| Estado | Situação |
|---:|---|
| `403` | Sem permissão para gerir o convite |
| `404` | Convite não encontrado |
| `409` | Convite já aceite |
| `410` | Convite expirado ou revogado |

## 6. Listar convites do agregado

`GET /api/households/{householdId}/invitations`

Requer `Owner` ou `Administrator`. Devolve convites pendentes e o histórico recente.

Resposta `200 OK`:

```json
[
  {
    "id": "3a36f468-6128-48e4-a260-35571816490e",
    "householdId": "8af810fc-f7f5-4fde-bb63-b530ae8fb4a1",
    "householdName": "Família Santos",
    "email": "maria@example.com",
    "role": 3,
    "emailSent": true,
    "status": 1,
    "expiresAt": "2026-10-11T14:30:00Z",
    "createdAt": "2026-10-04T14:30:00Z",
    "acceptedAt": null
  }
]
```

Por segurança, a listagem não volta a expor `code` nem `invitationUrl`. Para voltar a obter um código partilhável, o gestor usa a ação de regeneração, que invalida o código anterior e devolve um novo código e link.

## 7. Consultar um convite antes da autenticação

`GET /api/household-invitations/{code}`

Este endpoint é anónimo e serve para validar o código e apresentar contexto no registo. Por privacidade, não devolve o email completo.

Resposta `200 OK`:

```json
{
  "householdName": "Família Santos",
  "maskedEmail": "m***a@example.com",
  "role": 3,
  "expiresAt": "2026-10-11T14:30:00Z"
}
```

Erros:

| Estado | Situação |
|---:|---|
| `404` | Código desconhecido |
| `409` | Convite já aceite |
| `410` | Convite expirado ou revogado |

As respostas devem ser genéricas e não revelar se existe uma conta para o email convidado.

## 8. Aceitar um convite com um utilizador existente

`POST /api/household-invitations/{code}/accept`

Requer Bearer token. Não necessita de body: o backend usa o utilizador autenticado e confirma que o seu email corresponde ao destinatário.

Resposta `200 OK`:

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

Erros relevantes:

| Estado | Situação |
|---:|---|
| `401` | Utilizador sem sessão |
| `404` | Código desconhecido |
| `409` | Convite já aceite ou utilizador já associado ao agregado |
| `410` | Convite expirado ou revogado |
| `422` | O email da conta não corresponde ao email convidado |

Após aceitar, o frontend deve invalidar `GET /api/households`, selecionar o novo agregado e carregar os seus dados.

## 9. Revogar e regenerar

### Revogar

`DELETE /api/households/{householdId}/invitations/{invitationId}`

Resposta de sucesso: `204 No Content`.

Só convites pendentes podem ser revogados. Aplicam-se as mesmas permissões de gestão usadas na criação do convite.

### Regenerar o código

`POST /api/households/{householdId}/invitations/{invitationId}/regenerate-code`

Resposta `200 OK` com o mesmo formato da criação, incluindo o novo `code` e `invitationUrl`. O backend gera um novo código e uma nova validade, invalida o código anterior e define `emailSent: false`. Este endpoint não envia email. O frontend deve substituir imediatamente o código e link guardados em memória e, se o utilizador escolher essa opção, chamar depois o endpoint `send-email`.

## 10. Fluxos de interface

### 10.1 Criar convite

1. O gestor abre a área de membros e escolhe "Convidar membro".
2. O frontend carrega as roles e filtra as que o utilizador pode atribuir.
3. O gestor introduz o email e escolhe a role.
4. Depois do `201`, o frontend mostra o código e o link com botões "Copiar código" e "Copiar link".
5. Apenas se o gestor escolher "Enviar por email", o frontend chama `POST /api/households/{householdId}/invitations/{invitationId}/send-email`.
6. A lista de convites é atualizada sem misturar convites pendentes com membros efetivos.

### 10.2 Abrir o link do convite

O link usa o query parameter `invitationCode`:

```text
/register?invitationCode=FINGS-7K4M-92QP
```

Ao abrir a página, o frontend deve:

1. ler e normalizar `invitationCode` do URL;
2. preencher automaticamente o campo "Código do convite";
3. consultar o convite para mostrar agregado, role e validade;
4. manter o campo editável, para permitir corrigir um código recebido manualmente;
5. nunca guardar o código em logs, analytics ou mensagens de erro remotas.

### 10.3 Pessoa ainda não registada

1. O formulário inclui `invitationCode` no `POST /api/users`.
2. Depois do registo, o utilizador faz a autenticação OTP normal.
3. Após autenticar, o agregado recebido surge em `GET /api/households` com a role definida pelo convite.

### 10.4 Pessoa já registada

1. Se não tiver sessão, o frontend preserva temporariamente o código e encaminha para login OTP.
2. Depois da autenticação, chama `POST /api/household-invitations/{code}/accept`.
3. Em caso de sucesso, limpa o código preservado, atualiza os agregados e abre o agregado recebido.

### 10.5 Utilizador autenticado sem agregados

O estado vazio de `GET /api/households` deve apresentar:

- "Criar agregado";
- "Introduzir código de convite".

Ao introduzir um código, o frontend consulta primeiro o convite e pede confirmação antes de o aceitar.

## 11. Compatibilidade com a gestão de membros atual

O endpoint atual `POST /api/households/{householdId}/members` pode continuar disponível para associar imediatamente um utilizador já registado. O convite passa a ser o fluxo recomendado porque suporta pessoas ainda não registadas e deixa explícita a aceitação.

Depois de um convite ser aceite, o novo membro aparece normalmente em:

`GET /api/households/{householdId}/members`

O convite aceite não deve continuar na lista de pendentes, e a remoção futura do membro não reativa o convite.

## 12. Critérios de aceitação

- É possível criar uma conta sem `householdName` e obter uma lista de agregados vazia.
- É possível continuar a criar uma conta com um novo agregado.
- `Owner` e `Administrator` conseguem convidar de acordo com as suas permissões.
- A role é escolhida no convite e não pode ser alterada pela pessoa convidada.
- A criação ou regeneração expõe código e link ao gestor, sem enviar email automaticamente.
- O email só é enviado quando o frontend chama explicitamente `send-email`.
- O email enviado contém código, link, agregado, role e validade.
- O link abre o registo com o código automaticamente preenchido.
- O mesmo convite funciona para uma conta nova e para uma conta existente.
- Apenas a conta com o email convidado consegue aceitar.
- Um convite aceite, expirado ou revogado não pode voltar a ser usado.
- A aceitação nunca cria associações duplicadas.
- Falhas de registo ou aceitação não deixam dados parciais.
