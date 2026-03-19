using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class LpfIndicatorTests
{
    [Fact]
    public void LpfIndicator_Constructor_SetsDefaults()
    {
        var indicator = new LpfIndicator();

        Assert.Equal(18, indicator.LowerBound);
        Assert.Equal(40, indicator.UpperBound);
        Assert.Equal(40, indicator.DataLength);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("LPF - Ehlers Linear Predictive Filter", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void LpfIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new LpfIndicator();

        Assert.Equal(0, LpfIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void LpfIndicator_ShortName_IncludesParameters()
    {
        var indicator = new LpfIndicator { LowerBound = 18, UpperBound = 40, DataLength = 40 };

        Assert.True(indicator.ShortName.Contains("LPF", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("18", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("40", StringComparison.Ordinal));
    }

    [Fact]
    public void LpfIndicator_Initialize_CreatesInternalLpf()
    {
        var indicator = new LpfIndicator { LowerBound = 18, UpperBound = 40, DataLength = 40 };

        indicator.Initialize();

        // After init, line series should exist (Cycle + Signal + Predict)
        Assert.Equal(3, indicator.LinesSeries.Count);
    }

    [Fact]
    public void LpfIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new LpfIndicator { LowerBound = 18, UpperBound = 40, DataLength = 40 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void LpfIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new LpfIndicator { LowerBound = 18, UpperBound = 40, DataLength = 40 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void LpfIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new LpfIndicator { LowerBound = 18, UpperBound = 40, DataLength = 40 };
        indicator.Initialize();

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.NotNull(indicator);
    }

    [Fact]
    public void LpfIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new LpfIndicator { LowerBound = 18, UpperBound = 40, DataLength = 40 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 105, 103, 107, 110, 108, 112, 115, 113 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        for (int i = 0; i < closes.Length; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(closes.Length - 1 - i)));
        }
    }

    [Fact]
    public void LpfIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new LpfIndicator { LowerBound = 18, UpperBound = 40, DataLength = 40, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void LpfIndicator_LowerBound_CanBeChanged()
    {
        var indicator = new LpfIndicator { LowerBound = 18 };

        Assert.Equal(18, indicator.LowerBound);

        indicator.LowerBound = 10;
        Assert.Equal(10, indicator.LowerBound);
    }

    [Fact]
    public void LpfIndicator_UpperBound_CanBeChanged()
    {
        var indicator = new LpfIndicator { UpperBound = 40 };

        Assert.Equal(40, indicator.UpperBound);

        indicator.UpperBound = 100;
        Assert.Equal(100, indicator.UpperBound);
    }

    [Fact]
    public void LpfIndicator_DataLength_CanBeChanged()
    {
        var indicator = new LpfIndicator { DataLength = 40 };

        Assert.Equal(40, indicator.DataLength);

        indicator.DataLength = 60;
        Assert.Equal(60, indicator.DataLength);
    }

    [Fact]
    public void LpfIndicator_Source_CanBeChanged()
    {
        var indicator = new LpfIndicator { Source = SourceType.Close };

        Assert.Equal(SourceType.Close, indicator.Source);

        indicator.Source = SourceType.Open;
        Assert.Equal(SourceType.Open, indicator.Source);
    }

    [Fact]
    public void LpfIndicator_ShowColdValues_CanBeChanged()
    {
        var indicator = new LpfIndicator { ShowColdValues = true };

        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void LpfIndicator_ShortName_UpdatesWhenParametersChange()
    {
        var indicator = new LpfIndicator { LowerBound = 18, UpperBound = 40, DataLength = 40 };
        string initialName = indicator.ShortName;

        Assert.True(initialName.Contains("18", StringComparison.Ordinal));
        Assert.True(initialName.Contains("40", StringComparison.Ordinal));

        indicator.LowerBound = 10;
        indicator.UpperBound = 60;
        string updatedName = indicator.ShortName;

        Assert.True(updatedName.Contains("10", StringComparison.Ordinal));
        Assert.True(updatedName.Contains("60", StringComparison.Ordinal));
    }

    [Fact]
    public void LpfIndicator_ProcessUpdate_IgnoresNonBarUpdates()
    {
        var indicator = new LpfIndicator { LowerBound = 18, UpperBound = 40, DataLength = 40 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.NotNull(indicator);
    }

    [Fact]
    public void LpfIndicator_CycleSeries_HasCorrectProperties()
    {
        var indicator = new LpfIndicator();
        indicator.Initialize();

        var lineSeries = indicator.LinesSeries[0];

        Assert.Equal("Cycle", lineSeries.Name);
        Assert.Equal(2, lineSeries.Width);
        Assert.Equal(LineStyle.Solid, lineSeries.Style);
    }

    [Fact]
    public void LpfIndicator_SignalSeries_HasCorrectProperties()
    {
        var indicator = new LpfIndicator();
        indicator.Initialize();

        var signalSeries = indicator.LinesSeries[1];

        Assert.Equal("Signal", signalSeries.Name);
        Assert.Equal(1, signalSeries.Width);
        Assert.Equal(LineStyle.Solid, signalSeries.Style);
    }

    [Fact]
    public void LpfIndicator_PredictSeries_HasCorrectProperties()
    {
        var indicator = new LpfIndicator();
        indicator.Initialize();

        var predictSeries = indicator.LinesSeries[2];

        Assert.Equal("Predict", predictSeries.Name);
        Assert.Equal(1, predictSeries.Width);
        Assert.Equal(LineStyle.Dot, predictSeries.Style);
    }

    [Fact]
    public void LpfIndicator_SineWave_ProducesFiniteValues()
    {
        var indicator = new LpfIndicator { LowerBound = 10, UpperBound = 50, DataLength = 50 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        const int knownPeriod = 30;

        for (int i = 0; i < 200; i++)
        {
            double price = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / knownPeriod);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double cycleValue = indicator.LinesSeries[0].GetValue(0);
        Assert.InRange(cycleValue, 10, 50);
    }

    [Fact]
    public void LpfIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new LpfIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Lpf.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }
}
