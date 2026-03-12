using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class SwmaIndicatorTests
{
    [Fact]
    public void SwmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SwmaIndicator();

        Assert.Equal(4, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("SWMA - Symmetric Weighted Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void SwmaIndicator_MinHistoryDepths_IsZero()
    {
        var indicator = new SwmaIndicator { Period = 10 };

        Assert.Equal(0, SwmaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void SwmaIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new SwmaIndicator { Period = 6 };

        Assert.Contains("SWMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("6", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void SwmaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new SwmaIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Swma.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void SwmaIndicator_Initialize_CreatesInternalSwma()
    {
        var indicator = new SwmaIndicator { Period = 4 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void SwmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SwmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void SwmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new SwmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void SwmaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new SwmaIndicator { Period = 3 };
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
    public void SwmaIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new SwmaIndicator { Period = 3 };
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
    public void SwmaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new SwmaIndicator { Period = 3, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void SwmaIndicator_Period_CanBeChanged()
    {
        var indicator = new SwmaIndicator { Period = 4 };
        Assert.Equal(4, indicator.Period);

        indicator.Period = 10;
        Assert.Equal(10, indicator.Period);
        Assert.Equal(0, SwmaIndicator.MinHistoryDepths);
    }
}
