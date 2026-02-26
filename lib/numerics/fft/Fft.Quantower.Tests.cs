using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class FftIndicatorTests
{
    [Fact]
    public void FftIndicator_Constructor_SetsDefaults()
    {
        var indicator = new FftIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.Equal(64, indicator.WindowSize);
        Assert.Equal(4, indicator.MinPeriod);
        Assert.Equal(32, indicator.MaxPeriod);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("FFT - Fast Fourier Transform Dominant Cycle", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void FftIndicator_MinHistoryDepths_EqualsWindowSize()
    {
        var indicator = new FftIndicator { WindowSize = 64 };
        Assert.Equal(64, indicator.MinHistoryDepths);

        indicator.WindowSize = 32;
        Assert.Equal(32, indicator.MinHistoryDepths);

        indicator.WindowSize = 128;
        Assert.Equal(128, indicator.MinHistoryDepths);
    }

    [Fact]
    public void FftIndicator_ShortName_IsCorrect()
    {
        var indicator = new FftIndicator { WindowSize = 32, MinPeriod = 4, MaxPeriod = 16 };
        Assert.Equal("FFT(32,4,16)", indicator.ShortName);
    }

    [Fact]
    public void FftIndicator_ShortName_DefaultParams()
    {
        var indicator = new FftIndicator();
        Assert.Equal("FFT(64,4,32)", indicator.ShortName);
    }

    [Fact]
    public void FftIndicator_Initialize_CreatesThreeLineSeries()
    {
        var indicator = new FftIndicator();
        indicator.Initialize();

        Assert.Equal(3, indicator.LinesSeries.Count);
        Assert.Equal("Dominant Period", indicator.LinesSeries[0].Name);
        Assert.Equal("Max Period", indicator.LinesSeries[1].Name);
        Assert.Equal("Min Period", indicator.LinesSeries[2].Name);
    }

    [Fact]
    public void FftIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new FftIndicator { WindowSize = 32, MinPeriod = 4, MaxPeriod = 16 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        int windowSize = indicator.MinHistoryDepths;

        for (int i = 0; i < windowSize; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 105 + i, 95 - i, 100 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val), "Output must be finite after warmup");
        Assert.True(val >= 4.0 && val <= 16.0,
            $"Detected period {val:F2} must be in [4,16]");
    }

    [Fact]
    public void FftIndicator_ProcessUpdate_NewBar_AddsNewValue()
    {
        var indicator = new FftIndicator { WindowSize = 32, MinPeriod = 4, MaxPeriod = 16 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        int windowSize = indicator.MinHistoryDepths;
        for (int i = 0; i < windowSize; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 105, 95, 100 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        indicator.HistoricalData.AddBar(now.AddMinutes(windowSize), 0, 106, 96, 103);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(windowSize + 1, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void FftIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new FftIndicator { WindowSize = 32, MinPeriod = 4, MaxPeriod = 16 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 0, 105, 95, 100);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void FftIndicator_ReferenceLines_WithinBounds()
    {
        var indicator = new FftIndicator { WindowSize = 32, MinPeriod = 4, MaxPeriod = 16 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        int windowSize = indicator.MinHistoryDepths;
        for (int i = 0; i < windowSize + 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 105, 95, 100 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Verify max period reference line
        for (int i = 0; i < indicator.LinesSeries[1].Count; i++)
        {
            double maxPeriodVal = indicator.LinesSeries[1].GetValue(i);
            Assert.Equal(16.0, maxPeriodVal, 1e-10);
        }

        // Verify min period reference line
        for (int i = 0; i < indicator.LinesSeries[2].Count; i++)
        {
            double minPeriodVal = indicator.LinesSeries[2].GetValue(i);
            Assert.Equal(4.0, minPeriodVal, 1e-10);
        }
    }

    [Fact]
    public void FftIndicator_DifferentSourceType_Works()
    {
        var indicator = new FftIndicator { WindowSize = 32, MinPeriod = 4, MaxPeriod = 16, Source = SourceType.High };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        int windowSize = indicator.MinHistoryDepths;
        for (int i = 0; i < windowSize; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 110 + i, 90, 100);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val), "Output using High source must be finite");
    }

    [Fact]
    public void FftIndicator_MaxPeriodClamped_ToHalfWindow()
    {
        // MaxPeriod=40 with WindowSize=32 → should be clamped to 16 in OnInit
        var indicator = new FftIndicator { WindowSize = 32, MinPeriod = 4, MaxPeriod = 40 };
        indicator.Initialize(); // Should not throw

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 0, 105, 95, 100);
        // Should process without exception
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        Assert.Equal(1, indicator.LinesSeries[0].Count);
    }
}
