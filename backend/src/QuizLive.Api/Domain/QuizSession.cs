namespace QuizLive.Api.Domain;

/// <summary>Una serata. Esiste una sola sessione con IsActive = true.</summary>
public class QuizSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Serata Quiz";
    public GamePhase Phase { get; set; } = GamePhase.Lobby;
    public Guid? CurrentQuestionId { get; set; }
    public int PointsPerCorrect { get; set; } = 100;
    public bool IsActive { get; set; } = true;
    /// <summary>Contatore snapshot, incrementato a ogni transizione/modifica di stato.</summary>
    public long Seq { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
