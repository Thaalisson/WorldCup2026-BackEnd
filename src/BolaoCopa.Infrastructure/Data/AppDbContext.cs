using BolaoCopa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Pool> Pools => Set<Pool>();
    public DbSet<PoolParticipant> PoolParticipants => Set<PoolParticipant>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<ChampionPrediction> ChampionPredictions => Set<ChampionPrediction>();
    public DbSet<FeedEvent> FeedEvents => Set<FeedEvent>();
    public DbSet<RankingSnapshot> RankingSnapshots => Set<RankingSnapshot>();
    public DbSet<GroupPrediction> GroupPredictions => Set<GroupPrediction>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<Pool>().HasIndex(x => x.InviteCode).IsUnique();
        modelBuilder.Entity<Team>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Prediction>()
            .HasIndex(x => new { x.PoolId, x.UserId, x.MatchId }).IsUnique();
        modelBuilder.Entity<Prediction>().HasIndex(x => x.MatchId);
        modelBuilder.Entity<PoolParticipant>().HasIndex(x => x.UserId);
        modelBuilder.Entity<FeedEvent>().HasIndex(x => new { x.PoolId, x.OccurredAt });
        modelBuilder.Entity<RefreshToken>().HasIndex(x => x.Token);
        modelBuilder.Entity<RefreshToken>().HasIndex(x => new { x.UserId, x.IsRevoked });
    }
}
