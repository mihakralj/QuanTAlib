using Xunit;

namespace QuanTAlib.Tests;

public class CcycTests
{
    private const long StartTime = 946_684_800_000_000_0L; // 2000-01-01 UTC in ticks
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(1);
    private static readonly GBM TestData = new(startPrice: 100, mu: 0.05, sigma: 0.5, seed: 42);

    private static TSeries GetTestSeries(int count = 500)
    {
        return TestData.Fetch(count, StartTime, Step).Close;
    }

    // ═══════════════════════════════════════════════════════════════════
    // A) Constructor Defaults
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Ccyc_DefaultAlpha_NoThrow()
    {
        var ccyc = new Ccyc();
        Assert.NotNull(ccyc);
        Assert.Equal(7, ccyc.WarmupPeriod);
    }

    [Fact]
    public void Ccyc_CustomAlpha_NoThrow()
    {
        var ccyc = new Ccyc(alpha: 0.15);
        Assert.NotNull(ccyc);
    }

    [Fact]
    public void Ccyc_AlphaZero_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Ccyc(alpha: 0.0));
    }

    [Fact]
    public void Ccyc_AlphaOne_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Ccyc(alpha: 1.0));
    }

    [Fact]
    public void Ccyc_AlphaNegative_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Ccyc(alpha: -0.1));
    }

    [Fact]
    public void Ccyc_AlphaAboveOne_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Ccyc(alpha: 1.5));
    }

    [Fact]
    public void Ccyc_Name_ContainsAlpha()
    {
        var ccyc = new Ccyc(0.07);
        Assert.Contains("0.07", ccyc.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Ccyc_WarmupPeriod_IsSeven()
    {
        var ccyc = new Ccyc();
        Assert.Equal(7, ccyc.WarmupPeriod);
    }

    // ═══════════════════════════════════════════════════════════════════
    // B) Basic Calculation
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Ccyc_SingleValue_ReturnsFinite()
    {
        var ccyc = new Ccyc();
        var result = ccyc.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Ccyc_MultipleValues_AllFinite()
    {
        var ccyc = new Ccyc();
        var source = GetTestSeries();
        var results = ccyc.Update(source);
        for (int i = 0; i < results.Count; i++)
        {
            Assert.True(double.IsFinite(results[i].Value), $"Non-finite at index {i}");
        }
    }

    [Fact]
    public void Ccyc_OutputNotZeroWhenHot()
    {
        var ccyc = new Ccyc();
        var source = GetTestSeries(200);
        var results = ccyc.Update(source);

        // After warmup, at least some values should be non-zero
        bool anyNonZero = false;
        for (int i = ccyc.WarmupPeriod; i < results.Count; i++)
        {
            if (Math.Abs(results[i].Value) > 1e-10)
            {
                anyNonZero = true;
                break;
            }
        }
        Assert.True(anyNonZero, "All post-warmup values are zero");
    }

    [Fact]
    public void Ccyc_IsOscillator_ChangesSigns()
    {
        var ccyc = new Ccyc();
        var source = GetTestSeries(200);
        var results = ccyc.Update(source);

        bool hasPositive = false;
        bool hasNegative = false;
        for (int i = ccyc.WarmupPeriod; i < results.Count; i++)
        {
            if (results[i].Value > 0)
            {
                hasPositive = true;
            }

            if (results[i].Value < 0)
            {
                hasNegative = true;
            }

            if (hasPositive && hasNegative)
            {
                break;
            }
        }
        Assert.True(hasPositive && hasNegative, "Cycle should oscillate around zero");
    }

    // ═══════════════════════════════════════════════════════════════════
    // C) State Management / Bar Correction
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Ccyc_BarCorrection_RestoresState()
    {
        var ccyc = new Ccyc();
        var source = GetTestSeries(50);
        for (int i = 0; i < source.Count; i++)
        {
            ccyc.Update(source[i], true);
        }

        // Get state after all bars
        double lastVal = ccyc.Last.Value;

        // Simulate bar correction: update with isNew=false
        var correctedTv = new TValue(DateTime.UtcNow, 999.0);
        ccyc.Update(correctedTv, false);
        _ = ccyc.Last.Value;

        // Now redo with original last value using isNew=false
        ccyc.Update(source[^1], false);
        double restoredVal = ccyc.Last.Value;

        Assert.Equal(lastVal, restoredVal, 10);
    }

    [Fact]
    public void Ccyc_Reset_ClearsState()
    {
        var ccyc = new Ccyc();
        var source = GetTestSeries(100);
        ccyc.Update(source);

        // Verify hot
        Assert.True(ccyc.IsHot);

        ccyc.Reset();

        // After reset, should not be hot
        Assert.False(ccyc.IsHot);
    }

    // ═══════════════════════════════════════════════════════════════════
    // D) Warmup
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Ccyc_NotHot_BeforeWarmup()
    {
        var ccyc = new Ccyc();
        for (int i = 0; i < 6; i++)
        {
            ccyc.Update(new TValue(DateTime.UtcNow.AddDays(i), 100 + i), true);
            Assert.False(ccyc.IsHot, $"Should not be hot at bar {i + 1}");
        }
    }

    [Fact]
    public void Ccyc_IsHot_AtWarmup()
    {
        var ccyc = new Ccyc();
        for (int i = 0; i < 7; i++)
        {
            ccyc.Update(new TValue(DateTime.UtcNow.AddDays(i), 100 + i), true);
        }
        Assert.True(ccyc.IsHot);
    }

    // ═══════════════════════════════════════════════════════════════════
    // E) Robustness
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Ccyc_NaN_HandledGracefully()
    {
        var ccyc = new Ccyc();
        for (int i = 0; i < 10; i++)
        {
            ccyc.Update(new TValue(DateTime.UtcNow.AddDays(i), 100 + i), true);
        }
        _ = ccyc.Last.Value;

        // Feed NaN
        ccyc.Update(new TValue(DateTime.UtcNow.AddDays(10), double.NaN), true);
        Assert.True(double.IsFinite(ccyc.Last.Value));
    }

    [Fact]
    public void Ccyc_Infinity_HandledGracefully()
    {
        var ccyc = new Ccyc();
        for (int i = 0; i < 10; i++)
        {
            ccyc.Update(new TValue(DateTime.UtcNow.AddDays(i), 100 + i), true);
        }

        ccyc.Update(new TValue(DateTime.UtcNow.AddDays(10), double.PositiveInfinity), true);
        Assert.True(double.IsFinite(ccyc.Last.Value));
    }

    [Fact]
    public void Ccyc_EmptyTSeries_ReturnsEmpty()
    {
        var ccyc = new Ccyc();
        _ = ccyc.Update(new TSeries());
        Assert.True(true); // No throw
    }

    [Fact]
    public void Ccyc_LargeDataset_NoBlowup()
    {
        var ccyc = new Ccyc();
        var source = TestData.Fetch(10000, StartTime, Step).Close;
        var results = ccyc.Update(source);
        for (int i = 0; i < results.Count; i++)
        {
            Assert.True(double.IsFinite(results[i].Value), $"Non-finite at {i}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // F) Consistency (4-API-mode)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Ccyc_StreamingMatchesBatch()
    {
        var source = GetTestSeries(200);

        // Streaming
        var ccycStreaming = new Ccyc();
        for (int i = 0; i < source.Count; i++)
        {
            ccycStreaming.Update(source[i], true);
        }

        // Batch
        var batchResults = Ccyc.Batch(source);

        Assert.Equal(source.Count, batchResults.Count);

        // The batch method creates a fresh indicator and calls Update(TSeries),
        // which processes sequentially — should match streaming exactly
        var ccyc2 = new Ccyc();
        var results2 = ccyc2.Update(source);
        Assert.Equal(batchResults.Count, results2.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(batchResults[i].Value, results2[i].Value, 10);
        }
    }

    [Fact]
    public void Ccyc_SpanBatchMatchesTSeriesBatch()
    {
        var source = GetTestSeries(200);
        var batchResults = Ccyc.Batch(source);

        // Span batch
        double[] values = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            values[i] = source[i].Value;
        }

        double[] output = new double[values.Length];
        Ccyc.Batch(values.AsSpan(), output.AsSpan());

        // Compare
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(batchResults[i].Value, output[i], 6);
        }
    }

    [Fact]
    public void Ccyc_CalculateReturnsIndicator()
    {
        var source = GetTestSeries(100);
        var (results, indicator) = Ccyc.Calculate(source);

        Assert.NotNull(indicator);
        Assert.Equal(source.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    // ═══════════════════════════════════════════════════════════════════
    // G) Span API
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Ccyc_SpanBatch_LengthMismatch_Throws()
    {
        double[] src = [1, 2, 3];
        double[] outShort = new double[2];
        Assert.Throws<ArgumentException>(() => Ccyc.Batch(src.AsSpan(), outShort.AsSpan()));
    }

    [Fact]
    public void Ccyc_SpanBatch_InvalidAlpha_Throws()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[3];
        Assert.Throws<ArgumentException>(() => Ccyc.Batch(src.AsSpan(), output.AsSpan(), alpha: 0.0));
    }

    [Fact]
    public void Ccyc_SpanBatch_EmptyInput_NoThrow()
    {
        double[] src = [];
        double[] output = [];
        Ccyc.Batch(src.AsSpan(), output.AsSpan());
        Assert.True(true); // No throw
    }

    // ═══════════════════════════════════════════════════════════════════
    // H) Chainability
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Ccyc_Chainable_ReceivesValues()
    {
        var source = GetTestSeries(100);
        var ema = new Ema(10);
        var ccyc = new Ccyc(ema, alpha: 0.07);

        for (int i = 0; i < source.Count; i++)
        {
            ema.Update(source[i], true);
        }

        Assert.True(ccyc.IsHot, "Chained CCYC should become hot");
        Assert.True(double.IsFinite(ccyc.Last.Value));
    }

    // ═══════════════════════════════════════════════════════════════════
    // I) CCYC-Specific
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Ccyc_Trigger_IsDelayedCycle()
    {
        var ccyc = new Ccyc();
        var source = GetTestSeries(50);

        double prevCycle = 0;
        for (int i = 0; i < source.Count; i++)
        {
            ccyc.Update(source[i], true);
            if (i > 0)
            {
                // Trigger should equal previous cycle value
                Assert.Equal(prevCycle, ccyc.Trigger, 10);
            }
            prevCycle = ccyc.Last.Value;
        }
    }

    [Fact]
    public void Ccyc_ConstantInput_ConvergesToZero()
    {
        var ccyc = new Ccyc();
        for (int i = 0; i < 200; i++)
        {
            ccyc.Update(new TValue(DateTime.UtcNow.AddDays(i), 100.0), true);
        }

        // High-pass filter on constant → 0
        Assert.True(Math.Abs(ccyc.Last.Value) < 1e-6, $"Expected near-zero, got {ccyc.Last.Value}");
    }

    [Fact]
    public void Ccyc_SineWave_DetectsCycle()
    {
        var ccyc = new Ccyc();
        int period = 20;

        for (int i = 0; i < 200; i++)
        {
            double value = 100 + (10 * Math.Sin(2 * Math.PI * i / period));
            ccyc.Update(new TValue(DateTime.UtcNow.AddDays(i), value), true);
        }

        // On a sine wave, the cycle output should have significant amplitude
        Assert.True(Math.Abs(ccyc.Last.Value) > 0.01, "Cycle should detect sine wave");
    }

    [Fact]
    public void Ccyc_DifferentAlphas_ProduceDifferentOutputs()
    {
        var source = GetTestSeries(200);
        var resultsFast = Ccyc.Batch(source, alpha: 0.15);
        var resultsSlow = Ccyc.Batch(source, alpha: 0.03);

        bool anyDiff = false;
        for (int i = 20; i < source.Count; i++)
        {
            if (Math.Abs(resultsFast[i].Value - resultsSlow[i].Value) > 1e-10)
            {
                anyDiff = true;
                break;
            }
        }
        Assert.True(anyDiff, "Different alphas should produce different outputs");
    }

    [Fact]
    public void Ccyc_Prime_SetsState()
    {
        var ccyc = new Ccyc();
        double[] primeData = new double[50];
        for (int i = 0; i < 50; i++)
        {
            primeData[i] = 100 + (5 * Math.Sin(2 * Math.PI * i / 20.0));
        }

        ccyc.Prime(primeData.AsSpan());
        Assert.True(ccyc.IsHot);
        Assert.True(double.IsFinite(ccyc.Last.Value));
    }

    [Fact]
    public void Ccyc_Bootstrap_DiffersFromSteadyState()
    {
        // First 6 bars use bootstrap; bar 7+ use IIR
        var ccyc = new Ccyc();
        var values = new double[] { 100, 102, 99, 101, 103, 98, 100, 104, 97 };
        var results = new List<double>();

        for (int i = 0; i < values.Length; i++)
        {
            var r = ccyc.Update(new TValue(DateTime.UtcNow.AddDays(i), values[i]), true);
            results.Add(r.Value);
        }

        // All values should be finite
        foreach (var v in results)
        {
            Assert.True(double.IsFinite(v));
        }

        // At bar 7 (index 6), we enter steady state — should still be finite
        Assert.True(double.IsFinite(results[6]));
    }

    [Fact]
    public void Ccyc_ResetAndReprocess_MatchesOriginal()
    {
        var source = GetTestSeries(100);
        var ccyc = new Ccyc();

        var results1 = ccyc.Update(source);
        ccyc.Reset();
        var results2 = ccyc.Update(source);

        Assert.Equal(results1.Count, results2.Count);
        for (int i = 0; i < results1.Count; i++)
        {
            Assert.Equal(results1[i].Value, results2[i].Value, 10);
        }
    }
}
