using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class AmatIndicatorTests
{
    [Fact]
    public void AmatIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AmatIndicator();

        Assert.Equal(10, indicator.FastPeriod);
        Assert.Equal(50, indicator.SlowPeriod);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("AMAT - Archer Moving Averages Trends", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.False(indicator.OnBackGround);
    }

    [Fact]
    public void AmatIndicator_MinHistoryDepths_IsSlowPeriod()
    {
        var indicator = new AmatIndicator { FastPeriod = 10, SlowPeriod = 50 };
        Assert.Equal(50, indicator.MinHistoryDepths);

        indicator = new AmatIndicator { FastPeriod = 5, SlowPeriod = 100 };
        Assert.Equal(100, indicator.MinHistoryDepths);
    }

    [Fact]
    public void AmatIndicator_ShortName_IncludesParameters()
    {
        var indicator = new AmatIndicator { FastPeriod = 10, SlowPeriod = 50 };
        Assert.Equal("AMAT(10,50)", indicator.ShortName);

        indicator = new AmatIndicator { FastPeriod = 5, SlowPeriod = 20 };
        Assert.Equal("AMAT(5,20)", indicator.ShortName);
    }

    [Fact]
    public void AmatIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new AmatIndicator { FastPeriod = 10, SlowPeriod = 50 };
        indicator.Initialize();

        // Should have 5 line series: Trend, Strength, Fast EMA, Slow EMA, Zero
        Assert.Equal(5, indicator.LinesSeries.Count);
        Assert.Equal("Trend", indicator.LinesSeries[0].Name);
        Assert.Equal("Strength", indicator.LinesSeries[1].Name);
        Assert.Equal("Fast EMA", indicator.LinesSeries[2].Name);
        Assert.Equal("Slow EMA", indicator.LinesSeries[3].Name);
        Assert.Equal("Zero", indicator.LinesSeries[4].Name);
    }

    [Fact]
    public void AmatIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AmatIndicator { FastPeriod = 3, SlowPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // After one bar, all 5 series should have values
        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.Equal(1, indicator.LinesSeries[1].Count);
        Assert.Equal(1, indicator.LinesSeries[2].Count);
        Assert.Equal(1, indicator.LinesSeries[3].Count);
        Assert.Equal(1, indicator.LinesSeries[4].Count);
    }

    [Fact]
    public void AmatIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new AmatIndicator { FastPeriod = 3, SlowPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void AmatIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new AmatIndicator { FastPeriod = 3, SlowPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // NewTick should update without crashing
        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void AmatIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new AmatIndicator { FastPeriod = 3, SlowPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars in uptrend
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i),
                100 + i * 2,
                105 + i * 2,
                95 + i * 2,
                102 + i * 2);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.Equal(20, indicator.LinesSeries[0].Count);

        // Check that values are finite
        for (int i = 0; i < 20; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
            Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(i)));
            Assert.True(double.IsFinite(indicator.LinesSeries[2].GetValue(i)));
            Assert.True(double.IsFinite(indicator.LinesSeries[3].GetValue(i)));
            Assert.Equal(0, indicator.LinesSeries[4].GetValue(i)); // Zero line
        }
    }

    [Fact]
    public void AmatIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[]
        {
            SourceType.Open,
            SourceType.High,
            SourceType.Low,
            SourceType.Close,
            SourceType.HL2,
            SourceType.HLC3,
        };

        foreach (var source in sources)
        {
            var indicator = new AmatIndicator
            {
                FastPeriod = 3,
                SlowPeriod = 10,
                Source = source
            };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            // All source types should produce values without crashing
            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void AmatIndicator_FastPeriod_CanBeChanged()
    {
        var indicator = new AmatIndicator();
        indicator.FastPeriod = 5;

        Assert.Equal(5, indicator.FastPeriod);
        Assert.Equal("AMAT(5,50)", indicator.ShortName);
    }

    [Fact]
    public void AmatIndicator_SlowPeriod_CanBeChanged()
    {
        var indicator = new AmatIndicator();
        indicator.SlowPeriod = 100;

        Assert.Equal(100, indicator.SlowPeriod);
        Assert.Equal(100, indicator.MinHistoryDepths);
        Assert.Equal("AMAT(10,100)", indicator.ShortName);
    }

    [Fact]
    public void AmatIndicator_ShowColdValues_False_SetsNaN()
    {
        var indicator = new AmatIndicator
        {
            FastPeriod = 3,
            SlowPeriod = 100,
            ShowColdValues = false
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add a few bars (less than warmup)
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 102);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // With ShowColdValues = false, cold values should be NaN
        // (before warmup is complete)
        Assert.True(double.IsNaN(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void AmatIndicator_Uptrend_ProducesBullishSignal()
    {
        var indicator = new AmatIndicator { FastPeriod = 3, SlowPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Create a strong uptrend
        for (int i = 0; i < 30; i++)
        {
            double price = 100 + i * 5;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // After warmup in uptrend, should show bullish (+1)
        double lastTrend = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(1.0, lastTrend);
    }

    [Fact]
    public void AmatIndicator_Downtrend_ProducesBearishSignal()
    {
        var indicator = new AmatIndicator { FastPeriod = 3, SlowPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Create a strong downtrend
        for (int i = 0; i < 30; i++)
        {
            double price = 200 - i * 5;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // After warmup in downtrend, should show bearish (-1)
        double lastTrend = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(-1.0, lastTrend);
    }
}
