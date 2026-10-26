using QuizLive.Api.Services;

namespace QuizLive.Tests;

public class ScoreCalculatorTests
{
    [Fact]
    public void Grade_correct_and_wrong()
    {
        Assert.Equal((true, 100), ScoreCalculator.Grade('B', 'B', 100));
        Assert.Equal((false, 0), ScoreCalculator.Grade('A', 'B', 100));
    }

    [Fact]
    public void Rank_uses_competition_style_ties()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid(); var d = Guid.NewGuid();
        var ranks = ScoreCalculator.Rank(new[] { (a, "Alfa", 300), (b, "Beta", 200), (c, "Gamma", 200), (d, "Delta", 100) });
        Assert.Equal(1, ranks[a]);
        Assert.Equal(2, ranks[b]);
        Assert.Equal(2, ranks[c]);
        Assert.Equal(4, ranks[d]);
    }

    [Fact]
    public void Rank_orders_by_score_then_nickname()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var ranks = ScoreCalculator.Rank(new[] { (a, "zeta", 50), (b, "Alfa", 100) });
        Assert.Equal(1, ranks[b]);
        Assert.Equal(2, ranks[a]);
    }
}
