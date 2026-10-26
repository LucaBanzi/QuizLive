using QuizLive.Api.Dtos;

namespace QuizLive.Api.Hubs;

/// <summary>Messaggi server → client (SignalR fortemente tipizzato).</summary>
public interface IQuizClient
{
    Task PlayerStateUpdated(PlayerStateDto state);
    Task DisplayStateUpdated(GameStateDto state);
    Task AdminStateUpdated(AdminStateDto state);
    Task AnswerCountUpdated(int answered, int total);
    Task LeaderboardUpdated(LeaderboardEntryDto[] entries);
    /// <summary>reason: RESET | REPLACED | DEACTIVATED | TRANSFERRED</summary>
    Task SessionInvalidated(string reason);
}
