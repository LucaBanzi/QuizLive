using Microsoft.EntityFrameworkCore;
using QuizLive.Api.Data;
using QuizLive.Api.Domain;
using QuizLive.Api.Dtos;

namespace QuizLive.Api.Services;

/// <summary>Dati caricati dal DB da cui si derivano tutti i DTO. Costruibile a mano nei test.</summary>
public sealed class StateData
{
    public required QuizSession Session { get; init; }
    public List<Team> Teams { get; init; } = new();
    public Dictionary<Guid, int> Scores { get; init; } = new();
    public Dictionary<Guid, int> CorrectCounts { get; init; } = new();
    public Question? Current { get; init; }
    public List<Answer> CurrentAnswers { get; init; } = new();
    public int TotalQuestions { get; init; }
    public string? NextPreview { get; init; }
    public HashSet<Guid> Connected { get; init; } = new();

    public int ScoreOf(Guid teamId) => Scores.GetValueOrDefault(teamId);
}

/// <summary>
/// Costruisce i DTO filtrati per audience. REGOLA: CorrectLabel esce dal server solo
/// nella fase AnswerRevealed (e verso l'admin, che è autenticato).
/// </summary>
public static class SnapshotBuilder
{
    private static readonly char[] Labels = { 'A', 'B', 'C', 'D' };

    public static async Task<StateData> LoadAsync(QuizDbContext db, IReadOnlyCollection<Guid> connected, CancellationToken ct = default)
    {
        var session = await db.Sessions.AsNoTracking().FirstAsync(s => s.IsActive, ct);
        var teams = await db.Teams.AsNoTracking().Where(t => t.QuizSessionId == session.Id).ToListAsync(ct);
        var (scores, correct) = await ScoreService.TotalsAsync(db, session.Id, ct);
        var total = await db.Questions.CountAsync(q => q.QuizSessionId == session.Id, ct);

        Question? current = null;
        if (session.CurrentQuestionId != null)
            current = await db.Questions.AsNoTracking().Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == session.CurrentQuestionId, ct);

        var answers = current == null
            ? new List<Answer>()
            : await db.Answers.AsNoTracking().Where(a => a.QuestionId == current.Id).ToListAsync(ct);

        var after = current?.OrderIndex ?? 0;
        var next = await db.Questions.AsNoTracking()
            .Where(q => q.QuizSessionId == session.Id && q.OrderIndex > after)
            .OrderBy(q => q.OrderIndex).Select(q => q.Text).FirstOrDefaultAsync(ct);

        return new StateData
        {
            Session = session, Teams = teams, Scores = scores, CorrectCounts = correct, Current = current,
            CurrentAnswers = answers, TotalQuestions = total, NextPreview = next, Connected = connected.ToHashSet()
        };
    }

    // ---------- Classifica ----------
    public static LeaderboardEntryDto[] Leaderboard(StateData d)
    {
        var active = d.Teams.Where(t => t.IsActive).ToList();
        var ranks = ScoreCalculator.Rank(active.Select(t => (t.Id, t.Nickname, d.ScoreOf(t.Id))));
        var last = d.Current == null
            ? new Dictionary<Guid, int>()
            : d.CurrentAnswers.Where(a => a.IsCorrect != null).ToDictionary(a => a.TeamId, a => a.PointsAwarded);

        return active
            .OrderBy(t => ranks[t.Id]).ThenBy(t => t.Nickname, StringComparer.OrdinalIgnoreCase)
            .Select(t => new LeaderboardEntryDto(ranks[t.Id], t.Id, t.Nickname, t.TableNumber, d.ScoreOf(t.Id),
                d.CorrectCounts.GetValueOrDefault(t.Id), last.GetValueOrDefault(t.Id)))
            .ToArray();
    }

    // ---------- Display (pubblico) ----------
    public static GameStateDto Display(StateData d) => BuildGame(d, admin: false);

    // ---------- Admin ----------
    public static AdminStateDto Admin(StateData d)
    {
        var phase = d.Session.Phase;
        var answersVisible = phase is GamePhase.AnswersOpen or GamePhase.AnswersClosed or GamePhase.AnswerRevealed;
        var teams = d.Teams.OrderBy(t => t.TableNumber).Select(t =>
        {
            var a = answersVisible ? d.CurrentAnswers.FirstOrDefault(x => x.TeamId == t.Id) : null;
            return new TeamAdminDto(t.Id, t.Nickname, t.SpokespersonName, t.TableNumber, d.ScoreOf(t.Id),
                t.IsActive, d.Connected.Contains(t.Id), a != null, a?.SelectedLabel, t.RecoveryOpenUntil);
        }).ToArray();
        return new AdminStateDto(BuildGame(d, admin: true), d.Current?.CorrectLabel, d.NextPreview, teams);
    }

    // ---------- Giocatore (telefono) ----------
    public static PlayerStateDto Player(StateData d, Guid teamId)
    {
        var team = d.Teams.First(t => t.Id == teamId);
        var phase = d.Session.Phase;
        var active = d.Teams.Where(t => t.IsActive).ToList();
        var ranks = ScoreCalculator.Rank(active.Select(t => (t.Id, t.Nickname, d.ScoreOf(t.Id))));
        var rank = team.IsActive ? ranks.GetValueOrDefault(teamId) : 0;

        var hasQuestion = d.Current != null && IsQuestionPhase(phase);
        var mine = hasQuestion ? d.CurrentAnswers.FirstOrDefault(a => a.TeamId == teamId) : null;
        var revealed = phase == GamePhase.AnswerRevealed && d.Current != null;

        return new PlayerStateDto(
            d.Session.Seq, phase,
            hasQuestion ? d.Current!.Id : null,
            d.Current?.OrderIndex ?? 0, d.TotalQuestions,
            mine?.SelectedLabel,
            revealed ? mine?.IsCorrect ?? false : null,
            revealed ? d.Current!.CorrectLabel : null,
            d.ScoreOf(teamId), rank, team.Nickname);
    }

    // ---------- interni ----------
    private static bool IsQuestionPhase(GamePhase p) =>
        p is GamePhase.QuestionShown or GamePhase.AnswersOpen or GamePhase.AnswersClosed or GamePhase.AnswerRevealed;

    private static GameStateDto BuildGame(StateData d, bool admin)
    {
        var s = d.Session;
        var phase = s.Phase;
        var showQuestion = d.Current != null && (admin ? phase != GamePhase.Lobby : IsQuestionPhase(phase));
        var activeIds = d.Teams.Where(t => t.IsActive).Select(t => t.Id).ToHashSet();

        RevealDto? reveal = null;
        if (phase == GamePhase.AnswerRevealed && d.Current != null)
        {
            var votes = Labels.ToDictionary(l => l, _ => 0);
            foreach (var a in d.CurrentAnswers.Where(a => activeIds.Contains(a.TeamId)))
                if (votes.ContainsKey(a.SelectedLabel)) votes[a.SelectedLabel]++;
            reveal = new RevealDto(d.Current.CorrectLabel, d.Current.Explanation, votes);
        }

        // In Lobby la classifica serve al Display per mostrare l'elenco dei tavoli iscritti.
        var withBoard = admin || phase is GamePhase.Lobby or GamePhase.Leaderboard or GamePhase.Finished;
        var counting = phase is GamePhase.AnswersOpen or GamePhase.AnswersClosed or GamePhase.AnswerRevealed;
        var answered = counting ? d.CurrentAnswers.Count(a => activeIds.Contains(a.TeamId)) : 0;

        return new GameStateDto(s.Seq, phase, showQuestion ? ToQuestion(d) : null, reveal,
            answered, activeIds.Count, withBoard ? Leaderboard(d) : null, s.Name);
    }

    private static QuestionDto ToQuestion(StateData d)
    {
        var q = d.Current!;
        return new QuestionDto(q.Id, q.OrderIndex, d.TotalQuestions, q.Text, q.MediaType, q.MediaUrl,
            q.Options.OrderBy(o => o.Label).Select(o => new OptionDto(o.Label, o.Text)).ToArray());
    }
}
