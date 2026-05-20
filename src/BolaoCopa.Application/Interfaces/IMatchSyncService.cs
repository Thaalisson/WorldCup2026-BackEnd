namespace BolaoCopa.Application.Interfaces;

public interface IMatchSyncService
{
    /// <summary>Importa todos os jogos da Copa via api-football. Executar uma vez antes do torneio.</summary>
    Task<int> ImportFixturesAsync();

    /// <summary>Busca resultados do dia e atualiza placares + ranking. Chamado pelo job Hangfire.</summary>
    Task SyncResultsAsync();
}
