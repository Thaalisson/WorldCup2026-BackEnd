using BolaoCopa.Application.Interfaces;

namespace BolaoCopa.Api.Jobs;

/// <summary>
/// Job Hangfire que roda a cada 30 minutos.
/// Só consome a API da api-football.com durante o período da Copa (11/06 a 20/07/2026).
/// Fora desse período o job executa mas retorna imediatamente sem gastar quota.
/// </summary>
public class ResultSyncJob
{
    private readonly IMatchSyncService _sync;

    public ResultSyncJob(IMatchSyncService sync) => _sync = sync;

    public Task RunAsync() => _sync.SyncResultsAsync();
}
