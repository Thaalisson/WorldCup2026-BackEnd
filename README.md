# WorldCup2026-BackEnd

API (.NET 8) do Bolão da Copa 2026. Hospedado no Railway; banco PostgreSQL (Supabase).

## Pontuação (IMPORTANTE)

A pontuação é **simples** e bate exatamente com a tela "Configurar Pontuação" do app.
Por jogo, cada palpite vale:

| Acerto | Pontos (padrão) |
|---|---|
| **Placar exato** (mandante e visitante) | **10** |
| **Vencedor / empate** (sem ser placar exato) | **5** |
| Errou o vencedor | **0** |

> ⚠️ **Não há bônus** de saldo de gols nem de "acertou os gols de um time".
> Esses bônus ocultos existiram no passado e foram **removidos** porque não apareciam
> na tela de configuração e confundiam (geravam "+7/+8" sem explicação).
> A regra vive em [`ScoringService.cs`](src/BolaoCopa.Application/Services/ScoringService.cs).
> Cada bolão pode personalizar os valores (10/5) em `Pools.PointsExactScore` / `PointsCorrectResult`.

Pontos de fase final (campeão, vice, 3º, classificados de grupo) são configuráveis por bolão
e contados separadamente.

## Fonte de resultados

Resultados reais vêm do **football-data.org** (competição `WC`, plano gratuito).
Cliente: [`FootballDataApiClient`](src/BolaoCopa.Infrastructure/ExternalApi/FootballDataApiClient.cs).
Token na env var `FootballData__Token`. Um job (Hangfire) sincroniza placares a cada 30 min
durante a Copa e recalcula o ranking.

## Comandos de manutenção (CLI)

Rodar de `src/BolaoCopa.Api` — executam a tarefa e encerram (não sobem o servidor):

```bash
dotnet run -- db-stats                 # contagens (times, jogos, palpites, usuários...)
dotnet run -- sync-results             # força sincronização de placares agora
dotnet run -- link-fixtures-dry        # prévia do vínculo jogos<->football-data (não grava)
dotnet run -- link-fixtures            # vincula jogos do banco aos IDs reais da API
dotnet run -- validate-scoring [bolão] # recalcula e confere pontos x ranking (não grava)
dotnet run -- preview-simple [bolão]   # prévia de ranking com a regra simples (não grava)
dotnet run -- recalc-all               # recalcula TODOS os bolões (grava)
dotnet run -- match-predictions <cod>  # palpites de todos num jogo (cod = FifaMatchCode)
dotnet run -- set-prediction <user> <bolão> <CASA> <FORA> <golsCasa> <golsFora>
dotnet run -- set-password <email-ou-nome> [nova-senha]
```

Config local fica em `appsettings.local.json` (fora do git). Segredos de produção
(conexão, JWT, token) ficam nas variáveis de ambiente do Railway.
