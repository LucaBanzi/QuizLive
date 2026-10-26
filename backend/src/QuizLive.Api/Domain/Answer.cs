namespace QuizLive.Api.Domain;

public class Answer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public Guid QuestionId { get; set; }
    public char SelectedLabel { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    /// <summary>null finché non avviene il reveal.</summary>
    public bool? IsCorrect { get; set; }
    public int PointsAwarded { get; set; }
}
