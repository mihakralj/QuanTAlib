using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class CwtIndicatorTests
{
    [Fact]
    public void CwtIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CwtIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.Equal(10.0, indicator.Scale);
        Assert.Equal(6.0, indicator.Omega0);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("CWT - Continuous Wavelet Transform", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CwtIndicator_MinHistoryDepths_CorrectForScale10()
    {
        // scale=10: halfWindow=round(30)=30, windowSize=61
        var indicator = new CwtIndicator { Scale = 10.0 };
        Assert.Equal(61, indicator.MinHistoryDepths);
    }

    [Fact]
    public void CwtIndicator_MinHistoryDepths_CorrectForScale5()
    {
        // scale=5: halfWindow=round(15)=15, windowSize=31
        var indicator = new CwtIndicator { Scale = 5.0 };
        Assert.Equal(31, indicator.MinHistoryDepths);
    }

    [Fact]
    public void CwtIndicator_ShortName_IsCorrect()
    {
        var indicator = new CwtIndicator { Scale = 20.0, Omega0 = 5.0 };
        Assert.Equal("CWT(20,5)", indicator.ShortName);
    }

    [Fact]
    public void CwtIndicator_Initialize_CreatesTwoLineSeries()
    {
        var indicator = new CwtIndicator();
        indicator.Initialize();

        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Equal("CWT Magnitude", indicator.LinesSeries[0].Name);
        Assert.Equal("Zero", indicator.LinesSeries[1].Name);
    }

    [Fact]
    public void CwtIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        // scale=2: windowSize=13 bars needed
        var indicator = new CwtIndicator { Scale = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        int windowSize = indicator.MinHistoryDepths;

        for (int i = 0; i < windowSize; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 105 + i, 95 - i, 100 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // After windowSize bars, should have valid (non-cold) output
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val), "Output must be finite after warmup");
        Assert.True(val >= 0.0, $"CWT magnitude {val} must be >= 0");
    }

    [Fact]
    public void CwtIndicator_ProcessUpdate_NewBar_AddsNewValue()
    {
        var indicator = new CwtIndicator { Scale = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Feed windowSize historical bars
        int windowSize = indicator.MinHistoryDepths;
        for (int i = 0; i < windowSize; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 105, 95, 100 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Feed a new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(windowSize), 0, 106, 96, 103);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(windowSize + 1, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void CwtIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new CwtIndicator { Scale = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 0, 105, 95, 100);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // 2 values: one historical, one intra-bar update
        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void CwtIndicator_ZeroLine_IsAlwaysZero()
    {
        var indicator = new CwtIndicator { Scale = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        int windowSize = indicator.MinHistoryDepths;
        for (int i = 0; i < windowSize + 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 105, 95, 100 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Zero reference line should always be 0
        for (int i = 0; i < indicator.LinesSeries[1].Count; i++)
        {
            double zero = indicator.LinesSeries[1].GetValue(i);
            Assert.Equal(0.0, zero, 1e-10);
        }
    }

    [Fact]
    public void CwtIndicator_DifferentSourceType_Works()
    {
        var indicator = new CwtIndicator { Scale = 2.0, Source = SourceType.High };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        int windowSize = indicator.MinHistoryDepths;
        for (int i = 0; i < windowSize; i++)
        {
            // High = 110+i, Low = 90, Close = 100
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 110 + i, 90, 100);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val >= 0.0);
    }

    [Fact]
    public void CwtIndicator_OutputNonNegative_AfterManyBars()
    {
        var indicator = new CwtIndicator { Scale = 3.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 73001);
        var bars = gbm.Fetch(100, now.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            double price = bars.Close[i].Value;
            indicator.HistoricalData.AddBar(
                new DateTime(bars.Close[i].Time, DateTimeKind.Utc),
                0, price * 1.01, price * 0.99, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Check all computed values are >= 0
        for (int i = 0; i < indicator.LinesSeries[0].Count; i++)
        {
            double val = indicator.LinesSeries[0].GetValue(i);
            Assert.True(val >= 0.0, $"CWT magnitude {val} at index {i} must be >= 0");
        }
    }
}
