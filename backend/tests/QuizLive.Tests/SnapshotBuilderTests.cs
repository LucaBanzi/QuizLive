using System.Text.Json;
using System.Text.Json.Serialization;
using QuizLive.Api.Domain;
using QuizLive.Api.Services;

namespace QuizLive.Tests;

/// <summary>Criterio di accettazione 2: nessun payload pubblico contiene CorrectLabel prima del reveal.</summary>
public class SnapshotBuilderTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static (StateData Data, Guid TeamId) Make(GamePhase phase, bool graded)
    {
        var session = new QuizSession { Phase = phase, Seq = 7, Name = "Test" };
        var q = new Question
        {
            QuizSessionId = session.Id, OrderIndex = 1, Text = "Domanda?", CorrectLabel = 'C', Explanation = "perché sì",
            Options = new List<Option>
            {
                new() { Label = 'A', Text = "a" }, new() { Label = 'B', Text = "b" },
                new() { Label = 'C', Text = "c" }, new() { Label = 'D', Text = "d" }
            }
        };
        session.CurrentQuestionId = q.Id;
        var team = new Team { QuizSessionId = session.Id, Nickname = "Tavolo1", TableNumber = 1 };
        var other = new Team { QuizSessionId = session.Id, Nickname = "Tavolo2", TableNumber = 2 };
        var answers = new List<Answer>
        {
            new() { TeamId = team.Id, QuestionId = q.Id, SelectedLabel = 'C', IsCorrect = graded ? true : null, PointsAwarded = graded ? 100 : 0 },
            new() { TeamId = other.Id, QuestionId = q.Id, SelectedLabel = 'A', IsCorrect = graded ? false : null }
        };
        var data = new StateData
        {
            Session = session, Teams = { team, other }, Current = q, CurrentAnswers = answers, TotalQuestions = 3,
            Scores = graded ? new() { [team.Id] = 100 } : new()
        };
        return (data, team.Id);
    }

    [Theory]
    [InlineData(GamePhase.QuestionShown)]
    [InlineData(GamePhase.AnswersOpen)]
    [InlineData(GamePhase.AnswersClosed)]
    public void No_correct_label_before_reveal(GamePhase phase)
    {
        var (d, teamId) = Make(phase, graded: false);

        var display = JsonSerializer.Serialize(SnapshotBuilder.Display(d), Json);
        Assert.DoesNotContain("correctLabel", display, StringComparison.OrdinalIgnoreCase);
        Assert.Null(SnapshotBuilder.Display(d).Reveal);

        var player = SnapshotBuilder.Player(d, teamId);
        Assert.Null(player.CorrectLabel);
        Assert.Null(player.IsCorrect);
        Assert.Equal('C', player.MyAnswer);
    }

    [Fact]
    public void Correct_label_appears_on_reveal()
    {
        var (d, teamId) = Make(GamePhase.AnswerRevealed, graded: true);
        var display = SnapshotBuilder.Display(d);
        Assert.Equal('C', display.Reveal!.CorrectLabel);
        Assert.Equal(1, display.Reveal.VotesByLabel['C']);
        Assert.Equal(1, display.Reveal.VotesByLabel['A']);
        var player = SnapshotBuilder.Player(d, teamId);
        Assert.Equal('C', player.CorrectLabel);
        Assert.True(player.IsCorrect);
        Assert.Equal(100, player.Score);
        Assert.Equal(1, player.Rank);
    }

    [Fact]
    public void Phone_state_has_no_question_text()
    {
        var (d, teamId) = Make(GamePhase.AnswersOpen, graded: false);
        var json = JsonSerializer.Serialize(SnapshotBuilder.Player(d, teamId), Json);
        Assert.DoesNotContain("Domanda?", json);
    }

    [Fact]
    public void Leaderboard_shown_only_in_lobby_leaderboard_finished_for_display()
    {
        Assert.Null(SnapshotBuilder.Display(Make(GamePhase.AnswersOpen, false).Data).Leaderboard);
        Assert.NotNull(SnapshotBuilder.Display(Make(GamePhase.Leaderboard, true).Data).Leaderboard);
        Assert.NotNull(SnapshotBuilder.Display(Make(GamePhase.Finished, true).Data).Leaderboard);
    }

    [Fact]
    public void Deactivated_team_is_excluded_from_leaderboard()
    {
        var (d, _) = Make(GamePhase.Leaderboard, true);
        d.Teams[1].IsActive = false;
        Assert.Single(SnapshotBuilder.Leaderboard(d));
    }
}
