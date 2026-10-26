using Microsoft.AspNetCore.SignalR;
using QuizLive.Api.Services;

namespace QuizLive.Api.Hubs;

public sealed class HubBroadcaster(IHubContext<QuizHub, IQuizClient> hub, ConnectionTracker tracker, ILogger<HubBroadcaster> log)
    : IQuizBroadcaster
{
    public async Task PublishAsync(StateData d, PublishScope scope, IReadOnlyCollection<Guid>? onlyTeams = null)
    {
        var tasks = new List<Task>();

        if (scope.HasFlag(PublishScope.Display))
            tasks.Add(hub.Clients.Group(QuizHub.DisplayGroup).DisplayStateUpdated(SnapshotBuilder.Display(d)));

        if (scope.HasFlag(PublishScope.Admin))
            tasks.Add(hub.Clients.Group(QuizHub.AdminGroup).AdminStateUpdated(SnapshotBuilder.Admin(d)));

        if (scope.HasFlag(PublishScope.Leaderboard))
            tasks.Add(hub.Clients.Groups(new[] { QuizHub.DisplayGroup, QuizHub.AdminGroup }).LeaderboardUpdated(SnapshotBuilder.Leaderboard(d)));

        if (scope.HasFlag(PublishScope.Players))
        {
            var ids = onlyTeams ?? tracker.ConnectedTeamIds().ToList();
            var known = d.Teams.Select(t => t.Id).ToHashSet();
            foreach (var id in ids.Where(known.Contains))
                tasks.Add(hub.Clients.Group(QuizHub.TeamGroup(id)).PlayerStateUpdated(SnapshotBuilder.Player(d, id)));
        }

        await Task.WhenAll(tasks);
    }

    public Task PublishAnswerCountAsync(int answered, int total) =>
        hub.Clients.Groups(new[] { QuizHub.DisplayGroup, QuizHub.AdminGroup }).AnswerCountUpdated(answered, total);

    public async Task InvalidateTeamAsync(Guid teamId, string reason)
    {
        var connections = tracker.RemoveTeam(teamId);
        if (connections.Length == 0) return;
        try
        {
            await hub.Clients.Clients(connections).SessionInvalidated(reason);
            foreach (var c in connections)
                await hub.Groups.RemoveFromGroupAsync(c, QuizHub.TeamGroup(teamId));
        }
        catch (Exception ex) { log.LogWarning(ex, "Invalidazione sessione fallita per il team {TeamId}", teamId); }
    }
}
