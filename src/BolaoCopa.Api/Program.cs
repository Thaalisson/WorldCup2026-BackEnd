using System.Text;
using System.Threading.RateLimiting;
using BolaoCopa.Api;
using BolaoCopa.Application.Interfaces;
using BolaoCopa.Application.Services;
using BolaoCopa.Infrastructure.Data;
using BolaoCopa.Infrastructure.ExternalApi;
using BolaoCopa.Infrastructure.Repositories;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BolaoCopa.Api.Jobs;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Banco de dados
var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connStr));

// JWT — reads token from HttpOnly cookie or Authorization header
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
        };
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Cookies.TryGetValue("access_token", out var token))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

// Hangfire (usa o mesmo SQL Server)
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connStr)));
builder.Services.AddHangfireServer();

// HttpClient para football-data.org (plano gratuito cobre a Copa do Mundo — competição "WC")
builder.Services.AddHttpClient<IFootballApiClient, FootballDataApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.football-data.org/v4/");
    client.DefaultRequestHeaders.Add("X-Auth-Token", builder.Configuration["FootballData:Token"]);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Serviços de domínio
builder.Services.AddScoped<IScoringService, ScoringService>();
builder.Services.AddScoped<IRankingService, RankingService>();
builder.Services.AddScoped<IMatchSyncService, MatchSyncService>();

// Repositórios
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<IPredictionRepository, PredictionRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPoolRepository, PoolRepository>();
builder.Services.AddScoped<ITeamRepository, TeamRepository>();
builder.Services.AddScoped<IChampionPredictionRepository, ChampionPredictionRepository>();
builder.Services.AddScoped<IFeedRepository, FeedRepository>();
builder.Services.AddScoped<IRankingSnapshotRepository, RankingSnapshotRepository>();
builder.Services.AddScoped<IGroupPredictionRepository, GroupPredictionRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

// Jobs
builder.Services.AddScoped<ResultSyncJob>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var raw = builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:5173";
        var origins = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        policy.AllowAnyHeader().AllowAnyMethod().WithOrigins(origins).AllowCredentials();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("register", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("refresh", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

// CLI de manutenção (não sobe o servidor web nem exige login):
//   dotnet run -- reset-tournament   -> apaga jogos/seleções fictícios + palpites (mantém usuários/pools)
//   dotnet run -- import-fixtures     -> importa os jogos REAIS da Copa via football-data.org
//   dotnet run -- sync-results        -> grava placares finalizados e recalcula ranking
if (args.Length > 0 && args[0] is "reset-tournament" or "import-fixtures" or "sync-results"
        or "db-stats" or "link-fixtures" or "link-fixtures-dry" or "match-predictions"
        or "set-password" or "validate-scoring" or "set-prediction" or "preview-simple"
        or "recalc-all" or "report" or "setup-audit" or "audit-log" or "user-predictions" or "logins"
        or "export-predictions" or "set-prediction-all" or "add-participant" or "copy-predictions"
        or "copy-group-predictions" or "score-groups" or "group-bonus" or "sync-fixtures"
        or "set-champion" or "check-champion" or "check-group-preds" or "ranking-history" or "missing-preds"
        or "set-match-teams")
{
    await using var cliScope = app.Services.CreateAsyncScope();
    var sp = cliScope.ServiceProvider;

    switch (args[0])
    {
        case "copy-group-predictions":
        {
            var ctxG = sp.GetRequiredService<AppDbContext>();
            if (args.Length < 3) { Console.WriteLine("Uso: copy-group-predictions <origem> <destino> [bolão=BOLAO_BRP2026]"); break; }
            var fromU = await ctxG.Users.Where(u => u.Email == args[1] || u.Name == args[1]).ToListAsync();
            var toU = await ctxG.Users.Where(u => u.Email == args[2] || u.Name == args[2]).ToListAsync();
            if (fromU.Count != 1) { Console.WriteLine($"Origem '{args[1]}': {fromU.Count} encontrado(s) — use email."); break; }
            if (toU.Count != 1) { Console.WriteLine($"Destino '{args[2]}': {toU.Count} encontrado(s) — use email."); break; }
            var poolNameG = args.Length > 3 ? args[3] : "BOLAO_BRP2026";
            var poolG = await ctxG.Pools.FirstOrDefaultAsync(p => p.Name == poolNameG || p.InviteCode == poolNameG);
            if (poolG is null) { Console.WriteLine($"Bolão '{poolNameG}' não encontrado."); break; }

            var teamsG = await ctxG.Teams.ToDictionaryAsync(t => t.Id, t => t.Code);
            var src = await ctxG.GroupPredictions.Where(g => g.PoolId == poolG.Id && g.UserId == fromU[0].Id).ToListAsync();
            if (src.Count == 0) { Console.WriteLine($"{fromU[0].Name} não tem palpites de grupo em {poolG.Name}."); break; }
            var dst = (await ctxG.GroupPredictions.Where(g => g.PoolId == poolG.Id && g.UserId == toU[0].Id).ToListAsync())
                .ToDictionary(g => g.GroupName);

            int criados = 0, atualizados = 0;
            foreach (var s in src.OrderBy(g => g.GroupName))
            {
                if (dst.TryGetValue(s.GroupName, out var d))
                {
                    d.FirstPlaceTeamId = s.FirstPlaceTeamId;
                    d.SecondPlaceTeamId = s.SecondPlaceTeamId;
                    d.PointsEarned = s.PointsEarned;
                    atualizados++;
                }
                else
                {
                    await ctxG.GroupPredictions.AddAsync(new BolaoCopa.Domain.Entities.GroupPrediction
                    {
                        Id = Guid.NewGuid(), PoolId = poolG.Id, UserId = toU[0].Id, GroupName = s.GroupName,
                        FirstPlaceTeamId = s.FirstPlaceTeamId, SecondPlaceTeamId = s.SecondPlaceTeamId,
                        PointsEarned = s.PointsEarned
                    });
                    criados++;
                }
                Console.WriteLine($"  Grupo {s.GroupName}: 1º {teamsG.GetValueOrDefault(s.FirstPlaceTeamId, "?")} · 2º {teamsG.GetValueOrDefault(s.SecondPlaceTeamId, "?")}");
            }
            await ctxG.SaveChangesAsync();
            Console.WriteLine($"[copy-group-predictions] {fromU[0].Name} → {toU[0].Name} em {poolG.Name}: {criados} criados, {atualizados} atualizados");
            break;
        }

        case "score-groups":
        {
            // Pontua palpites de classificação de grupo para todos os bolões.
            // Regra: qualifierPts (padrão=3) por cada time que o usuário previu e que realmente ficou no top-2 do grupo.
            // Desempate da tabela: pontos → saldo de gols → gols marcados (igual à tabela oficial).
            var ctxSg = sp.GetRequiredService<AppDbContext>();
            var rankingSg = sp.GetRequiredService<IRankingService>();
            var poolsSg = await ctxSg.Pools.OrderBy(p => p.Name).ToListAsync();
            var userNamesSg = await ctxSg.Users.ToDictionaryAsync(u => u.Id, u => u.Name);

            int totalPoolsSg = 0;
            foreach (var poolSg in poolsSg)
            {
                await rankingSg.ScoreGroupPredictionsForPoolAsync(poolSg.Id);

                // Relatório por bolão
                var groupPredsSg = await ctxSg.GroupPredictions
                    .Where(gp => gp.PoolId == poolSg.Id && gp.PointsEarned > 0)
                    .ToListAsync();
                var byUserSg = groupPredsSg
                    .GroupBy(gp => gp.UserId)
                    .ToDictionary(g => g.Key, g => g.Sum(gp => gp.PointsEarned));

                var participantsSg = await ctxSg.PoolParticipants
                    .Where(pp => pp.PoolId == poolSg.Id)
                    .OrderByDescending(pp => pp.TotalPoints)
                    .ToListAsync();

                Console.WriteLine($"\n── {poolSg.Name} (qualifierPts={poolSg.PointsGroupQualifier}) ──");
                foreach (var p in participantsSg)
                {
                    var name = userNamesSg.GetValueOrDefault(p.UserId, "?");
                    var gPts = byUserSg.GetValueOrDefault(p.UserId, 0);
                    Console.WriteLine($"  {name,-25} grupos={gPts,3} pts | total={p.TotalPoints,4} pts");
                }
                totalPoolsSg++;
            }
            Console.WriteLine($"\n[score-groups] Concluído: {totalPoolsSg} bolão(ões) pontuados.");
            break;
        }

        case "group-bonus":
        {
            // group-bonus <bolão> <pts> <user1> [user2 ...]
            // Adiciona <pts> ao TotalPoints de cada usuário no bolão (para compensar quem esqueceu de palpitar nos grupos).
            // Uso: dotnet run -- group-bonus BOLAO_BRP2026 54 "Andre de Martini" Renan Brenno israel
            if (args.Length < 4) { Console.WriteLine("Uso: group-bonus <bolão> <pts> <user1> [user2 ...]"); break; }
            var ctxGb = sp.GetRequiredService<AppDbContext>();
            var poolGb = await ctxGb.Pools.FirstOrDefaultAsync(p => p.Name == args[1] || p.InviteCode == args[1]);
            if (poolGb is null) { Console.WriteLine($"Bolão '{args[1]}' não encontrado."); break; }
            if (!int.TryParse(args[2], out var bonusPts) || bonusPts < 0) { Console.WriteLine("Pts deve ser um número positivo."); break; }

            var userNamesGb = args.Skip(3).ToList();
            int aplicados = 0;
            foreach (var nameOrEmail in userNamesGb)
            {
                var us = await ctxGb.Users.Where(u => u.Email == nameOrEmail || u.Name == nameOrEmail).ToListAsync();
                if (us.Count != 1) { Console.WriteLine($"  '{nameOrEmail}': {us.Count} usuário(s) encontrado(s) — ignorado."); continue; }

                var part = await ctxGb.PoolParticipants.FirstOrDefaultAsync(pp => pp.PoolId == poolGb.Id && pp.UserId == us[0].Id);
                if (part is null) { Console.WriteLine($"  {us[0].Name}: não é participante de {poolGb.Name} — ignorado."); continue; }

                part.TotalPoints += bonusPts;
                Console.WriteLine($"  {us[0].Name}: +{bonusPts} pts → total agora = {part.TotalPoints}");
                aplicados++;
            }
            await ctxGb.SaveChangesAsync();
            Console.WriteLine($"[group-bonus] {aplicados}/{userNamesGb.Count} usuário(s) atualizado(s) em {poolGb.Name}.");
            break;
        }

        case "set-champion":
        {
            // set-champion <bolão> <usuario> <1°cod> <2°cod> <3°cod>
            // Ex.: dotnet run -- set-champion BRP_2026 "Lucas Rittes" BRA FRA ARG
            if (args.Length < 6) { Console.WriteLine("Uso: set-champion <bolão> <usuario> <1°time> <2°time> <3°time>"); break; }
            var ctxSc = sp.GetRequiredService<AppDbContext>();
            var poolSc = await ctxSc.Pools.FirstOrDefaultAsync(p => p.Name == args[1] || p.InviteCode == args[1]);
            if (poolSc is null) { Console.WriteLine($"Bolão '{args[1]}' não encontrado."); break; }

            var usersSc = await ctxSc.Users.Where(u => u.Email == args[2] || u.Name == args[2]).ToListAsync();
            if (usersSc.Count != 1) { Console.WriteLine($"Usuário '{args[2]}': {usersSc.Count} encontrado(s)."); break; }
            var userSc = usersSc[0];

            var t1 = await ctxSc.Teams.FirstOrDefaultAsync(t => t.Code == args[3]);
            var t2 = await ctxSc.Teams.FirstOrDefaultAsync(t => t.Code == args[4]);
            var t3 = await ctxSc.Teams.FirstOrDefaultAsync(t => t.Code == args[5]);
            if (t1 is null) { Console.WriteLine($"Time '{args[3]}' não encontrado."); break; }
            if (t2 is null) { Console.WriteLine($"Time '{args[4]}' não encontrado."); break; }
            if (t3 is null) { Console.WriteLine($"Time '{args[5]}' não encontrado."); break; }

            var existing = await ctxSc.ChampionPredictions
                .FirstOrDefaultAsync(cp => cp.PoolId == poolSc.Id && cp.UserId == userSc.Id);
            if (existing is not null)
            {
                existing.ChampionTeamId  = t1.Id;
                existing.RunnerUpTeamId  = t2.Id;
                existing.ThirdPlaceTeamId = t3.Id;
                ctxSc.ChampionPredictions.Update(existing);
            }
            else
            {
                await ctxSc.ChampionPredictions.AddAsync(new BolaoCopa.Domain.Entities.ChampionPrediction
                {
                    Id = Guid.NewGuid(),
                    PoolId = poolSc.Id,
                    UserId = userSc.Id,
                    ChampionTeamId  = t1.Id,
                    RunnerUpTeamId  = t2.Id,
                    ThirdPlaceTeamId = t3.Id,
                });
            }
            await ctxSc.SaveChangesAsync();
            Console.WriteLine($"[set-champion] {userSc.Name} em {poolSc.Name}: 1° {t1.Name} · 2° {t2.Name} · 3° {t3.Name}");
            break;
        }

        case "check-champion":
        {
            // check-champion <bolão>  → mostra quem preencheu e quem não preencheu palpite de campeão
            if (args.Length < 2) { Console.WriteLine("Uso: check-champion <bolão>"); break; }
            var ctxCh = sp.GetRequiredService<AppDbContext>();
            var poolCh = await ctxCh.Pools.FirstOrDefaultAsync(p => p.Name == args[1] || p.InviteCode == args[1]);
            if (poolCh is null) { Console.WriteLine($"Bolão '{args[1]}' não encontrado."); break; }

            var parts = await (
                from pp in ctxCh.PoolParticipants
                join u in ctxCh.Users on pp.UserId equals u.Id
                where pp.PoolId == poolCh.Id
                select new { u.Id, u.Name }
            ).ToListAsync();

            var picks = await ctxCh.ChampionPredictions
                .Where(cp => cp.PoolId == poolCh.Id)
                .ToListAsync();

            var teamIds = picks.SelectMany(p => new[] { p.ChampionTeamId, p.RunnerUpTeamId, p.ThirdPlaceTeamId })
                               .Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
            var teams = await ctxCh.Teams.Where(t => teamIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name);

            var pickByUser = picks.ToDictionary(p => p.UserId);

            Console.WriteLine($"\n[check-champion] {poolCh.Name} — {parts.Count} participante(s)\n");
            Console.WriteLine("✅ PREENCHERAM:");
            foreach (var p in parts.Where(p => pickByUser.ContainsKey(p.Id)))
            {
                var pick = pickByUser[p.Id];
                var t1 = pick.ChampionTeamId  != Guid.Empty ? teams.GetValueOrDefault(pick.ChampionTeamId,  "?") : "—";
                var t2 = pick.RunnerUpTeamId.HasValue       ? teams.GetValueOrDefault(pick.RunnerUpTeamId.Value,  "?") : "—";
                var t3 = pick.ThirdPlaceTeamId.HasValue     ? teams.GetValueOrDefault(pick.ThirdPlaceTeamId.Value, "?") : "—";
                Console.WriteLine($"  {p.Name}: 1°{t1} · 2°{t2} · 3°{t3}");
            }
            Console.WriteLine("\n❌ NÃO PREENCHERAM:");
            foreach (var p in parts.Where(p => !pickByUser.ContainsKey(p.Id)))
                Console.WriteLine($"  {p.Name}");
            Console.WriteLine();
            break;
        }

        case "copy-predictions":
        {
            var ctxC = sp.GetRequiredService<AppDbContext>();
            if (args.Length < 4) { Console.WriteLine("Uso: copy-predictions <email-ou-nome> <bolaoOrigem> <bolaoDestino>"); break; }
            var usC = await ctxC.Users.Where(u => u.Email == args[1] || u.Name == args[1]).ToListAsync();
            if (usC.Count != 1) { Console.WriteLine($"Usuário '{args[1]}': {usC.Count} encontrado(s) — use o email."); break; }
            var uidC = usC[0].Id;

            // Origem: entre os bolões que casam o nome, o que o usuário PARTICIPA (resolve nome duplicado).
            var origCandidatos = await ctxC.Pools.Where(p => p.Name == args[2] || p.InviteCode == args[2]).ToListAsync();
            var origViaPart = new List<BolaoCopa.Domain.Entities.Pool>();
            foreach (var p in origCandidatos)
                if (await ctxC.PoolParticipants.AnyAsync(pp => pp.PoolId == p.Id && pp.UserId == uidC)
                    || await ctxC.Predictions.AnyAsync(pr => pr.PoolId == p.Id && pr.UserId == uidC))
                    origViaPart.Add(p);
            if (origViaPart.Count != 1) { Console.WriteLine($"Origem '{args[2]}': {origViaPart.Count} bolão(ões) com esse usuário — ambíguo."); break; }
            var origem = origViaPart[0];

            var destino = await ctxC.Pools.FirstOrDefaultAsync(p => p.Name == args[3] || p.InviteCode == args[3]);
            if (destino is null) { Console.WriteLine($"Destino '{args[3]}' não encontrado."); break; }

            // Garante participação no destino
            var partC = await ctxC.PoolParticipants.FirstOrDefaultAsync(pp => pp.PoolId == destino.Id && pp.UserId == uidC);
            if (partC is null)
            {
                partC = new BolaoCopa.Domain.Entities.PoolParticipant { Id = Guid.NewGuid(), PoolId = destino.Id, UserId = uidC, JoinedAt = DateTime.UtcNow };
                await ctxC.PoolParticipants.AddAsync(partC);
            }

            var srcPreds = await ctxC.Predictions.Where(p => p.PoolId == origem.Id && p.UserId == uidC).ToListAsync();
            var dstPreds = await ctxC.Predictions.Where(p => p.PoolId == destino.Id && p.UserId == uidC).ToListAsync();
            var dstByMatch = dstPreds.ToDictionary(p => p.MatchId);
            int criados = 0, atualizados = 0;
            foreach (var s in srcPreds)
            {
                if (dstByMatch.TryGetValue(s.MatchId, out var d))
                {
                    if (d.HomeScorePrediction != s.HomeScorePrediction || d.AwayScorePrediction != s.AwayScorePrediction)
                    {
                        d.HomeScorePrediction = s.HomeScorePrediction;
                        d.AwayScorePrediction = s.AwayScorePrediction;
                        d.UpdatedAt = DateTime.UtcNow;
                        atualizados++;
                    }
                }
                else
                {
                    await ctxC.Predictions.AddAsync(new BolaoCopa.Domain.Entities.Prediction
                    {
                        Id = Guid.NewGuid(), PoolId = destino.Id, UserId = uidC, MatchId = s.MatchId,
                        HomeScorePrediction = s.HomeScorePrediction, AwayScorePrediction = s.AwayScorePrediction
                    });
                    criados++;
                }
            }
            await ctxC.SaveChangesAsync();

            // Repontua só o usuário no destino (jogos finalizados)
            var cfgC = new BolaoCopa.Application.DTOs.ScoringConfigDto(
                destino.PointsExactScore, destino.PointsCorrectResult, destino.PointsChampion,
                destino.PointsRunnerUp, destino.PointsThirdPlace, destino.PointsGroupQualifier);
            var scoringC = sp.GetRequiredService<IScoringService>();
            var finC = await ctxC.Matches.Where(m => m.IsFinished).ToDictionaryAsync(m => m.Id);
            var nowPreds = await ctxC.Predictions.Where(p => p.PoolId == destino.Id && p.UserId == uidC && finC.Keys.Contains(p.MatchId)).ToListAsync();
            int totC = 0, exC = 0, ceC = 0;
            foreach (var pr in nowPreds)
            {
                var m = finC[pr.MatchId];
                var pts = scoringC.CalculateMatchPredictionPoints(pr, m, cfgC);
                pr.PointsEarned = pts; totC += pts;
                if (pr.HomeScorePrediction == m.HomeScore && pr.AwayScorePrediction == m.AwayScore) exC++;
                else if (pts > 0) ceC++;
            }
            partC.TotalPoints = totC; partC.ExactScores = exC; partC.CorrectResults = ceC;
            await ctxC.SaveChangesAsync();

            Console.WriteLine($"[copy-predictions] {usC[0].Name}: {origem.Name} → {destino.Name} | " +
                $"{criados} criados, {atualizados} atualizados | novos pontos em {destino.Name}: {totC} ({exC} exatos, {ceC} vencedor)");
            break;
        }

        case "add-participant":
        {
            var ctxAp = sp.GetRequiredService<AppDbContext>();
            if (args.Length < 3) { Console.WriteLine("Uso: add-participant <email-ou-nome> <bolão>"); break; }
            var usAp = await ctxAp.Users.Where(u => u.Email == args[1] || u.Name == args[1]).ToListAsync();
            if (usAp.Count != 1) { Console.WriteLine($"Usuário '{args[1]}': {usAp.Count} encontrado(s) — use o email."); break; }
            var poolAp = await ctxAp.Pools.FirstOrDefaultAsync(p => p.Name == args[2] || p.InviteCode == args[2]);
            if (poolAp is null) { Console.WriteLine($"Bolão '{args[2]}' não encontrado."); break; }

            var part = await ctxAp.PoolParticipants.FirstOrDefaultAsync(pp => pp.PoolId == poolAp.Id && pp.UserId == usAp[0].Id);
            var jaEra = part is not null;
            if (part is null)
            {
                part = new BolaoCopa.Domain.Entities.PoolParticipant
                { Id = Guid.NewGuid(), PoolId = poolAp.Id, UserId = usAp[0].Id, JoinedAt = DateTime.UtcNow };
                await ctxAp.PoolParticipants.AddAsync(part);
            }

            // Pontua SÓ os palpites do Andre nesse bolão (não mexe nos outros participantes).
            var cfgAp = new BolaoCopa.Application.DTOs.ScoringConfigDto(
                poolAp.PointsExactScore, poolAp.PointsCorrectResult, poolAp.PointsChampion,
                poolAp.PointsRunnerUp, poolAp.PointsThirdPlace, poolAp.PointsGroupQualifier);
            var scoringAp = sp.GetRequiredService<IScoringService>();
            var finishedAp = await ctxAp.Matches.Where(m => m.IsFinished).ToDictionaryAsync(m => m.Id);
            var predsAp = await ctxAp.Predictions
                .Where(p => p.PoolId == poolAp.Id && p.UserId == usAp[0].Id && finishedAp.Keys.Contains(p.MatchId))
                .ToListAsync();

            int totalAp = 0, exatosAp = 0, certosAp = 0;
            foreach (var pr in predsAp)
            {
                var m = finishedAp[pr.MatchId];
                var pts = scoringAp.CalculateMatchPredictionPoints(pr, m, cfgAp);
                pr.PointsEarned = pts;
                totalAp += pts;
                if (pr.HomeScorePrediction == m.HomeScore && pr.AwayScorePrediction == m.AwayScore) exatosAp++;
                else if (pts > 0) certosAp++;
            }
            part.TotalPoints = totalAp;
            part.ExactScores = exatosAp;
            part.CorrectResults = certosAp;
            await ctxAp.SaveChangesAsync();

            var totPredsAp = await ctxAp.Predictions.CountAsync(p => p.PoolId == poolAp.Id && p.UserId == usAp[0].Id);
            Console.WriteLine($"[add-participant] {usAp[0].Name} {(jaEra ? "já era participante" : "ADICIONADO")} em {poolAp.Name}. " +
                $"Palpites no bolão: {totPredsAp} | pontos: {totalAp} ({exatosAp} exatos, {certosAp} no vencedor)");
            break;
        }

        case "set-prediction-all":
        {
            var ctxA = sp.GetRequiredService<AppDbContext>();
            if (args.Length < 6) { Console.WriteLine("Uso: set-prediction-all <email-ou-nome> <CASA> <FORA> <golsCasa> <golsFora>"); break; }
            var usA = await ctxA.Users.Where(u => u.Email == args[1] || u.Name == args[1]).ToListAsync();
            if (usA.Count != 1) { Console.WriteLine($"Usuário '{args[1]}': {usA.Count} encontrado(s) — use o email."); break; }
            var homeA = await ctxA.Teams.FirstOrDefaultAsync(t => t.Code == args[2]);
            var awayA = await ctxA.Teams.FirstOrDefaultAsync(t => t.Code == args[3]);
            if (homeA is null || awayA is null) { Console.WriteLine("Código de seleção inválido."); break; }
            var matchA = await ctxA.Matches.FirstOrDefaultAsync(m => m.HomeTeamId == homeA.Id && m.AwayTeamId == awayA.Id);
            if (matchA is null) { Console.WriteLine($"Jogo {args[2]}×{args[3]} não encontrado."); break; }
            if (matchA.IsFinished) { Console.WriteLine($"Jogo {args[2]}×{args[3]} JÁ ENCERROU — não vou alterar palpite de jogo finalizado."); break; }
            var hsA = int.Parse(args[4]); var asA = int.Parse(args[5]);

            // Todos os bolões em que o usuário participa
            var poolIdsA = await ctxA.PoolParticipants.Where(pp => pp.UserId == usA[0].Id).Select(pp => pp.PoolId).ToListAsync();
            var poolNamesA = await ctxA.Pools.Where(p => poolIdsA.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);

            int novos = 0, atualizados = 0;
            foreach (var pid in poolIdsA)
            {
                var pr = await ctxA.Predictions.FirstOrDefaultAsync(p => p.PoolId == pid && p.UserId == usA[0].Id && p.MatchId == matchA.Id);
                if (pr is null)
                {
                    await ctxA.Predictions.AddAsync(new BolaoCopa.Domain.Entities.Prediction
                    {
                        Id = Guid.NewGuid(), PoolId = pid, UserId = usA[0].Id, MatchId = matchA.Id,
                        HomeScorePrediction = hsA, AwayScorePrediction = asA
                    });
                    novos++;
                }
                else { pr.HomeScorePrediction = hsA; pr.AwayScorePrediction = asA; pr.UpdatedAt = DateTime.UtcNow; atualizados++; }
                Console.WriteLine($"  [{poolNamesA.GetValueOrDefault(pid)}] {args[2]} {hsA}×{asA} {args[3]}");
            }
            await ctxA.SaveChangesAsync();
            Console.WriteLine($"[set-prediction-all] {usA[0].Name} | {args[2]} {hsA}×{asA} {args[3]} | {poolIdsA.Count} bolão(ões) ({novos} criados, {atualizados} atualizados)");
            break;
        }

        case "set-prediction":
        {
            if (args.Length < 7)
            {
                Console.WriteLine("Uso: set-prediction <email-ou-nome> <bolao> <CASA> <FORA> <golsCasa> <golsFora>");
                Console.WriteLine("Ex.: set-prediction \"Marcos Mendes\" FFC_BOLAO AUS TUR 2 0");
                break;
            }
            var ctxp = sp.GetRequiredService<AppDbContext>();
            var users = await ctxp.Users.Where(u => u.Email == args[1] || u.Name == args[1]).ToListAsync();
            if (users.Count != 1) { Console.WriteLine($"Usuário '{args[1]}': {users.Count} encontrado(s) — use o email exato."); break; }
            var pool = await ctxp.Pools.FirstOrDefaultAsync(p => p.Name == args[2] || p.InviteCode == args[2]);
            if (pool is null) { Console.WriteLine($"Bolão '{args[2]}' não encontrado."); break; }
            var home = await ctxp.Teams.FirstOrDefaultAsync(t => t.Code == args[3]);
            var away = await ctxp.Teams.FirstOrDefaultAsync(t => t.Code == args[4]);
            if (home is null || away is null) { Console.WriteLine("Código de seleção inválido."); break; }
            var match = await ctxp.Matches.FirstOrDefaultAsync(m => m.HomeTeamId == home.Id && m.AwayTeamId == away.Id);
            if (match is null) { Console.WriteLine($"Jogo {args[3]}×{args[4]} não encontrado."); break; }
            var hs = int.Parse(args[5]); var asc = int.Parse(args[6]);

            var pred = await ctxp.Predictions.FirstOrDefaultAsync(p =>
                p.PoolId == pool.Id && p.UserId == users[0].Id && p.MatchId == match.Id);
            var novo = pred is null;
            if (pred is null)
            {
                pred = new BolaoCopa.Domain.Entities.Prediction
                {
                    Id = Guid.NewGuid(), PoolId = pool.Id, UserId = users[0].Id, MatchId = match.Id,
                    HomeScorePrediction = hs, AwayScorePrediction = asc
                };
                await ctxp.Predictions.AddAsync(pred);
            }
            else { pred.HomeScorePrediction = hs; pred.AwayScorePrediction = asc; pred.UpdatedAt = DateTime.UtcNow; }
            await ctxp.SaveChangesAsync();

            // Se o jogo já acabou, reprocessa a pontuação (idempotente: só o palpite alterado muda).
            if (match.IsFinished)
                await sp.GetRequiredService<IRankingService>().RecalculateForMatchAsync(match);

            var pts = (await ctxp.Predictions.AsNoTracking().FirstAsync(p => p.Id == pred.Id)).PointsEarned;
            Console.WriteLine($"Palpite {(novo ? "criado" : "atualizado")}: {users[0].Name} | {pool.Name} | " +
                $"{args[3]} {hs}×{asc} {args[4]} | jogo finalizado={match.IsFinished} | pontos deste jogo={pts}");
            break;
        }

        case "validate-scoring":
            var validator = new BolaoCopa.Api.ScoringValidator(
                sp.GetRequiredService<AppDbContext>(),
                sp.GetRequiredService<IScoringService>());
            Console.WriteLine(await validator.RunAsync(args.Length > 1 ? args[1] : null));
            break;

        case "recalc-all":
        {
            var ctxr = sp.GetRequiredService<AppDbContext>();
            var ranking = sp.GetRequiredService<IRankingService>();
            var poolIds = await ctxr.Pools.Select(p => new { p.Id, p.Name }).ToListAsync();
            foreach (var pl in poolIds)
            {
                await ranking.RecalculateForPoolAsync(pl.Id);
                Console.WriteLine($"[recalc-all] {pl.Name} recalculado.");
            }
            Console.WriteLine($"[recalc-all] Concluído: {poolIds.Count} bolão(ões).");
            break;
        }

        case "preview-simple":
            var previewer = new BolaoCopa.Api.ScoringValidator(
                sp.GetRequiredService<AppDbContext>(),
                sp.GetRequiredService<IScoringService>());
            Console.WriteLine(await previewer.PreviewSimpleAsync(args.Length > 1 ? args[1] : null));
            break;

        case "export-predictions":
        {
            var ctxe = sp.GetRequiredService<AppDbContext>();
            if (args.Length < 2) { Console.WriteLine("Uso: export-predictions <email-ou-nome> [bolão]"); break; }
            var eu = await ctxe.Users.Where(u => u.Email == args[1] || u.Name == args[1]).ToListAsync();
            if (eu.Count != 1) { Console.WriteLine($"Usuário '{args[1]}': {eu.Count} encontrado(s) — use o email."); break; }

            var teamsE = await ctxe.Teams.ToDictionaryAsync(t => t.Id);
            var matchesE = await ctxe.Matches.ToDictionaryAsync(m => m.Id);
            var poolsE = await ctxe.Pools.ToDictionaryAsync(p => p.Id, p => p.Name);

            var qe = ctxe.Predictions.Where(p => p.UserId == eu[0].Id);
            if (args.Length > 2)
            {
                var pf = await ctxe.Pools.Where(p => p.Name == args[2] || p.InviteCode == args[2]).Select(p => p.Id).ToListAsync();
                qe = qe.Where(p => pf.Contains(p.PoolId));
            }
            var predsE = await qe.ToListAsync();

            var items = predsE
                .OrderBy(p => poolsE.GetValueOrDefault(p.PoolId))
                .ThenBy(p => matchesE.GetValueOrDefault(p.MatchId)?.KickoffAt)
                .Select(p =>
                {
                    var m = matchesE.GetValueOrDefault(p.MatchId);
                    var home = m != null ? teamsE.GetValueOrDefault(m.HomeTeamId) : null;
                    var away = m != null ? teamsE.GetValueOrDefault(m.AwayTeamId) : null;
                    return new
                    {
                        pool = poolsE.GetValueOrDefault(p.PoolId),
                        jogo = home != null && away != null ? $"{home.Code} x {away.Code}" : "?",
                        mandante = home?.Name,
                        visitante = away?.Name,
                        kickoff = m?.KickoffAt,
                        palpite = new { mandante = p.HomeScorePrediction, visitante = p.AwayScorePrediction },
                        resultado = m != null && m.IsFinished ? new { mandante = m.HomeScore, visitante = m.AwayScore } : null,
                        pontos = p.PointsEarned,
                        criadoEm = p.CreatedAt,
                        editadoEm = p.UpdatedAt
                    };
                }).ToList();

            var payload = new { usuario = eu[0].Name, email = eu[0].Email, geradoEm = DateTime.UtcNow, total = items.Count, palpites = items };
            var json = System.Text.Json.JsonSerializer.Serialize(payload,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

            var fileName = $"palpites_{eu[0].Name.Replace(" ", "_")}.json";
            await System.IO.File.WriteAllTextAsync(fileName, json);
            Console.WriteLine(json);
            Console.WriteLine($">> {items.Count} palpite(s) salvos em {fileName}");
            break;
        }

        case "logins":
        {
            var ctxl = sp.GetRequiredService<AppDbContext>();
            if (args.Length < 2) { Console.WriteLine("Uso: logins <email-ou-nome>"); break; }
            var lu = await ctxl.Users.Where(u => u.Email == args[1] || u.Name == args[1]).ToListAsync();
            if (lu.Count != 1) { Console.WriteLine($"Usuário '{args[1]}': {lu.Count} encontrado(s)."); break; }
            var toks = await ctxl.RefreshTokens.Where(t => t.UserId == lu[0].Id)
                .OrderByDescending(t => t.CreatedAt).Take(15).ToListAsync();
            Console.WriteLine($"[logins] {lu[0].Name} <{lu[0].Email}> — {toks.Count} sessão(ões) recente(s) (CreatedAt = login):");
            foreach (var tk in toks)
                Console.WriteLine($"  login {tk.CreatedAt:dd/MM HH:mm} | expira {tk.ExpiresAt:dd/MM} | revogado={tk.IsRevoked}");
            break;
        }

        case "user-predictions":
        {
            var ctxu = sp.GetRequiredService<AppDbContext>();
            if (args.Length < 2) { Console.WriteLine("Uso: user-predictions <email-ou-nome> [bolão]"); break; }
            var us = await ctxu.Users.Where(u => u.Email == args[1] || u.Name == args[1]).ToListAsync();
            if (us.Count != 1) { Console.WriteLine($"Usuário '{args[1]}': {us.Count} encontrado(s) — use o email."); break; }
            var uid2 = us[0].Id;

            var teams2 = await ctxu.Teams.ToDictionaryAsync(t => t.Id);
            var matches2 = await ctxu.Matches.ToDictionaryAsync(m => m.Id);
            var poolsById = await ctxu.Pools.ToDictionaryAsync(p => p.Id, p => p.Name);

            var q = ctxu.Predictions.Where(p => p.UserId == uid2);
            if (args.Length > 2)
            {
                var poolFilter = await ctxu.Pools
                    .Where(p => p.Name == args[2] || p.InviteCode == args[2])
                    .Select(p => p.Id).ToListAsync();
                q = q.Where(p => poolFilter.Contains(p.PoolId));
            }
            var preds = await q.ToListAsync();

            Console.WriteLine($"[user-predictions] {us[0].Name} <{us[0].Email}> — {preds.Count} palpite(s)");
            Console.WriteLine("  (Criado = quando palpitou · Editado = se mudou depois)");
            foreach (var p in preds.OrderBy(p => poolsById.GetValueOrDefault(p.PoolId)).ThenBy(p => matches2.GetValueOrDefault(p.MatchId)?.KickoffAt))
            {
                var m = matches2.GetValueOrDefault(p.MatchId);
                var lbl = m != null && teams2.ContainsKey(m.HomeTeamId) && teams2.ContainsKey(m.AwayTeamId)
                    ? $"{teams2[m.HomeTeamId].Code}×{teams2[m.AwayTeamId].Code}" : "?";
                var pool = poolsById.GetValueOrDefault(p.PoolId) ?? "?";
                var edit = p.UpdatedAt.HasValue ? $"✏️ EDITADO em {p.UpdatedAt:dd/MM HH:mm}" : "—";
                Console.WriteLine($"  [{pool}] {lbl} = {p.HomeScorePrediction}×{p.AwayScorePrediction} | criado {p.CreatedAt:dd/MM HH:mm} | {edit}");
            }
            break;
        }

        case "setup-audit":
        {
            const string auditDdl = """
                CREATE TABLE IF NOT EXISTS prediction_audit (
                  id bigserial PRIMARY KEY, prediction_id uuid, pool_id uuid, user_id uuid, match_id uuid,
                  operation text, old_home int, old_away int, new_home int, new_away int,
                  old_points int, new_points int, db_user text, changed_at timestamptz NOT NULL DEFAULT now());

                CREATE OR REPLACE FUNCTION fn_prediction_audit() RETURNS trigger AS $$
                BEGIN
                  IF (TG_OP = 'DELETE') THEN
                    INSERT INTO prediction_audit(prediction_id,pool_id,user_id,match_id,operation,old_home,old_away,new_home,new_away,old_points,new_points,db_user)
                    VALUES (OLD."Id",OLD."PoolId",OLD."UserId",OLD."MatchId",'DELETE',OLD."HomeScorePrediction",OLD."AwayScorePrediction",NULL,NULL,OLD."PointsEarned",NULL,current_user);
                    RETURN OLD;
                  ELSIF (TG_OP = 'UPDATE') THEN
                    IF (OLD."HomeScorePrediction" IS DISTINCT FROM NEW."HomeScorePrediction" OR OLD."AwayScorePrediction" IS DISTINCT FROM NEW."AwayScorePrediction") THEN
                      INSERT INTO prediction_audit(prediction_id,pool_id,user_id,match_id,operation,old_home,old_away,new_home,new_away,old_points,new_points,db_user)
                      VALUES (NEW."Id",NEW."PoolId",NEW."UserId",NEW."MatchId",'UPDATE',OLD."HomeScorePrediction",OLD."AwayScorePrediction",NEW."HomeScorePrediction",NEW."AwayScorePrediction",OLD."PointsEarned",NEW."PointsEarned",current_user);
                    END IF;
                    RETURN NEW;
                  ELSIF (TG_OP = 'INSERT') THEN
                    INSERT INTO prediction_audit(prediction_id,pool_id,user_id,match_id,operation,old_home,old_away,new_home,new_away,old_points,new_points,db_user)
                    VALUES (NEW."Id",NEW."PoolId",NEW."UserId",NEW."MatchId",'INSERT',NULL,NULL,NEW."HomeScorePrediction",NEW."AwayScorePrediction",NULL,NEW."PointsEarned",current_user);
                    RETURN NEW;
                  END IF;
                  RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS trg_prediction_audit ON "Predictions";
                CREATE TRIGGER trg_prediction_audit AFTER INSERT OR UPDATE OR DELETE ON "Predictions"
                FOR EACH ROW EXECUTE FUNCTION fn_prediction_audit();
                """;
            await sp.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(auditDdl);
            Console.WriteLine("[setup-audit] Tabela prediction_audit + trigger criados (idempotente).");
            break;
        }

        case "audit-log":
        {
            var ctxa = sp.GetRequiredService<AppDbContext>();
            var n = args.Length > 1 && int.TryParse(args[1], out var x) ? x : 30;
            var rows = await ctxa.Database.SqlQueryRaw<AuditRow>(
                "SELECT id \"Id\", prediction_id \"PredictionId\", pool_id \"PoolId\", user_id \"UserId\", " +
                "match_id \"MatchId\", operation \"Operation\", old_home \"OldHome\", old_away \"OldAway\", " +
                "new_home \"NewHome\", new_away \"NewAway\", db_user \"DbUser\", changed_at \"ChangedAt\" " +
                "FROM prediction_audit ORDER BY changed_at DESC LIMIT " + n).ToListAsync();

            if (rows.Count == 0) { Console.WriteLine("[audit-log] Nenhum registro ainda (alterações futuras serão gravadas)."); break; }

            var users = await ctxa.Users.ToDictionaryAsync(u => u.Id, u => u.Name);
            var pools = await ctxa.Pools.ToDictionaryAsync(p => p.Id, p => p.Name);
            var teams = await ctxa.Teams.ToDictionaryAsync(t => t.Id);
            var matches = await ctxa.Matches.ToDictionaryAsync(m => m.Id);
            Console.WriteLine($"[audit-log] últimas {rows.Count} alterações de palpite:");
            foreach (var r in rows)
            {
                var who = r.UserId is Guid uid && users.TryGetValue(uid, out var un) ? un : "?";
                var pool = r.PoolId is Guid pid && pools.TryGetValue(pid, out var pn) ? pn : "?";
                var mt = "?";
                if (r.MatchId is Guid mid && matches.TryGetValue(mid, out var m)
                    && teams.TryGetValue(m.HomeTeamId, out var ht) && teams.TryGetValue(m.AwayTeamId, out var at))
                    mt = $"{ht.Code}×{at.Code}";
                var oldv = r.OldHome.HasValue ? $"{r.OldHome}×{r.OldAway}" : "—";
                var newv = r.NewHome.HasValue ? $"{r.NewHome}×{r.NewAway}" : "—";
                Console.WriteLine($"  {r.ChangedAt:dd/MM HH:mm} | {r.Operation,-6} | {pool} | {who} | {mt} | {oldv} → {newv} | por {r.DbUser}");
            }
            break;
        }

        case "report":
            var reporter = new BolaoCopa.Api.ScoringValidator(
                sp.GetRequiredService<AppDbContext>(),
                sp.GetRequiredService<IScoringService>());
            var report = await reporter.ReportAsync(args.Length > 1 ? args[1] : null);
            Console.WriteLine(report);
            await System.IO.File.WriteAllTextAsync("relatorio_pontos.txt", report);
            Console.WriteLine(">> Salvo em relatorio_pontos.txt");
            break;

        case "link-fixtures":
        case "link-fixtures-dry":
            var linker = new BolaoCopa.Api.FixtureLinker(
                sp.GetRequiredService<AppDbContext>(),
                sp.GetRequiredService<IFootballApiClient>());
            Console.WriteLine(await linker.RunAsync(apply: args[0] == "link-fixtures"));
            break;

        case "set-password":
        {
            var ctx3 = sp.GetRequiredService<AppDbContext>();
            var id = args.Length > 1 ? args[1] : "";
            var newPwd = args.Length > 2 ? args[2] : "Bolao@2026";
            if (string.IsNullOrWhiteSpace(id))
            {
                Console.WriteLine("Uso: set-password <email-ou-nome> [nova-senha]");
                break;
            }

            var found = await ctx3.Users
                .Where(u => u.Email == id || u.Name == id)
                .ToListAsync();

            if (found.Count == 0) { Console.WriteLine($"Nenhum usuário com email/nome '{id}'."); break; }
            if (found.Count > 1)
            {
                Console.WriteLine($"Mais de um usuário encontrado para '{id}' — use o EMAIL exato:");
                foreach (var u in found) Console.WriteLine($"  {u.Name} <{u.Email}>");
                break;
            }

            var user = found[0];
            user.PasswordHash = BolaoCopa.Infrastructure.Helpers.PasswordHasher.Hash(newPwd);
            await ctx3.SaveChangesAsync();
            Console.WriteLine($"Senha redefinida para {user.Name} <{user.Email}>. Nova senha: {newPwd}");
            break;
        }

        case "match-predictions":
        {
            var ctx2 = sp.GetRequiredService<AppDbContext>();
            var code = args.Length > 1 ? args[1] : "1"; // FifaMatchCode (1 = México x África do Sul)
            var match = await ctx2.Matches.FirstOrDefaultAsync(m => m.FifaMatchCode == code);
            if (match is null) { Console.WriteLine($"Jogo de código {code} não encontrado."); break; }

            var home = await ctx2.Teams.FirstAsync(t => t.Id == match.HomeTeamId);
            var away = await ctx2.Teams.FirstAsync(t => t.Id == match.AwayTeamId);
            var placar = match.IsFinished ? $"{match.HomeScore}-{match.AwayScore}" : "(não finalizado)";
            Console.WriteLine($"=== {home.Code} {home.Name} x {away.Name} {away.Code} | resultado: {placar} ===");

            var rows = await (from p in ctx2.Predictions
                              join u in ctx2.Users on p.UserId equals u.Id
                              join pool in ctx2.Pools on p.PoolId equals pool.Id
                              where p.MatchId == match.Id
                              orderby pool.Name, u.Name
                              select new { Pool = pool.Name, User = u.Name,
                                           H = p.HomeScorePrediction, A = p.AwayScorePrediction,
                                           Pts = p.PointsEarned }).ToListAsync();

            Console.WriteLine($"Total de palpites: {rows.Count}");
            foreach (var r in rows)
            {
                var acertou = match.IsFinished && r.H == match.HomeScore && r.A == match.AwayScore ? " ✔ PLACAR EXATO" : "";
                Console.WriteLine($"  [{r.Pool}] {r.User}: {r.H}-{r.A} | pts={r.Pts}{acertou}");
            }
            break;
        }

        case "db-stats":
            var ctx = sp.GetRequiredService<AppDbContext>();
            Console.WriteLine($"[db-stats] Teams={await ctx.Teams.CountAsync()} " +
                $"Matches={await ctx.Matches.CountAsync()} " +
                $"Finished={await ctx.Matches.CountAsync(m => m.IsFinished)} " +
                $"Predictions={await ctx.Predictions.CountAsync()} " +
                $"Users={await ctx.Users.CountAsync()} " +
                $"Pools={await ctx.Pools.CountAsync()}");
            break;

        case "reset-tournament":
            var db = sp.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(@"
                DELETE FROM ""Predictions"";
                DELETE FROM ""FeedEvents"";
                DELETE FROM ""GroupPredictions"";
                DELETE FROM ""ChampionPredictions"";
                DELETE FROM ""Players"";
                DELETE FROM ""RankingSnapshots"";
                DELETE FROM ""Matches"";
                DELETE FROM ""Teams"";
            ");
            Console.WriteLine("[reset-tournament] Chaveamento ficticio removido (usuarios e pools mantidos).");
            break;

        case "import-fixtures":
            var count = await sp.GetRequiredService<IMatchSyncService>().ImportFixturesAsync();
            Console.WriteLine($"[import-fixtures] {count} jogo(s) real(is) importado(s).");
            break;

        case "set-match-teams":
        {
            // Uso: set-match-teams <apiFixtureId> <codeCasa> <codeFora>
            // Ex.: set-match-teams 100 ARG SUI
            if (args.Length < 4) { Console.WriteLine("Uso: set-match-teams <apiFixtureId> <codeCasa> <codeFora>"); break; }
            var smtCtx = sp.GetRequiredService<AppDbContext>();
            if (!int.TryParse(args[1], out var fixtureId)) { Console.WriteLine("fixtureId inválido."); break; }
            var match = await smtCtx.Matches.FirstOrDefaultAsync(m => m.ApiFootballFixtureId == fixtureId)
                     ?? await smtCtx.Matches.FirstOrDefaultAsync(m => m.FifaMatchCode == args[1]);
            if (match is null) { Console.WriteLine($"Jogo com ApiFixtureId={fixtureId} não encontrado."); break; }
            var home = await smtCtx.Teams.FirstOrDefaultAsync(t => t.Code == args[2].ToUpper());
            var away = await smtCtx.Teams.FirstOrDefaultAsync(t => t.Code == args[3].ToUpper());
            if (home is null) { Console.WriteLine($"Time '{args[2]}' não encontrado."); break; }
            if (away is null) { Console.WriteLine($"Time '{args[3]}' não encontrado."); break; }
            match.HomeTeamId = home.Id;
            match.AwayTeamId = away.Id;
            await smtCtx.SaveChangesAsync();
            Console.WriteLine($"[set-match-teams] Fixture {fixtureId} → {home.Code} {home.Name} x {away.Name} {away.Code}");
            break;
        }

        case "missing-preds":
        {
            // Uso: missing-preds <bolao> <nome-parcial>
            if (args.Length < 3) { Console.WriteLine("Uso: missing-preds <bolao> <nome>"); break; }
            var mpCtx = sp.GetRequiredService<AppDbContext>();
            var mpPool = await mpCtx.Pools.FirstOrDefaultAsync(p => p.Name == args[1] || p.InviteCode == args[1]);
            if (mpPool is null) { Console.WriteLine($"Bolão '{args[1]}' não encontrado."); break; }
            var mpUsers = await mpCtx.Users.Where(u => u.Name.ToLower().Contains(args[2].ToLower())).ToListAsync();
            if (mpUsers.Count == 0) { Console.WriteLine($"Usuário '{args[2]}' não encontrado."); break; }
            if (mpUsers.Count > 1) { Console.WriteLine($"Mais de um: {string.Join(", ", mpUsers.Select(u => u.Name))}"); break; }
            var mpUser = mpUsers[0];

            var finishedMatches = await (
                from m in mpCtx.Matches
                join ht in mpCtx.Teams on m.HomeTeamId equals ht.Id
                join at in mpCtx.Teams on m.AwayTeamId equals at.Id
                where m.IsFinished
                orderby m.KickoffAt
                select new { m.Id, m.KickoffAt, m.Stage, HomeCode = ht.Code, AwayCode = at.Code, m.HomeScore, m.AwayScore }
            ).ToListAsync();

            var userPredMatchIds = (await mpCtx.Predictions
                .Where(p => p.PoolId == mpPool.Id && p.UserId == mpUser.Id)
                .Select(p => p.MatchId).ToListAsync()).ToHashSet();

            var missing = finishedMatches.Where(m => !userPredMatchIds.Contains(m.Id)).ToList();
            Console.WriteLine($"\n[missing-preds] {mpUser.Name} | {mpPool.Name} | {missing.Count} jogo(s) sem palpite de {finishedMatches.Count} finalizados:");
            foreach (var m in missing) {
                var stage = m.Stage switch {
                    BolaoCopa.Domain.Enums.TournamentStage.GroupStage   => "Grupos",
                    BolaoCopa.Domain.Enums.TournamentStage.RoundOf32    => "R32",
                    BolaoCopa.Domain.Enums.TournamentStage.RoundOf16    => "Oitavas",
                    BolaoCopa.Domain.Enums.TournamentStage.QuarterFinal => "Quartas",
                    BolaoCopa.Domain.Enums.TournamentStage.SemiFinal    => "Semi",
                    _ => "?"
                };
                Console.WriteLine($"  [{stage}] {m.HomeCode}×{m.AwayCode} {m.HomeScore}-{m.AwayScore} ({m.KickoffAt:dd/MM HH:mm})");
            }
            break;
        }

        case "ranking-history":
        {
            // Uso: ranking-history <bolao> [nome-parcial-jogador]
            if (args.Length < 2) { Console.WriteLine("Uso: ranking-history <bolao> [nome-parcial]"); break; }
            var rhCtx = sp.GetRequiredService<AppDbContext>();
            var rhPool = await rhCtx.Pools.FirstOrDefaultAsync(p => p.Name == args[1] || p.InviteCode == args[1]);
            if (rhPool is null) { Console.WriteLine($"Bolão '{args[1]}' não encontrado."); break; }
            string? focusName = args.Length > 2 ? args[2].ToLower() : null;

            // Participantes do bolão com TotalPoints atual
            var participants = await (
                from pp in rhCtx.PoolParticipants
                join u in rhCtx.Users on pp.UserId equals u.Id
                where pp.PoolId == rhPool.Id
                orderby u.Name
                select new { u.Id, u.Name, pp.TotalPoints }
            ).ToListAsync();

            // Pontos do mata-mata por jogador (para deduzir do total e obter base pré-mata-mata)
            var knockoutEarned = await (
                from pred in rhCtx.Predictions
                join m in rhCtx.Matches on pred.MatchId equals m.Id
                where pred.PoolId == rhPool.Id && m.IsFinished && m.Stage != BolaoCopa.Domain.Enums.TournamentStage.GroupStage
                select new { pred.UserId, pred.PointsEarned }
            ).ToListAsync();
            var knockoutEarnedByUser = knockoutEarned
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.PointsEarned));

            // Base = TotalPoints atual - pontos mata-mata (inclui bônus de grupos)
            var groupPts = participants.ToDictionary(
                p => p.Id,
                p => p.TotalPoints - knockoutEarnedByUser.GetValueOrDefault(p.Id, 0));

            // Jogos do mata-mata finalizados, em ordem cronológica
            var knockoutMatches = await (
                from m in rhCtx.Matches
                join ht in rhCtx.Teams on m.HomeTeamId equals ht.Id
                join at in rhCtx.Teams on m.AwayTeamId equals at.Id
                where m.IsFinished && m.Stage != BolaoCopa.Domain.Enums.TournamentStage.GroupStage
                orderby m.KickoffAt
                select new { m.Id, m.KickoffAt, m.Stage, m.HomeScore, m.AwayScore,
                             HomeCode = ht.Code, AwayCode = at.Code }
            ).ToListAsync();

            if (!knockoutMatches.Any()) { Console.WriteLine("Nenhum jogo do mata-mata finalizado ainda."); break; }

            // Palpites do mata-mata por jogo
            var knockoutPreds = await (
                from pred in rhCtx.Predictions
                join m in rhCtx.Matches on pred.MatchId equals m.Id
                where pred.PoolId == rhPool.Id && m.IsFinished && m.Stage != BolaoCopa.Domain.Enums.TournamentStage.GroupStage
                select new { pred.UserId, pred.MatchId, pred.PointsEarned }
            ).ToListAsync();
            var predsByMatch = knockoutPreds
                .GroupBy(p => p.MatchId)
                .ToDictionary(g => g.Key, g => g.ToDictionary(p => p.UserId, p => p.PointsEarned));

            static string StagePt(BolaoCopa.Domain.Enums.TournamentStage s) => s switch {
                BolaoCopa.Domain.Enums.TournamentStage.RoundOf32    => "R32",
                BolaoCopa.Domain.Enums.TournamentStage.RoundOf16    => "Oitavas",
                BolaoCopa.Domain.Enums.TournamentStage.QuarterFinal => "Quartas",
                BolaoCopa.Domain.Enums.TournamentStage.SemiFinal    => "Semi",
                BolaoCopa.Domain.Enums.TournamentStage.ThirdPlace   => "3º lugar",
                BolaoCopa.Domain.Enums.TournamentStage.Final        => "Final",
                _ => "?"
            };

            // Acumular pontos e registrar posição após cada jogo
            var cumulative = participants.ToDictionary(p => p.Id, p => groupPts[p.Id]);

            Console.WriteLine($"\n🏆 {rhPool.Name} — EVOLUÇÃO DO RANKING (mata-mata)");
            Console.WriteLine($"   Pontos de grupos contabilizados antes do mata-mata.\n");

            // Linha de cabeçalho com posição inicial
            var initialRanking = participants
                .Select(p => new { p.Id, p.Name, Pts = cumulative[p.Id] })
                .OrderByDescending(x => x.Pts).ThenBy(x => x.Name)
                .Select((x, i) => new { x.Id, x.Name, x.Pts, Pos = i + 1 })
                .ToList();

            if (focusName != null) {
                var init = initialRanking.FirstOrDefault(x => x.Name.ToLower().Contains(focusName));
                if (init != null)
                    Console.WriteLine($"  📍 ANTES DO MATA-MATA: {init.Pos}° {init.Name} — {init.Pts} pts");
            } else {
                Console.WriteLine("  📍 ANTES DO MATA-MATA:");
                foreach (var x in initialRanking)
                    Console.WriteLine($"      {x.Pos}° {x.Name} — {x.Pts} pts");
            }
            Console.WriteLine();

            foreach (var km in knockoutMatches) {
                var matchPreds = predsByMatch.GetValueOrDefault(km.Id, new());
                foreach (var p in participants)
                    if (matchPreds.TryGetValue(p.Id, out var earned))
                        cumulative[p.Id] += earned;

                var ranking = participants
                    .Select(p => new { p.Id, p.Name, Pts = cumulative[p.Id], MatchPts = matchPreds.GetValueOrDefault(p.Id, -1) })
                    .OrderByDescending(x => x.Pts).ThenBy(x => x.Name)
                    .Select((x, i) => new { x.Id, x.Name, x.Pts, x.MatchPts, Pos = i + 1 })
                    .ToList();

                var label = $"{StagePt(km.Stage)} {km.HomeCode}×{km.AwayCode} {km.HomeScore}-{km.AwayScore}";

                if (focusName != null) {
                    var player = ranking.FirstOrDefault(x => x.Name.ToLower().Contains(focusName));
                    if (player != null) {
                        var ptsLabel = player.MatchPts >= 0 ? $"+{player.MatchPts}" : "sem palpite";
                        Console.WriteLine($"  [{label}]  {player.Pos}° {player.Name} — {player.Pts} pts ({ptsLabel})");
                    }
                } else {
                    Console.WriteLine($"  [{label}]");
                    foreach (var x in ranking) {
                        var ptsLabel = x.MatchPts >= 0 ? $"+{x.MatchPts}" : "sem palpite";
                        Console.WriteLine($"      {x.Pos}° {x.Name} — {x.Pts} pts ({ptsLabel})");
                    }
                    Console.WriteLine();
                }
            }
            Console.WriteLine();
            break;
        }

        case "sync-results":
            await sp.GetRequiredService<IMatchSyncService>().SyncResultsAsync();
            Console.WriteLine("[sync-results] Placares sincronizados e ranking recalculado.");
            break;

        case "sync-fixtures":
        {
            var sfCount = await sp.GetRequiredService<IMatchSyncService>().SyncFixtureTeamsAsync();
            Console.WriteLine($"[sync-fixtures] {sfCount} jogo(s) com times atualizados da API.");
            break;
        }
    }

    return;
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[]
    {
        new HangfireAuthFilter(
            app.Configuration["Jwt:Secret"]!,
            app.Configuration["Jwt:Issuer"]!,
            app.Configuration["Jwt:Audience"]!)
    }
});

app.MapControllers();

// Ensure RefreshTokens table exists (idempotent — safe to run on every startup)
await using (var startupScope = app.Services.CreateAsyncScope())
{
    var db = startupScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS ""RefreshTokens"" (
            ""Id""         uuid                        NOT NULL,
            ""UserId""     uuid                        NOT NULL,
            ""Token""      text                        NOT NULL,
            ""ExpiresAt""  timestamp with time zone    NOT NULL,
            ""IsRevoked""  boolean                     NOT NULL DEFAULT false,
            ""CreatedAt""  timestamp with time zone    NOT NULL DEFAULT NOW(),
            CONSTRAINT ""PK_RefreshTokens"" PRIMARY KEY (""Id"")
        );
        CREATE INDEX IF NOT EXISTS ""IX_RefreshTokens_Token""            ON ""RefreshTokens""    (""Token"");
        CREATE INDEX IF NOT EXISTS ""IX_RefreshTokens_UserId_IsRevoked""  ON ""RefreshTokens""    (""UserId"", ""IsRevoked"");
        CREATE INDEX IF NOT EXISTS ""IX_PoolParticipants_UserId""         ON ""PoolParticipants"" (""UserId"");
        CREATE INDEX IF NOT EXISTS ""IX_Predictions_MatchId""             ON ""Predictions""      (""MatchId"");
        CREATE INDEX IF NOT EXISTS ""IX_FeedEvents_PoolId_OccurredAt""    ON ""FeedEvents""       (""PoolId"", ""OccurredAt"" DESC);
    ");
}

// Job: roda a cada 30 min — só faz chamada externa durante 11/06 a 20/07/2026
RecurringJob.AddOrUpdate<ResultSyncJob>(
    "sync-match-results",
    job => job.RunAsync(),
    "*/30 * * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

app.Run();

// Projeção para o comando CLI audit-log (lê a tabela prediction_audit).
record AuditRow(
    long Id, Guid? PredictionId, Guid? PoolId, Guid? UserId, Guid? MatchId,
    string Operation, int? OldHome, int? OldAway, int? NewHome, int? NewAway,
    string DbUser, DateTime ChangedAt);
