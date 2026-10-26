using Microsoft.EntityFrameworkCore;
using QuizLive.Api.Data;

namespace QuizLive.Api.Services;

public static class QuestionOrdering
{
    /// <summary>
    /// Assegna OrderIndex 1..n nell'ordine dato. Due passaggi (offset temporaneo) per non violare
    /// l'indice univoco (QuizSessionId, OrderIndex) durante lo scambio.
    /// </summary>
    public static async Task ApplyAsync(QuizDbContext db, Guid sessionId, IReadOnlyList<Guid> orderedIds)
    {
        var questions = await db.Questions.Where(q => q.QuizSessionId == sessionId).ToListAsync();
        foreach (var q in questions) q.OrderIndex += 100000;
        await db.SaveChangesAsync();
        for (var i = 0; i < orderedIds.Count; i++)
            questions.First(q => q.Id == orderedIds[i]).OrderIndex = i + 1;
        await db.SaveChangesAsync();
    }

    public static async Task NormalizeAsync(QuizDbContext db, Guid sessionId)
    {
        var ids = await db.Questions.Where(q => q.QuizSessionId == sessionId)
            .OrderBy(q => q.OrderIndex).Select(q => q.Id).ToListAsync();
        await ApplyAsync(db, sessionId, ids);
    }
}
