<img width="100%" alt="Halloween 2026" src="https://baltaio.blob.core.windows.net/static/images/v4/challenges/halloween-2026/banner.jpg" />

## 🎃 Halloween - Desafio 2

Oi, eu sou o [seu nome aqui] e este é o espaço onde compartilho minha jornada de aprendizado durante o desafio **Halloween 2026**, realizado pelo [balta.io](https://balta.io). 👻

Aqui você vai encontrar projetos, exercícios e códigos que estou desenvolvendo durante o desafio.

### Sobre este desafio
Neste desafio o objetivo é criar um encurtador de URLs completo utilizando os conceitos de Spec-Driven Development

#### O que foi implementado
- [Listar detalhes da implementação]

Neste processo eu aprendi:
* ✅

## Bagde
<img src="https://baltaio.blob.core.windows.net/static/images/v4/challenges/halloween-2026/02.png" width="200" />

## Problema
--

## Sobre o Halloween 2026
O desafio **Halloween 2026** consiste em implementar implementar o modelo Spec Driven Development de ponta a ponta, criando apps completas com IA.

### Veja meu progresso no desafio
[Incluir link para o repositório central]

## Estrutura da solução
- `src/LinkShortener.Api`: API HTTP com Minimal API
- `src/LinkShortener.Application`: serviços e regras de aplicação
- `src/LinkShortener.Infrastructure`: persistência com EF Core e SQLite
- `tests/LinkShortener.Tests`: testes com xUnit

### Executar e validar
```powershell
dotnet build LinkShortener.sln
dotnet test LinkShortener.sln
dotnet run --project .\src\LinkShortener.Api --launch-profile http
```

Durante o desenvolvimento, a documentação OpenAPI fica em
`http://localhost:5041/swagger/v1/swagger.json` e a interface Swagger UI em
`http://localhost:5041/swagger`.

### Criar um link pela API
Com a API em execução, envie uma URL de destino:
```powershell
$link = Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5041/api/links `
  -ContentType "application/json" `
  -Body '{"targetUrl":"https://example.com"}'
```

A resposta contém `shortUrl`, `code` e `managementKey`. Guarde a chave: ela é
retornada somente na criação e necessária para consultar as estatísticas do link.
A base do link curto pode ser alterada
em `ShortLinks:BaseUrl`, no `appsettings.json`. A aplicação aplica as migrações
do banco ao iniciar. Para escolher um código personalizado, inclua a propriedade
`code` no corpo; ele deve ter de 4 a 20 caracteres, usando letras maiúsculas de
A a Z e dígitos de 1 a 9. Por exemplo:

```json
{
  "targetUrl": "https://example.com",
  "code": "MEULINK7"
}
```

O código personalizado já utilizado retorna `409 Conflict`; códigos inválidos
retornam `400 Bad Request` com `ProblemDetails`.

### Consultar estatísticas de acesso
Use a chave de gerenciamento retornada ao criar o link:

```powershell
Invoke-RestMethod `
  -Method Get `
  -Uri "http://localhost:5041/api/links/$($link.code)/stats" `
  -Headers @{ "X-Management-Key" = $link.managementKey }
```

Uma chave ausente ou inválida retorna `401 Unauthorized`. O banco armazena apenas
o hash da chave, não seu valor original.

### Cache de redirecionamentos
Links válidos resolvidos por redirecionamento são mantidos em cache na memória
do processo. O cache é limitado a 10.000 entradas e cada entrada expira após
5 minutos sem uso, no máximo em 30 minutos, ou quando o link expira. O contador
de acessos continua sendo atualizado no banco a cada redirecionamento válido.
