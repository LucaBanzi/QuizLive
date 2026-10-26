namespace QuizLive.Api.Services;

[Flags]
public enum PublishScope
{
    None = 0, Display = 1, Admin = 2, Leaderboard = 4, Players = 8,
    All = Display | Admin | Leaderboard | Players
}

/// <summary>Astrae l'invio dei messaggi SignalR, così il GameEngine è testabile senza hub.</summary>
public interface IQuizBroadcaster
{
    Task PublishAsync(StateData data, PublishScope scope, IReadOnlyCollection<Guid>? onlyTeams = null);
    Task PublishAnswerCountAsync(int answered, int total);
    /// <summary>Invia SessionInvalidated(reason) alle connessioni del team e le scollega dal gruppo.</summary>
    Task InvalidateTeamAsync(Guid teamId, string reason);
}
