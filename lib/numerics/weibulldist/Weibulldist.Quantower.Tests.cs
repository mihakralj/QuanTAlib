using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class WeibulldistIndicatorTests
{
    [Fact]
    public void WeibulldistIndicator_Constructor_SetsDefaults()
    {
        var indicator = new WeibulldistIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.Equal(1.5, indicator.K);
        Assert.Equal(1.0, indicator.Lambda);
        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("WEIBULLDIST - Weibull Distribution CDF", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void WeibulldistIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new WeibulldistIndicator { Period = 30 };
        Assert.Equal(30, indicator.MinHistoryDepths);
    }

    [Fact]
    public void WeibulldistIndicator_ShortName_IsCorrect()
    {
        var indicator = new WeibulldistIndicator { K = 2.0, Lambda = 0.5, Period = 20 };
        Assert.Equal("WEIBULLDIST(2.00,0.50,20)", indicator.ShortName);
    }

    [Fact]
    public void WeibulldistIndicator_Initialize_CreatesTwoLineSeries()
    {
        var indicator = new WeibulldistIndicator();
        indicator.Initialize();

        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Equal("WeibullDist", indicator.LinesSeries[0].Name);
        Assert.Equal("Mid", indicator.LinesSeries[1].Name);
    }

    [Fact]
    public void WeibulldistIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new WeibulldistIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 105 + i, 95 - i, 100 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // After 5 bars (= period), should have valid output
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val), "Output must be finite after warmup");
        Assert.True(val >= 0.0 && val <= 1.0, $"Output {val} must be in [0,1]");
    }

    [Fact]
    public void WeibulldistIndicator_ProcessUpdate_NewBar_AddsNewValue()
    {
        var indicator = new WeibulldistIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Feed 3 historical bars
        for (int i = 0; i < 3; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 105, 95, 100 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Feed a new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(3), 0, 106, 96, 103);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(4, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void WeibulldistIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new WeibulldistIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 0, 105, 95, 100);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // 2 values: one historical, one intra-bar update
        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void WeibulldistIndicator_MidLine_IsAlwaysHalf()
    {
        var indicator = new WeibulldistIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 105, 95, 100 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Mid line should always be 0.5
        for (int i = 0; i < indicator.LinesSeries[1].Count; i++)
        {
            double mid = indicator.LinesSeries[1].GetValue(i);
            Assert.Equal(0.5, mid, 1e-10);
        }
    }

    [Fact]
    public void WeibulldistIndicator_DifferentSourceType_Works()
    {
        var indicator = new WeibulldistIndicator { Period = 3, Source = SourceType.High };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 3; i++)
        {
            // High = 110+i
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 110 + i, 90, 100);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void WeibulldistIndicator_OutputInRange_AfterManyBars()
    {
        var indicator = new WeibulldistIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 74001);
        var bars = gbm.Fetch(50, now.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            double price = bars.Close[i].Value;
            indicator.HistoricalData.AddBar(
                new DateTime(bars.Close[i].Time, DateTimeKind.Utc),
                0, price * 1.01, price * 0.99, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Check all computed values are in [0, 1]
        for (int i = 0; i < indicator.LinesSeries[0].Count; i++)
        {
            double val = indicator.LinesSeries[0].GetValue(i);
            Assert.True(val >= 0.0 && val <= 1.0, $"Value {val} at index {i} out of range");
        }
    }

    [Fact]
    public void WeibulldistIndicator_HighK_OutputFinite()
    {
        // With k=5.0, S-curve shape; CDF stays low until near scale
        var indicator = new WeibulldistIndicator { Period = 5, K = 5.0, Lambda = 1.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 101 + i, 99 + i, 100 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val >= 0.0 && val <= 1.0);
    }

    [Fact]
    public void WeibulldistIndicator_CustomParams_ShortNameReflects()
    {
        var indicator = new WeibulldistIndicator { K = 3.6, Lambda = 2.0, Period = 30 };
        Assert.Equal("WEIBULLDIST(3.60,2.00,30)", indicator.ShortName);
    }
}
