using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class FramaIndicatorTests
{
    [Fact]
    public void FramaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new FramaIndicator();

        Assert.Equal(16, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("FRAMA - Ehlers Fractal Adaptive Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void FramaIndicator_MinHistoryDepths_ReturnsZero()
    {
        var indicator = new FramaIndicator { Period = 20 };

        Assert.Equal(0, FramaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void FramaIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new FramaIndicator { Period = 21 };

        Assert.Contains("FRAMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("21", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void FramaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new FramaIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Frama.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void FramaIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new FramaIndicator { Period = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void FramaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new FramaIndicator { Period = 4 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        int warmup = indicator.Period % 2 == 0 ? indicator.Period : indicator.Period + 1;
        for (int i = 0; i < warmup; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 102);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.Equal(warmup, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void FramaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new FramaIndicator { Period = 4 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void FramaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new FramaIndicator { Period = 4 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        int warmup = indicator.Period % 2 == 0 ? indicator.Period : indicator.Period + 1;
        for (int i = 0; i < warmup; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 102);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double firstValue = indicator.LinesSeries[0].GetValue(0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }
}
