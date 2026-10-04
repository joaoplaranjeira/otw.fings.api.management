# Email OTP da Fings para Loops

## Importar no Loops

1. Cria um email do tipo **Transactional**.
2. Escolhe a opção de estilo por código e importa `fings-otp-loops.zip`.
3. Confirma que o Loops detetou a variável obrigatória `otpCode`.
4. Usa como assunto: `O teu código de acesso à Fings`.
5. Usa como preview text: `O teu código de acesso está pronto. É válido durante 10 minutos.`
6. Publica o email e copia o respetivo `transactionalId`.
7. Confirma que o domínio de envio está verificado no Loops.

## Configuração da API

Depois de publicares o email, configura no ficheiro `.env` da API:

```env
Loops__ApiKey=CHAVE_DA_API_LOOPS
Loops__TransactionalId=ID_DO_EMAIL_TRANSACIONAL_PUBLICADO
Loops__InvitationTransactionalId=ID_DO_EMAIL_TRANSACIONAL_DE_CONVITE
```

Não coloques estes valores em `appsettings.json` nem os adiciones ao repositório. O `transactionalId` tem de identificar a versão publicada deste email transacional.

O ficheiro `fings-otp.mjml` é a fonte editável. O ficheiro `fings-otp-preview.html` serve apenas para pré-visualização local; mostra o código fictício `482913`.

## Payload de envio

```json
{
  "transactionalId": "SUBSTITUIR_PELO_ID_DO_LOOPS",
  "email": "utilizador@exemplo.pt",
  "dataVariables": {
    "otpCode": "482913"
  }
}
```

Endpoint: `POST https://app.loops.so/api/v1/transactional`

`otpCode` é obrigatório e sensível a maiúsculas/minúsculas. Envia-o como string para preservar eventuais zeros à esquerda.

## Email de convite para agregado

Cria um segundo email transacional a partir de `fings-household-invitation.mjml`, publica-o e configura o respetivo ID em `Loops__InvitationTransactionalId`.

Variáveis obrigatórias:

- `householdName`;
- `role`;
- `invitationCode`;
- `invitationUrl`;
- `expiresAt`.

## Teste antes de produção

1. Confirma no ecrã de publicação do Loops que `otpCode` aparece como variável obrigatória.
2. Envia primeiro para um endereço `@example.com` ou `@test.com`; o Loops processa o pedido sem entregar uma mensagem real.
3. Envia depois para uma caixa de teste real e confirma assunto, preview, código, versão móvel e modo escuro.
4. Confirma nos Metrics do email transacional que o envio foi aceite.
