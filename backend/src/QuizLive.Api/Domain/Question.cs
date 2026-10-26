namespace QuizLive.Api.Domain;

public class Question
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuizSessionId { get; set; }
    public int OrderIndex { get; set; }
    public string Text { get; set; } = "";
    public MediaType MediaType { get; set; } = MediaType.None;
    public string? MediaUrl { get; set; }
    public char CorrectLabel { get; set; } = 'A';
    public string? Explanation { get; set; }
    public ICollection<Option> Options { get; set; } = new List<Option>();
}
