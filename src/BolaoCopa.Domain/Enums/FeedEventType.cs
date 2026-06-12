namespace BolaoCopa.Domain.Enums;

public enum FeedEventType
{
    ExactScore = 1,
    CorrectResult = 2,
    ZeroPoints = 3,
    // Pontuou parcial (acertou saldo e/ou gols de um time) SEM acertar o vencedor.
    Partial = 4
}
