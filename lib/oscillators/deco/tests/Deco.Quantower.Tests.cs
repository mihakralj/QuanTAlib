using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public sealed class DecoIndicatorTests
{
    [Fact]
    public void DecoIndicator_Constructor_SetsDefaults()
    {
        var indicator = new DecoIndicator();

        Assert.Equal(30, indicator.ShortPeriod);
        Assert.Equal(60, indicator.LongPeriod);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("DECO - Ehlers Decycler Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void DecoIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new DecoIndicator { ShortPeriod = 10, LongPeriod = 20 };

        Assert.Equal(0, DecoIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void DecoIndicator_ShortName_IncludesParameters()
    {
        var indicator = new DecoIndicator { ShortPeriod = 10, LongPeriod = 30 };
        indicator.Initialize();

        Assert.Contains("DECO", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("30", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void DecoIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new DecoIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Deco.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void DecoIndicator_Initialize_CreatesInternalDeco()
    {
        var indicator = new DecoIndicator { ShortPeriod = 5, LongPeriod = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void DecoIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new DecoIndicator { ShortPeriod = 5, LongPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
    }

    [Fact]
    public void DecoIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new DecoIndicator { ShortPeriod = 5, LongPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Add a new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 130, 110, 125);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
    }

    [Fact]
    public void DecoIndicator_ProcessUpdate_DifferentSources()
    {
        foreach (SourceType source in new[] { SourceType.Close, SourceType.Open, SourceType.High, SourceType.Low })
        {
            var indicator = new DecoIndicator { ShortPeriod = 5, LongPeriod = 10, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 15; i++)
            {
                indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
                var args = new UpdateArgs(UpdateReason.HistoricalBar);
                indicator.ProcessUpdate(args);
            }

            double value = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(value), $"Source {source} produced non-finite value");
        }
    }

    [Fact]
    public void DecoIndicator_Reinitialize_ResetsState()
    {
        var indicator = new DecoIndicator { ShortPeriod = 5, LongPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 15; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Re-initialize should reset
        indicator.Initialize();
        Assert.Single(indicator.LinesSeries);
    }
}
