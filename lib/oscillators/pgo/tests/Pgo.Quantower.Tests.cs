using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class PgoIndicatorTests
{
    [Fact]
    public void PgoIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PgoIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("PGO - Pretty Good Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void PgoIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new PgoIndicator { Period = 14 };

        Assert.Equal(0, PgoIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void PgoIndicator_ShortName_IncludesParameters()
    {
        var indicator = new PgoIndicator { Period = 20 };
        indicator.Initialize();

        Assert.Contains("PGO", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void PgoIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new PgoIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Pgo.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void PgoIndicator_Initialize_CreatesInternalPgo()
    {
        var indicator = new PgoIndicator { Period = 10 };

        indicator.Initialize();

        Assert.Equal(4, indicator.LinesSeries.Count);
    }

    [Fact]
    public void PgoIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PgoIndicator { Period = 5 };
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
    public void PgoIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new PgoIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 130, 110, 125);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void PgoIndicator_Parameters_CanBeChanged()
    {
        var indicator = new PgoIndicator { Period = 14 };

        indicator.Period = 20;

        Assert.Equal(20, indicator.Period);
        Assert.Equal(0, PgoIndicator.MinHistoryDepths);
    }

    [Fact]
    public void PgoIndicator_ReferenceLines_SetCorrectly()
    {
        var indicator = new PgoIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Zero line should be 0
        Assert.Equal(0.0, indicator.LinesSeries[1].GetValue(0));
        // Overbought line should be 3
        Assert.Equal(3.0, indicator.LinesSeries[2].GetValue(0));
        // Oversold line should be -3
        Assert.Equal(-3.0, indicator.LinesSeries[3].GetValue(0));
    }
}
