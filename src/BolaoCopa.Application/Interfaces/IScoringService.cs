using BolaoCopa.Application.DTOs;
using BolaoCopa.Domain.Entities;

namespace BolaoCopa.Application.Interfaces;

public interface IScoringService
{
    int CalculateMatchPredictionPoints(Prediction prediction, Match match, ScoringConfigDto? config = null);
}
