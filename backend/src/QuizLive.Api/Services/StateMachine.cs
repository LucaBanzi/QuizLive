using QuizLive.Api.Domain;

namespace QuizLive.Api.Services;

public enum AdminAction { NextQuestion, ShowQuestion, OpenAnswers, CloseAnswers, RevealAnswer, ShowLeaderboard, FinishQuiz, GoToQuestion }

/// <summary>Tabella delle transizioni ammesse (§C.2). Pura, senza dipendenze: facile da testare.</summary>
public static class StateMachine
{
    public static GamePhase? Next(GamePhase from, AdminAction action) => action switch
    {
        AdminAction.NextQuestion when from is GamePhase.Lobby or GamePhase.AnswerRevealed or GamePhase.Leaderboard => GamePhase.Ready,
        AdminAction.ShowQuestion when from == GamePhase.Ready => GamePhase.QuestionShown,
        AdminAction.OpenAnswers when from == GamePhase.QuestionShown => GamePhase.AnswersOpen,
        AdminAction.CloseAnswers when from == GamePhase.AnswersOpen => GamePhase.AnswersClosed,
        AdminAction.RevealAnswer when from is GamePhase.AnswersOpen or GamePhase.AnswersClosed => GamePhase.AnswerRevealed,
        AdminAction.ShowLeaderboard when from is GamePhase.Lobby or GamePhase.AnswerRevealed => GamePhase.Leaderboard,
        AdminAction.ShowLeaderboard when from == GamePhase.Finished => GamePhase.Finished,
        AdminAction.FinishQuiz when from != GamePhase.Finished => GamePhase.Finished,
        AdminAction.GoToQuestion => GamePhase.Ready,
        _ => null
    };
}
