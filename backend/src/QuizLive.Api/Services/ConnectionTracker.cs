using System.Collections.Concurrent;

namespace QuizLive.Api.Services;

/// <summary>connectionId → teamId, per l'indicatore "connesso" e per invalidare le sessioni.</summary>
public sealed class ConnectionTracker
{
    private readonly ConcurrentDictionary<string, Guid> _conn = new();

    public void Add(string connectionId, Guid teamId) => _conn[connectionId] = teamId;
    public Guid? Remove(string connectionId) => _conn.TryRemove(connectionId, out var t) ? t : null;
    public Guid? TeamOf(string connectionId) => _conn.TryGetValue(connectionId, out var t) ? t : null;

    public string[] RemoveTeam(Guid teamId)
    {
        var ids = _conn.Where(kv => kv.Value == teamId).Select(kv => kv.Key).ToArray();
        foreach (var id in ids) _conn.TryRemove(id, out _);
        return ids;
    }

    public HashSet<Guid> ConnectedTeamIds() => _conn.Values.ToHashSet();
}
