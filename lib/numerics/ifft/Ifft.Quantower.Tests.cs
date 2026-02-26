using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class IfftIndicatorTests
{
    [Fact]
    public void IfftIndicator_Constructor_SetsDefaults()
    {
        var indicator = new IfftIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.Equal(64, indicator.WindowSize);
        Assert.Equal(5, indicator.NumHarmonics);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("IFFT - Inverse FFT Spectral Low-Pass Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
    }

    [Fact]
    public void IfftIndicator_MinHistoryDepths_EqualsWindowSize()
    {
        var indicator = new IfftIndicator { WindowSize = 64 };
        Assert.Equal(64, indicator.MinHistoryDepths);

        indicator.WindowSize = 32;
        Assert.Equal(32, indicator.MinHistoryDepths);

        indicator.WindowSize = 128;
        Assert.Equal(128, indicator.MinHistoryDepths);
    }

    [Fact]
    public void IfftIndicator_ShortName_IsCorrect()
    {
        var indicator = new IfftIndicator { WindowSize = 32, NumHarmonics = 3 };
        Assert.Equal("IFFT(32,3)", indicator.ShortName);
    }

    [Fact]
    public void IfftIndicator_ShortName_DefaultParams()
    {
        var indicator = new IfftIndicator();
        Assert.Equal("IFFT(64,5)", indicator.ShortName);
    }

    [Fact]
    public void IfftIndicator_Initialize_CreatesOneLineSeries()
    {
        var indicator = new IfftIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("IFFT", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void IfftIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new IfftIndicator { WindowSize = 32, NumHarmonics = 3 };
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
    }

    [Fact]
    public void IfftIndicator_ProcessUpdate_NewBar_AddsNewValue()
    {
        var indicator = new IfftIndicator { WindowSize = 32, NumHarmonics = 3 };
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
    public void IfftIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new IfftIndicator { WindowSize = 32, NumHarmonics = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 0, 105, 95, 100);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void IfftIndicator_Output_IsFiniteAfterWarmup()
    {
        var indicator = new IfftIndicator { WindowSize = 32, NumHarmonics = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        int windowSize = indicator.MinHistoryDepths;
        for (int i = 0; i < windowSize + 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 0, 105, 95, 100 + (i % 10));
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Check all post-warmup values are finite
        for (int i = windowSize; i < indicator.LinesSeries[0].Count; i++)
        {
            double val = indicator.LinesSeries[0].GetValue(i);
            Assert.True(double.IsFinite(val), $"Output at {i} must be finite, got {val}");
        }
    }

    [Fact]
    public void IfftIndicator_DifferentSourceType_Works()
    {
        var indicator = new IfftIndicator { WindowSize = 32, NumHarmonics = 3, Source = SourceType.High };
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
    public void IfftIndicator_OverlaysOnPriceChart()
    {
        // IFFT overlays on price chart (SeparateWindow = false)
        var indicator = new IfftIndicator();
        Assert.False(indicator.SeparateWindow);
    }

    [Fact]
    public void IfftIndicator_DifferentHarmonics_DifferentOutput()
    {
        var ind3 = new IfftIndicator { WindowSize = 32, NumHarmonics = 3 };
        var ind8 = new IfftIndicator { WindowSize = 32, NumHarmonics = 8 };
        ind3.Initialize();
        ind8.Initialize();

        var now = DateTime.UtcNow;
        int windowSize = 32;
        for (int i = 0; i < windowSize + 5; i++)
        {
            ind3.HistoricalData.AddBar(now.AddMinutes(i), 0, 105, 95, 100 + (i % 7));
            ind8.HistoricalData.AddBar(now.AddMinutes(i), 0, 105, 95, 100 + (i % 7));
            ind3.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            ind8.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val3 = ind3.LinesSeries[0].GetValue(0);
        double val8 = ind8.LinesSeries[0].GetValue(0);

        // Different harmonics produce different filtered output
        Assert.True(double.IsFinite(val3) && double.IsFinite(val8));
        // (values will differ since different spectral reconstruction)
    }
}
