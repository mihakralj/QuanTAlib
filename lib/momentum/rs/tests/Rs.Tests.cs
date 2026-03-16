using Xunit;

namespace QuanTAlib.Tests;

public class RsTests
{
    private const double Epsilon = 1e-10;

    // ==================== CONSTRUCTION ====================
    [Fact]
    public void Constructor_DefaultParameters()
    {
        var rs = new Rs();
        Assert.Equal("Rs", rs.Name);
        Assert.Equal(1, rs.SmoothPeriod);
    }

    [Fact]
    public void Constructor_CustomSmoothPeriod()
    {
        var rs = new Rs(10);
        Assert.Equal("Rs(10)", rs.Name);
        Assert.Equal(10, rs.SmoothPeriod);
    }

    [Fact]
    public void Constructor_ZeroPeriod_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new Rs(0));
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new Rs(-1));
    }

    // ==================== BASIC CALCULATIONS ====================
    [Fact]
    public void Update_SimpleRatio_ReturnsCorrectValue()
    {
        var rs = new Rs();
        var result = rs.Update(100.0, 50.0);
        Assert.Equal(2.0, result.Value, 10);
    }

    [Fact]
    public void Update_FractionRatio_ReturnsCorrectValue()
    {
        var rs = new Rs();
        var result = rs.Update(50.0, 100.0);
        Assert.Equal(0.5, result.Value, 10);
    }

    [Fact]
    public void Update_EqualValues_ReturnsOne()
    {
        var rs = new Rs();
        var result = rs.Update(100.0, 100.0);
        Assert.Equal(1.0, result.Value, 10);
    }

    [Fact]
    public void Update_DivisionByZero_ReturnsNaN()
    {
        var rs = new Rs();
        var result = rs.Update(100.0, 0.0);
        Assert.True(double.IsNaN(result.Value));
    }

    [Fact]
    public void RawRatio_MatchesUnsmoothedResult()
    {
        var rs = new Rs();
        rs.Update(200.0, 100.0);
        Assert.Equal(2.0, rs.RawRatio, 10);
        Assert.Equal(rs.RawRatio, rs.Last.Value, 10);
    }

    // ==================== SMOOTHING ====================
    [Fact]
    public void Update_WithSmoothing_SmoothsRatio()
    {
        var rs = new Rs(5);
        var results = new List<double>();

        // Feed increasing prices with 2:1 base ratio
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 200.0 + i;
            double compPrice = 100.0 + (i * 0.5);
            rs.Update(basePrice, compPrice);
            results.Add(rs.Last.Value);
        }

        // After warmup, smoothed values should be less volatile
        Assert.True(rs.IsHot);
        Assert.True(results[^1] > 1.5);  // Base is outperforming
    }

    [Fact]
    public void Update_SmoothedVsRaw_DifferAfterMultipleUpdates()
    {
        var prsRaw = new Rs(1);
        var prsSmoothed = new Rs(10);

        var baseBars = new GBM(seed: 42).Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var compBars = new GBM(seed: 123).Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        for (int i = 0; i < 30; i++)
        {
            prsRaw.Update(baseBars.Close[i], compBars.Close[i]);
            prsSmoothed.Update(baseBars.Close[i], compBars.Close[i]);
        }

        // Smoothed should differ from raw due to EMA averaging
        Assert.NotEqual(prsRaw.Last.Value, prsSmoothed.Last.Value, 3);
    }

    // ==================== IsHot ====================
    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var rs = new Rs(10);
        rs.Update(100.0, 50.0);
        Assert.False(rs.IsHot);
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var rs = new Rs(5);
        for (int i = 0; i < 10; i++)
        {
            rs.Update(100.0 + i, 50.0 + i);
        }
        Assert.True(rs.IsHot);
    }

    [Fact]
    public void IsHot_NoSmoothing_TrueImmediately()
    {
        var rs = new Rs(1);
        rs.Update(100.0, 50.0);
        Assert.True(rs.IsHot);
    }

    // ==================== BAR CORRECTION ====================
    [Fact]
    public void Update_BarCorrection_RestoresState()
    {
        var prs1 = new Rs(5);
        var prs2 = new Rs(5);

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
        var rs = new Rs(3);

        for (int i = 0; i < 5; i++)
        {
            rs.Update(100.0 + i, 50.0 + i);
        }

        // Add bar
        rs.Update(110.0, 55.0, true);

        // Multiple corrections
        rs.Update(115.0, 60.0, false);
        rs.Update(120.0, 65.0, false);
        rs.Update(110.0, 55.0, false);  // Back to original

        Assert.True(double.IsFinite(rs.Last.Value));
    }

    // ==================== RESET ====================
    [Fact]
    public void Reset_ClearsState()
    {
        var rs = new Rs(5);

        for (int i = 0; i < 10; i++)
        {
            rs.Update(100.0 + i, 50.0 + i);
        }

        Assert.NotEqual(default, rs.Last);
        Assert.True(rs.IsHot);

        rs.Reset();

        Assert.Equal(default, rs.Last);
        Assert.False(rs.IsHot);
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

        var result = Rs.Batch(baseSeries, compSeries, 5);

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

        Assert.Throws<ArgumentException>(() => Rs.Batch(baseSeries, compSeries));
    }

    [Fact]
    public void Calculate_Span_MismatchedLengths_ThrowsException()
    {
        double[] baseArr = new double[10];
        double[] compArr = new double[5];
        double[] output = new double[10];

        Assert.Throws<ArgumentException>(() => Rs.Batch(baseArr, compArr, output));
    }

    [Fact]
    public void Calculate_Span_OutputMismatch_ThrowsException()
    {
        double[] baseArr = new double[10];
        double[] compArr = new double[10];
        double[] output = new double[5];

        Assert.Throws<ArgumentException>(() => Rs.Batch(baseArr, compArr, output));
    }

    [Fact]
    public void Calculate_Span_InvalidPeriod_ThrowsException()
    {
        double[] baseArr = new double[10];
        double[] compArr = new double[10];
        double[] output = new double[10];

        Assert.Throws<ArgumentException>(() => Rs.Batch(baseArr, compArr, output, 0));
    }

    // ==================== EDGE CASES ====================
    [Fact]
    public void Update_NaNInput_UsesLastValidValue()
    {
        var rs = new Rs();

        rs.Update(100.0, 50.0);
        _ = rs.Last.Value;

        var result = rs.Update(double.NaN, double.NaN);
        // Should use last valid values (100/50 pattern OR fallback)
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_InfinityInput_UsesLastValidValue()
    {
        var rs = new Rs();

        rs.Update(100.0, 50.0);

        var result = rs.Update(double.PositiveInfinity, 50.0);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_VerySmallComparison_HandlesCorrectly()
    {
        var rs = new Rs();
        var result = rs.Update(100.0, 1e-15);

        // Values below epsilon (1e-10) are treated as zero -> returns NaN
        Assert.True(double.IsNaN(result.Value));
    }

    [Fact]
    public void Update_NegativeValues_HandlesCorrectly()
    {
        var rs = new Rs();

        // Negative values (like P&L or temperature)
        var result = rs.Update(-50.0, -25.0);
        Assert.Equal(2.0, result.Value, 10);
    }

    // ==================== PRIME ====================
    [Fact]
    public void Prime_Single_ThrowsNotSupported()
    {
        var rs = new Rs(5);
        double[] data = [100, 101, 102, 103, 104];

        Assert.Throws<NotSupportedException>(() => rs.Prime(data));
    }

    [Fact]
    public void Prime_Dual_InitializesState()
    {
        var rs = new Rs(3);
        double[] baseData = [100, 102, 104, 106, 108, 110];
        double[] compData = [50, 51, 52, 53, 54, 55];

        rs.Prime(baseData, compData);

        Assert.NotEqual(default, rs.Last);
    }

    [Fact]
    public void Prime_MismatchedLengths_ThrowsException()
    {
        var rs = new Rs(3);
        double[] baseData = [100, 102, 104];
        double[] compData = [50, 51];

        Assert.Throws<ArgumentException>(() => rs.Prime(baseData, compData));
    }

    // ==================== NOT SUPPORTED ====================
    [Fact]
    public void Update_SingleInput_ThrowsNotSupported()
    {
        var rs = new Rs();
        var input = new TValue(DateTime.Now, 100.0);

        Assert.Throws<NotSupportedException>(() => rs.Update(input));
    }

    [Fact]
    public void Update_TSeries_ThrowsNotSupported()
    {
        var rs = new Rs();
        var source = new TSeries();
        source.Add(new TValue(DateTime.Now, 100.0));

        Assert.Throws<NotSupportedException>(() => rs.Update(source));
    }

    // ==================== PERFORMANCE SCENARIOS ====================
    [Fact]
    public void Update_OutperformanceScenario_IncreasingRatio()
    {
        var rs = new Rs(5);

        // Base grows faster than comparison
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100.0 + (i * 2);   // +2 per bar
            double compPrice = 100.0 + (i * 1);   // +1 per bar
            rs.Update(basePrice, compPrice);
        }

        // Ratio should be increasing (base outperforming)
        Assert.True(rs.Last.Value > 1.0);
    }

    [Fact]
    public void Update_UnderperformanceScenario_DecreasingRatio()
    {
        var rs = new Rs(5);

        // Base grows slower than comparison
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100.0 + (i * 1);   // +1 per bar
            double compPrice = 100.0 + (i * 2);   // +2 per bar
            rs.Update(basePrice, compPrice);
        }

        // Ratio should be decreasing (base underperforming)
        Assert.True(rs.Last.Value < 1.0);
    }

    [Fact]
    public void Update_ParallelMovement_StableRatio()
    {
        var rs = new Rs(5);

        // Both grow at same rate
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100.0 + (i * 2);
            double compPrice = 50.0 + (i * 1);
            rs.Update(basePrice, compPrice);
        }

        // Initial ratio was 2.0, should stay around there
        Assert.InRange(rs.Last.Value, 1.9, 2.1);
    }

    // ==================== BATCH CALCULATION ====================
    [Fact]
    public void Calculate_Batch_MatchesStreaming()
    {
        var baseBars = new GBM(seed: 42).Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var compBars = new GBM(seed: 123).Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var baseSeries = baseBars.Close;
        var compSeries = compBars.Close;

        // Batch calculation
        var batchResult = Rs.Batch(baseSeries, compSeries, 5);

        // Streaming calculation
        var rs = new Rs(5);
        var streamingResults = new List<double>();
        for (int i = 0; i < baseSeries.Count; i++)
        {
            streamingResults.Add(rs.Update(baseSeries[i], compSeries[i], true).Value);
        }

        // Compare
        for (int i = 0; i < baseSeries.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], streamingResults[i], 6);
        }
    }
}
