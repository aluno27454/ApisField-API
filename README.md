# ApisField API

API REST que serve a app Android [ApisField](https://github.com/aluno27454/ApisField): autenticação, colmeias e leituras dos sensores, alertas, visitas com fotografias e avistamentos de vespa asiática.

Trabalho prático de **Desenvolvimento de Aplicações Móveis**, Licenciatura em Engenharia Informática, Instituto Politécnico de Tomar, 2026/27. A API dá suporte à app e não é avaliada. Os dados iniciais são fictícios, mas plausíveis (região de Tomar).

## Tecnologia

- ASP.NET Core (.NET 10), controllers
- Entity Framework Core + SQLite
- Autenticação JWT (Bearer)
- xUnit + `WebApplicationFactory` para testes de integração
- Alojamento: Azure App Service (Linux, B1, Always On)

## Como correr

```bash
dotnet run --project ApisField.Api        # http://localhost:5000
dotnet test                               # testes
```

A base de dados SQLite é criada e preenchida no primeiro arranque.

## Utilizadores iniciais

| Email | Palavra-passe | Papel |
|---|---|---|
| admin@apisfield.pt | Admin2026! | admin |
| andre@apisfield.pt | Abelha2026! | apicultor (4 colmeias) |
| samuel@apisfield.pt | Abelha2026! | apicultor (2 colmeias) |

## Documentação

- [Contrato da API](docs/CONTRATO.md): modelos JSON, rotas, validações e códigos de erro

## Autores

André Vassalo ([@aluno27454](https://github.com/aluno27454)) · Samuel Ponte ([@SamuelPonte](https://github.com/SamuelPonte))
