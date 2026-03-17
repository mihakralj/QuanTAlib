using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public sealed class ConvexityIndicatorTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var ind = new ConvexityIndicator();
        Assert.Equal("CONVEXITY - Beta Convexity", ind.Name);
        Assert.True(ind.SeparateWindow);
        Assert.Equal(20, ind.Period);
        Assert.True(ind.ShowColdValues);
    }

    [Fact]
    public void MinHistoryDepths_EqualsZero()
    {
        Assert.Equal(0, ConvexityIndicator.MinHistoryDepths);
        IWatchlistIndicator w = new ConvexityIndicator();
        Assert.Equal(0, w.MinHistoryDepths);
    }

    [Fact]
    public void ShortName_IncludesParameters()
    {
        var ind = new ConvexityIndicator { Period = 30 };
        ind.Initialize();
        Assert.Contains("CONVEXITY", ind.ShortName, StringComparison.Ordinal);
        Assert.Contains("30", ind.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceCodeLink_IsValid()
    {
        var ind = new ConvexityIndicator();
        Assert.Contains("github.com", ind.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Convexity.Quantower.cs", ind.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void Initialize_CreatesLineSeries()
    {
        var ind = new ConvexityIndicator();
        ind.Initialize();
        Assert.Equal(5, ind.LinesSeries.Count);
    }

    [Fact]
    public void ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var ind = new ConvexityIndicator { Period = 5 };
        ind.Initialize();

        for (int i = 0; i < 10; i++)
        {
            ind.HistoricalData.AddBar(
                DateTime.UtcNow.AddMinutes(i),
                100 + i * 0.5, 101 + i * 0.5, 99 + i * 0.5, 100.5 + i * 0.5, 1000);
        }

        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double val = ind.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void ProcessUpdate_NewBar_ComputesValue()
    {
        var ind = new ConvexityIndicator { Period = 3 };
        ind.Initialize();

        ind.HistoricalData.AddBar(DateTime.UtcNow, 100, 101, 99, 100.5, 1000);
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        ind.HistoricalData.AddBar(DateTime.UtcNow.AddMinutes(1), 101, 102, 100, 101.5, 1100);
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.True(double.IsFinite(ind.LinesSeries[0].GetValue(0)));
    }
}
