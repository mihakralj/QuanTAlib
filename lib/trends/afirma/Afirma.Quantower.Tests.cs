using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class AfirmaIndicatorTests
{
    [Fact]
    public void AfirmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AfirmaIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.Equal(6, indicator.Taps);
        Assert.Equal(Afirma.WindowType.BlackmanHarris, indicator.Window);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("AFIRMA - Autoregressive FIR Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AfirmaIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new AfirmaIndicator { Period = 20, Taps = 10 };

        Assert.Equal(0, AfirmaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void AfirmaIndicator_ShortName_IncludesParameters()
    {
        var indicator = new AfirmaIndicator { Period = 15, Taps = 8 };

        Assert.Contains("AFIRMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("8", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void AfirmaIndicator_Initialize_CreatesInternalAfirma()
    {
        var indicator = new AfirmaIndicator { Period = 10, Taps = 6 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void AfirmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AfirmaIndicator { Period = 5, Taps = 3 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process update
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Line series should have a value
        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void AfirmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new AfirmaIndicator { Period = 5, Taps = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void AfirmaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new AfirmaIndicator { Period = 5, Taps = 3 };
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
    public void AfirmaIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new AfirmaIndicator { Period = 5, Taps = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105, 107, 106, 108 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // All values should be finite
        for (int i = 0; i < closes.Length; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(closes.Length - 1 - i)));
        }
    }

    [Fact]
    public void AfirmaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new AfirmaIndicator { Period = 5, Taps = 3, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void AfirmaIndicator_DifferentWindowTypes_Work()
    {
        var windows = new[]
        {
            Afirma.WindowType.Rectangular,
            Afirma.WindowType.Hanning,
            Afirma.WindowType.Hamming,
            Afirma.WindowType.Blackman,
            Afirma.WindowType.BlackmanHarris
        };

        foreach (var window in windows)
        {
            var indicator = new AfirmaIndicator { Period = 5, Taps = 5, Window = window };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Window {window} should produce finite value");
        }
    }

    [Fact]
    public void AfirmaIndicator_Period_CanBeChanged()
    {
        var indicator = new AfirmaIndicator { Period = 5 };
        Assert.Equal(5, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
    }

    [Fact]
    public void AfirmaIndicator_Taps_CanBeChanged()
    {
        var indicator = new AfirmaIndicator { Taps = 5 };
        Assert.Equal(5, indicator.Taps);

        indicator.Taps = 12;
        Assert.Equal(12, indicator.Taps);
    }

    [Fact]
    public void AfirmaIndicator_Window_CanBeChanged()
    {
        var indicator = new AfirmaIndicator { Window = Afirma.WindowType.Hanning };
        Assert.Equal(Afirma.WindowType.Hanning, indicator.Window);

        indicator.Window = Afirma.WindowType.Blackman;
        Assert.Equal(Afirma.WindowType.Blackman, indicator.Window);
    }
}
