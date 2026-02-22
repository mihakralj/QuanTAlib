using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class GdemaIndicatorTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var ind = new GdemaIndicator();
        Assert.Equal(10, ind.Period);
        Assert.Equal(1.0, ind.VFactor);
        Assert.Equal(SourceType.Close, ind.Source);
        Assert.True(ind.ShowColdValues);
    }

    [Fact]
    public void Initialize_CreatesLineSeries()
    {
        var ind = new GdemaIndicator();
        ind.Initialize();
        Assert.Single(ind.LinesSeries);
    }

    [Fact]
    public void MinHistoryDepths_EqualsZero()
    {
        Assert.Equal(0, GdemaIndicator.MinHistoryDepths);
    }

    [Fact]
    public void SourceCodeLink_IsValid()
    {
        var ind = new GdemaIndicator();
        Assert.Contains("Gdema.Quantower.cs", ind.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void ShortName_IncludesPeriodAndSource()
    {
        var ind = new GdemaIndicator();
        ind.Initialize();
        Assert.Contains("GDEMA", ind.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", ind.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void Period_CanBeChanged()
    {
        var ind = new GdemaIndicator { Period = 20, VFactor = 1.5 };
        ind.Initialize();
        Assert.Equal(20, ind.Period);
        Assert.Equal(1.5, ind.VFactor);
    }

    [Fact]
    public void ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var ind = new GdemaIndicator { Period = 3 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        ind.ProcessUpdate(args);

        Assert.Equal(1, ind.LinesSeries[0].Count);
        Assert.True(double.IsFinite(ind.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void ProcessUpdate_NewBar_ComputesValue()
    {
        var ind = new GdemaIndicator { Period = 3 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 105, 95, 102);
        ind.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, ind.LinesSeries[0].Count);
    }

    [Fact]
    public void ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var ind = new GdemaIndicator { Period = 3 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 105, 95, 102);
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        ind.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 98, 106);
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        double value = ind.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
    }

    [Fact]
    public void DifferentSourceTypes_Work()
    {
        foreach (var sourceType in new[] { SourceType.Close, SourceType.Open, SourceType.High, SourceType.Low })
        {
            var ind = new GdemaIndicator { Source = sourceType, Period = 3 };
            ind.Initialize();

            var now = DateTime.UtcNow;
            ind.HistoricalData.AddBar(now, 100, 110, 90, 105);
            ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            double value = ind.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(value), $"Failed for source type {sourceType}");
        }
    }
}
