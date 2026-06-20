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
        or "recalc-all" or "report" or "setup-audit" or "audit-log")
{
    await using var cliScope = app.Services.CreateAsyncScope();
    var sp = cliScope.ServiceProvider;

    switch (args[0])
    {
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

        case "sync-results":
            await sp.GetRequiredService<IMatchSyncService>().SyncResultsAsync();
            Console.WriteLine("[sync-results] Placares sincronizados e ranking recalculado.");
            break;
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
