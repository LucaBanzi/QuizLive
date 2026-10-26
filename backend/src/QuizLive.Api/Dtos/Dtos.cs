using QuizLive.Api.Domain;

namespace QuizLive.Api.Dtos;

// ---------- Stato di gioco (contratto condiviso col frontend: src/api/types.ts) ----------
public record OptionDto(char Label, string Text);
public record QuestionDto(Guid Id, int Index, int Total, string Text, MediaType MediaType, string? MediaUrl, OptionDto[] Options);
public record RevealDto(char CorrectLabel, string? Explanation, Dictionary<char, int> VotesByLabel);
public record LeaderboardEntryDto(int Rank, Guid TeamId, string Nickname, int TableNumber, int Score, int CorrectCount, int LastQuestionPoints);
public record GameStateDto(long Seq, GamePhase Phase, QuestionDto? Question, RevealDto? Reveal,
    int AnsweredCount, int ActiveTeams, LeaderboardEntryDto[]? Leaderboard, string QuizName);

// Telefono: payload minimo (niente testi domanda né media)
public record PlayerStateDto(long Seq, GamePhase Phase, Guid? QuestionId, int QuestionIndex, int TotalQuestions,
    char? MyAnswer, bool? IsCorrect, char? CorrectLabel, int Score, int Rank, string Nickname);

public record TeamAdminDto(Guid Id, string Nickname, string SpokespersonName, int TableNumber, int Score,
    bool IsActive, bool IsConnected, bool AnsweredCurrent, char? SelectedLabel, DateTime? RecoveryOpenUntil);

public record AdminStateDto(GameStateDto Game, char? CurrentCorrectLabel, string? NextQuestionPreview, TeamAdminDto[] Teams);

public record AnswerAckDto(string Status, char? StoredLabel);

// ---------- Richieste hub ----------
public record RegisterTeamRequest(string Nickname, string SpokespersonName, int TableNumber);
public record RegisterResult(Guid TeamId, Guid SessionToken, PlayerStateDto State);
public record RejoinRequest(string Nickname, Guid? SessionToken);
public record RejoinResult(Guid TeamId, Guid SessionToken, PlayerStateDto State);
public record SubmitAnswerRequest(Guid QuestionId, char Label, string? ClientRequestId);

// ---------- REST ----------
public record LoginRequest(string? Password);
public record LoginResponse(string Token, DateTime ExpiresAt);
public record PublicInfoDto(string QuizName, string PublicOrigin);
public record OptionEditDto(char Label, string Text);
public record QuestionEditDto(string Text, MediaType MediaType, string? MediaUrl, char CorrectLabel, string? Explanation, OptionEditDto[] Options);
public record QuestionAdminDto(Guid Id, int OrderIndex, string Text, MediaType MediaType, string? MediaUrl, char CorrectLabel, string? Explanation, OptionEditDto[] Options);
public record ReorderRequest(Guid[] OrderedIds);
