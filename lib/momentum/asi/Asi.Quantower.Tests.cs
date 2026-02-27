using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class AsiIndicatorTests
{
    [Fact]
    public void AsiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AsiIndicator();

        Assert.Equal(3.0, indicator.LimitMove);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ASI - Accumulation Swing Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AsiIndicator_MinHistoryDepths_EqualsTwo()
    {
        var indicator = new AsiIndicator();

        Assert.Equal(2, AsiIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(2, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void AsiIndicator_ShortName_IncludesLimitMove()
    {
        var indicator = new AsiIndicator { LimitMove = 5.0 };

        Assert.Contains("ASI", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("5", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void AsiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new AsiIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Asi.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void AsiIndicator_Initialize_CreatesInternalAsi()
    {
        var indicator = new AsiIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, one line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void AsiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AsiIndicator { LimitMove = 3.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void AsiIndicator_ProcessUpdate_TwoBars_IsHotProducesValue()
    {
        var indicator = new AsiIndicator { LimitMove = 3.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void AsiIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new AsiIndicator { LimitMove = 3.0 };
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
    public void AsiIndicator_MultipleUpdates_AccumulatesCorrectly()
    {
        var indicator = new AsiIndicator { LimitMove = 3.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] opens = { 100, 102, 104, 103, 105 };
        double[] closes = { 102, 104, 103, 105, 107 };

        for (int i = 0; i < opens.Length; i++)
        {
            double o = opens[i];
            double c = closes[i];
            indicator.HistoricalData.AddBar(now.AddMinutes(i), o, c + 1, o - 1, c);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // All values should be finite
        for (int i = 0; i < opens.Length; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(opens.Length - 1 - i)),
                $"Bar {i} should produce finite value");
        }
    }

    [Fact]
    public void AsiIndicator_UpTrendData_ProducesPositiveValue()
    {
        var indicator = new AsiIndicator { LimitMove = 3.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            double p = 100.0 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), p, p + 1, p - 1, p + 0.5);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // After warmup (bar 2+), ASI should be positive for uptrend
        double lastValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastValue > 0, $"Uptrend should produce positive ASI, got {lastValue}");
    }

    [Fact]
    public void AsiIndicator_LimitMove_CanBeChanged()
    {
        var indicator = new AsiIndicator { LimitMove = 5.0 };
        Assert.Equal(5.0, indicator.LimitMove);

        indicator.LimitMove = 10.0;
        Assert.Equal(10.0, indicator.LimitMove);
        Assert.Equal(2, AsiIndicator.MinHistoryDepths);
    }
}
