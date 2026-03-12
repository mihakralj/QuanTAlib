using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class TukeyWIndicatorTests
{
    [Fact]
    public void TukeyWIndicator_Constructor_SetsDefaults()
    {
        var indicator = new TukeyWIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(0.5, indicator.Alpha);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("TUKEY_W - Tukey (Tapered Cosine) Window Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void TukeyWIndicator_MinHistoryDepths_IsZero()
    {
        var indicator = new TukeyWIndicator { Period = 20 };

        Assert.Equal(0, TukeyWIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void TukeyWIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new TukeyWIndicator { Period = 10, Alpha = 0.75 };

        Assert.Contains("TUKEY_W", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("0.75", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void TukeyWIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new TukeyWIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Tukey_w.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void TukeyWIndicator_Initialize_CreatesInternalTukeyW()
    {
        var indicator = new TukeyWIndicator { Period = 20 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void TukeyWIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new TukeyWIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void TukeyWIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new TukeyWIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void TukeyWIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new TukeyWIndicator { Period = 5 };
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
    public void TukeyWIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new TukeyWIndicator { Period = 5 };
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
    public void TukeyWIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new TukeyWIndicator { Period = 5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void TukeyWIndicator_Period_CanBeChanged()
    {
        var indicator = new TukeyWIndicator { Period = 20 };
        Assert.Equal(20, indicator.Period);

        indicator.Period = 30;
        Assert.Equal(30, indicator.Period);
        Assert.Equal(0, TukeyWIndicator.MinHistoryDepths);
    }

    [Fact]
    public void TukeyWIndicator_Alpha_CanBeChanged()
    {
        var indicator = new TukeyWIndicator { Alpha = 0.5 };
        Assert.Equal(0.5, indicator.Alpha);

        indicator.Alpha = 0.75;
        Assert.Equal(0.75, indicator.Alpha);
    }
}
