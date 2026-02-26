using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class SakIndicatorTests
{
    [Fact]
    public void SakIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SakIndicator();

        Assert.Equal("BP", indicator.FilterType);
        Assert.Equal(20, indicator.Period);
        Assert.Equal(10, indicator.N);
        Assert.Equal(0.1, indicator.Delta);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("SAK - Swiss Army Knife Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void SakIndicator_MinHistoryDepths_IsZero()
    {
        var indicator = new SakIndicator { Period = 20 };

        Assert.Equal(0, SakIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void SakIndicator_ShortName_IncludesFilterTypeAndPeriod()
    {
        var indicator = new SakIndicator { FilterType = "EMA", Period = 15 };

        Assert.Contains("SAK", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("EMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void SakIndicator_Initialize_CreatesInternalSak()
    {
        var indicator = new SakIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void SakIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SakIndicator { FilterType = "EMA", Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void SakIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new SakIndicator { FilterType = "EMA", Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void SakIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new SakIndicator { FilterType = "EMA", Period = 3 };
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
    public void SakIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new SakIndicator { FilterType = "EMA", Period = 3 };
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

        double lastSak = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastSak >= 99 && lastSak <= 111);
    }

    [Fact]
    public void SakIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new SakIndicator { FilterType = "EMA", Period = 3, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void SakIndicator_AllFilterTypes_InitializeAndCompute()
    {
        string[] filterTypes = { "EMA", "HP", "Smooth", "Gauss", "Butter", "2PHP", "BP", "BS", "SMA" };

        foreach (var filterType in filterTypes)
        {
            var indicator = new SakIndicator { FilterType = filterType, Period = 5, N = 3 };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            double[] closes = { 100, 101, 102, 103, 104, 105 };
            foreach (var close in closes)
            {
                indicator.HistoricalData.AddBar(now, close, close + 1, close - 1, close);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
                now = now.AddMinutes(1);
            }

            double lastVal = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(lastVal),
                $"FilterType {filterType} should produce finite value, got {lastVal}");
        }
    }

    [Fact]
    public void SakIndicator_Period_CanBeChanged()
    {
        var indicator = new SakIndicator { Period = 5 };
        Assert.Equal(5, indicator.Period);

        indicator.Period = 25;
        Assert.Equal(25, indicator.Period);
        Assert.Equal(0, SakIndicator.MinHistoryDepths);
    }
}
