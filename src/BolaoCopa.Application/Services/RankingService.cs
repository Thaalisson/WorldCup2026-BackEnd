using BolaoCopa.Application.DTOs;
using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using BolaoCopa.Domain.Enums;

namespace BolaoCopa.Application.Services;

public class RankingService : IRankingService
{
    private readonly IPredictionRepository _predictions;
    private readonly IPoolRepository _pools;
    private readonly IMatchRepository _matches;
    private readonly IScoringService _scoring;
    private readonly IFeedRepository _feed;
    private readonly IRankingSnapshotRepository _snapshots;
    private readonly IUserRepository _users;
    private readonly ITeamRepository _teams;
    private readonly IGroupPredictionRepository _groupPredictions;

    public RankingService(
        IPredictionRepository predictions,
        IPoolRepository pools,
        IMatchRepository matches,
        IScoringService scoring,
        IFeedRepository feed,
        IRankingSnapshotRepository snapshots,
        IUserRepository users,
        ITeamRepository teams,
        IGroupPredictionRepository groupPredictions)
    {
        _predictions = predictions;
        _pools = pools;
        _matches = matches;
        _scoring = scoring;
        _feed = feed;
        _snapshots = snapshots;
        _users = users;
        _teams = teams;
        _groupPredictions = groupPredictions;
    }

    public async Task RecalculateForMatchAsync(Match match)
    {
        var allTeams = await _teams.GetAllAsync();
        var teamMap = allTeams.ToDictionary(t => t.Id);
        var matchLabel = BuildMatchLabel(match, teamMap);
        var roundLabel = MatchRoundLabel(match.Stage);

        var allPredictions = await _predictions.GetByMatchIdAsync(match.Id);
        var poolIds = allPredictions.Select(p => p.PoolId).Distinct().ToList();

        // Cache pool scoring configs
        var poolConfigs = new Dictionary<Guid, ScoringConfigDto>();
        foreach (var pid in poolIds)
        {
            var pool = await _pools.GetByIdAsync(pid);
            poolConfigs[pid] = pool is null ? new ScoringConfigDto(10, 5, 15, 10, 5, 3) : PoolConfig(pool);
        }

        foreach (var prediction in allPredictions)
        {
            var config = poolConfigs.GetValueOrDefault(prediction.PoolId);
            var newPoints = _scoring.CalculateMatchPredictionPoints(prediction, match, config);
            var diff = newPoints - prediction.PointsEarned;
            prediction.PointsEarned = newPoints;
            await _predictions.UpdateAsync(prediction);

            if (diff == 0) continue;

            var participant = await _pools.GetParticipantAsync(prediction.PoolId, prediction.UserId);
            if (participant is null) continue;

            participant.TotalPoints += diff;
            if (newPoints == (config?.PointsExactScore ?? 10)) participant.ExactScores++;
            else if (newPoints >= (config?.PointsCorrectResult ?? 5)) participant.CorrectResults++;
            await _pools.UpdateParticipantAsync(participant);

            var user = await _users.GetByIdAsync(prediction.UserId);
            if (user is not null)
            {
                var exactPts = config?.PointsExactScore ?? 10;
                var eventType = newPoints == exactPts  ? (int)FeedEventType.ExactScore
                              : newPoints > 0          ? (int)FeedEventType.CorrectResult
                              :                          (int)FeedEventType.ZeroPoints;

                await _feed.AddAsync(new FeedEvent
                {
                    Id = Guid.NewGuid(),
                    PoolId = prediction.PoolId,
                    UserId = prediction.UserId,
                    MatchId = match.Id,
                    UserName = user.Name,
                    MatchLabel = matchLabel,
                    EventType = eventType,
                    Points = newPoints,
                    OccurredAt = DateTime.UtcNow
                });
            }
        }

        var snapshotList = new List<RankingSnapshot>();
        foreach (var poolId in poolIds)
        {
            var participants = await _pools.GetParticipantsAsync(poolId);
            snapshotList.AddRange(participants.Select(p => new RankingSnapshot
            {
                Id = Guid.NewGuid(),
                PoolId = poolId,
                UserId = p.UserId,
                Round = roundLabel,
                TotalPoints = p.TotalPoints,
                SnappedAt = DateTime.UtcNow
            }));
        }

        if (snapshotList.Count > 0)
            await _snapshots.AddRangeAsync(snapshotList);
    }

    public async Task RecalculateForPoolAsync(Guid poolId)
    {
        var pool = await _pools.GetByIdAsync(poolId);
        var config = pool is null ? new ScoringConfigDto(10, 5, 15, 10, 5, 3) : PoolConfig(pool);

        var participants = await _pools.GetParticipantsAsync(poolId);
        foreach (var p in participants)
        {
            p.TotalPoints = 0;
            p.ExactScores = 0;
            p.CorrectResults = 0;
            await _pools.UpdateParticipantAsync(p);
        }

        var finishedMatches = await _matches.GetFinishedMatchesAsync();
        foreach (var match in finishedMatches)
        {
            var predictions = await _predictions.GetByMatchAndPoolAsync(match.Id, poolId);
            foreach (var prediction in predictions)
            {
                var points = _scoring.CalculateMatchPredictionPoints(prediction, match, config);
                prediction.PointsEarned = points;
                await _predictions.UpdateAsync(prediction);

                var participant = await _pools.GetParticipantAsync(poolId, prediction.UserId);
                if (participant is null) continue;

                participant.TotalPoints += points;
                if (points == config.PointsExactScore) participant.ExactScores++;
                else if (points >= config.PointsCorrectResult) participant.CorrectResults++;
                await _pools.UpdateParticipantAsync(participant);
            }

            var roundLabel = MatchRoundLabel(match.Stage);
            var current = await _pools.GetParticipantsAsync(poolId);
            var snapshots = current.Select(p => new RankingSnapshot
            {
                Id = Guid.NewGuid(),
                PoolId = poolId,
                UserId = p.UserId,
                Round = roundLabel,
                TotalPoints = p.TotalPoints,
                SnappedAt = DateTime.UtcNow
            }).ToList();

            if (snapshots.Count > 0)
                await _snapshots.AddRangeAsync(snapshots);
        }
    }

    public async Task ScoreGroupPredictionsForPoolAsync(Guid poolId)
    {
        var pool = await _pools.GetByIdAsync(poolId);
        int qualifierPts = pool?.PointsGroupQualifier ?? 3;

        // Compute actual group standings from finished group matches
        var finishedMatches = await _matches.GetFinishedMatchesAsync();
        var groupMatches = finishedMatches.Where(m => m.GroupName is not null).ToList();

        // Build standing points per team per group
        var standing = new Dictionary<Guid, (string Group, int Points)>();
        foreach (var m in groupMatches)
        {
            if (!standing.ContainsKey(m.HomeTeamId)) standing[m.HomeTeamId] = (m.GroupName!, 0);
            if (!standing.ContainsKey(m.AwayTeamId)) standing[m.AwayTeamId] = (m.GroupName!, 0);

            int hp = 0, ap = 0;
            if (m.HomeScore > m.AwayScore)      { hp = 3; }
            else if (m.HomeScore == m.AwayScore) { hp = 1; ap = 1; }
            else                                 { ap = 3; }

            standing[m.HomeTeamId] = (standing[m.HomeTeamId].Group, standing[m.HomeTeamId].Points + hp);
            standing[m.AwayTeamId] = (standing[m.AwayTeamId].Group, standing[m.AwayTeamId].Points + ap);
        }

        // Top 2 per group
        var qualifiers = standing
            .GroupBy(kv => kv.Value.Group)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(kv => kv.Value.Points)
                       .Take(2)
                       .Select(kv => kv.Key)
                       .ToHashSet());

        // Score group predictions
        var groupPredictions = await _groupPredictions.GetByPoolAsync(poolId);
        foreach (var gp in groupPredictions)
        {
            if (!qualifiers.TryGetValue(gp.GroupName, out var top2)) continue;

            var oldPoints = gp.PointsEarned;
            var newPoints = (top2.Contains(gp.FirstPlaceTeamId) ? qualifierPts : 0)
                          + (top2.Contains(gp.SecondPlaceTeamId) ? qualifierPts : 0);

            gp.PointsEarned = newPoints;
            await _groupPredictions.UpdateAsync(gp);

            var diff = newPoints - oldPoints;
            if (diff == 0) continue;

            var participant = await _pools.GetParticipantAsync(poolId, gp.UserId);
            if (participant is null) continue;

            participant.TotalPoints += diff;
            await _pools.UpdateParticipantAsync(participant);
        }
    }

    private static ScoringConfigDto PoolConfig(Pool pool) => new(
        pool.PointsExactScore,
        pool.PointsCorrectResult,
        pool.PointsChampion,
        pool.PointsRunnerUp,
        pool.PointsThirdPlace,
        pool.PointsGroupQualifier);

    private static string MatchRoundLabel(TournamentStage stage) => stage switch
    {
        TournamentStage.GroupStage   => "GF",
        TournamentStage.RoundOf32    => "R32",
        TournamentStage.RoundOf16    => "R16",
        TournamentStage.QuarterFinal => "QF",
        TournamentStage.SemiFinal    => "SF",
        TournamentStage.ThirdPlace   => "3°",
        TournamentStage.Final        => "FIN",
        _                            => "?"
    };

    private static string BuildMatchLabel(Match match, Dictionary<Guid, Team> teamMap)
    {
        var home = teamMap.TryGetValue(match.HomeTeamId, out var h) ? h.Code : "?";
        var away = teamMap.TryGetValue(match.AwayTeamId, out var a) ? a.Code : "?";
        var score = match.HomeScore.HasValue && match.AwayScore.HasValue
            ? $"{match.HomeScore}×{match.AwayScore}"
            : "vs";
        return $"{home} {score} {away}";
    }
}
