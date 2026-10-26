using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuizLive.Api.Data;
using QuizLive.Api.Domain;
using QuizLive.Api.Dtos;

namespace QuizLive.Api.Services;

/// <summary>
/// Cuore del gioco (singleton). Il server è autoritativo: ogni transizione e ogni SubmitAnswer
/// passano dal semaforo (SemaphoreSlim(1,1)), quindi non esistono race tra "chiudi risposte" e invio.
/// Ordine di ogni operazione: valida → modifica → SaveChanges (commit) → broadcast.
/// Se il DB fallisce lo stato non avanza e nessun client riceve nulla.
/// </summary>
public sealed class GameEngine
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IServiceScopeFactory _scopes;
    private readonly IQuizBroadcaster _bc;
    private readonly ConnectionTracker _tracker;
    private readonly ILogger<GameEngine> _log;
    private readonly int _maxTeams;
    private readonly Coalescer _countCoalescer;
    private readonly Coalescer _adminCoalescer;

    public GameEngine(IServiceScopeFactory scopes, IQuizBroadcaster bc, ConnectionTracker tracker,
        IConfiguration cfg, ILogger<GameEngine> log)
    {
        _scopes = scopes; _bc = bc; _tracker = tracker; _log = log;
        _maxTeams = cfg.GetValue("Quiz:MaxTeams", 60);
        _countCoalescer = new Coalescer(PushAnswerCountAsync, TimeSpan.FromMilliseconds(200));
        _adminCoalescer = new Coalescer(PushAdminAsync, TimeSpan.FromMilliseconds(400));
    }

    // ------------------------------------------------------------------ infrastruttura
    private async Task<T> RunAsync<T>(Func<QuizDbContext, QuizSession, Task<T>> body)
    {
        await _gate.WaitAsync();
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
            return await body(db, await GetSessionAsync(db));
        }
        finally { _gate.Release(); }
    }

    private async Task<T> ReadAsync<T>(Func<QuizDbContext, Task<T>> body)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
        return await body(db);
    }

    private static async Task<QuizSession> GetSessionAsync(QuizDbContext db)
    {
        var s = await db.Sessions.FirstOrDefaultAsync(x => x.IsActive);
        if (s == null)
        {
            s = new QuizSession();
            db.Sessions.Add(s);
            await db.SaveChangesAsync();
        }
        return s;
    }

    private static void Touch(QuizSession s) { s.Seq++; s.UpdatedAt = DateTime.UtcNow; }

    private async Task<StateData> PublishAsync(QuizDbContext db, PublishScope scope, IReadOnlyCollection<Guid>? onlyTeams = null)
    {
        var d = await SnapshotBuilder.LoadAsync(db, _tracker.ConnectedTeamIds());
        try { await _bc.PublishAsync(d, scope, onlyTeams); }
        catch (Exception ex) { _log.LogWarning(ex, "Broadcast fallito (lo stato è già salvato)."); }
        return d;
    }

    // ------------------------------------------------------------------ letture
    public Task<AdminStateDto> GetAdminStateAsync() => ReadAsync(async db =>
        SnapshotBuilder.Admin(await SnapshotBuilder.LoadAsync(db, _tracker.ConnectedTeamIds())));

    public Task<GameStateDto> GetDisplayStateAsync() => ReadAsync(async db =>
        SnapshotBuilder.Display(await SnapshotBuilder.LoadAsync(db, _tracker.ConnectedTeamIds())));

    public Task<PlayerStateDto> GetPlayerStateAsync(Guid teamId) => ReadAsync(async db =>
    {
        var d = await SnapshotBuilder.LoadAsync(db, _tracker.ConnectedTeamIds());
        if (d.Teams.All(t => t.Id != teamId)) throw new HubException("NOT_REGISTERED");
        return SnapshotBuilder.Player(d, teamId);
    });

    // ------------------------------------------------------------------ transizioni (admin)
    public Task<AdminStateDto> TransitionAsync(AdminAction action, Guid? questionId = null) => RunAsync(async (db, s) =>
    {
        var next = StateMachine.Next(s.Phase, action) ?? throw new HubException("INVALID_TRANSITION");

        switch (action)
        {
            case AdminAction.NextQuestion:
            {
                if (!await db.Questions.AnyAsync(q => q.QuizSessionId == s.Id)) throw new HubException("NO_QUESTIONS");
                var curIdx = s.CurrentQuestionId == null ? 0
                    : await db.Questions.Where(q => q.Id == s.CurrentQuestionId).Select(q => q.OrderIndex).FirstOrDefaultAsync();
                var nextQ = await db.Questions.Where(q => q.QuizSessionId == s.Id && q.OrderIndex > curIdx)
                    .OrderBy(q => q.OrderIndex).FirstOrDefaultAsync();
                if (nextQ == null) next = GamePhase.Finished;   // domande finite: la CurrentQuestion resta l'ultima
                else s.CurrentQuestionId = nextQ.Id;
                break;
            }
            case AdminAction.GoToQuestion:
            {
                if (questionId == null || !await db.Questions.AnyAsync(q => q.Id == questionId && q.QuizSessionId == s.Id))
                    throw new HubException("QUESTION_NOT_FOUND");
                s.CurrentQuestionId = questionId;
                break;
            }
            case AdminAction.OpenAnswers:
            {
                // azzera il contatore: eventuali risposte residue (es. domanda riproposta con GoToQuestion) vengono cancellate
                var stale = await db.Answers.Where(a => a.QuestionId == s.CurrentQuestionId).ToListAsync();
                db.Answers.RemoveRange(stale);
                break;
            }
            case AdminAction.RevealAnswer:
                await GradeCurrentAsync(db, s);
                break;
        }

        s.Phase = next;
        Touch(s);
        await db.SaveChangesAsync();           // commit PRIMA del broadcast
        var d = await PublishAsync(db, PublishScope.All);
        return SnapshotBuilder.Admin(d);
    });

    /// <summary>Idempotente: ricalcola IsCorrect e PointsAwarded per tutte le risposte della domanda corrente.</summary>
    private static async Task GradeCurrentAsync(QuizDbContext db, QuizSession s)
    {
        var q = await db.Questions.AsNoTracking().FirstAsync(x => x.Id == s.CurrentQuestionId);
        var answers = await db.Answers.Where(a => a.QuestionId == q.Id).ToListAsync();
        foreach (var a in answers)
        {
            var (ok, pts) = ScoreCalculator.Grade(a.SelectedLabel, q.CorrectLabel, s.PointsPerCorrect);
            a.IsCorrect = ok;
            a.PointsAwarded = pts;
        }
    }

    // ------------------------------------------------------------------ giocatori
    public Task<RegisterResult> RegisterAsync(RegisterTeamRequest req) => RunAsync(async (db, s) =>
    {
        var (nick, spokes, table) = TeamService.Validate(req);
        var norm = TeamService.Normalize(nick);

        async Task CheckAsync()
        {
            if (await db.Teams.AnyAsync(t => t.QuizSessionId == s.Id && t.NicknameNormalized == norm))
                throw new HubException("NICKNAME_TAKEN");
            if (await db.Teams.AnyAsync(t => t.QuizSessionId == s.Id && t.IsActive && t.TableNumber == table))
                throw new HubException("TABLE_TAKEN");
            if (await db.Teams.CountAsync(t => t.QuizSessionId == s.Id && t.IsActive) >= _maxTeams)
                throw new HubException("TEAM_LIMIT");
        }

        await CheckAsync();
        var team = new Team
        {
            QuizSessionId = s.Id, Nickname = nick, NicknameNormalized = norm,
            SpokespersonName = spokes, TableNumber = table
        };
        db.Teams.Add(team);
        Touch(s);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            db.Entry(team).State = EntityState.Detached;
            await CheckAsync();     // individua il vincolo violato in una corsa tra due iscrizioni
            throw new HubException("VALIDATION");
        }

        var d = await PublishAsync(db, PublishScope.Display | PublishScope.Admin | PublishScope.Leaderboard, Array.Empty<Guid>());
        return new RegisterResult(team.Id, team.SessionToken, SnapshotBuilder.Player(d, team.Id));
    });

    public Task<RejoinResult> RejoinAsync(string nickname, Guid? token) => RunAsync(async (db, s) =>
    {
        var norm = TeamService.Normalize(nickname ?? "");
        var team = await db.Teams.FirstOrDefaultAsync(t => t.QuizSessionId == s.Id && t.NicknameNormalized == norm);
        if (team == null || !team.IsActive) throw new HubException("NOT_AUTHORIZED");

        var now = DateTime.UtcNow;
        var replaced = false;
        if (token != null && token == team.SessionToken)
        {
            // caso 1: token valido
        }
        else if (team.RecoveryOpenUntil != null && team.RecoveryOpenUntil > now)
        {
            // caso 2: finestra di recupero aperta dall'admin (token assente o errato)
            team.SessionToken = Guid.NewGuid();
            team.RecoveryOpenUntil = null;
            replaced = true;
        }
        else throw new HubException(token == null ? "RECOVERY_CLOSED" : "NOT_AUTHORIZED");

        team.LastSeenAt = now;
        if (replaced) Touch(s);
        await db.SaveChangesAsync();

        if (replaced) await _bc.InvalidateTeamAsync(team.Id, "REPLACED");
        var d = replaced
            ? await PublishAsync(db, PublishScope.Admin, Array.Empty<Guid>())
            : await SnapshotBuilder.LoadAsync(db, _tracker.ConnectedTeamIds());
        return new RejoinResult(team.Id, team.SessionToken, SnapshotBuilder.Player(d, team.Id));
    });

    /// <summary>Ordine di verifica: team → fase → domanda → etichetta → risposta già presente (§E.3).</summary>
    public Task<AnswerAckDto> SubmitAnswerAsync(Guid teamId, SubmitAnswerRequest req) => RunAsync(async (db, s) =>
    {
        var team = await db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId && t.QuizSessionId == s.Id);
        if (team == null || !team.IsActive) return new AnswerAckDto("NotRegistered", null);
        if (s.Phase != GamePhase.AnswersOpen) return new AnswerAckDto("Closed", null);
        if (s.CurrentQuestionId == null || req.QuestionId != s.CurrentQuestionId) return new AnswerAckDto("WrongQuestion", null);

        var label = char.ToUpperInvariant(req.Label);
        if (label is < 'A' or > 'D') throw new HubException("VALIDATION");

        var existing = await db.Answers.AsNoTracking().FirstOrDefaultAsync(a => a.TeamId == teamId && a.QuestionId == req.QuestionId);
        if (existing != null) return new AnswerAckDto("AlreadyAnswered", existing.SelectedLabel);

        db.Answers.Add(new Answer { TeamId = teamId, QuestionId = req.QuestionId, SelectedLabel = label });
        Touch(s);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            // indice univoco (TeamId, QuestionId): trattato come AlreadyAnswered
            db.ChangeTracker.Clear();
            var again = await db.Answers.AsNoTracking().FirstOrDefaultAsync(a => a.TeamId == teamId && a.QuestionId == req.QuestionId);
            if (again != null) return new AnswerAckDto("AlreadyAnswered", again.SelectedLabel);
            throw;
        }

        _countCoalescer.Trigger();
        _adminCoalescer.Trigger();
        await PublishAsync(db, PublishScope.Players, new[] { teamId });
        return new AnswerAckDto("Accepted", label);
    });

    // ------------------------------------------------------------------ gestione tavoli (admin)
    public Task<AdminStateDto> AdjustScoreAsync(Guid teamId, int delta, string reason, string admin) => RunAsync(async (db, s) =>
    {
        reason = TeamService.Clean(reason);
        if (delta == 0 || Math.Abs(delta) > 100000 || reason.Length is 0 or > 200) throw new HubException("VALIDATION");
        if (!await db.Teams.AnyAsync(t => t.Id == teamId && t.QuizSessionId == s.Id)) throw new HubException("TEAM_NOT_FOUND");

        db.Adjustments.Add(new ScoreAdjustment { TeamId = teamId, Delta = delta, Reason = reason, Kind = AdjustmentKind.Manual, CreatedBy = admin });
        Touch(s);
        await db.SaveChangesAsync();
        return SnapshotBuilder.Admin(await PublishAsync(db, PublishScope.All));
    });

    public Task<AdminStateDto> UnlockTeamAsync(Guid teamId, int minutes) => RunAsync(async (db, s) =>
    {
        var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == teamId && t.QuizSessionId == s.Id)
                   ?? throw new HubException("TEAM_NOT_FOUND");
        minutes = Math.Clamp(minutes, 1, 120);
        team.SessionToken = Guid.NewGuid();                    // la vecchia sessione smette di funzionare subito
        team.RecoveryOpenUntil = DateTime.UtcNow.AddMinutes(minutes);
        Touch(s);
        await db.SaveChangesAsync();
        await _bc.InvalidateTeamAsync(teamId, "RESET");
        return SnapshotBuilder.Admin(await PublishAsync(db, PublishScope.All));
    });

    public Task<AdminStateDto> TransferTeamAsync(Guid sourceId, Guid targetId, string admin) => RunAsync(async (db, s) =>
    {
        if (sourceId == targetId) throw new HubException("VALIDATION");
        var src = await db.Teams.FirstOrDefaultAsync(t => t.Id == sourceId && t.QuizSessionId == s.Id) ?? throw new HubException("TEAM_NOT_FOUND");
        var tgt = await db.Teams.FirstOrDefaultAsync(t => t.Id == targetId && t.QuizSessionId == s.Id) ?? throw new HubException("TEAM_NOT_FOUND");

        await using var tx = await db.Database.BeginTransactionAsync();

        var srcAnswers = await db.Answers.Where(a => a.TeamId == sourceId).ToListAsync();
        var tgtAnswers = await db.Answers.Where(a => a.TeamId == targetId).ToListAsync();
        var toDelete = new List<Answer>();
        var toMove = new List<Answer>();
        foreach (var a in srcAnswers)
        {
            var other = tgtAnswers.FirstOrDefault(x => x.QuestionId == a.QuestionId);
            if (other == null) { toMove.Add(a); continue; }
            // conflitto: si tiene la risposta con PointsAwarded maggiore (a parità, quella del target)
            if (a.PointsAwarded > other.PointsAwarded) { toDelete.Add(other); toMove.Add(a); }
            else toDelete.Add(a);
        }
        db.Answers.RemoveRange(toDelete);
        await db.SaveChangesAsync();                           // prima le cancellazioni: indice univoco (TeamId, QuestionId)

        foreach (var a in toMove) a.TeamId = targetId;
        foreach (var adj in await db.Adjustments.Where(x => x.TeamId == sourceId).ToListAsync()) adj.TeamId = targetId;

        var reason = $"Trasferimento da {src.Nickname}";
        db.Adjustments.Add(new ScoreAdjustment
        {
            TeamId = targetId, Delta = 0, Kind = AdjustmentKind.Transfer, CreatedBy = admin,
            Reason = reason.Length > 200 ? reason[..200] : reason
        });
        src.IsActive = false;
        Touch(s);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        await _bc.InvalidateTeamAsync(sourceId, "TRANSFERRED");
        return SnapshotBuilder.Admin(await PublishAsync(db, PublishScope.All));
    });

    public Task<AdminStateDto> SetTeamActiveAsync(Guid teamId, bool active) => RunAsync(async (db, s) =>
    {
        var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == teamId && t.QuizSessionId == s.Id) ?? throw new HubException("TEAM_NOT_FOUND");
        if (active && !team.IsActive &&
            await db.Teams.AnyAsync(t => t.QuizSessionId == s.Id && t.Id != teamId && t.IsActive && t.TableNumber == team.TableNumber))
            throw new HubException("TABLE_TAKEN");

        team.IsActive = active;
        Touch(s);
        await db.SaveChangesAsync();
        if (!active) await _bc.InvalidateTeamAsync(teamId, "DEACTIVATED");
        return SnapshotBuilder.Admin(await PublishAsync(db, PublishScope.All));
    });

    public Task<AdminStateDto> ResetTeamAnswerAsync(Guid teamId) => RunAsync(async (db, s) =>
    {
        if (s.Phase is not (GamePhase.AnswersOpen or GamePhase.AnswersClosed)) throw new HubException("INVALID_PHASE");
        var ans = await db.Answers.FirstOrDefaultAsync(a => a.TeamId == teamId && a.QuestionId == s.CurrentQuestionId);
        if (ans != null) db.Answers.Remove(ans);
        Touch(s);
        await db.SaveChangesAsync();
        _countCoalescer.Trigger();
        return SnapshotBuilder.Admin(await PublishAsync(db, PublishScope.All));
    });

    /// <summary>Azzera risposte, correzioni e team; riporta la serata a Lobby. Le domande restano.</summary>
    public Task<AdminStateDto> ResetQuizAsync() => RunAsync(async (db, s) =>
    {
        var teams = await db.Teams.Where(t => t.QuizSessionId == s.Id).ToListAsync();
        var ids = teams.Select(t => t.Id).ToList();
        db.Answers.RemoveRange(await db.Answers.Where(a => ids.Contains(a.TeamId)).ToListAsync());
        db.Adjustments.RemoveRange(await db.Adjustments.Where(a => ids.Contains(a.TeamId)).ToListAsync());
        db.Teams.RemoveRange(teams);
        s.Phase = GamePhase.Lobby;
        s.CurrentQuestionId = null;
        Touch(s);
        await db.SaveChangesAsync();
        foreach (var id in ids) await _bc.InvalidateTeamAsync(id, "RESET");
        return SnapshotBuilder.Admin(await PublishAsync(db, PublishScope.All));
    });

    /// <summary>Da chiamare dopo modifiche alle domande (CRUD, riordino, import).</summary>
    public Task RefreshAsync() => RunAsync(async (db, s) =>
    {
        Touch(s);
        await db.SaveChangesAsync();
        await PublishAsync(db, PublishScope.Display | PublishScope.Admin);
        return true;
    });

    // ------------------------------------------------------------------ connessioni
    public void ConnectionChanged() => _adminCoalescer.Trigger();

    public async Task TeamDisconnectedAsync(Guid teamId)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
            var team = await db.Teams.FindAsync(teamId);
            if (team != null) { team.LastSeenAt = DateTime.UtcNow; await db.SaveChangesAsync(); }
        }
        catch (Exception ex) { _log.LogDebug(ex, "Aggiornamento LastSeenAt fallito"); }
        _adminCoalescer.Trigger();
    }

    private async Task PushAnswerCountAsync()
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
        var s = await db.Sessions.AsNoTracking().FirstAsync(x => x.IsActive);
        if (s.CurrentQuestionId == null) return;
        var answered = await db.Answers.CountAsync(a => a.QuestionId == s.CurrentQuestionId && a.Team.IsActive);
        var total = await db.Teams.CountAsync(t => t.QuizSessionId == s.Id && t.IsActive);
        await _bc.PublishAnswerCountAsync(answered, total);
    }

    private async Task PushAdminAsync()
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
        var d = await SnapshotBuilder.LoadAsync(db, _tracker.ConnectedTeamIds());
        await _bc.PublishAsync(d, PublishScope.Admin);
    }
}
