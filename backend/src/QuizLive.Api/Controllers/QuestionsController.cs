using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizLive.Api.Data;
using QuizLive.Api.Domain;
using QuizLive.Api.Dtos;
using QuizLive.Api.Services;

namespace QuizLive.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/admin/questions")]
public class QuestionsController(QuizDbContext db, GameEngine engine) : ControllerBase
{
    private static readonly char[] Labels = { 'A', 'B', 'C', 'D' };

    [HttpGet]
    public async Task<QuestionAdminDto[]> List()
    {
        var sid = await SessionIdAsync();
        var items = await db.Questions.AsNoTracking().Include(q => q.Options)
            .Where(q => q.QuizSessionId == sid).OrderBy(q => q.OrderIndex).ToListAsync();
        return items.Select(ToDto).ToArray();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] QuestionEditDto dto)
    {
        if (Validate(dto) is { } err) return BadRequest(new { error = err });
        var s = await db.Sessions.FirstAsync(x => x.IsActive);
        var max = await db.Questions.Where(q => q.QuizSessionId == s.Id).Select(q => (int?)q.OrderIndex).MaxAsync() ?? 0;
        var q = Build(dto, s.Id, max + 1);
        db.Questions.Add(q);
        await db.SaveChangesAsync();
        await engine.RefreshAsync();
        return Ok(ToDto(q));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] QuestionEditDto dto)
    {
        if (Validate(dto) is { } err) return BadRequest(new { error = err });
        var q = await db.Questions.Include(x => x.Options).FirstOrDefaultAsync(x => x.Id == id);
        if (q == null) return NotFound();
        if (await InUseAsync(id)) return Conflict(new { error = "QUESTION_IN_USE" });

        q.Text = TeamService.Clean(dto.Text);
        q.MediaType = dto.MediaType;
        q.MediaUrl = dto.MediaType == MediaType.None ? null : dto.MediaUrl;
        q.CorrectLabel = dto.CorrectLabel;
        q.Explanation = string.IsNullOrWhiteSpace(dto.Explanation) ? null : dto.Explanation.Trim();
        foreach (var o in q.Options) o.Text = dto.Options.First(x => x.Label == o.Label).Text.Trim();
        await db.SaveChangesAsync();
        await engine.RefreshAsync();
        return Ok(ToDto(q));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var q = await db.Questions.FirstOrDefaultAsync(x => x.Id == id);
        if (q == null) return NotFound();
        if (await InUseAsync(id)) return Conflict(new { error = "QUESTION_IN_USE" });
        db.Questions.Remove(q);
        await db.SaveChangesAsync();
        await QuestionOrdering.NormalizeAsync(db, q.QuizSessionId);
        await engine.RefreshAsync();
        return NoContent();
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderRequest req)
    {
        var sid = await SessionIdAsync();
        var existing = await db.Questions.Where(q => q.QuizSessionId == sid).Select(q => q.Id).ToListAsync();
        if (req.OrderedIds == null || req.OrderedIds.Length != existing.Count ||
            !req.OrderedIds.ToHashSet().SetEquals(existing))
            return BadRequest(new { error = "L'elenco deve contenere tutte le domande, una volta sola." });
        await QuestionOrdering.ApplyAsync(db, sid, req.OrderedIds);
        await engine.RefreshAsync();
        return NoContent();
    }

    /// <summary>JSON array di domande (stesso schema del POST). mode=append (default) | replace.</summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] QuestionEditDto[] items, [FromQuery] string mode = "append")
    {
        if (items == null || items.Length == 0) return BadRequest(new { error = "Nessuna domanda da importare." });
        for (var i = 0; i < items.Length; i++)
            if (Validate(items[i]) is { } err) return BadRequest(new { error = $"Domanda {i + 1}: {err}" });

        var s = await db.Sessions.FirstAsync(x => x.IsActive);
        var replace = string.Equals(mode, "replace", StringComparison.OrdinalIgnoreCase);
        if (replace && s.Phase != GamePhase.Lobby)
            return Conflict(new { error = "La sostituzione è consentita solo in fase Lobby." });

        if (replace)
        {
            db.Questions.RemoveRange(await db.Questions.Where(q => q.QuizSessionId == s.Id).ToListAsync());
            await db.SaveChangesAsync();
        }
        var max = replace ? 0 : await db.Questions.Where(q => q.QuizSessionId == s.Id).Select(q => (int?)q.OrderIndex).MaxAsync() ?? 0;
        foreach (var dto in items) db.Questions.Add(Build(dto, s.Id, ++max));
        await db.SaveChangesAsync();
        await engine.RefreshAsync();
        return Ok(new { imported = items.Length });
    }

    // ------------------------------------------------------------------ helper
    private async Task<Guid> SessionIdAsync() => await db.Sessions.Where(s => s.IsActive).Select(s => s.Id).FirstAsync();

    /// <summary>Modifica/cancellazione vietata sulla domanda corrente se la fase non è Lobby/Ready.</summary>
    private async Task<bool> InUseAsync(Guid questionId)
    {
        var s = await db.Sessions.AsNoTracking().FirstAsync(x => x.IsActive);
        return s.CurrentQuestionId == questionId && s.Phase is not (GamePhase.Lobby or GamePhase.Ready);
    }

    private static Question Build(QuestionEditDto dto, Guid sessionId, int order) => new()
    {
        QuizSessionId = sessionId,
        OrderIndex = order,
        Text = TeamService.Clean(dto.Text),
        MediaType = dto.MediaType,
        MediaUrl = dto.MediaType == MediaType.None ? null : dto.MediaUrl,
        CorrectLabel = dto.CorrectLabel,
        Explanation = string.IsNullOrWhiteSpace(dto.Explanation) ? null : dto.Explanation.Trim(),
        Options = dto.Options.OrderBy(o => o.Label).Select(o => new Option { Label = o.Label, Text = o.Text.Trim() }).ToList()
    };

    private static QuestionAdminDto ToDto(Question q) => new(q.Id, q.OrderIndex, q.Text, q.MediaType, q.MediaUrl,
        q.CorrectLabel, q.Explanation, q.Options.OrderBy(o => o.Label).Select(o => new OptionEditDto(o.Label, o.Text)).ToArray());

    private static string? Validate(QuestionEditDto q)
    {
        if (q == null) return "Dati mancanti.";
        var text = TeamService.Clean(q.Text);
        if (text.Length is 0 or > 500) return "Il testo della domanda deve essere di 1-500 caratteri.";
        if (q.Options is not { Length: 4 }) return "Servono esattamente 4 opzioni (A-D).";
        if (!q.Options.Select(o => o.Label).OrderBy(c => c).SequenceEqual(Labels)) return "Le opzioni devono essere A, B, C, D (una per lettera).";
        if (q.Options.Any(o => string.IsNullOrWhiteSpace(o.Text) || o.Text.Trim().Length > 200)) return "Ogni opzione deve avere 1-200 caratteri.";
        if (!Labels.Contains(q.CorrectLabel)) return "La risposta corretta deve essere A, B, C o D.";
        if (q.Explanation is { Length: > 500 }) return "La spiegazione può avere al massimo 500 caratteri.";
        if (q.MediaType != MediaType.None)
        {
            if (string.IsNullOrEmpty(q.MediaUrl) || !q.MediaUrl.StartsWith("/media/") || q.MediaUrl.Contains("..") || q.MediaUrl.Length > 300)
                return "URL media non valido (usa il caricamento file).";
        }
        return null;
    }
}
