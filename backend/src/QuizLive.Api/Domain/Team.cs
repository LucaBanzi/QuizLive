namespace QuizLive.Api.Domain;

public class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuizSessionId { get; set; }
    public string Nickname { get; set; } = "";
    public string NicknameNormalized { get; set; } = "";
    public string SpokespersonName { get; set; } = "";
    public int TableNumber { get; set; }
    public Guid SessionToken { get; set; } = Guid.NewGuid();
    public bool IsActive { get; set; } = true;
    public DateTime? RecoveryOpenUntil { get; set; }
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    public ICollection<ScoreAdjustment> Adjustments { get; set; } = new List<ScoreAdjustment>();
}
