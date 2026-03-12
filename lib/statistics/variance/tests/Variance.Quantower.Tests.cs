using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public sealed class VarianceIndicatorTests
{
    [Fact]
    public void VarianceIndicator_Constructor_SetsDefaults()
    {
        var indicator = new VarianceIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.False(indicator.IsPopulation);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Contains("VAR", indicator.Name, StringComparison.Ordinal);
        Assert.True(indicator.SeparateWindow);
        Assert.False(indicator.OnBackGround);
    }

    [Fact]
    public void VarianceIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new VarianceIndicator { Period = 20 };

        Assert.Equal(20, indicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(20, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void VarianceIndicator_ShortName_IncludesParameters()
    {
        var indicator = new VarianceIndicator { Period = 20, IsPopulation = false };

        Assert.Contains("VAR", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("Samp", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void VarianceIndicator_ShortName_ShowsPopulation()
    {
        var indicator = new VarianceIndicator { Period = 14, IsPopulation = true };

        Assert.Contains("Pop", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void VarianceIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new VarianceIndicator { Period = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void VarianceIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new VarianceIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void VarianceIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new VarianceIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void VarianceIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new VarianceIndicator { Period = 5 };
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
    public void VarianceIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new VarianceIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 105, 103, 107, 110 };

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
    public void VarianceIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new VarianceIndicator { Period = 5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void VarianceIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new VarianceIndicator { ShowColdValues = true };
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void VarianceIndicator_ConstantInput_ZeroVariance()
    {
        var indicator = new VarianceIndicator { Period = 5, IsPopulation = true };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 100, 100, 100);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double variance = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0.0, variance, 6);
    }

    [Fact]
    public void VarianceIndicator_KnownValues_ComputesCorrectly()
    {
        // For values {2, 4, 4, 4, 5, 5, 7, 9}, population variance = 4.0
        var indicator = new VarianceIndicator { Period = 8, IsPopulation = true };
        indicator.Initialize();

        double[] values = { 2, 4, 4, 4, 5, 5, 7, 9 };
        var now = DateTime.UtcNow;
        foreach (var v in values)
        {
            indicator.HistoricalData.AddBar(now, v, v, v, v);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        double variance = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(4.0, variance, 4);
    }

    [Fact]
    public void VarianceIndicator_SampleVsPopulation_DifferentResults()
    {
        double[] values = { 2, 4, 4, 4, 5, 5, 7, 9 };

        var popIndicator = new VarianceIndicator { Period = 8, IsPopulation = true };
        popIndicator.Initialize();

        var sampIndicator = new VarianceIndicator { Period = 8, IsPopulation = false };
        sampIndicator.Initialize();

        var now = DateTime.UtcNow;
        foreach (var v in values)
        {
            popIndicator.HistoricalData.AddBar(now, v, v, v, v);
            popIndicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            sampIndicator.HistoricalData.AddBar(now, v, v, v, v);
            sampIndicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            now = now.AddMinutes(1);
        }

        double popVar = popIndicator.LinesSeries[0].GetValue(0);
        double sampVar = sampIndicator.LinesSeries[0].GetValue(0);

        // Sample variance (N-1) should be larger than population variance (N)
        Assert.True(sampVar > popVar, "Sample variance should be larger than population variance");
    }

    [Fact]
    public void VarianceIndicator_OutputIsNonNegative()
    {
        var indicator = new VarianceIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 98, 103, 97, 105, 95, 110, 90, 102, 101 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 5, close - 5, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        for (int i = 0; i < closes.Length; i++)
        {
            double val = indicator.LinesSeries[0].GetValue(closes.Length - 1 - i);
            Assert.True(val >= 0, $"Variance at index {i} should be non-negative, got {val}");
        }
    }

    [Fact]
    public void VarianceIndicator_Description_IsSet()
    {
        var indicator = new VarianceIndicator();

        Assert.NotNull(indicator.Description);
        Assert.NotEmpty(indicator.Description);
        Assert.Contains("dispersion", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VarianceIndicator_DifferentPeriods_ProduceDifferentResults()
    {
        double[] values = { 100, 102, 98, 105, 97, 110, 95, 108, 101, 103 };

        var short5 = new VarianceIndicator { Period = 3 };
        short5.Initialize();
        var long10 = new VarianceIndicator { Period = 10 };
        long10.Initialize();

        var now = DateTime.UtcNow;
        foreach (var v in values)
        {
            short5.HistoricalData.AddBar(now, v, v + 2, v - 2, v);
            short5.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            long10.HistoricalData.AddBar(now, v, v + 2, v - 2, v);
            long10.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            now = now.AddMinutes(1);
        }

        double varShort = short5.LinesSeries[0].GetValue(0);
        double varLong = long10.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(varShort));
        Assert.True(double.IsFinite(varLong));
        // Different periods should generally give different variance values
        Assert.NotEqual(varShort, varLong, 2);
    }

    [Fact]
    public void VarianceIndicator_LineSeries_HasCorrectProperties()
    {
        var indicator = new VarianceIndicator();
        indicator.Initialize();

        var lineSeries = indicator.LinesSeries[0];

        Assert.Equal(2, lineSeries.Width);
        Assert.Equal(LineStyle.Solid, lineSeries.Style);
    }
}
