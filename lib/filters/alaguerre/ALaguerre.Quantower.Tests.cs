using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class ALaguerreIndicatorTests
{
    [Fact]
    public void ALaguerreIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ALaguerreIndicator();

        Assert.Equal(20, indicator.Length);
        Assert.Equal(5, indicator.MedianLength);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ALAGUERRE - Adaptive Laguerre Filter (Ehlers)", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void ALaguerreIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new ALaguerreIndicator { Length = 10 };

        Assert.Equal(0, ALaguerreIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void ALaguerreIndicator_ShortName_IncludesLengthAndMedianLength()
    {
        var indicator = new ALaguerreIndicator { Length = 10, MedianLength = 3 };

        Assert.Contains("ALAGUERRE", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("3", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void ALaguerreIndicator_Initialize_CreatesInternalALaguerre()
    {
        var indicator = new ALaguerreIndicator { Length = 20, MedianLength = 5 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ALaguerreIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ALaguerreIndicator { Length = 20, MedianLength = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void ALaguerreIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new ALaguerreIndicator { Length = 20, MedianLength = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void ALaguerreIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new ALaguerreIndicator { Length = 20, MedianLength = 5 };
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
    public void ALaguerreIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new ALaguerreIndicator { Length = 10, MedianLength = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = [100, 102, 104, 103, 105, 107, 106];

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

        double lastVal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastVal >= 95 && lastVal <= 115);
    }

    [Fact]
    public void ALaguerreIndicator_DifferentSources_Work()
    {
        var sourceTypes = new[] { SourceType.Close, SourceType.Open, SourceType.HL2, SourceType.HLC3 };

        foreach (var sourceType in sourceTypes)
        {
            var indicator = new ALaguerreIndicator { Length = 20, MedianLength = 5, Source = sourceType };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {sourceType} should produce finite value");
        }
    }

    [Fact]
    public void ALaguerreIndicator_Parameters_Accessible()
    {
        var indicator = new ALaguerreIndicator { Length = 10, MedianLength = 7 };
        Assert.Equal(10, indicator.Length);
        Assert.Equal(7, indicator.MedianLength);

        indicator.Length = 30;
        indicator.MedianLength = 9;
        Assert.Equal(30, indicator.Length);
        Assert.Equal(9, indicator.MedianLength);
    }
}
