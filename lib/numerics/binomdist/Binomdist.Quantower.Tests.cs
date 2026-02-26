using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class BinomdistIndicatorTests
{
    [Fact]
    public void BinomdistIndicator_Constructor_SetsDefaults()
    {
        var indicator = new BinomdistIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.Equal(50, indicator.Period);
        Assert.Equal(20, indicator.Trials);
        Assert.Equal(10, indicator.Threshold);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("BINOMDIST - Binomial Distribution CDF", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void BinomdistIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new BinomdistIndicator { Period = 30 };
        Assert.Equal(30, indicator.MinHistoryDepths);
    }

    [Fact]
    public void BinomdistIndicator_ShortName_IsCorrect()
    {
        var indicator = new BinomdistIndicator { Period = 20, Trials = 15, Threshold = 7 };
        Assert.Equal("BINOMDIST(20,15,7)", indicator.ShortName);
    }

    [Fact]
    public void BinomdistIndicator_Initialize_CreatesTwoLineSeries()
    {
        var indicator = new BinomdistIndicator();
        indicator.Initialize();

        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Equal("BinomDist", indicator.LinesSeries[0].Name);
        Assert.Equal("Mid", indicator.LinesSeries[1].Name);
    }

    [Fact]
    public void BinomdistIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new BinomdistIndicator { Period = 5, Trials = 10, Threshold = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 105 + i, 95 - i, 100 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val), "Output must be finite after warmup");
        Assert.True(val >= 0.0 && val <= 1.0, $"Output {val} must be in [0,1]");
    }

    [Fact]
    public void BinomdistIndicator_ProcessUpdate_NewBar_AddsNewValue()
    {
        var indicator = new BinomdistIndicator { Period = 3, Trials = 10, Threshold = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 3; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 105, 95, 100 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        indicator.HistoricalData.AddBar(now.AddMinutes(3), 0, 106, 96, 103);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(4, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void BinomdistIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new BinomdistIndicator { Period = 3, Trials = 10, Threshold = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 0, 105, 95, 100);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void BinomdistIndicator_MidLine_IsAlwaysHalf()
    {
        var indicator = new BinomdistIndicator { Period = 3, Trials = 10, Threshold = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 105, 95, 100 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        for (int i = 0; i < indicator.LinesSeries[1].Count; i++)
        {
            double mid = indicator.LinesSeries[1].GetValue(i);
            Assert.Equal(0.5, mid, 1e-10);
        }
    }

    [Fact]
    public void BinomdistIndicator_DifferentSourceType_Works()
    {
        var indicator = new BinomdistIndicator { Period = 3, Trials = 10, Threshold = 5, Source = SourceType.High };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 3; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 110 + i, 90, 100);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void BinomdistIndicator_OutputInRange_AfterManyBars()
    {
        var indicator = new BinomdistIndicator { Period = 20, Trials = 10, Threshold = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 62001);
        var bars = gbm.Fetch(50, now.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            double price = bars.Close[i].Value;
            indicator.HistoricalData.AddBar(
                new DateTime(bars.Close[i].Time, DateTimeKind.Utc),
                0, price * 1.01, price * 0.99, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        for (int i = 0; i < indicator.LinesSeries[0].Count; i++)
        {
            double val = indicator.LinesSeries[0].GetValue(i);
            Assert.True(val >= 0.0 && val <= 1.0, $"Value {val} at index {i} out of range");
        }
    }

    [Fact]
    public void BinomdistIndicator_ParameterChange_ReflectsInShortName()
    {
        var indicator = new BinomdistIndicator();
        indicator.Period = 10;
        indicator.Trials = 5;
        indicator.Threshold = 2;
        Assert.Equal("BINOMDIST(10,5,2)", indicator.ShortName);
    }
}
