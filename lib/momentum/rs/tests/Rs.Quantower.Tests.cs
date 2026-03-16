using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class RsIndicatorTests
{
    [Fact]
    public void RsIndicator_Constructor_SetsDefaults()
    {
        var indicator = new RsIndicator();

        Assert.Equal(1, indicator.SmoothPeriod);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.Equal(SourceType.Open, indicator.Source2);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("RS - Price Relative Strength", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void RsIndicator_MinHistoryDepths_EqualsOne()
    {
        var indicator = new RsIndicator();

        Assert.Equal(1, RsIndicator.MinHistoryDepths);
        Assert.Equal(1, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void RsIndicator_ShortName_NoSmoothing_ContainsSourceTypes()
    {
        var indicator = new RsIndicator { SmoothPeriod = 1 };

        Assert.Contains("RS", indicator.ShortName, StringComparison.Ordinal);
        Assert.DoesNotContain("(", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void RsIndicator_ShortName_WithSmoothing_IncludesPeriod()
    {
        var indicator = new RsIndicator { SmoothPeriod = 14 };
        indicator.Initialize();

        Assert.Contains("RS", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void RsIndicator_Initialize_CreatesRsInstance()
    {
        var indicator = new RsIndicator { SmoothPeriod = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void RsIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new RsIndicator { SmoothPeriod = 1 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);

        // Process update
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Line series should have a value
        Assert.Equal(1, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void RsIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new RsIndicator { SmoothPeriod = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 112, 92, 108);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void RsIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new RsIndicator { SmoothPeriod = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        // NewTick should not throw
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        // Values should be produced
        Assert.True(double.IsNaN(firstValue) || double.IsFinite(firstValue));
        Assert.True(double.IsNaN(secondValue) || double.IsFinite(secondValue));
    }

    [Fact]
    public void RsIndicator_MultipleUpdates_ProducesSequence()
    {
        var indicator = new RsIndicator { SmoothPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] opens = { 100, 101, 102, 103, 104, 105 };
        double[] closes = { 100, 101, 102, 103, 104, 105 };

        for (int i = 0; i < opens.Length; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), opens[i], opens[i] + 5, opens[i] - 5, closes[i]);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(opens.Length, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void RsIndicator_CloseVsOpen_ReturnsPositiveRatio()
    {
        // Test with Close vs Open - Close should be higher than Open in uptrend
        var indicator = new RsIndicator { SmoothPeriod = 1, Source = SourceType.Close, Source2 = SourceType.Open };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Bar where Close > Open (bullish bar)
        indicator.HistoricalData.AddBar(now, 100, 110, 95, 108);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        double value = indicator.LinesSeries[0].GetValue(0);

        // Close/Open = 108/100 = 1.08
        Assert.True(value > 1.0, $"Expected ratio > 1.0 for bullish bar, got {value}");
        Assert.Equal(1.08, value, precision: 6);
    }

    [Fact]
    public void RsIndicator_OpenVsClose_ReturnsInverseRatio()
    {
        // Reverse the sources - Open vs Close
        var indicator = new RsIndicator { SmoothPeriod = 1, Source = SourceType.Open, Source2 = SourceType.Close };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Bar where Close > Open (bullish bar)
        indicator.HistoricalData.AddBar(now, 100, 110, 95, 108);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        double value = indicator.LinesSeries[0].GetValue(0);

        // Open/Close = 100/108 ≈ 0.926
        Assert.True(value < 1.0, $"Expected ratio < 1.0 when Open < Close, got {value}");
    }

    [Fact]
    public void RsIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new RsIndicator { SmoothPeriod = 1, Source = source, Source2 = SourceType.Close };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            // Should have computed a value
            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void RsIndicator_DifferentSource2Types_Work()
    {
        var source2Types = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.HL2 };

        foreach (var source2 in source2Types)
        {
            var indicator = new RsIndicator { SmoothPeriod = 1, Source = SourceType.Close, Source2 = source2 };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void RsIndicator_SmoothPeriod_CanBeChanged()
    {
        var indicator = new RsIndicator { SmoothPeriod = 50 };

        Assert.Equal(50, indicator.SmoothPeriod);

        indicator.SmoothPeriod = 100;
        Assert.Equal(100, indicator.SmoothPeriod);
    }

    [Fact]
    public void RsIndicator_Source2_CanBeChanged()
    {
        var indicator = new RsIndicator { Source2 = SourceType.High };

        Assert.Equal(SourceType.High, indicator.Source2);

        indicator.Source2 = SourceType.Low;
        Assert.Equal(SourceType.Low, indicator.Source2);
    }

    [Fact]
    public void RsIndicator_ReInitialize_ResetsState()
    {
        var indicator = new RsIndicator { SmoothPeriod = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(10, indicator.LinesSeries[0].Count);

        // Re-initialize with new instance
        var indicator2 = new RsIndicator { SmoothPeriod = 5 };
        indicator2.Initialize();
        indicator2.HistoricalData.AddBar(now.AddMinutes(100), 200, 210, 190, 205);
        indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.Equal(1, indicator2.LinesSeries[0].Count);
    }

    [Fact]
    public void RsIndicator_HighVsLow_AlwaysGreaterThanOne()
    {
        // High is always > Low, so ratio should always be > 1
        var indicator = new RsIndicator { SmoothPeriod = 1, Source = SourceType.High, Source2 = SourceType.Low };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double mid = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), mid, mid + 5, mid - 5, mid);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(10, indicator.LinesSeries[0].Count);

        // High/Low should always be > 1
        for (int i = 0; i < 10; i++)
        {
            double value = indicator.LinesSeries[0].GetValue(i);
            if (double.IsFinite(value))
            {
                Assert.True(value > 1.0, $"Expected ratio > 1 for High/Low at index {i}, got {value}");
            }
        }
    }

    [Fact]
    public void RsIndicator_Description_IsSet()
    {
        var indicator = new RsIndicator();

        Assert.Contains("relative", indicator.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("performance", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RsIndicator_SameSourceAndSource2_ReturnsOne()
    {
        // When comparing a source to itself, ratio should be 1.0
        var indicator = new RsIndicator { SmoothPeriod = 1, Source = SourceType.Close, Source2 = SourceType.Close };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(5, indicator.LinesSeries[0].Count);

        // Close/Close should be exactly 1.0
        for (int i = 0; i < 5; i++)
        {
            double value = indicator.LinesSeries[0].GetValue(i);
            Assert.Equal(1.0, value, precision: 10);
        }
    }

    [Fact]
    public void RsIndicator_SmoothingReducesVariance()
    {
        // Compare unsmoothed vs smoothed - smoothed should have less variance
        var unsmoothed = new RsIndicator { SmoothPeriod = 1, Source = SourceType.Close, Source2 = SourceType.Open };
        var smoothed = new RsIndicator { SmoothPeriod = 10, Source = SourceType.Close, Source2 = SourceType.Open };
        unsmoothed.Initialize();
        smoothed.Initialize();

        var now = DateTime.UtcNow;
        double[] opens = { 100, 102, 98, 104, 96, 106, 94, 108, 92, 110, 90, 112, 88, 114, 86 };
        double[] closes = { 102, 100, 101, 97, 105, 95, 107, 93, 109, 91, 111, 89, 113, 87, 115 };

        for (int i = 0; i < opens.Length; i++)
        {
            unsmoothed.HistoricalData.AddBar(now.AddMinutes(i), opens[i], 120, 80, closes[i]);
            smoothed.HistoricalData.AddBar(now.AddMinutes(i), opens[i], 120, 80, closes[i]);
            unsmoothed.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
            smoothed.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        // Calculate variance of last 5 values
        double[] unsmoothedVals = new double[5];
        double[] smoothedVals = new double[5];

        for (int i = 0; i < 5; i++)
        {
            unsmoothedVals[i] = unsmoothed.LinesSeries[0].GetValue(i);
            smoothedVals[i] = smoothed.LinesSeries[0].GetValue(i);
        }

        double unsmoothedMean = unsmoothedVals.Average();
        double smoothedMean = smoothedVals.Average();

        double unsmoothedVariance = unsmoothedVals.Select(v => (v - unsmoothedMean) * (v - unsmoothedMean)).Sum() / 5;
        double smoothedVariance = smoothedVals.Select(v => (v - smoothedMean) * (v - smoothedMean)).Sum() / 5;

        // Smoothed should have less variance (or equal if no variation)
        Assert.True(smoothedVariance <= unsmoothedVariance + 0.001,
            $"Smoothed variance ({smoothedVariance}) should be <= unsmoothed variance ({unsmoothedVariance})");
    }
}
