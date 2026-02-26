using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class DwtIndicatorTests
{
    [Fact]
    public void DwtIndicator_Constructor_SetsDefaults()
    {
        var indicator = new DwtIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.Equal(4, indicator.Levels);
        Assert.Equal(0, indicator.OutputComponent);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("DWT - Discrete Wavelet Transform", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void DwtIndicator_MinHistoryDepths_CorrectForLevel4()
    {
        // levels=4: bufferSize = 2^4 = 16
        var indicator = new DwtIndicator { Levels = 4 };
        Assert.Equal(16, indicator.MinHistoryDepths);
    }

    [Fact]
    public void DwtIndicator_MinHistoryDepths_CorrectForLevel2()
    {
        // levels=2: bufferSize = 2^2 = 4
        var indicator = new DwtIndicator { Levels = 2 };
        Assert.Equal(4, indicator.MinHistoryDepths);
    }

    [Fact]
    public void DwtIndicator_MinHistoryDepths_CorrectForLevel8()
    {
        // levels=8: bufferSize = 2^8 = 256
        var indicator = new DwtIndicator { Levels = 8 };
        Assert.Equal(256, indicator.MinHistoryDepths);
    }

    [Fact]
    public void DwtIndicator_ShortName_IsCorrect()
    {
        var indicator = new DwtIndicator { Levels = 3, OutputComponent = 1 };
        Assert.Equal("DWT(3,1)", indicator.ShortName);
    }

    [Fact]
    public void DwtIndicator_Initialize_CreatesTwoLineSeries()
    {
        var indicator = new DwtIndicator();
        indicator.Initialize();

        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Equal("DWT Component", indicator.LinesSeries[0].Name);
        Assert.Equal("Zero", indicator.LinesSeries[1].Name);
    }

    [Fact]
    public void DwtIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        // levels=2: warmup = 4 bars
        var indicator = new DwtIndicator { Levels = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        int warmup = indicator.MinHistoryDepths;

        for (int i = 0; i < warmup; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 105 + i, 95 - i, 100 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // After warmup, should have valid (non-cold) output
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val), "Output must be finite after warmup");
    }

    [Fact]
    public void DwtIndicator_ProcessUpdate_NewBar_AddsNewValue()
    {
        var indicator = new DwtIndicator { Levels = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        int warmup = indicator.MinHistoryDepths;
        for (int i = 0; i < warmup; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 105, 95, 100 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Feed a new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(warmup), 0, 106, 96, 103);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(warmup + 1, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void DwtIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new DwtIndicator { Levels = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 0, 105, 95, 100);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // 2 values: one historical, one intra-bar update
        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void DwtIndicator_ZeroLine_IsAlwaysZero()
    {
        var indicator = new DwtIndicator { Levels = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        int warmup = indicator.MinHistoryDepths;
        for (int i = 0; i < warmup + 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 105, 95, 100 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        for (int i = 0; i < indicator.LinesSeries[1].Count; i++)
        {
            double zero = indicator.LinesSeries[1].GetValue(i);
            Assert.Equal(0.0, zero, 1e-10);
        }
    }

    [Fact]
    public void DwtIndicator_DifferentSourceType_Works()
    {
        var indicator = new DwtIndicator { Levels = 2, Source = SourceType.High };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        int warmup = indicator.MinHistoryDepths;
        for (int i = 0; i < warmup; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 110 + i, 90, 100);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void DwtIndicator_DetailOutput_Works()
    {
        // OutputComponent = 1 → detail at level 1
        var indicator = new DwtIndicator { Levels = 3, OutputComponent = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        int warmup = indicator.MinHistoryDepths;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 83001);
        var bars = gbm.Fetch(warmup + 5, now.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            double price = bars.Close[i].Value;
            indicator.HistoricalData.AddBar(
                new DateTime(bars.Close[i].Time, DateTimeKind.Utc),
                0, price * 1.01, price * 0.99, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val), $"DWT detail output {val} must be finite");
    }

    [Fact]
    public void DwtIndicator_OutputNonCold_AfterManyBars()
    {
        var indicator = new DwtIndicator { Levels = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 83002);
        var bars = gbm.Fetch(50, now.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            double price = bars.Close[i].Value;
            indicator.HistoricalData.AddBar(
                new DateTime(bars.Close[i].Time, DateTimeKind.Utc),
                0, price * 1.01, price * 0.99, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // All computed values should be finite
        for (int i = 0; i < indicator.LinesSeries[0].Count; i++)
        {
            double val = indicator.LinesSeries[0].GetValue(i);
            Assert.True(double.IsFinite(val), $"DWT value {val} at index {i} must be finite");
        }
    }
}
