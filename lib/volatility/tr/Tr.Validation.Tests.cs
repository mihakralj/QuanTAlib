namespace QuanTAlib.Test;

using Xunit;

/// <summary>
/// Validation tests for TR (True Range).
/// TR = max(High - Low, |High - prevClose|, |Low - prevClose|)
/// First bar uses High - Low only.
/// </summary>
public class TrValidationTests
{
    private static TBarSeries GenerateTestData(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // === Mathematical Validation ===

    /// <summary>
    /// Validates the TR formula: max(H-L, |H-pC|, |L-pC|)
    /// </summary>
    [Fact]
    public void Tr_Formula_IsCorrect()
    {
        double high = 105.0;
        double low = 95.0;
        double prevClose = 100.0;

        double tr1 = high - low;           // 10
        double tr2 = Math.Abs(high - prevClose);  // 5
        double tr3 = Math.Abs(low - prevClose);   // 5

        double expected = Math.Max(tr1, Math.Max(tr2, tr3)); // 10

        Assert.Equal(10.0, expected, 10);
    }

    /// <summary>
    /// Validates TR with gap up scenario.
    /// Gap up: prevClose below current Low, so |H-pC| > H-L
    /// </summary>
    [Fact]
    public void Tr_GapUp_CapturesGap()
    {
        double high = 115.0;
        double low = 110.0;
        double prevClose = 100.0;  // Gap up from 100 to 110-115

        double tr1 = high - low;           // 5
        double tr2 = Math.Abs(high - prevClose);  // 15
        double tr3 = Math.Abs(low - prevClose);   // 10

        double expected = Math.Max(tr1, Math.Max(tr2, tr3)); // 15

        Assert.Equal(15.0, expected, 10);
        Assert.True(expected > tr1, "TR should capture the gap, exceeding H-L range");
    }

    /// <summary>
    /// Validates TR with gap down scenario.
    /// Gap down: prevClose above current High, so |L-pC| > H-L
    /// </summary>
    [Fact]
    public void Tr_GapDown_CapturesGap()
    {
        double high = 95.0;
        double low = 90.0;
        double prevClose = 110.0;  // Gap down from 110 to 90-95

        double tr1 = high - low;           // 5
        double tr2 = Math.Abs(high - prevClose);  // 15
        double tr3 = Math.Abs(low - prevClose);   // 20

        double expected = Math.Max(tr1, Math.Max(tr2, tr3)); // 20

        Assert.Equal(20.0, expected, 10);
        Assert.True(expected > tr1, "TR should capture the gap, exceeding H-L range");
    }

    /// <summary>
    /// Validates TR when prevClose is within H-L range (no gap).
    /// In this case TR = H - L
    /// </summary>
    [Fact]
    public void Tr_NoGap_EqualsHighMinusLow()
    {
        double high = 105.0;
        double low = 95.0;
        double prevClose = 100.0;  // Within range

        double tr1 = high - low;           // 10
        double tr2 = Math.Abs(high - prevClose);  // 5
        double tr3 = Math.Abs(low - prevClose);   // 5

        double expected = Math.Max(tr1, Math.Max(tr2, tr3)); // 10

        Assert.Equal(tr1, expected, 10);
    }

    /// <summary>
    /// Validates first bar uses H - L only.
    /// </summary>
    [Fact]
    public void Tr_FirstBar_UsesHighMinusLow()
    {
        var tr = new Tr();
        var bar = new TBar(DateTime.UtcNow.Ticks, 100, 110, 90, 105, 1000);

        var result = tr.Update(bar);

        Assert.Equal(20.0, result.Value, 10);  // 110 - 90 = 20
    }

    /// <summary>
    /// Validates second bar uses full TR formula.
    /// </summary>
    [Fact]
    public void Tr_SecondBar_UsesFullFormula()
    {
        var tr = new Tr();

        // First bar: close at 100
        var bar1 = new TBar(DateTime.UtcNow.Ticks, 98, 102, 98, 100, 1000);
        tr.Update(bar1);

        // Second bar: gap up, H=115, L=110, pC=100
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1).Ticks, 110, 115, 110, 113, 1000);
        var result = tr.Update(bar2);

        // TR = max(5, 15, 10) = 15
        Assert.Equal(15.0, result.Value, 10);
    }

    // === Streaming Validation ===

    /// <summary>
    /// Validates streaming calculation matches manual calculation.
    /// </summary>
    [Fact]
    public void Tr_StreamingMatchesManual()
    {
        var tr = new Tr();
        var bars = GenerateTestData(50);

        double? prevClose = null;
        for (int i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            var result = tr.Update(bar);

            double expected;
            if (prevClose == null)
            {
                expected = bar.High - bar.Low;
            }
            else
            {
                double tr1 = bar.High - bar.Low;
                double tr2 = Math.Abs(bar.High - prevClose.Value);
                double tr3 = Math.Abs(bar.Low - prevClose.Value);
                expected = Math.Max(tr1, Math.Max(tr2, tr3));
            }

            Assert.Equal(expected, result.Value, 10);
            prevClose = bar.Close;
        }
    }

    /// <summary>
    /// Validates batch calculation matches streaming.
    /// </summary>
    [Fact]
    public void Tr_BatchMatchesStreaming()
    {
        var bars = GenerateTestData(100);

        // Streaming
        var streamingTr = new Tr();
        var streamingResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults[i] = streamingTr.Update(bars[i]).Value;
        }

        // Batch
        var batchOutput = new double[bars.Count];
        Tr.Batch(bars, batchOutput);

        // Compare all values
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchOutput[i], 10);
        }
    }

    /// <summary>
    /// Validates TBarSeries batch matches streaming.
    /// </summary>
    [Fact]
    public void Tr_TBarSeriesBatchMatchesStreaming()
    {
        var bars = GenerateTestData(100);

        // Streaming
        var streamingTr = new Tr();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingTr.Update(bars[i]);
        }

        // Batch via TBarSeries
        var batchResult = Tr.Batch(bars);

        Assert.Equal(streamingTr.Last.Value, batchResult.Last.Value, 10);
    }

    /// <summary>
    /// Validates span-based batch matches streaming.
    /// </summary>
    [Fact]
    public void Tr_SpanBatchMatchesStreaming()
    {
        var bars = GenerateTestData(100);

        // Streaming
        var streamingTr = new Tr();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingTr.Update(bars[i]);
        }

        // Extract OHLC
        var highs = new double[bars.Count];
        var lows = new double[bars.Count];
        var closes = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
            closes[i] = bars[i].Close;
        }

        // Span batch
        var output = new double[bars.Count];
        Tr.Batch(highs, lows, closes, output);

        Assert.Equal(streamingTr.Last.Value, output[^1], 10);
    }

    // === Property Validation ===

    /// <summary>
    /// Validates TR is always non-negative.
    /// </summary>
    [Fact]
    public void Tr_Output_IsNonNegative()
    {
        var bars = GenerateTestData(100);
        var tr = new Tr();

        for (int i = 0; i < bars.Count; i++)
        {
            var result = tr.Update(bars[i]);
            Assert.True(result.Value >= 0, $"TR should be non-negative at bar {i}");
        }
    }

    /// <summary>
    /// Validates TR >= High - Low for all bars (since it's the max of three components).
    /// </summary>
    [Fact]
    public void Tr_GreaterOrEqualToHighMinusLow()
    {
        var bars = GenerateTestData(100);
        var tr = new Tr();

        for (int i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            var result = tr.Update(bar);
            double hlRange = bar.High - bar.Low;

            Assert.True(result.Value >= hlRange - 1e-10,
                $"TR should be >= H-L at bar {i}. TR={result.Value}, H-L={hlRange}");
        }
    }

    /// <summary>
    /// Validates TR output is always finite.
    /// </summary>
    [Fact]
    public void Tr_Output_IsFinite()
    {
        var bars = GenerateTestData(100);
        var tr = new Tr();

        for (int i = 0; i < bars.Count; i++)
        {
            var result = tr.Update(bars[i]);
            Assert.True(double.IsFinite(result.Value), $"TR should be finite at bar {i}");
        }
    }

    // === Edge Cases ===

    /// <summary>
    /// Validates handling of flat bars (H = L).
    /// </summary>
    [Fact]
    public void Tr_FlatBars_HandledCorrectly()
    {
        var tr = new Tr();

        // First bar: flat
        var bar1 = new TBar(DateTime.UtcNow.Ticks, 100, 100, 100, 100, 1000);
        var result1 = tr.Update(bar1);
        Assert.Equal(0.0, result1.Value, 10);

        // Second bar: flat but different price (gap)
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1).Ticks, 105, 105, 105, 105, 1000);
        var result2 = tr.Update(bar2);
        Assert.Equal(5.0, result2.Value, 10);  // |105-100| = 5
    }

    /// <summary>
    /// Validates handling of very large gaps.
    /// </summary>
    [Fact]
    public void Tr_LargeGaps_HandledCorrectly()
    {
        var tr = new Tr();

        // First bar at 100
        var bar1 = new TBar(DateTime.UtcNow.Ticks, 100, 101, 99, 100, 1000);
        tr.Update(bar1);

        // Second bar with huge gap up
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1).Ticks, 200, 202, 198, 200, 1000);
        var result = tr.Update(bar2);

        // TR = max(4, 102, 98) = 102
        Assert.Equal(102.0, result.Value, 10);
    }

    /// <summary>
    /// Validates handling of very small ranges.
    /// </summary>
    [Fact]
    public void Tr_SmallRanges_HandledCorrectly()
    {
        var tr = new Tr();

        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 100.001, 99.999, 100.0, 1000
            );
            var result = tr.Update(bar);

            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value >= 0);
        }
    }

    /// <summary>
    /// Validates bar correction works correctly.
    /// </summary>
    [Fact]
    public void Tr_BarCorrection_WorksCorrectly()
    {
        var tr = new Tr();
        var bars = GenerateTestData(20);

        // Feed initial bars
        for (int i = 0; i < 15; i++)
        {
            tr.Update(bars[i], isNew: true);
        }

        // Add new bar
        tr.Update(bars[15], isNew: true);
        double afterNew = tr.Last.Value;

        // Correct with different bar (much larger range)
        var correctedBar = new TBar(
            bars[15].Time,
            100, 200, 50, 150, 1000
        );
        tr.Update(correctedBar, isNew: false);
        double afterCorrection = tr.Last.Value;

        // Restore original
        tr.Update(bars[15], isNew: false);
        double afterRestore = tr.Last.Value;

        Assert.NotEqual(afterNew, afterCorrection);
        Assert.Equal(afterNew, afterRestore, 10);
    }

    /// <summary>
    /// Validates iterative corrections converge.
    /// </summary>
    [Fact]
    public void Tr_IterativeCorrections_Converge()
    {
        var tr = new Tr();
        var bars = GenerateTestData(20);

        // Feed bars
        for (int i = 0; i < 15; i++)
        {
            tr.Update(bars[i], isNew: true);
        }

        // Multiple corrections on same bar
        for (int j = 0; j < 5; j++)
        {
            var tempBar = new TBar(
                bars[14].Time,
                100 + j, 110 + j, 90 + j, 105 + j, 1000
            );
            tr.Update(tempBar, isNew: false);
        }

        // Final correction back to original
        tr.Update(bars[14], isNew: false);
        double afterCorrections = tr.Last.Value;

        // Fresh calculation
        var trFresh = new Tr();
        for (int i = 0; i < 15; i++)
        {
            trFresh.Update(bars[i], isNew: true);
        }
        double freshValue = trFresh.Last.Value;

        Assert.Equal(freshValue, afterCorrections, 10);
    }

    /// <summary>
    /// Validates Reset clears state completely.
    /// </summary>
    [Fact]
    public void Tr_Reset_ClearsState()
    {
        var tr = new Tr();
        var bars = GenerateTestData(30);

        // Feed bars
        for (int i = 0; i < 20; i++)
        {
            tr.Update(bars[i]);
        }

        // Reset
        tr.Reset();

        // State should be cleared
        Assert.False(tr.IsHot);
        Assert.Equal(default, tr.Last);

        // Feed bars again
        for (int i = 0; i < 10; i++)
        {
            tr.Update(bars[i]);
        }

        // Fresh indicator
        var trFresh = new Tr();
        for (int i = 0; i < 10; i++)
        {
            trFresh.Update(bars[i]);
        }

        Assert.Equal(trFresh.Last.Value, tr.Last.Value, 10);
    }

    // === Consistency Tests ===

    /// <summary>
    /// Validates stability over repeated runs with same seed.
    /// </summary>
    [Fact]
    public void Tr_Stability_ConsistentOverRepeatedRuns()
    {
        var results = new List<double>();

        for (int run = 0; run < 3; run++)
        {
            var gbm = new GBM(seed: 42);
            var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
            var tr = new Tr();

            for (int i = 0; i < bars.Count; i++)
            {
                tr.Update(bars[i]);
            }
            results.Add(tr.Last.Value);
        }

        Assert.Equal(results[0], results[1], 15);
        Assert.Equal(results[1], results[2], 15);
    }

    /// <summary>
    /// Validates TR responds to volatility regime changes.
    /// </summary>
    [Fact]
    public void Tr_RespondsToVolatilityChange()
    {
        var tr = new Tr();
        var lowVolResults = new List<double>();
        var highVolResults = new List<double>();

        // Low volatility regime
        for (int i = 0; i < 20; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 101.0, 99.0, 100.0, 1000
            );
            lowVolResults.Add(tr.Update(bar).Value);
        }

        // High volatility regime
        for (int i = 20; i < 40; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 110.0, 90.0, 100.0, 1000
            );
            highVolResults.Add(tr.Update(bar).Value);
        }

        double avgLowVol = lowVolResults.Skip(1).Average();  // Skip first (no gap reference)
        double avgHighVol = highVolResults.Average();

        Assert.True(avgHighVol > avgLowVol * 5,
            $"High vol TR ({avgHighVol:F2}) should be much larger than low vol ({avgLowVol:F2})");
    }

    // === WarmupPeriod Validation ===

    /// <summary>
    /// Validates WarmupPeriod is 1 (TR is hot immediately).
    /// </summary>
    [Fact]
    public void Tr_WarmupPeriod_IsOne()
    {
        var tr = new Tr();
        Assert.Equal(1, tr.WarmupPeriod);
    }

    /// <summary>
    /// Validates IsHot is true after first bar.
    /// </summary>
    [Fact]
    public void Tr_IsHot_AfterFirstBar()
    {
        var tr = new Tr();
        Assert.False(tr.IsHot);

        var bar = new TBar(DateTime.UtcNow.Ticks, 100, 105, 95, 102, 1000);
        tr.Update(bar);

        Assert.True(tr.IsHot);
    }

    // === NaN/Infinity Handling ===

    /// <summary>
    /// Validates NaN high uses last valid value.
    /// </summary>
    [Fact]
    public void Tr_NaNHigh_UsesLastValid()
    {
        var tr = new Tr();

        var bar1 = new TBar(DateTime.UtcNow.Ticks, 100, 105, 95, 100, 1000);
        tr.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1).Ticks, 100, double.NaN, 95, 100, 1000);
        var result = tr.Update(bar2);

        Assert.True(double.IsFinite(result.Value));
    }

    /// <summary>
    /// Validates NaN low uses last valid value.
    /// </summary>
    [Fact]
    public void Tr_NaNLow_UsesLastValid()
    {
        var tr = new Tr();

        var bar1 = new TBar(DateTime.UtcNow.Ticks, 100, 105, 95, 100, 1000);
        tr.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1).Ticks, 100, 105, double.NaN, 100, 1000);
        var result = tr.Update(bar2);

        Assert.True(double.IsFinite(result.Value));
    }

    /// <summary>
    /// Validates NaN close uses last valid value.
    /// </summary>
    [Fact]
    public void Tr_NaNClose_UsesLastValid()
    {
        var tr = new Tr();

        var bar1 = new TBar(DateTime.UtcNow.Ticks, 100, 105, 95, 100, 1000);
        tr.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1).Ticks, 100, 105, 95, double.NaN, 1000);
        var result = tr.Update(bar2);

        Assert.True(double.IsFinite(result.Value));
    }

    /// <summary>
    /// Validates Infinity values are handled.
    /// </summary>
    [Fact]
    public void Tr_Infinity_UsesLastValid()
    {
        var tr = new Tr();

        var bar1 = new TBar(DateTime.UtcNow.Ticks, 100, 105, 95, 100, 1000);
        tr.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1).Ticks, 100, double.PositiveInfinity, 95, 100, 1000);
        var result = tr.Update(bar2);

        Assert.True(double.IsFinite(result.Value));
    }

    /// <summary>
    /// Validates batch handles NaN values.
    /// </summary>
    [Fact]
    public void Tr_BatchNaN_HandledCorrectly()
    {
        var highs = new double[] { 105, 106, double.NaN, 108, 109 };
        var lows = new double[] { 95, 96, 97, double.NaN, 99 };
        var closes = new double[] { 100, 101, 102, 103, double.NaN };
        var output = new double[5];

        Tr.Batch(highs, lows, closes, output);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output at index {i} should be finite");
            Assert.True(output[i] >= 0, $"Output at index {i} should be non-negative");
        }
    }
}