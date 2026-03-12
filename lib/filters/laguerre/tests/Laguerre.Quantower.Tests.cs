using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class LaguerreIndicatorTests
{
    [Fact]
    public void LaguerreIndicator_Constructor_SetsDefaults()
    {
        var indicator = new LaguerreIndicator();

        Assert.Equal(0.8, indicator.Gamma);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("LAGUERRE - Ehlers Laguerre Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void LaguerreIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new LaguerreIndicator { Gamma = 0.5 };

        Assert.Equal(0, LaguerreIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void LaguerreIndicator_ShortName_IncludesGammaAndSource()
    {
        var indicator = new LaguerreIndicator { Gamma = 0.7 };

        Assert.Contains("LAGUERRE", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("0.70", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void LaguerreIndicator_Initialize_CreatesInternalLaguerre()
    {
        var indicator = new LaguerreIndicator { Gamma = 0.8 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void LaguerreIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new LaguerreIndicator { Gamma = 0.8 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void LaguerreIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new LaguerreIndicator { Gamma = 0.8 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void LaguerreIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new LaguerreIndicator { Gamma = 0.8 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    [Fact]
    public void LaguerreIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new LaguerreIndicator { Gamma = 0.8 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105, 107, 106 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        for (int i = 0; i < closes.Length; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(closes.Length - 1 - i)));
        }

        double lastLag = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastLag >= 95 && lastLag <= 115);
    }

    [Fact]
    public void LaguerreIndicator_DifferentSources_Work()
    {
        var sourceTypes = new[] { SourceType.Close, SourceType.Open, SourceType.HL2, SourceType.HLC3 };

        foreach (var sourceType in sourceTypes)
        {
            var indicator = new LaguerreIndicator { Gamma = 0.8, Source = sourceType };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {sourceType} should produce finite value");
        }
    }

    [Fact]
    public void LaguerreIndicator_GammaParameter_Accessible()
    {
        var indicator = new LaguerreIndicator { Gamma = 0.5 };
        Assert.Equal(0.5, indicator.Gamma);

        indicator.Gamma = 0.9;
        Assert.Equal(0.9, indicator.Gamma);
    }
}
