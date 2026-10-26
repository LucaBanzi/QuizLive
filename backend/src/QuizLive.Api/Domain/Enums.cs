namespace QuizLive.Api.Domain;

public enum GamePhase { Lobby, Ready, QuestionShown, AnswersOpen, AnswersClosed, AnswerRevealed, Leaderboard, Finished }
public enum MediaType { None, Image, Video, Audio }
public enum AdjustmentKind { Manual, Transfer }
