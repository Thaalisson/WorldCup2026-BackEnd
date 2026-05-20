using BolaoCopa.Application.DTOs;
using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;

namespace BolaoCopa.Application.Services;

public class ScoringService : IScoringService
{
    public int CalculateMatchPredictionPoints(Prediction prediction, Match match, ScoringConfigDto? config = null)
    {
        if (!match.IsFinished || match.HomeScore is null || match.AwayScore is null)
            return 0;

        int exactPts   = config?.PointsExactScore    ?? 10;
        int correctPts = config?.PointsCorrectResult ?? 5;

        var predictedHome = prediction.HomeScorePrediction;
        var predictedAway = prediction.AwayScorePrediction;
        var realHome = match.HomeScore.Value;
        var realAway = match.AwayScore.Value;

        if (predictedHome == realHome && predictedAway == realAway)
            return exactPts;

        var points = 0;

        if (GetResult(predictedHome, predictedAway) == GetResult(realHome, realAway))
            points += correctPts;

        if (predictedHome - predictedAway == realHome - realAway)
            points += 3;

        if (predictedHome == realHome)
            points += 2;

        if (predictedAway == realAway)
            points += 2;

        return points;
    }

    private static string GetResult(int home, int away)
    {
        if (home > away) return "HOME";
        if (away > home) return "AWAY";
        return "DRAW";
    }
}
