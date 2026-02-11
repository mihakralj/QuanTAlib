using Xunit;

namespace QuanTAlib.Tests;

public class PrsTests
{
    private const double Epsilon = 1e-10;

    // ==================== CONSTRUCTION ====================
    [Fact]
    public void Constructor_DefaultParameters()
    {
        var prs = new Prs();
        Assert.Equal("Prs", prs.Name);
        Assert.Equal(1, prs.SmoothPeriod);
    }

    [Fact]
    public void Constructor_CustomSmoothPeriod()
    {
        var prs = new Prs(10);
        Assert.Equal("Prs(10)", prs.Name);
        Assert.Equal(10, prs.SmoothPeriod);
    }

    [Fact]
    public void Constructor_ZeroPeriod_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new Prs(0));
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new Prs(-1));
    }

    // ==================== BASIC CALCULATIONS ====================
    [Fact]
    public void Update_SimpleRatio_ReturnsCorrectValue()
    {
        var prs = new Prs();
        var result = prs.Update(100.0, 50.0);
        Assert.Equal(2.0, result.Value, 10);
    }

    [Fact]
    public void Update_FractionRatio_ReturnsCorrectValue()
    {
        var prs = new Prs();
        var result = prs.Update(50.0, 100.0);
        Assert.Equal(0.5, result.Value, 10);
    }

    [Fact]
    public void Update_EqualValues_ReturnsOne()
    {
        var prs = new Prs();
        var result = prs.Update(100.0, 100.0);
        Assert.Equal(1.0, result.Value, 10);
    }

    [Fact]
    public void Update_DivisionByZero_ReturnsNaN()
    {
        var prs = new Prs();
        var result = prs.Update(100.0, 0.0);
        Assert.True(double.IsNaN(result.Value));
    }

    [Fact]
    public void RawRatio_MatchesUnsmoothedResult()
    {
        var prs = new Prs();
        prs.Update(200.0, 100.0);
        Assert.Equal(2.0, prs.RawRatio, 10);
        Assert.Equal(prs.RawRatio, prs.Last.Value, 10);
    }

    // ==================== SMOOTHING ====================
    [Fact]
    public void Update_WithSmoothing_SmoothsRatio()
    {
        var prs = new Prs(5);
        var results = new List<double>();

        // Feed increasing prices with 2:1 base ratio
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 200.0 + i;
            double compPrice = 100.0 + i * 0.5;
            prs.Update(basePrice, compPrice);
            results.Add(prs.Last.Value);
        }

        // After warmup, smoothed values should be less volatile
        Assert.True(prs.IsHot);
        Assert.True(results[^1] > 1.5);  // Base is outperforming
    }

    [Fact]
    public void Update_SmoothedVsRaw_DifferAfterMultipleUpdates()
    {
        var prsRaw = new Prs(1);
        var prsSmoothed = new Prs(10);

        var random = new Random(42);
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100.0 + random.NextDouble() * 10;
            double compPrice = 100.0 + random.NextDouble() * 10;
            prsRaw.Update(basePrice, compPrice);
            prsSmoothed.Update(basePrice, compPrice);
        }

        // Smoothed should differ from raw due to EMA averaging
        Assert.NotEqual(prsRaw.Last.Value, prsSmoothed.Last.Value, 3);
    }

    // ==================== IsHot ====================
    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var prs = new Prs(10);
        prs.Update(100.0, 50.0);
        Assert.False(prs.IsHot);
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var prs = new Prs(5);
        for (int i = 0; i < 10; i++)
        {
            prs.Update(100.0 + i, 50.0 + i);
        }
        Assert.True(prs.IsHot);
    }

    [Fact]
    public void IsHot_NoSmoothing_TrueImmediately()
    {
        var prs = new Prs(1);
        prs.Update(100.0, 50.0);
        Assert.True(prs.IsHot);
    }

    // ==================== BAR CORRECTION ====================
    [Fact]
    public void Update_BarCorrection_RestoresState()
    {
        var prs1 = new Prs(5);
        var prs2 = new Prs(5);

        // Feed same initial data
        for (int i = 0; i < 10; i++)
        {
            prs1.Update(100.0 + i, 50.0 + i);
            prs2.Update(100.0 + i, 50.0 + i);
        }

        // prs1: Add another bar
        prs1.Update(120.0, 60.0, true);

        // prs2: Add wrong bar, then correct it
        prs2.Update(999.0, 999.0, true);
        prs2.Update(120.0, 60.0, false);

        // Values should match
        Assert.Equal(prs1.Last.Value, prs2.Last.Value, 9);
    }

    [Fact]
    public void Update_MultipleCorrections_FinalValueCorrect()
    {
        var prs = new Prs(3);

        for (int i = 0; i < 5; i++)
        {
            prs.Update(100.0 + i, 50.0 + i);
        }

        // Add bar
        prs.Update(110.0, 55.0, true);

        // Multiple corrections
        prs.Update(115.0, 60.0, false);
        prs.Update(120.0, 65.0, false);
        prs.Update(110.0, 55.0, false);  // Back to original

        Assert.True(double.IsFinite(prs.Last.Value));
    }

    // ==================== RESET ====================
    [Fact]
    public void Reset_ClearsState()
    {
        var prs = new Prs(5);

        for (int i = 0; i < 10; i++)
        {
            prs.Update(100.0 + i, 50.0 + i);
        }

        Assert.NotEqual(default, prs.Last);
        Assert.True(prs.IsHot);

        prs.Reset();

        Assert.Equal(default, prs.Last);
        Assert.False(prs.IsHot);
    }

    // ==================== STATIC CALCULATE ====================
    [Fact]
    public void Calculate_TSeries_ReturnsCorrectLength()
    {
        var baseSeries = new TSeries();
        var compSeries = new TSeries();

        for (int i = 0; i < 20; i++)
        {
            baseSeries.Add(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i));
            compSeries.Add(new TValue(DateTime.Now.AddMinutes(i), 50.0 + i));
        }

        var result = Prs.Batch(baseSeries, compSeries, 5);

        Assert.Equal(baseSeries.Count, result.Count);
    }

    [Fact]
    public void Calculate_MismatchedLengths_ThrowsException()
    {
        var baseSeries = new TSeries();
        var compSeries = new TSeries();

        for (int i = 0; i < 10; i++)
        {
            baseSeries.Add(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i));
        }
        for (int i = 0; i < 5; i++)
        {
            compSeries.Add(new TValue(DateTime.Now.AddMinutes(i), 50.0 + i));
        }

        Assert.Throws<ArgumentException>(() => Prs.Batch(baseSeries, compSeries));
    }

    [Fact]
    public void Calculate_Span_MismatchedLengths_ThrowsException()
    {
        double[] baseArr = new double[10];
        double[] compArr = new double[5];
        double[] output = new double[10];

        Assert.Throws<ArgumentException>(() => Prs.Batch(baseArr, compArr, output));
    }

    [Fact]
    public void Calculate_Span_OutputMismatch_ThrowsException()
    {
        double[] baseArr = new double[10];
        double[] compArr = new double[10];
        double[] output = new double[5];

        Assert.Throws<ArgumentException>(() => Prs.Batch(baseArr, compArr, output));
    }

    [Fact]
    public void Calculate_Span_InvalidPeriod_ThrowsException()
    {
        double[] baseArr = new double[10];
        double[] compArr = new double[10];
        double[] output = new double[10];

        Assert.Throws<ArgumentException>(() => Prs.Batch(baseArr, compArr, output, 0));
    }

    // ==================== EDGE CASES ====================
    [Fact]
    public void Update_NaNInput_UsesLastValidValue()
    {
        var prs = new Prs();

        prs.Update(100.0, 50.0);
        _ = prs.Last.Value;

        var result = prs.Update(double.NaN, double.NaN);
        // Should use last valid values (100/50 pattern OR fallback)
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_InfinityInput_UsesLastValidValue()
    {
        var prs = new Prs();

        prs.Update(100.0, 50.0);

        var result = prs.Update(double.PositiveInfinity, 50.0);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_VerySmallComparison_HandlesCorrectly()
    {
        var prs = new Prs();
        var result = prs.Update(100.0, 1e-15);

        // Values below epsilon (1e-10) are treated as zero -> returns NaN
        Assert.True(double.IsNaN(result.Value));
    }

    [Fact]
    public void Update_NegativeValues_HandlesCorrectly()
    {
        var prs = new Prs();

        // Negative values (like P&L or temperature)
        var result = prs.Update(-50.0, -25.0);
        Assert.Equal(2.0, result.Value, 10);
    }

    // ==================== PRIME ====================
    [Fact]
    public void Prime_Single_ThrowsNotSupported()
    {
        var prs = new Prs(5);
        double[] data = [100, 101, 102, 103, 104];

        Assert.Throws<NotSupportedException>(() => prs.Prime(data));
    }

    [Fact]
    public void Prime_Dual_InitializesState()
    {
        var prs = new Prs(3);
        double[] baseData = [100, 102, 104, 106, 108, 110];
        double[] compData = [50, 51, 52, 53, 54, 55];

        prs.Prime(baseData, compData);

        Assert.NotEqual(default, prs.Last);
    }

    [Fact]
    public void Prime_MismatchedLengths_ThrowsException()
    {
        var prs = new Prs(3);
        double[] baseData = [100, 102, 104];
        double[] compData = [50, 51];

        Assert.Throws<ArgumentException>(() => prs.Prime(baseData, compData));
    }

    // ==================== NOT SUPPORTED ====================
    [Fact]
    public void Update_SingleInput_ThrowsNotSupported()
    {
        var prs = new Prs();
        var input = new TValue(DateTime.Now, 100.0);

        Assert.Throws<NotSupportedException>(() => prs.Update(input));
    }

    [Fact]
    public void Update_TSeries_ThrowsNotSupported()
    {
        var prs = new Prs();
        var source = new TSeries();
        source.Add(new TValue(DateTime.Now, 100.0));

        Assert.Throws<NotSupportedException>(() => prs.Update(source));
    }

    // ==================== PERFORMANCE SCENARIOS ====================
    [Fact]
    public void Update_OutperformanceScenario_IncreasingRatio()
    {
        var prs = new Prs(5);

        // Base grows faster than comparison
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100.0 + i * 2;   // +2 per bar
            double compPrice = 100.0 + i * 1;   // +1 per bar
            prs.Update(basePrice, compPrice);
        }

        // Ratio should be increasing (base outperforming)
        Assert.True(prs.Last.Value > 1.0);
    }

    [Fact]
    public void Update_UnderperformanceScenario_DecreasingRatio()
    {
        var prs = new Prs(5);

        // Base grows slower than comparison
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100.0 + i * 1;   // +1 per bar
            double compPrice = 100.0 + i * 2;   // +2 per bar
            prs.Update(basePrice, compPrice);
        }

        // Ratio should be decreasing (base underperforming)
        Assert.True(prs.Last.Value < 1.0);
    }

    [Fact]
    public void Update_ParallelMovement_StableRatio()
    {
        var prs = new Prs(5);

        // Both grow at same rate
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100.0 + i * 2;
            double compPrice = 50.0 + i * 1;
            prs.Update(basePrice, compPrice);
        }

        // Initial ratio was 2.0, should stay around there
        Assert.InRange(prs.Last.Value, 1.9, 2.1);
    }

    // ==================== BATCH CALCULATION ====================
    [Fact]
    public void Calculate_Batch_MatchesStreaming()
    {
        var baseSeries = new TSeries();
        var compSeries = new TSeries();
        var random = new Random(42);

        for (int i = 0; i < 50; i++)
        {
            baseSeries.Add(new TValue(DateTime.Now.AddMinutes(i), 100.0 + random.NextDouble() * 10));
            compSeries.Add(new TValue(DateTime.Now.AddMinutes(i), 50.0 + random.NextDouble() * 5));
        }

        // Batch calculation
        var batchResult = Prs.Batch(baseSeries, compSeries, 5);

        // Streaming calculation
        var prs = new Prs(5);
        var streamingResults = new List<double>();
        for (int i = 0; i < baseSeries.Count; i++)
        {
            streamingResults.Add(prs.Update(baseSeries[i], compSeries[i], true).Value);
        }

        // Compare
        for (int i = 0; i < baseSeries.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], streamingResults[i], 6);
        }
    }
}
