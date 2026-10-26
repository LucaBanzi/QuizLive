using QuizLive.Api.Domain;
using QuizLive.Api.Services;

namespace QuizLive.Tests;

public class StateMachineTests
{
    [Theory]
    [InlineData(GamePhase.Lobby, AdminAction.NextQuestion, GamePhase.Ready)]
    [InlineData(GamePhase.AnswerRevealed, AdminAction.NextQuestion, GamePhase.Ready)]
    [InlineData(GamePhase.Leaderboard, AdminAction.NextQuestion, GamePhase.Ready)]
    [InlineData(GamePhase.Ready, AdminAction.ShowQuestion, GamePhase.QuestionShown)]
    [InlineData(GamePhase.QuestionShown, AdminAction.OpenAnswers, GamePhase.AnswersOpen)]
    [InlineData(GamePhase.AnswersOpen, AdminAction.CloseAnswers, GamePhase.AnswersClosed)]
    [InlineData(GamePhase.AnswersOpen, AdminAction.RevealAnswer, GamePhase.AnswerRevealed)]
    [InlineData(GamePhase.AnswersClosed, AdminAction.RevealAnswer, GamePhase.AnswerRevealed)]
    [InlineData(GamePhase.Lobby, AdminAction.ShowLeaderboard, GamePhase.Leaderboard)]
    [InlineData(GamePhase.AnswerRevealed, AdminAction.ShowLeaderboard, GamePhase.Leaderboard)]
    [InlineData(GamePhase.Finished, AdminAction.ShowLeaderboard, GamePhase.Finished)]
    [InlineData(GamePhase.AnswersOpen, AdminAction.FinishQuiz, GamePhase.Finished)]
    [InlineData(GamePhase.Lobby, AdminAction.GoToQuestion, GamePhase.Ready)]
    [InlineData(GamePhase.Finished, AdminAction.GoToQuestion, GamePhase.Ready)]
    public void Allowed_transitions(GamePhase from, AdminAction action, GamePhase expected)
        => Assert.Equal(expected, StateMachine.Next(from, action));

    [Theory]
    [InlineData(GamePhase.Ready, AdminAction.NextQuestion)]
    [InlineData(GamePhase.Lobby, AdminAction.ShowQuestion)]
    [InlineData(GamePhase.QuestionShown, AdminAction.RevealAnswer)]
    [InlineData(GamePhase.AnswersOpen, AdminAction.OpenAnswers)]       // doppio click
    [InlineData(GamePhase.AnswersClosed, AdminAction.CloseAnswers)]
    [InlineData(GamePhase.AnswerRevealed, AdminAction.RevealAnswer)]
    [InlineData(GamePhase.Leaderboard, AdminAction.ShowLeaderboard)]
    [InlineData(GamePhase.Finished, AdminAction.FinishQuiz)]
    [InlineData(GamePhase.Finished, AdminAction.NextQuestion)]
    public void Rejected_transitions(GamePhase from, AdminAction action)
        => Assert.Null(StateMachine.Next(from, action));

    [Fact]
    public void Full_question_cycle()
    {
        var phase = GamePhase.Lobby;
        foreach (var a in new[] { AdminAction.NextQuestion, AdminAction.ShowQuestion, AdminAction.OpenAnswers,
                     AdminAction.CloseAnswers, AdminAction.RevealAnswer, AdminAction.ShowLeaderboard, AdminAction.NextQuestion })
            phase = StateMachine.Next(phase, a) ?? throw new Exception($"{a} rifiutata da {phase}");
        Assert.Equal(GamePhase.Ready, phase);
    }
}
