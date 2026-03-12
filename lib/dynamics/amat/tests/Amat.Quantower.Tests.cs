using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public sealed class AmatIndicatorTests
{
    [Fact]
    public void AmatIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AmatIndicator();

        Assert.Equal(10, indicator.FastPeriod);
        Assert.Equal(50, indicator.SlowPeriod);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("AMAT - Archer Moving Averages Trends", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AmatIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new AmatIndicator { FastPeriod = 10, SlowPeriod = 50 };

        Assert.Equal(0, AmatIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void AmatIndicator_ShortName_IncludesParameters()
    {
        var indicator = new AmatIndicator { FastPeriod = 8, SlowPeriod = 40 };

        Assert.Contains("AMAT", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("8", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("40", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void AmatIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new AmatIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Amat", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void AmatIndicator_Initialize_CreatesInternalAmat()
    {
        var indicator = new AmatIndicator { FastPeriod = 10, SlowPeriod = 50 };

        indicator.Initialize();

        // Trend + Strength = 2 line series
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void AmatIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AmatIndicator { FastPeriod = 5, SlowPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double trend = indicator.LinesSeries[0].GetValue(0);
        double strength = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(trend));
        Assert.True(double.IsFinite(strength));
    }

    [Fact]
    public void AmatIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new AmatIndicator { FastPeriod = 5, SlowPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 15; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Simulate a new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(15), 115, 125, 105, 120);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double trend = indicator.LinesSeries[0].GetValue(0);
        double strength = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(trend));
        Assert.True(double.IsFinite(strength));
    }

    [Fact]
    public void AmatIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new AmatIndicator { FastPeriod = 3, SlowPeriod = 8, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite trend value");
            Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(0)),
                $"Source {source} should produce finite strength value");
        }
    }

    [Fact]
    public void AmatIndicator_Periods_CanBeChanged()
    {
        var indicator = new AmatIndicator { FastPeriod = 5, SlowPeriod = 20 };
        Assert.Equal(5, indicator.FastPeriod);
        Assert.Equal(20, indicator.SlowPeriod);

        indicator.FastPeriod = 15;
        indicator.SlowPeriod = 60;
        Assert.Equal(15, indicator.FastPeriod);
        Assert.Equal(60, indicator.SlowPeriod);
        Assert.Equal(0, AmatIndicator.MinHistoryDepths);
    }
}
