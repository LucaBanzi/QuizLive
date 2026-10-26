using Microsoft.AspNetCore.SignalR;
using QuizLive.Api.Dtos;

namespace QuizLive.Api.Services;

public static class TeamService
{
    /// <summary>Testo semplice: scarta i caratteri di controllo e fa il trim.</summary>
    public static string Clean(string? s) => new string((s ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();

    public static string Normalize(string nickname) => Clean(nickname).ToLowerInvariant();

    public static (string Nickname, string Spokesperson, int Table) Validate(RegisterTeamRequest r)
    {
        var nick = Clean(r.Nickname);
        var spokes = Clean(r.SpokespersonName);
        if (nick.Length is < 2 or > 24) throw new HubException("VALIDATION");
        if (spokes.Length is < 2 or > 80) throw new HubException("VALIDATION");
        if (r.TableNumber is < 1 or > 99) throw new HubException("VALIDATION");
        return (nick, spokes, r.TableNumber);
    }
}
