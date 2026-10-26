namespace QuizLive.Api.Domain;

public class ScoreAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public int Delta { get; set; }
    public string Reason { get; set; } = "";
    public AdjustmentKind Kind { get; set; } = AdjustmentKind.Manual;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "admin";
}
