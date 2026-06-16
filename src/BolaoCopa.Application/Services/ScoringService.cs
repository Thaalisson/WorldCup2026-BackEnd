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

        // Regra simples, igual à tela de configuração: placar exato OU acertou o
        // vencedor/empate. Sem bônus de saldo/gols (que não apareciam na config).
        if (predictedHome == realHome && predictedAway == realAway)
            return exactPts;

        if (GetResult(predictedHome, predictedAway) == GetResult(realHome, realAway))
            return correctPts;

        return 0;
    }

    private static string GetResult(int home, int away)
    {
        if (home > away) return "HOME";
        if (away > home) return "AWAY";
        return "DRAW";
    }
}
