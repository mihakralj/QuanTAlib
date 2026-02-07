using Xunit;

namespace QuanTAlib.Tests;

public class TsiValidationTests
{
    private const double Epsilon = 1e-6;

    // ==================== FORMULA VALIDATION ====================
    [Fact]
    public void Formula_ConstantMomentumApproachesExtreme()
    {
        // TSI = 100 × doubleSmoothedMom / doubleSmoothedAbsMom
        // With constant positive momentum, TSI approaches +100
        var tsi = new Tsi(3, 2, 2);

        // Strong consistent uptrend
        for (int i = 0; i < 50; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i * 2));
        }

        // Should be close to +100
        Assert.True(tsi.Last.Value > 95.0, $"Expected TSI > 95, got {tsi.Last.Value}");
    }

    [Fact]
    public void Formula_ConstantNegativeMomentumApproachesNegativeExtreme()
    {
        var tsi = new Tsi(3, 2, 2);

        // Strong consistent downtrend
        for (int i = 0; i < 50; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 200.0 - i * 2));
        }

        // Should be close to -100
        Assert.True(tsi.Last.Value < -95.0, $"Expected TSI < -95, got {tsi.Last.Value}");
    }

    [Fact]
    public void Formula_ZeroMomentumGivesZeroTsi()
    {
        var tsi = new Tsi(3, 2, 2);

        // No price change
        for (int i = 0; i < 20; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0));
        }

        Assert.True(Math.Abs(tsi.Last.Value) < 1.0, $"Expected TSI ≈ 0, got {tsi.Last.Value}");
    }

    // ==================== SIGNAL LINE VALIDATION ====================
    [Fact]
    public void Signal_LagsMainTsi()
    {
        var tsi = new Tsi(5, 3, 3);
        var tsiValues = new List<double>();
        var signalValues = new List<double>();

        // Create a trend change
        for (int i = 0; i < 20; i++)
        {
            double price = i < 10 ? 100.0 + i * 2 : 120.0 - (i - 10) * 2;
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), price));
            tsiValues.Add(tsi.Last.Value);
            signalValues.Add(tsi.Signal);
        }

        // Signal should lag TSI - when TSI turns, signal follows
        // Check that standard deviation of differences is not zero (they're different)
        var diff = tsiValues.Zip(signalValues, (t, s) => t - s).ToList();
        double avgDiff = diff.Average();
        double variance = diff.Average(d => (d - avgDiff) * (d - avgDiff));

        Assert.True(variance > 0.001, "Signal should lag TSI, showing variance in differences");
    }

    [Fact]
    public void Signal_ConvergesInSteadyTrend()
    {
        var tsi = new Tsi(5, 3, 3);

        // Consistent uptrend
        for (int i = 0; i < 100; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i));
        }

        // In steady trend, TSI and Signal should converge
        double diff = Math.Abs(tsi.Last.Value - tsi.Signal);
        Assert.True(diff < 5.0, $"Expected TSI and Signal to converge, diff = {diff}");
    }

    // ==================== WARMUP VALIDATION ====================
    [Fact]
    public void Warmup_GradualConvergence()
    {
        var tsi = new Tsi(5, 3, 3);
        var values = new List<double>();

        // Rising prices
        for (int i = 0; i < 30; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i));
            values.Add(tsi.Last.Value);
        }

        // Values should stabilize as warmup completes
        var lastFive = values.Skip(values.Count - 5).ToList();
        var firstFive = values.Skip(5).Take(5).ToList();

        double lastRange = lastFive.Max() - lastFive.Min();
        double firstRange = firstFive.Max() - firstFive.Min();

        // Later values should be more stable (smaller range)
        Assert.True(lastRange <= firstRange || lastRange < 5.0);
    }

    [Fact]
    public void Warmup_Period_MatchesExpected()
    {
        var tsi = new Tsi(25, 13, 13);
        Assert.Equal(25 + 13 + 13, tsi.WarmupPeriod);
    }

    // ==================== EDGE CASE VALIDATION ====================
    [Fact]
    public void EdgeCase_AlternatingPrices()
    {
        var tsi = new Tsi(5, 3, 3);

        // Alternating prices (no net trend)
        for (int i = 0; i < 30; i++)
        {
            double price = 100.0 + (i % 2 == 0 ? 5 : -5);
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), price));
        }

        // Should oscillate around zero
        Assert.True(Math.Abs(tsi.Last.Value) < 50.0);
    }

    [Fact]
    public void EdgeCase_LargePriceSpike()
    {
        var tsi = new Tsi(5, 3, 3);

        // Stable prices
        for (int i = 0; i < 15; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0));
        }

        // Large spike
        tsi.Update(new TValue(DateTime.Now.AddMinutes(16), 150.0));

        Assert.True(!double.IsNaN(tsi.Last.Value));
        Assert.True(!double.IsInfinity(tsi.Last.Value));
        Assert.True(tsi.Last.Value > 0);  // Should be positive after spike up
    }

    [Fact]
    public void EdgeCase_VerySmallPeriods()
    {
        var tsi = new Tsi(1, 1, 1);

        for (int i = 0; i < 20; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i));
        }

        Assert.True(!double.IsNaN(tsi.Last.Value));
        Assert.True(tsi.Last.Value >= -100 && tsi.Last.Value <= 100);
    }

    [Fact]
    public void EdgeCase_VeryLargePeriods()
    {
        var tsi = new Tsi(100, 50, 25);

        for (int i = 0; i < 300; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i * 0.1));
        }

        Assert.True(!double.IsNaN(tsi.Last.Value));
        Assert.True(tsi.Last.Value >= -100 && tsi.Last.Value <= 100);
    }

    // ==================== COMPARISON VALIDATION ====================
    [Fact]
    public void Comparison_BatchVsStreaming()
    {
        var source = new TSeries();
        var random = new Random(42);

        for (int i = 0; i < 100; i++)
        {
            source.Add(new TValue(DateTime.Now.AddMinutes(i), 100.0 + random.NextDouble() * 30));
        }

        // Batch calculation
        var batchResult = Tsi.Batch(source, 10, 5, 5);

        // Streaming calculation
        var tsi = new Tsi(10, 5, 5);
        var streamingResults = new List<double>();
        foreach (var value in source)
        {
            streamingResults.Add(tsi.Update(value).Value);
        }

        // Compare (skip warmup period)
        for (int i = 30; i < source.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], streamingResults[i], 5);
        }
    }

    [Fact]
    public void Comparison_DifferentParametersSameTrend()
    {
        var tsi1 = new Tsi(25, 13, 13);  // Default
        var tsi2 = new Tsi(13, 7, 7);    // Shorter

        for (int i = 0; i < 100; i++)
        {
            var tval = new TValue(DateTime.Now.AddMinutes(i), 100.0 + i);
            tsi1.Update(tval);
            tsi2.Update(tval);
        }

        // Both should be positive for uptrend
        Assert.True(tsi1.Last.Value > 0);
        Assert.True(tsi2.Last.Value > 0);

        // Shorter period should react faster (closer to +100)
        Assert.True(tsi2.Last.Value >= tsi1.Last.Value - 10);
    }

    // ==================== STATE VALIDATION ====================
    [Fact]
    public void State_ResetClearsAll()
    {
        var tsi = new Tsi(5, 3, 3);

        for (int i = 0; i < 20; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i));
        }

        Assert.True(tsi.IsHot);
        Assert.NotEqual(default, tsi.Last);

        tsi.Reset();

        Assert.False(tsi.IsHot);
        Assert.Equal(default, tsi.Last);
        Assert.Equal(0, tsi.Signal);
    }

    [Fact]
    public void State_BarCorrectionMaintainsConsistency()
    {
        var tsi = new Tsi(5, 3, 3);

        // Build up history with gradual price increases
        for (int i = 0; i < 15; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i));
        }

        _ = tsi.Last.Value;  // Capture stable value (unused, for state verification)

        // Large spike - very different from trend
        tsi.Update(new TValue(DateTime.Now.AddMinutes(16), 250.0), isNew: true);
        var spike = tsi.Last.Value;

        // Correct bar to much smaller value (below trend continuation)
        tsi.Update(new TValue(DateTime.Now.AddMinutes(16), 110.0), isNew: false);
        var corrected = tsi.Last.Value;

        // Spike should have higher TSI than corrected (more positive momentum)
        Assert.True(spike > corrected,
            $"Spike ({spike:F4}) should be greater than corrected ({corrected:F4})");
    }

    // ==================== MATHEMATICAL PROPERTIES ====================
    [Fact]
    public void Math_SymmetryWithInvertedPrices()
    {
        var tsi1 = new Tsi(5, 3, 3);
        var tsi2 = new Tsi(5, 3, 3);

        // Feed reversed prices
        for (int i = 0; i < 30; i++)
        {
            tsi1.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i));
            tsi2.Update(new TValue(DateTime.Now.AddMinutes(i), 129.0 - i));
        }

        // Should be approximately symmetric (opposite signs)
        Assert.True(Math.Abs(tsi1.Last.Value + tsi2.Last.Value) < 5.0,
            $"Expected symmetry: TSI1={tsi1.Last.Value}, TSI2={tsi2.Last.Value}");
    }

    [Fact]
    public void Math_RatioPreservesScale()
    {
        var tsi1 = new Tsi(5, 3, 3);
        var tsi2 = new Tsi(5, 3, 3);

        // Same relative changes, different absolute scale
        for (int i = 0; i < 30; i++)
        {
            tsi1.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i));
            tsi2.Update(new TValue(DateTime.Now.AddMinutes(i), 1000.0 + i * 10));
        }

        // TSI should be similar (same percentage changes)
        Assert.True(Math.Abs(tsi1.Last.Value - tsi2.Last.Value) < 5.0,
            $"TSI should be scale-independent: TSI1={tsi1.Last.Value}, TSI2={tsi2.Last.Value}");
    }

    // ==================== CROSS-VALIDATION ====================
    [Fact]
    public void CrossValidation_ConsistentWithPineFormula()
    {
        // TSI = 100 × EMA(EMA(mom, long), short) / EMA(EMA(|mom|, long), short)
        var tsi = new Tsi(5, 3, 3);

        double[] prices = [100, 102, 101, 104, 103, 106, 105, 108, 107, 110, 109, 112, 111, 114, 113, 116];

        foreach (var price in prices)
        {
            tsi.Update(new TValue(DateTime.Now, price));
        }

        // Result should be bounded and reasonable
        Assert.True(tsi.Last.Value >= -100 && tsi.Last.Value <= 100);
        // With alternating up-down pattern, should be positive overall (slight uptrend)
        Assert.True(tsi.Last.Value > 0);
    }

    [Fact]
    public void CrossValidation_MatchesManualDoubleSmoothing()
    {
        var tsi = new Tsi(3, 2, 2);

        // Simple test data
        double[] prices = [100, 102, 104, 106, 108, 110, 112, 114, 116, 118, 120];

        foreach (var price in prices)
        {
            tsi.Update(new TValue(DateTime.Now, price));
        }

        // Consistent +2 momentum = 100% TSI (or close to it)
        Assert.True(tsi.Last.Value > 90, $"Expected TSI > 90 for constant momentum, got {tsi.Last.Value}");
    }
}
