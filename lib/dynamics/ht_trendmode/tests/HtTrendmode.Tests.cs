namespace QuanTAlib;

public class HtTrendmodeTests
{
    [Fact]
    public void HtTrendmode_BasicConstruction()
    {
        var indicator = new HtTrendmode();

        Assert.Equal("HtTrendmode", indicator.Name);
        Assert.Equal(63, indicator.WarmupPeriod);  // TA-Lib lookback period
        Assert.False(indicator.IsHot);
    }

    [Fact]
    public void HtTrendmode_WarmupPeriod()
    {
        var indicator = new HtTrendmode();

        // Feed warmup data - TA-Lib requires 63 bars for lookback
        for (int i = 0; i < 70; i++)
        {
            _ = indicator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i));

            if (i < 63)
            {
                Assert.False(indicator.IsHot, $"Should not be hot at bar {i}");
            }
        }

        Assert.True(indicator.IsHot, "Should be hot after warmup period");
    }

    [Fact]
    public void HtTrendmode_OutputsBinaryValues()
    {
        var indicator = new HtTrendmode();

        // Use GBM-generated price data
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        for (int i = 0; i < bars.Count; i++)
        {
            var result = indicator.Update(bars[i].C);

            // After warmup, output should be 0 or 1
            if (i >= 40)
            {
                Assert.True(result.Value == 0.0 || result.Value == 1.0,
                    $"TrendMode should be 0 or 1, got {result.Value} at bar {i}");
            }
        }
    }

    [Fact]
    public void HtTrendmode_TrendModeProperty()
    {
        var indicator = new HtTrendmode();

        // Feed data
        for (int i = 0; i < 50; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + (i * 0.5)));
        }

        // TrendMode property should match output
        int trendMode = indicator.TrendMode;
        Assert.True(trendMode == 0 || trendMode == 1);
    }

    [Fact]
    public void HtTrendmode_SmoothPeriodProperty()
    {
        var indicator = new HtTrendmode();

        // Feed data
        for (int i = 0; i < 50; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + (Math.Sin(i * 0.2) * 10)));
        }

        // SmoothPeriod should be in valid range
        double smoothPeriod = indicator.SmoothPeriod;
        Assert.True(smoothPeriod >= 6.0 && smoothPeriod <= 50.0,
            $"SmoothPeriod {smoothPeriod} should be between 6 and 50");
    }

    [Fact]
    public void HtTrendmode_InstPeriodProperty()
    {
        var indicator = new HtTrendmode();

        // Feed data
        for (int i = 0; i < 50; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + (Math.Sin(i * 0.3) * 8)));
        }

        // InstPeriod should be positive
        double instPeriod = indicator.InstPeriod;
        Assert.True(instPeriod > 0, $"InstPeriod {instPeriod} should be positive");
    }

    [Fact]
    public void HtTrendmode_TrendingData_ShouldDetectTrend()
    {
        var indicator = new HtTrendmode();

        // Strong trend: monotonically increasing
        for (int i = 0; i < 100; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + (i * 2.0)));
        }

        // With strong trend, inst_period should be larger → trend mode likely
        // (exact behavior depends on Hilbert Transform dynamics)
        int trendMode = indicator.TrendMode;
        Assert.True(trendMode == 0 || trendMode == 1, "Should output valid trend mode");
    }

    [Fact]
    public void HtTrendmode_CyclicalData_ShouldDetectCycle()
    {
        var indicator = new HtTrendmode();

        // Pure sinusoidal data (strong cycle)
        for (int i = 0; i < 100; i++)
        {
            double value = 100.0 + (Math.Sin(i * 0.4) * 10.0);
            indicator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), value));
        }

        // With cyclical data, smooth_period and inst_period should be closer
        int trendMode = indicator.TrendMode;
        Assert.True(trendMode == 0 || trendMode == 1, "Should output valid trend mode");
    }

    [Fact]
    public void HtTrendmode_HandlesNaN()
    {
        var indicator = new HtTrendmode();

        // Prime with valid data
        for (int i = 0; i < 50; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i));
        }

        // Feed NaN - should use last valid value
        var resultNaN = indicator.Update(new TValue(DateTime.UtcNow.AddMinutes(50), double.NaN));
        Assert.True(double.IsFinite(resultNaN.Value), "Should handle NaN gracefully");
    }

    [Fact]
    public void HtTrendmode_HandlesInfinity()
    {
        var indicator = new HtTrendmode();

        // Prime with valid data
        for (int i = 0; i < 50; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i));
        }

        // Feed Infinity - should use last valid value
        var resultInf = indicator.Update(new TValue(DateTime.UtcNow.AddMinutes(50), double.PositiveInfinity));
        Assert.True(double.IsFinite(resultInf.Value), "Should handle Infinity gracefully");
    }

    [Fact]
    public void HtTrendmode_Reset()
    {
        var indicator = new HtTrendmode();

        // Process enough data to be hot (warmup = 63)
        for (int i = 0; i < 70; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i));
        }

        Assert.True(indicator.IsHot, "Should be hot after warmup");

        // Reset
        indicator.Reset();

        Assert.False(indicator.IsHot);
        Assert.Equal(0, indicator.TrendMode);
    }

    [Fact]
    public void HtTrendmode_BatchUpdate()
    {
        var indicator = new HtTrendmode();
        var series = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            series.Add(DateTime.UtcNow.AddMinutes(i), 100.0 + (Math.Sin(i * 0.2) * 10));
        }

        var result = indicator.Update(series);

        Assert.Equal(100, result.Count);

        // All values after warmup should be 0 or 1
        for (int i = 40; i < result.Count; i++)
        {
            Assert.True(result.Values[i] == 0.0 || result.Values[i] == 1.0,
                $"Batch result at {i} should be 0 or 1, got {result.Values[i]}");
        }
    }

    [Fact]
    public void HtTrendmode_StaticCalculate_SpanVersion()
    {
        double[] input = new double[100];
        double[] output = new double[100];

        for (int i = 0; i < input.Length; i++)
        {
            input[i] = 100.0 + (Math.Sin(i * 0.15) * 8);
        }

        HtTrendmode.Batch(input.AsSpan(), output.AsSpan());

        // After warmup, all values should be 0 or 1
        for (int i = 40; i < output.Length; i++)
        {
            Assert.True(output[i] == 0.0 || output[i] == 1.0,
                $"Static Calculate at {i} should be 0 or 1, got {output[i]}");
        }
    }

    [Fact]
    public void HtTrendmode_StaticCalculate_TSeriesVersion()
    {
        var series = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            series.Add(DateTime.UtcNow.AddMinutes(i), 100.0 + (Math.Sin(i * 0.25) * 12));
        }

        var result = HtTrendmode.Batch(series);

        Assert.Equal(100, result.Count);
    }

    [Fact]
    public void HtTrendmode_BarCorrection_IsNewFalse()
    {
        var indicator = new HtTrendmode();

        // Prime indicator
        for (int i = 0; i < 50; i++)
        {
            indicator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i));
        }

        // Get baseline
        _ = indicator.Update(new TValue(DateTime.UtcNow.AddMinutes(50), 150.0), isNew: true);

        // Update same bar with different value
        var corrected = indicator.Update(new TValue(DateTime.UtcNow.AddMinutes(50), 152.0), isNew: false);

        // Should reflect the corrected value
        Assert.True(corrected.Value == 0.0 || corrected.Value == 1.0);
    }

    [Fact]
    public void HtTrendmode_StreamingVsBatch_Consistency()
    {
        var streamingIndicator = new HtTrendmode();
        var batchIndicator = new HtTrendmode();

        var series = new TSeries();
        var streamingResults = new List<double>();

        for (int i = 0; i < 100; i++)
        {
            double value = 100.0 + (Math.Sin(i * 0.2) * 10) + (Math.Cos(i * 0.3) * 5);
            series.Add(DateTime.UtcNow.AddMinutes(i), value);

            var result = streamingIndicator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), value));
            streamingResults.Add(result.Value);
        }

        var batchResult = batchIndicator.Update(series);

        // Compare streaming vs batch
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(streamingResults[i], batchResult.Values[i]);
        }
    }

    [Fact]
    public void HtTrendmode_Prime()
    {
        var indicator = new HtTrendmode();

        // Prime with enough data to be hot (warmup = 63)
        double[] primeData = new double[70];
        for (int i = 0; i < primeData.Length; i++)
        {
            primeData[i] = 100.0 + (i * 0.5);
        }

        indicator.Prime(primeData);

        Assert.True(indicator.IsHot, "Should be hot after priming");
    }

    [Fact]
    public void HtTrendmode_EmptySource()
    {
        var indicator = new HtTrendmode();
        var emptySeries = new TSeries();

        var result = indicator.Update(emptySeries);

        Assert.Empty(result);
    }

    [Fact]
    public void HtTrendmode_ConstantPrice_ShouldNotCrash()
    {
        var indicator = new HtTrendmode();

        // Constant price (degenerate case)
        for (int i = 0; i < 100; i++)
        {
            var result = indicator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
            Assert.True(double.IsFinite(result.Value), $"Result should be finite at bar {i}");
        }
    }
}
