using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public sealed class KendallIndicatorTests
{
    [Fact]
    public void KendallIndicator_Constructor_SetsDefaults()
    {
        var indicator = new KendallIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.Equal(SourceType.Open, indicator.Source2);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("KENDALL - Kendall Tau-a Rank Correlation", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void KendallIndicator_MinHistoryDepths_EqualsTwo()
    {
        var indicator = new KendallIndicator();

        Assert.Equal(2, KendallIndicator.MinHistoryDepths);
        Assert.Equal(2, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void KendallIndicator_ShortName_IncludesPeriodAndSources()
    {
        var indicator = new KendallIndicator { Period = 20 };

        Assert.Contains("KENDALL", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void KendallIndicator_Initialize_CreatesInternalKendall()
    {
        var indicator = new KendallIndicator { Period = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void KendallIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new KendallIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void KendallIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new KendallIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void KendallIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new KendallIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsNaN(firstValue) || double.IsFinite(firstValue));
        Assert.True(double.IsNaN(secondValue) || double.IsFinite(secondValue));
    }

    [Fact]
    public void KendallIndicator_MultipleUpdates_ProducesSequence()
    {
        var indicator = new KendallIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] opens = [100, 101, 102, 103, 104, 105];
        double[] closes = [100, 101, 102, 103, 104, 105];

        for (int i = 0; i < opens.Length; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), opens[i], opens[i] + 5, opens[i] - 5, closes[i]);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(opens.Length, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void KendallIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new KendallIndicator { Period = 5, Source = source, Source2 = SourceType.Close };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

            // Should not throw and should produce output
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }
}
