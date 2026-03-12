using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class WaveletIndicatorTests
{
    [Fact]
    public void WaveletIndicator_Constructor_SetsDefaults()
    {
        var indicator = new WaveletIndicator();

        Assert.Equal(4, indicator.Levels);
        Assert.Equal(1.0, indicator.ThreshMult);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("WAVELET - À Trous Wavelet Denoising Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void WaveletIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new WaveletIndicator { Levels = 4, ThreshMult = 1.0 };

        Assert.Equal(0, WaveletIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void WaveletIndicator_ShortName_IncludesParameters()
    {
        var indicator = new WaveletIndicator { Levels = 4, ThreshMult = 1.0 };

        Assert.Contains("WAVELET", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("4", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("1.0", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void WaveletIndicator_Initialize_CreatesInternalWavelet()
    {
        var indicator = new WaveletIndicator { Levels = 4, ThreshMult = 1.0 };

        indicator.Initialize();

        _ = Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void WaveletIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new WaveletIndicator { Levels = 2, ThreshMult = 1.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void WaveletIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new WaveletIndicator { Levels = 2, ThreshMult = 1.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void WaveletIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new WaveletIndicator { Levels = 2, ThreshMult = 1.0 };
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
    public void WaveletIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new WaveletIndicator { Levels = 2, ThreshMult = 1.0, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void WaveletIndicator_Parameters_CanBeChanged()
    {
        var indicator = new WaveletIndicator { Levels = 4, ThreshMult = 1.0 };
        Assert.Equal(4, indicator.Levels);
        Assert.Equal(1.0, indicator.ThreshMult);

        indicator.Levels = 3;
        indicator.ThreshMult = 2.0;
        Assert.Equal(3, indicator.Levels);
        Assert.Equal(2.0, indicator.ThreshMult);
    }
}
