using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using QuizLive.Api.Dtos;
using QuizLive.Api.Services;

namespace QuizLive.Api.Hubs;

public sealed class QuizHub(GameEngine engine, ConnectionTracker tracker) : Hub<IQuizClient>
{
    public const string DisplayGroup = "display";
    public const string AdminGroup = "admin";
    public static string TeamGroup(Guid teamId) => $"team:{teamId}";
    private const string TeamKey = "teamId";
    private const string AttemptsKey = "attempts";

    // ============================================================ Giocatore / Display (anonimi)

    public async Task<RegisterResult> RegisterTeam(RegisterTeamRequest request)
    {
        Throttle();
        var result = await engine.RegisterAsync(request);
        await AttachAsync(result.TeamId);
        return result;
    }

    public async Task<RejoinResult> Rejoin(RejoinRequest request)
    {
        Throttle();
        var result = await engine.RejoinAsync(request.Nickname, request.SessionToken);
        await AttachAsync(result.TeamId);
        return result;
    }

    public async Task<AnswerAckDto> SubmitAnswer(SubmitAnswerRequest request)
    {
        // Mai fidarsi di un teamId inviato dal client: l'identità è quella legata alla connessione.
        var teamId = CurrentTeamId() ?? throw new HubException("NOT_REGISTERED");
        return await engine.SubmitAnswerAsync(teamId, request);
    }

    public async Task<PlayerStateDto> GetMyState()
    {
        var teamId = CurrentTeamId() ?? throw new HubException("NOT_REGISTERED");
        return await engine.GetPlayerStateAsync(teamId);
    }

    public async Task<GameStateDto> JoinDisplay()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, DisplayGroup);
        return await engine.GetDisplayStateAsync();
    }

    // ============================================================ Admin

    [Authorize(Roles = "admin")]
    public async Task<AdminStateDto> JoinAdmin()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
        return await engine.GetAdminStateAsync();
    }

    [Authorize(Roles = "admin")] public Task<AdminStateDto> NextQuestion() => engine.TransitionAsync(AdminAction.NextQuestion);
    [Authorize(Roles = "admin")] public Task<AdminStateDto> ShowQuestion() => engine.TransitionAsync(AdminAction.ShowQuestion);
    [Authorize(Roles = "admin")] public Task<AdminStateDto> OpenAnswers() => engine.TransitionAsync(AdminAction.OpenAnswers);
    [Authorize(Roles = "admin")] public Task<AdminStateDto> CloseAnswers() => engine.TransitionAsync(AdminAction.CloseAnswers);
    [Authorize(Roles = "admin")] public Task<AdminStateDto> RevealAnswer() => engine.TransitionAsync(AdminAction.RevealAnswer);
    [Authorize(Roles = "admin")] public Task<AdminStateDto> ShowLeaderboard() => engine.TransitionAsync(AdminAction.ShowLeaderboard);
    [Authorize(Roles = "admin")] public Task<AdminStateDto> FinishQuiz() => engine.TransitionAsync(AdminAction.FinishQuiz);
    [Authorize(Roles = "admin")] public Task<AdminStateDto> GoToQuestion(Guid questionId) => engine.TransitionAsync(AdminAction.GoToQuestion, questionId);

    [Authorize(Roles = "admin")]
    public Task<AdminStateDto> AdjustScore(Guid teamId, int delta, string reason) => engine.AdjustScoreAsync(teamId, delta, reason, AdminName());

    [Authorize(Roles = "admin")]
    public Task<AdminStateDto> UnlockTeam(Guid teamId, int minutes = 10) => engine.UnlockTeamAsync(teamId, minutes);

    [Authorize(Roles = "admin")]
    public Task<AdminStateDto> TransferTeam(Guid sourceTeamId, Guid targetTeamId) => engine.TransferTeamAsync(sourceTeamId, targetTeamId, AdminName());

    [Authorize(Roles = "admin")]
    public Task<AdminStateDto> SetTeamActive(Guid teamId, bool active) => engine.SetTeamActiveAsync(teamId, active);

    [Authorize(Roles = "admin")]
    public Task<AdminStateDto> ResetTeamAnswer(Guid teamId) => engine.ResetTeamAnswerAsync(teamId);

    // ============================================================ Ciclo di vita

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var teamId = tracker.Remove(Context.ConnectionId);
        if (teamId != null) await engine.TeamDisconnectedAsync(teamId.Value);
        await base.OnDisconnectedAsync(exception);
    }

    // ============================================================ interni

    private async Task AttachAsync(Guid teamId)
    {
        tracker.Add(Context.ConnectionId, teamId);
        Context.Items[TeamKey] = teamId;
        await Groups.AddToGroupAsync(Context.ConnectionId, TeamGroup(teamId));
        engine.ConnectionChanged();
    }

    /// <summary>Identità valida solo se impostata da Register/Rejoin E ancora tracciata (non invalidata da Unlock/Transfer...).</summary>
    private Guid? CurrentTeamId()
    {
        if (Context.Items.TryGetValue(TeamKey, out var o) && o is Guid id && tracker.TeamOf(Context.ConnectionId) == id)
            return id;
        return null;
    }

    private string AdminName() => Context.User?.Identity?.Name ?? "admin";

    /// <summary>Max 10 tentativi al minuto per connessione (Register/Rejoin).</summary>
    private void Throttle()
    {
        if (!(Context.Items.TryGetValue(AttemptsKey, out var o) && o is Queue<DateTime> q))
        {
            q = new Queue<DateTime>();
            Context.Items[AttemptsKey] = q;
        }
        var now = DateTime.UtcNow;
        while (q.Count > 0 && now - q.Peek() > TimeSpan.FromMinutes(1)) q.Dequeue();
        if (q.Count >= 10) throw new HubException("RATE_LIMIT");
        q.Enqueue(now);
    }
}
