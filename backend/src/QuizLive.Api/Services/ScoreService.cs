using Microsoft.EntityFrameworkCore;
using QuizLive.Api.Data;

namespace QuizLive.Api.Services;

/// <summary>Score = Σ Answer.PointsAwarded + Σ ScoreAdjustment.Delta, sempre derivato (mai memorizzato).</summary>
public static class ScoreService
{
    public static async Task<(Dictionary<Guid, int> Scores, Dictionary<Guid, int> Correct)> TotalsAsync(
        QuizDbContext db, Guid sessionId, CancellationToken ct = default)
    {
        var fromAnswers = await db.Answers.AsNoTracking()
            .Where(a => a.Team.QuizSessionId == sessionId)
            .GroupBy(a => a.TeamId)
            .Select(g => new { TeamId = g.Key, Points = g.Sum(x => x.PointsAwarded), Correct = g.Count(x => x.IsCorrect == true) })
            .ToListAsync(ct);

        var fromAdjustments = await db.Adjustments.AsNoTracking()
            .Where(a => a.Team.QuizSessionId == sessionId)
            .GroupBy(a => a.TeamId)
            .Select(g => new { TeamId = g.Key, Delta = g.Sum(x => x.Delta) })
            .ToListAsync(ct);

        var scores = new Dictionary<Guid, int>();
        var correct = new Dictionary<Guid, int>();
        foreach (var a in fromAnswers) { scores[a.TeamId] = a.Points; correct[a.TeamId] = a.Correct; }
        foreach (var a in fromAdjustments) scores[a.TeamId] = scores.GetValueOrDefault(a.TeamId) + a.Delta;
        return (scores, correct);
    }
}
