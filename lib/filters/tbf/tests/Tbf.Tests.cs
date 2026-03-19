namespace QuanTAlib.Tests;

public class TbfTests
{
    private static TSeries MakeSeries(int count = 500)
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.5, seed: 42);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        return bars.Close;
    }

    // ========== A. Constructor & Validation ==========

    [Fact]
    public void Tbf_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tbf(1));      // period < 2
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tbf(-1));     // period < 2
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tbf(20, 0));  // bandwidth < MinBandwidth
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tbf(20, -0.1)); // bandwidth < 0
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tbf(20, 0.1, 0)); // length < 1
    }

    [Fact]
    public void Tbf_Constructor_DefaultParameters()
    {
        var tbf = new Tbf();
        Assert.Contains("20", tbf.Name, StringComparison.Ordinal);
        Assert.Contains("0.10", tbf.Name, StringComparison.Ordinal);
        Assert.Contains("10", tbf.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Tbf_Constructor_CustomParameters()
    {
        var tbf = new Tbf(30, 0.2, 15);
        Assert.Contains("30", tbf.Name, StringComparison.Ordinal);
        Assert.Contains("0.20", tbf.Name, StringComparison.Ordinal);
        Assert.Contains("15", tbf.Name, StringComparison.Ordinal);
    }

    // ========== B. Basic Functionality ==========

    [Fact]
    public void Tbf_Update_ProducesFiniteOutput()
    {
        var tbf = new Tbf();
        TSeries series = MakeSeries(200);

        for (int i = 0; i < series.Count; i++)
        {
            tbf.Update(series[i], isNew: true);
        }

        Assert.True(double.IsFinite(tbf.Last.Value));
        Assert.True(double.IsFinite(tbf.Bp.Value));
    }

    [Fact]
    public void Tbf_ConstantInput_ProducesZero()
    {
        var tbf = new Tbf(20, 0.1, 10);
        double constant = 100.0;

        for (int i = 0; i < 100; i++)
        {
            tbf.Update(new TValue(DateTime.UtcNow, constant), isNew: true);
        }

        // Bandpass of constant = 0 (no oscillation)
        Assert.True(Math.Abs(tbf.Last.Value) < 1e-6);
        Assert.True(Math.Abs(tbf.Bp.Value) < 1e-6);
    }

    [Fact]
    public void Tbf_OutputOscillatesAroundZero()
    {
        var tbf = new Tbf();
        TSeries series = MakeSeries(500);
        int positive = 0, negative = 0;

        for (int i = 0; i < series.Count; i++)
        {
            tbf.Update(series[i], isNew: true);
            if (tbf.IsHot)
            {
                if (tbf.Last.Value > 0)
                {
                    positive++;
                }
                else if (tbf.Last.Value < 0)
                {
                    negative++;
                }
            }
        }

        // Both positive and negative values should exist
        Assert.True(positive > 0, "Should have positive values");
        Assert.True(negative > 0, "Should have negative values");
    }

    // ========== C. Warmup & IsHot ==========

    [Fact]
    public void Tbf_IsHot_WhenBarsReachWarmupPeriod()
    {
        var tbf = new Tbf(20, 0.1, 10);
        int warmup = 10 + 2; // length + 2

        for (int i = 0; i < warmup - 1; i++)
        {
            tbf.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
            Assert.False(tbf.IsHot, $"Should be cold at bar {i}");
        }

        tbf.Update(new TValue(DateTime.UtcNow, 200.0), isNew: true);
        Assert.True(tbf.IsHot, "Should be hot after warmup period");
    }

    [Fact]
    public void Tbf_WarmupPeriod_EqualsLengthPlus2()
    {
        var tbf = new Tbf(20, 0.1, 15);
        Assert.Equal(17, tbf.WarmupPeriod); // 15 + 2

        var tbf2 = new Tbf(20, 0.1, 5);
        Assert.Equal(7, tbf2.WarmupPeriod); // 5 + 2
    }

    // ========== D. Bar Correction ==========

    [Fact]
    public void Tbf_BarCorrection_RestoresState()
    {
        var tbf = new Tbf();
        TSeries series = MakeSeries(50);

        // Feed first 40 bars
        for (int i = 0; i < 40; i++)
        {
            tbf.Update(series[i], isNew: true);
        }

        // Feed bar 41 as new
        tbf.Update(new TValue(DateTime.UtcNow, 999.0), isNew: true);
        double valueBadBar = tbf.Last.Value;

        // Correct bar 41 with proper value
        tbf.Update(series[40], isNew: false);
        double valueCorrected = tbf.Last.Value;

        // The bad bar should differ from the corrected one
        Assert.NotEqual(valueBadBar, valueCorrected);
    }

    [Fact]
    public void Tbf_BarCorrection_MultipleCorrectionsSameResult()
    {
        var tbf = new Tbf();
        TSeries series = MakeSeries(50);

        for (int i = 0; i < 40; i++)
        {
            tbf.Update(series[i], isNew: true);
        }

        // Correct the same bar multiple times
        tbf.Update(series[40], isNew: true);
        tbf.Update(new TValue(DateTime.UtcNow, 123.0), isNew: false);
        tbf.Update(new TValue(DateTime.UtcNow, 456.0), isNew: false);
        tbf.Update(series[40], isNew: false);
        double finalValue = tbf.Last.Value;

        // Fresh indicator for comparison
        var tbf2 = new Tbf();
        for (int i = 0; i <= 40; i++)
        {
            tbf2.Update(series[i], isNew: true);
        }

        Assert.Equal(tbf2.Last.Value, finalValue, 10);
    }

    // ========== E. Reset ==========

    [Fact]
    public void Tbf_Reset_ClearsState()
    {
        var tbf = new Tbf();
        TSeries series = MakeSeries(50);

        for (int i = 0; i < 50; i++)
        {
            tbf.Update(series[i], isNew: true);
        }

        tbf.Reset();
        Assert.False(tbf.IsHot);
    }

    [Fact]
    public void Tbf_Reset_ProducesSameResultsOnReplay()
    {
        var tbf = new Tbf();
        TSeries series = MakeSeries(100);

        for (int i = 0; i < series.Count; i++)
        {
            tbf.Update(series[i], isNew: true);
        }
        double firstRun = tbf.Last.Value;

        tbf.Reset();
        for (int i = 0; i < series.Count; i++)
        {
            tbf.Update(series[i], isNew: true);
        }
        double secondRun = tbf.Last.Value;

        Assert.Equal(firstRun, secondRun, 10);
    }

    // ========== F. NaN / Infinity Handling ==========

    [Fact]
    public void Tbf_NaN_UsesLastValidValue()
    {
        var tbf = new Tbf(20, 0.1, 5);

        for (int i = 0; i < 20; i++)
        {
            tbf.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        tbf.Update(new TValue(DateTime.UtcNow, double.NaN), isNew: true);
        Assert.True(double.IsFinite(tbf.Last.Value));
    }

    [Fact]
    public void Tbf_Infinity_UsesLastValidValue()
    {
        var tbf = new Tbf(20, 0.1, 5);

        for (int i = 0; i < 20; i++)
        {
            tbf.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        tbf.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity), isNew: true);
        Assert.True(double.IsFinite(tbf.Last.Value));
    }

    // ========== G. All-Modes Consistency ==========

    [Fact]
    public void Tbf_AllModes_ProduceSameResult()
    {
        int period = 20;
        double bandwidth = 0.1;
        int length = 10;
        TSeries series = MakeSeries(200);

        // Mode 1: Streaming
        var streaming = new Tbf(period, bandwidth, length);
        for (int i = 0; i < series.Count; i++)
        {
            streaming.Update(series[i], isNew: true);
        }

        // Mode 2: Batch TSeries
        var batchResult = Tbf.Batch(series, period, bandwidth, length);

        // Mode 3: Span-based
        double[] source = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            source[i] = series[i].Value;
        }
        double[] spanTbf = new double[series.Count];
        double[] spanBp = new double[series.Count];
        Tbf.Batch(source.AsSpan(), spanTbf.AsSpan(), spanBp.AsSpan(), period, bandwidth, length);

        // Compare all modes
        Assert.Equal(streaming.Last.Value, batchResult[^1].Value, 8);
        Assert.Equal(streaming.Last.Value, spanTbf[^1], 8);
    }

    [Fact]
    public void Tbf_SpanBatch_ValidatesInputs()
    {
        double[] source = new double[10];
        double[] tbfOut = new double[10];
        double[] bpOut = new double[10];
        double[] wrongSize = new double[5];

        Assert.Throws<ArgumentException>(() =>
            Tbf.Batch(source.AsSpan(), wrongSize.AsSpan(), bpOut.AsSpan()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Tbf.Batch(source.AsSpan(), tbfOut.AsSpan(), bpOut.AsSpan(), period: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Tbf.Batch(source.AsSpan(), tbfOut.AsSpan(), bpOut.AsSpan(), bandwidth: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Tbf.Batch(source.AsSpan(), tbfOut.AsSpan(), bpOut.AsSpan(), length: 0));
    }

    // ========== H. Static Calculate Method ==========

    [Fact]
    public void Tbf_StaticCalculate_Works()
    {
        TSeries series = MakeSeries(200);
        var (results, indicator) = Tbf.Calculate(series, 20, 0.1, 10);

        Assert.Equal(series.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    // ========== Behavioral Tests ==========

    [Fact]
    public void Tbf_TruncatedVsStandard_DifferAfterShock()
    {
        var tbf = new Tbf(20, 0.1, 10);

        // Feed stable prices
        for (int i = 0; i < 50; i++)
        {
            tbf.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        }

        // Price shock
        tbf.Update(new TValue(DateTime.UtcNow, 200.0), isNew: true);

        // Feed more stable prices - the truncated version should recover faster
        for (int i = 0; i < 30; i++)
        {
            tbf.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        }

        // After 30 bars of constant price post-shock, truncated should be dampened
        // (closer to zero than standard) because the shock is beyond the truncation window
        Assert.True(Math.Abs(tbf.Last.Value) < Math.Abs(tbf.Bp.Value) + 1e-3,
            "Truncated BP should recover from shock faster than standard BP");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void Tbf_DifferentLengths_AllProduceValidOutput(int length)
    {
        var tbf = new Tbf(20, 0.1, length);
        TSeries series = MakeSeries(200);

        for (int i = 0; i < series.Count; i++)
        {
            tbf.Update(series[i], isNew: true);
        }

        Assert.True(double.IsFinite(tbf.Last.Value));
        Assert.True(tbf.IsHot);
    }

    [Theory]
    [InlineData(10, 0.1)]
    [InlineData(20, 0.1)]
    [InlineData(30, 0.2)]
    [InlineData(50, 0.3)]
    public void Tbf_DifferentPeriodBandwidth_AllProduceValidOutput(int period, double bandwidth)
    {
        var tbf = new Tbf(period, bandwidth, 10);
        TSeries series = MakeSeries(200);

        for (int i = 0; i < series.Count; i++)
        {
            tbf.Update(series[i], isNew: true);
        }

        Assert.True(double.IsFinite(tbf.Last.Value));
    }

    [Fact]
    public void Tbf_SineInput_DetectsCycle()
    {
        var tbf = new Tbf(20, 0.3, 25); // wider bandwidth to capture
        double period = 20.0;
        int totalBars = 200;

        for (int i = 0; i < totalBars; i++)
        {
            double value = Math.Sin(2.0 * Math.PI * i / period);
            tbf.Update(new TValue(DateTime.UtcNow, value), isNew: true);
        }

        // The filter should produce non-trivial output for a sine at the center period
        // After warmup, the output should be significant
        Assert.True(Math.Abs(tbf.Last.Value) > 0.01,
            "TBF should detect cycle at center period");
    }

    [Fact]
    public void Tbf_UpdateTSeries_ReturnsCorrectCount()
    {
        var tbf = new Tbf();
        TSeries series = MakeSeries(100);
        TSeries result = tbf.Update(series);

        Assert.Equal(series.Count, result.Count);
    }

    [Fact]
    public void Tbf_NullSource_Throws()
    {
        var tbf = new Tbf();
        Assert.Throws<ArgumentNullException>(() => tbf.Update((TSeries)null!));
    }

    [Fact]
    public void Tbf_Prime_InitializesState()
    {
        var tbf = new Tbf(20, 0.1, 10);
        double[] primeData = new double[50];
        for (int i = 0; i < 50; i++)
        {
            primeData[i] = 100.0 + Math.Sin(i * 0.3);
        }

        tbf.Prime(primeData);
        Assert.True(tbf.IsHot);
        Assert.True(double.IsFinite(tbf.Last.Value));
    }

    [Fact]
    public void Tbf_Bp_AlsoProducesOutput()
    {
        var tbf = new Tbf();
        TSeries series = MakeSeries(100);

        for (int i = 0; i < series.Count; i++)
        {
            tbf.Update(series[i], isNew: true);
        }

        // Standard BP should also have finite output
        Assert.True(double.IsFinite(tbf.Bp.Value));
    }
}
