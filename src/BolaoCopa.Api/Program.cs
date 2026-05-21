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

// HttpClient para api-football.com
builder.Services.AddHttpClient<IFootballApiClient, FootballApiClient>(client =>
{
    client.BaseAddress = new Uri("https://v3.football.api-sports.io/");
    client.DefaultRequestHeaders.Add("x-apisports-key", builder.Configuration["ApiFootball:ApiKey"]);
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
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

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
        CREATE INDEX IF NOT EXISTS ""IX_RefreshTokens_Token"" ON ""RefreshTokens"" (""Token"");
    ");
}

// Job: roda a cada 30 min — só faz chamada externa durante 11/06 a 20/07/2026
RecurringJob.AddOrUpdate<ResultSyncJob>(
    "sync-match-results",
    job => job.RunAsync(),
    "*/30 * * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

app.Run();
