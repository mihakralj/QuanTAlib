using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class KaiserIndicatorTests
{
    [Fact]
    public void KaiserIndicator_Constructor_SetsDefaults()
    {
        var indicator = new KaiserIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(3.0, indicator.Beta);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("KAISER - Kaiser Window Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void KaiserIndicator_MinHistoryDepths_IsZero()
    {
        var indicator = new KaiserIndicator { Period = 14 };

        Assert.Equal(0, KaiserIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void KaiserIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new KaiserIndicator { Period = 10, Beta = 5.0 };

        Assert.Contains("KAISER", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("5.0", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void KaiserIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new KaiserIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Kaiser.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void KaiserIndicator_Initialize_CreatesInternalKaiser()
    {
        var indicator = new KaiserIndicator { Period = 14 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void KaiserIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new KaiserIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void KaiserIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new KaiserIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void KaiserIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new KaiserIndicator { Period = 5 };
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
    public void KaiserIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new KaiserIndicator { Period = 5 };
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
    }

    [Fact]
    public void KaiserIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new KaiserIndicator { Period = 5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void KaiserIndicator_Period_CanBeChanged()
    {
        var indicator = new KaiserIndicator { Period = 14 };
        Assert.Equal(14, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
        Assert.Equal(0, KaiserIndicator.MinHistoryDepths);
    }

    [Fact]
    public void KaiserIndicator_Beta_CanBeChanged()
    {
        var indicator = new KaiserIndicator { Beta = 3.0 };
        Assert.Equal(3.0, indicator.Beta);

        indicator.Beta = 8.6;
        Assert.Equal(8.6, indicator.Beta);
    }
}
