using BolaoCopa.Application.DTOs;

namespace BolaoCopa.Application.Interfaces;

public interface IFootballApiClient
{
    /// <summary>Todos os jogos da Copa (usado na importação inicial).</summary>
    Task<IReadOnlyList<ApiFixtureDto>> GetAllFixturesAsync();

    /// <summary>Jogos de hoje com status FT/AET/PEN (usado pelo job diário).</summary>
    Task<IReadOnlyList<ApiFixtureDto>> GetTodayFinishedFixturesAsync();
}
