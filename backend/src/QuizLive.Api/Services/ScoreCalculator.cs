namespace QuizLive.Api.Services;

/// <summary>Logica di punteggio e classifica, pura (§B.3).</summary>
public static class ScoreCalculator
{
    public static (bool IsCorrect, int Points) Grade(char selected, char correct, int pointsPerCorrect)
        => selected == correct ? (true, pointsPerCorrect) : (false, 0);

    /// <summary>Rank "competition" (1, 2, 2, 4): stesso punteggio ⇒ stesso rank. Ordine: Score desc, Nickname asc.</summary>
    public static Dictionary<Guid, int> Rank(IEnumerable<(Guid Id, string Nickname, int Score)> teams)
    {
        var ordered = teams
            .OrderByDescending(t => t.Score)
            .ThenBy(t => t.Nickname, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var result = new Dictionary<Guid, int>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var rank = i > 0 && ordered[i].Score == ordered[i - 1].Score ? result[ordered[i - 1].Id] : i + 1;
            result[ordered[i].Id] = rank;
        }
        return result;
    }
}
