using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using QuizLive.Api.Data;
using QuizLive.Api.Domain;
using QuizLive.Api.Dtos;
using QuizLive.Api.Services;

namespace QuizLive.Tests;

public class GameEngineTests
{
    private sealed class NoopBroadcaster : IQuizBroadcaster
    {
        public List<(Guid Team, string Reason)> Invalidated { get; } = new();
        public Task PublishAsync(StateData data, PublishScope scope, IReadOnlyCollection<Guid>? onlyTeams = null) => Task.CompletedTask;
        public Task PublishAnswerCountAsync(int answered, int total) => Task.CompletedTask;
        public Task InvalidateTeamAsync(Guid teamId, string reason) { Invalidated.Add((teamId, reason)); return Task.CompletedTask; }
    }

    private sealed class Fixture
    {
        public GameEngine Engine = null!;
        public NoopBroadcaster Bc = new();
        public IServiceProvider Sp = null!;
        public List<Guid> QuestionIds = new();

        public static async Task<Fixture> CreateAsync(int questions = 2)
        {
            var f = new Fixture();
            var name = Guid.NewGuid().ToString();
            var services = new ServiceCollection();
            services.AddDbContext<QuizDbContext>(o => o.UseInMemoryDatabase(name)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            f.Sp = services.BuildServiceProvider();
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Quiz:MaxTeams"] = "3" }).Build();
            f.Engine = new GameEngine(f.Sp.GetRequiredService<IServiceScopeFactory>(), f.Bc, new ConnectionTracker(), cfg, NullLogger<GameEngine>.Instance);

            using var scope = f.Sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
            var s = new QuizSession { IsActive = true };
            db.Sessions.Add(s);
            for (var i = 1; i <= questions; i++)
            {
                var q = new Question
                {
                    QuizSessionId = s.Id, OrderIndex = i, Text = $"Domanda {i}", CorrectLabel = 'B',
                    Options = "ABCD".Select(l => new Option { Label = l, Text = $"opt {l}" }).ToList()
                };
                db.Questions.Add(q);
                f.QuestionIds.Add(q.Id);
            }
            await db.SaveChangesAsync();
            return f;
        }

        public async Task<RegisterResult> Register(string nick, int table) =>
            await Engine.RegisterAsync(new RegisterTeamRequest(nick, "Mario Rossi", table));

        public async Task OpenFirstQuestion()
        {
            await Engine.TransitionAsync(AdminAction.NextQuestion);
            await Engine.TransitionAsync(AdminAction.ShowQuestion);
            await Engine.TransitionAsync(AdminAction.OpenAnswers);
        }
    }

    private static async Task<string> ErrorOf(Func<Task> act) => (await Assert.ThrowsAsync<HubException>(act)).Message;

    [Fact]
    public async Task Register_validates_and_rejects_duplicates()
    {
        var f = await Fixture.CreateAsync();
        await f.Register("Gufi", 1);
        Assert.Equal("NICKNAME_TAKEN", await ErrorOf(() => f.Register("  gufi ", 2)));
        Assert.Equal("TABLE_TAKEN", await ErrorOf(() => f.Register("Altri", 1)));
        Assert.Equal("VALIDATION", await ErrorOf(() => f.Register("x", 5)));
        Assert.Equal("VALIDATION", await ErrorOf(() => f.Register("Valido", 100)));
    }

    [Fact]
    public async Task Register_respects_team_limit()
    {
        var f = await Fixture.CreateAsync();
        await f.Register("a1", 1); await f.Register("a2", 2); await f.Register("a3", 3);
        Assert.Equal("TEAM_LIMIT", await ErrorOf(() => f.Register("a4", 4)));
    }

    [Fact]
    public async Task Invalid_transition_has_no_effect()
    {
        var f = await Fixture.CreateAsync();
        Assert.Equal("INVALID_TRANSITION", await ErrorOf(() => f.Engine.TransitionAsync(AdminAction.OpenAnswers)));
        var state = await f.Engine.GetAdminStateAsync();
        Assert.Equal(GamePhase.Lobby, state.Game.Phase);
    }

    [Fact]
    public async Task Double_click_second_request_fails()
    {
        var f = await Fixture.CreateAsync();
        await f.Engine.TransitionAsync(AdminAction.NextQuestion);
        Assert.Equal("INVALID_TRANSITION", await ErrorOf(() => f.Engine.TransitionAsync(AdminAction.NextQuestion)));
    }

    [Fact]
    public async Task Submit_is_single_and_final()
    {
        var f = await Fixture.CreateAsync();
        var t = await f.Register("Gufi", 1);
        var qid = f.QuestionIds[0];

        await f.Engine.TransitionAsync(AdminAction.NextQuestion);
        await f.Engine.TransitionAsync(AdminAction.ShowQuestion);
        Assert.Equal("Closed", (await f.Engine.SubmitAnswerAsync(t.TeamId, new(qid, 'B', "1"))).Status);

        await f.Engine.TransitionAsync(AdminAction.OpenAnswers);
        Assert.Equal("WrongQuestion", (await f.Engine.SubmitAnswerAsync(t.TeamId, new(Guid.NewGuid(), 'B', "1"))).Status);
        Assert.Equal("NotRegistered", (await f.Engine.SubmitAnswerAsync(Guid.NewGuid(), new(qid, 'B', "1"))).Status);

        var first = await f.Engine.SubmitAnswerAsync(t.TeamId, new(qid, 'A', "2"));
        Assert.Equal("Accepted", first.Status);
        var again = await f.Engine.SubmitAnswerAsync(t.TeamId, new(qid, 'D', "3"));
        Assert.Equal("AlreadyAnswered", again.Status);
        Assert.Equal('A', again.StoredLabel);   // la lettera diversa NON sovrascrive

        using var scope = f.Sp.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<QuizDbContext>().Answers.CountAsync());
    }

    [Fact]
    public async Task Reveal_awards_points_and_leaderboard_ranks()
    {
        var f = await Fixture.CreateAsync();
        var win = await f.Register("Vincitori", 1);
        var lose = await f.Register("Perdenti", 2);
        await f.OpenFirstQuestion();
        var qid = f.QuestionIds[0];
        await f.Engine.SubmitAnswerAsync(win.TeamId, new(qid, 'B', "a"));
        await f.Engine.SubmitAnswerAsync(lose.TeamId, new(qid, 'C', "b"));

        await f.Engine.TransitionAsync(AdminAction.CloseAnswers);
        Assert.Equal("Closed", (await f.Engine.SubmitAnswerAsync(lose.TeamId, new(qid, 'B', "c"))).Status);
        var admin = await f.Engine.TransitionAsync(AdminAction.RevealAnswer);

        Assert.Equal(GamePhase.AnswerRevealed, admin.Game.Phase);
        Assert.Equal('B', admin.Game.Reveal!.CorrectLabel);
        var lb = admin.Game.Leaderboard!;
        Assert.Equal("Vincitori", lb[0].Nickname);
        Assert.Equal(100, lb[0].Score);
        Assert.Equal(1, lb[0].Rank);
        Assert.Equal(2, lb[1].Rank);
        Assert.Equal(100, lb[0].LastQuestionPoints);

        // reveal ripetibile: idempotente
        await f.Engine.TransitionAsync(AdminAction.ShowLeaderboard);
        var player = await f.Engine.GetPlayerStateAsync(win.TeamId);
        Assert.Equal(100, player.Score);
    }

    [Fact]
    public async Task Adjustments_change_score_and_are_derived()
    {
        var f = await Fixture.CreateAsync();
        var t = await f.Register("Gufi", 1);
        await f.Engine.AdjustScoreAsync(t.TeamId, 50, "bonus", "admin");
        await f.Engine.AdjustScoreAsync(t.TeamId, -20, "penalità", "admin");
        Assert.Equal(30, (await f.Engine.GetPlayerStateAsync(t.TeamId)).Score);
        Assert.Equal("VALIDATION", await ErrorOf(() => f.Engine.AdjustScoreAsync(t.TeamId, 0, "x", "admin")));
        Assert.Equal("VALIDATION", await ErrorOf(() => f.Engine.AdjustScoreAsync(t.TeamId, 10, " ", "admin")));
    }

    [Fact]
    public async Task Unlock_then_rejoin_with_nickname_only()
    {
        var f = await Fixture.CreateAsync();
        var t = await f.Register("Gufi", 1);

        Assert.Equal("RECOVERY_CLOSED", await ErrorOf(() => f.Engine.RejoinAsync("Gufi", null)));
        Assert.Equal("NOT_AUTHORIZED", await ErrorOf(() => f.Engine.RejoinAsync("Gufi", Guid.NewGuid())));
        Assert.Equal("NOT_AUTHORIZED", await ErrorOf(() => f.Engine.RejoinAsync("Inesistente", null)));

        var ok = await f.Engine.RejoinAsync("gufi", t.SessionToken);          // caso 1: token valido
        Assert.Equal(t.TeamId, ok.TeamId);

        await f.Engine.UnlockTeamAsync(t.TeamId, 10);
        Assert.Contains(f.Bc.Invalidated, x => x.Team == t.TeamId && x.Reason == "RESET");

        var recovered = await f.Engine.RejoinAsync("Gufi", null);              // caso 2: finestra aperta
        Assert.NotEqual(t.SessionToken, recovered.SessionToken);
        Assert.Contains(f.Bc.Invalidated, x => x.Team == t.TeamId && x.Reason == "REPLACED");
        Assert.Equal("RECOVERY_CLOSED", await ErrorOf(() => f.Engine.RejoinAsync("Gufi", null)));   // finestra chiusa
    }

    [Fact]
    public async Task Transfer_moves_answers_and_points_and_resolves_conflicts()
    {
        var f = await Fixture.CreateAsync(2);
        var oldT = await f.Register("Vecchio", 1);
        var newT = await f.Register("Nuovo", 2);

        // domanda 1: il vecchio risponde giusto, il nuovo sbagliato (conflitto → vince il vecchio, più punti)
        await f.OpenFirstQuestion();
        await f.Engine.SubmitAnswerAsync(oldT.TeamId, new(f.QuestionIds[0], 'B', "1"));
        await f.Engine.SubmitAnswerAsync(newT.TeamId, new(f.QuestionIds[0], 'A', "2"));
        await f.Engine.TransitionAsync(AdminAction.RevealAnswer);
        await f.Engine.AdjustScoreAsync(oldT.TeamId, 25, "bonus", "admin");

        var admin = await f.Engine.TransferTeamAsync(oldT.TeamId, newT.TeamId, "admin");

        var target = admin.Teams.Single(t => t.Id == newT.TeamId);
        var source = admin.Teams.Single(t => t.Id == oldT.TeamId);
        Assert.Equal(125, target.Score);
        Assert.False(source.IsActive);
        Assert.Contains(f.Bc.Invalidated, x => x.Team == oldT.TeamId && x.Reason == "TRANSFERRED");
        Assert.Equal(1, admin.Game.Leaderboard!.Length);
    }

    [Fact]
    public async Task ResetTeamAnswer_only_while_answers_open_or_closed()
    {
        var f = await Fixture.CreateAsync();
        var t = await f.Register("Gufi", 1);
        Assert.Equal("INVALID_PHASE", await ErrorOf(() => f.Engine.ResetTeamAnswerAsync(t.TeamId)));
        await f.OpenFirstQuestion();
        await f.Engine.SubmitAnswerAsync(t.TeamId, new(f.QuestionIds[0], 'A', "1"));
        await f.Engine.ResetTeamAnswerAsync(t.TeamId);
        Assert.Equal("Accepted", (await f.Engine.SubmitAnswerAsync(t.TeamId, new(f.QuestionIds[0], 'C', "2"))).Status);
    }

    [Fact]
    public async Task Running_out_of_questions_finishes_the_quiz()
    {
        var f = await Fixture.CreateAsync(1);
        await f.OpenFirstQuestion();
        await f.Engine.TransitionAsync(AdminAction.RevealAnswer);
        var state = await f.Engine.TransitionAsync(AdminAction.NextQuestion);
        Assert.Equal(GamePhase.Finished, state.Game.Phase);
    }

    [Fact]
    public async Task Reset_quiz_clears_teams_and_returns_to_lobby()
    {
        var f = await Fixture.CreateAsync();
        var t = await f.Register("Gufi", 1);
        await f.OpenFirstQuestion();
        var state = await f.Engine.ResetQuizAsync();
        Assert.Equal(GamePhase.Lobby, state.Game.Phase);
        Assert.Empty(state.Teams);
        Assert.Contains(f.Bc.Invalidated, x => x.Team == t.TeamId && x.Reason == "RESET");
    }
}
