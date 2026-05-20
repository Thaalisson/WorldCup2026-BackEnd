using BolaoCopa.Domain.Entities;

namespace BolaoCopa.Application.Interfaces;

public interface IRankingService
{
    Task RecalculateForMatchAsync(Match match);
    Task RecalculateForPoolAsync(Guid poolId);
    Task ScoreGroupPredictionsForPoolAsync(Guid poolId);
}
