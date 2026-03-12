using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public sealed class SgmaIndicatorTests
{
    [Fact]
    public void SgmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SgmaIndicator();

        Assert.Equal(9, indicator.Period);
        Assert.Equal(2, indicator.Degree);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("SGMA - Savitzky-Golay Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void SgmaIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new SgmaIndicator { Period = 9, Degree = 2 };

        Assert.Equal(0, SgmaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void SgmaIndicator_ShortName_IncludesPeriodAndDegree()
    {
        var indicator = new SgmaIndicator { Period = 15, Degree = 3 };

        Assert.Contains("SGMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("3", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void SgmaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new SgmaIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Sgma", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void SgmaIndicator_Initialize_CreatesInternalSgma()
    {
        var indicator = new SgmaIndicator { Period = 9, Degree = 2 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void SgmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SgmaIndicator { Period = 3, Degree = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void SgmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new SgmaIndicator { Period = 3, Degree = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void SgmaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new SgmaIndicator { Period = 3, Degree = 2 };
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
    public void SgmaIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new SgmaIndicator { Period = 3, Degree = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105 };

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
    public void SgmaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new SgmaIndicator { Period = 3, Degree = 2, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void SgmaIndicator_Period_CanBeChanged()
    {
        var indicator = new SgmaIndicator { Period = 5 };
        Assert.Equal(5, indicator.Period);

        indicator.Period = 21;
        Assert.Equal(21, indicator.Period);
        Assert.Equal(0, SgmaIndicator.MinHistoryDepths);
    }
}
