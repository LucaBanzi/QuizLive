namespace QuizLive.Api.Domain;

public class Option
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuestionId { get; set; }
    public char Label { get; set; }
    public string Text { get; set; } = "";
}
