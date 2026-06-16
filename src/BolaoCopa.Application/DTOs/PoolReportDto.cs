namespace BolaoCopa.Application.DTOs;

// Relatório de transparência de um bolão: de onde cada jogador ganhou pontos.

public record PoolReportGameDto(
    string MatchLabel,       // ex.: "MEX 2×0 RSA" (resultado real)
    string PredictionLabel,  // ex.: "2×0" (palpite do jogador) — vazio se não palpitou
    int Points,
    string Outcome           // "exato" | "vencedor" | "errou" | "sem_palpite"
);

public record PoolReportPlayerDto(
    string UserName,
    int Position,
    int TotalPoints,
    int Exatos,
    int Vencedor,
    int Erros,
    int SemPalpite,
    IReadOnlyList<PoolReportGameDto> Games
);

public record PoolReportDto(
    string PoolName,
    int ExactPoints,
    int CorrectPoints,
    int FinishedCount,
    IReadOnlyList<PoolReportPlayerDto> Players
);
