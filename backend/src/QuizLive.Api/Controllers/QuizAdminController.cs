using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizLive.Api.Data;
using QuizLive.Api.Services;

namespace QuizLive.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/admin")]
public class QuizAdminController(QuizDbContext db, GameEngine engine) : ControllerBase
{
    /// <summary>Usato dal frontend per verificare il token prima di aprire l'hub.</summary>
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { ok = true });

    [HttpPost("quiz/reset")]
    public async Task<IActionResult> Reset([FromQuery] bool confirm = false)
    {
        if (!confirm) return BadRequest(new { error = "Conferma mancante (?confirm=true)." });
        await engine.ResetQuizAsync();
        return NoContent();
    }

    /// <summary>Classifica finale + dettaglio risposte per tavolo (CSV UTF-8 con BOM, separatore ';').</summary>
    [HttpGet("export/results.csv")]
    public async Task<IActionResult> Export()
    {
        var s = await db.Sessions.AsNoTracking().FirstAsync(x => x.IsActive);
        var teams = await db.Teams.AsNoTracking().Where(t => t.QuizSessionId == s.Id).ToListAsync();
        var (scores, correct) = await ScoreService.TotalsAsync(db, s.Id);
        var questions = await db.Questions.AsNoTracking().Where(q => q.QuizSessionId == s.Id).OrderBy(q => q.OrderIndex).ToListAsync();
        var answers = await db.Answers.AsNoTracking().Where(a => a.Team.QuizSessionId == s.Id).ToListAsync();
        var ranks = ScoreCalculator.Rank(teams.Where(t => t.IsActive).Select(t => (t.Id, t.Nickname, scores.GetValueOrDefault(t.Id))));

        var sb = new StringBuilder();
        sb.AppendLine("Classifica");
        sb.AppendLine("Posizione;Nickname;Tavolo;Portavoce;Punteggio;Risposte corrette;Attivo");
        foreach (var t in teams.OrderBy(t => ranks.GetValueOrDefault(t.Id, int.MaxValue)).ThenBy(t => t.Nickname))
            sb.AppendLine(string.Join(';', ranks.TryGetValue(t.Id, out var r) ? r.ToString() : "-", Csv(t.Nickname), t.TableNumber,
                Csv(t.SpokespersonName), scores.GetValueOrDefault(t.Id), correct.GetValueOrDefault(t.Id), t.IsActive ? "sì" : "no"));

        sb.AppendLine();
        sb.AppendLine("Dettaglio risposte");
        sb.AppendLine("Tavolo;Nickname;Domanda n.;Testo domanda;Risposta data;Risposta corretta;Esito;Punti");
        foreach (var t in teams.OrderBy(t => t.TableNumber))
            foreach (var q in questions)
            {
                var a = answers.FirstOrDefault(x => x.TeamId == t.Id && x.QuestionId == q.Id);
                if (a == null) continue;
                sb.AppendLine(string.Join(';', t.TableNumber, Csv(t.Nickname), q.OrderIndex, Csv(q.Text), a.SelectedLabel, q.CorrectLabel,
                    a.IsCorrect == null ? "n/d" : a.IsCorrect == true ? "corretta" : "sbagliata", a.PointsAwarded));
            }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"quizlive-risultati-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }

    /// <summary>Escape CSV + neutralizzazione delle formule (=, +, -, @) per Excel.</summary>
    private static string Csv(string v)
    {
        if (v.Length > 0 && "=+-@".Contains(v[0])) v = "'" + v;
        return v.Contains(';') || v.Contains('"') || v.Contains('\n') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }
}
