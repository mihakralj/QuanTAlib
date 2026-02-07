using System;
using Xunit;

namespace QuanTAlib.Tests;

public class TtmLrcTests
{
    private const double Epsilon = 1e-10;

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultPeriod_SetsTo100()
    {
        var indicator = new TtmLrc();
        Assert.Equal(100, indicator.WarmupPeriod);
        Assert.Equal("TtmLrc(100)", indicator.Name);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsCorrectly()
    {
        var indicator = new TtmLrc(50);
        Assert.Equal(50, indicator.WarmupPeriod);
        Assert.Equal("TtmLrc(50)", indicator.Name);
    }

    [Fact]
    public void Constructor_PeriodOfOne_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TtmLrc(1));
    }

    [Fact]
    public void Constructor_ZeroPeriod_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TtmLrc(0));
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TtmLrc(-5));
    }

    #endregion

    #region IsHot/Warmup Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var indicator = new TtmLrc(10);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 9; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + i), isNew: true);
            Assert.False(indicator.IsHot, $"Should not be hot at point {i + 1}");
        }
    }

    [Fact]
    public void IsHot_AtExactWarmup_ReturnsTrue()
    {
        var indicator = new TtmLrc(10);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + i), isNew: true);
        }

        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void IsHot_AfterWarmup_RemainsTrue()
    {
        var indicator = new TtmLrc(5);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + i), isNew: true);
        }

        Assert.True(indicator.IsHot);
    }

    #endregion

    #region Band Symmetry Tests

    [Fact]
    public void Bands_Symmetry_Upper1AndLower1EquidistantFromMiddle()
    {
        var indicator = new TtmLrc(10);
        var now = DateTime.UtcNow;
        var rng = new Random(42);

        for (int i = 0; i < 15; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + rng.NextDouble() * 10), isNew: true);
        }

        double mid = indicator.Midline.Value;
        double upper1 = indicator.Upper1.Value;
        double lower1 = indicator.Lower1.Value;

        double distUp = upper1 - mid;
        double distDown = mid - lower1;

        Assert.True(Math.Abs(distUp - distDown) < Epsilon, $"Upper1 and Lower1 should be equidistant from middle. Up: {distUp}, Down: {distDown}");
        Assert.Equal(indicator.StdDev, distUp, 10);
    }

    [Fact]
    public void Bands_Symmetry_Upper2AndLower2EquidistantFromMiddle()
    {
        var indicator = new TtmLrc(10);
        var now = DateTime.UtcNow;
        var rng = new Random(42);

        for (int i = 0; i < 15; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + rng.NextDouble() * 10), isNew: true);
        }

        double mid = indicator.Midline.Value;
        double upper2 = indicator.Upper2.Value;
        double lower2 = indicator.Lower2.Value;

        double distUp = upper2 - mid;
        double distDown = mid - lower2;

        Assert.True(Math.Abs(distUp - distDown) < Epsilon, $"Upper2 and Lower2 should be equidistant from middle. Up: {distUp}, Down: {distDown}");
        Assert.Equal(2.0 * indicator.StdDev, distUp, 10);
    }

    [Fact]
    public void Bands_Ordering_UpperGreaterThanMiddleGreaterThanLower()
    {
        var indicator = new TtmLrc(10);
        var now = DateTime.UtcNow;
        var rng = new Random(42);

        for (int i = 0; i < 15; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + rng.NextDouble() * 10), isNew: true);
        }

        Assert.True(indicator.Upper2.Value >= indicator.Upper1.Value, "Upper2 should be >= Upper1");
        Assert.True(indicator.Upper1.Value >= indicator.Midline.Value, "Upper1 should be >= Midline");
        Assert.True(indicator.Midline.Value >= indicator.Lower1.Value, "Midline should be >= Lower1");
        Assert.True(indicator.Lower1.Value >= indicator.Lower2.Value, "Lower1 should be >= Lower2");
    }

    #endregion

    #region Linear Data Tests

    [Fact]
    public void LinearData_PerfectTrend_ZeroStdDev()
    {
        var indicator = new TtmLrc(10);
        var now = DateTime.UtcNow;

        // Perfect linear data: y = 100 + 2*x
        for (int i = 0; i < 15; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + 2.0 * i), isNew: true);
        }

        Assert.True(indicator.IsHot);
        Assert.True(Math.Abs(indicator.StdDev) < 1e-9, $"StdDev should be 0 for perfect linear data, got {indicator.StdDev}");
        Assert.True(Math.Abs(indicator.Slope - 2.0) < 1e-9, $"Slope should be 2.0, got {indicator.Slope}");
        Assert.True(Math.Abs(indicator.RSquared - 1.0) < 1e-9, $"R² should be 1.0 for perfect fit, got {indicator.RSquared}");

        // All bands should equal midline when StdDev is 0
        Assert.Equal(indicator.Midline.Value, indicator.Upper1.Value, 10);
        Assert.Equal(indicator.Midline.Value, indicator.Lower1.Value, 10);
        Assert.Equal(indicator.Midline.Value, indicator.Upper2.Value, 10);
        Assert.Equal(indicator.Midline.Value, indicator.Lower2.Value, 10);
    }

    [Fact]
    public void LinearData_PositiveSlope_SlopeIsPositive()
    {
        var indicator = new TtmLrc(10);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + 5.0 * i), isNew: true);
        }

        Assert.True(indicator.Slope > 0, $"Slope should be positive for uptrend, got {indicator.Slope}");
    }

    [Fact]
    public void LinearData_NegativeSlope_SlopeIsNegative()
    {
        var indicator = new TtmLrc(10);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 - 3.0 * i), isNew: true);
        }

        Assert.True(indicator.Slope < 0, $"Slope should be negative for downtrend, got {indicator.Slope}");
    }

    [Fact]
    public void FlatData_ZeroSlope()
    {
        var indicator = new TtmLrc(10);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100), isNew: true);
        }

        Assert.True(Math.Abs(indicator.Slope) < 1e-10, $"Slope should be 0 for flat data, got {indicator.Slope}");
        Assert.True(Math.Abs(indicator.StdDev) < 1e-10, $"StdDev should be 0 for constant data, got {indicator.StdDev}");
    }

    #endregion

    #region R-Squared Tests

    [Fact]
    public void RSquared_PerfectFit_EqualsOne()
    {
        var indicator = new TtmLrc(10);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + 2.0 * i), isNew: true);
        }

        Assert.True(Math.Abs(indicator.RSquared - 1.0) < 1e-9, $"R² should be 1.0 for perfect linear fit, got {indicator.RSquared}");
    }

    [Fact]
    public void RSquared_RandomData_LessThanOne()
    {
        var indicator = new TtmLrc(20);
        var now = DateTime.UtcNow;
        var rng = new Random(42);

        for (int i = 0; i < 30; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + rng.NextDouble() * 50), isNew: true);
        }

        Assert.True(indicator.RSquared < 1.0, $"R² should be less than 1.0 for random data, got {indicator.RSquared}");
        Assert.True(indicator.RSquared >= 0.0, $"R² should be non-negative, got {indicator.RSquared}");
    }

    [Fact]
    public void RSquared_ClampedBetweenZeroAndOne()
    {
        var indicator = new TtmLrc(5);
        var now = DateTime.UtcNow;
        var rng = new Random(123);

        for (int i = 0; i < 20; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + rng.NextDouble() * 100 - 50), isNew: true);
            Assert.True(indicator.RSquared >= 0.0 && indicator.RSquared <= 1.0, $"R² should be in [0,1], got {indicator.RSquared}");
        }
    }

    #endregion

    #region Bar Correction Tests

    [Fact]
    public void BarCorrection_IsNewFalse_RevertsToPreviousState()
    {
        var indicator = new TtmLrc(5);
        var now = DateTime.UtcNow;

        // Establish base state
        for (int i = 0; i < 10; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + i), isNew: true);
        }

        double originalMid = indicator.Midline.Value;
        double originalSlope = indicator.Slope;
        double originalStdDev = indicator.StdDev;

        // Apply correction with new value
        indicator.Update(new TValue(now.AddMinutes(9), 200), isNew: false);

        // Should now have different values
        Assert.NotEqual(originalMid, indicator.Midline.Value);

        // Correct back to original value
        indicator.Update(new TValue(now.AddMinutes(9), 100 + 9), isNew: false);

        // Should be back to original state
        Assert.Equal(originalMid, indicator.Midline.Value, 10);
        Assert.Equal(originalSlope, indicator.Slope, 10);
        Assert.Equal(originalStdDev, indicator.StdDev, 10);
    }

    [Fact]
    public void BarCorrection_MultipleCorrections_MaintainsConsistentBase()
    {
        var indicator = new TtmLrc(5);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 8; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + i * 2), isNew: true);
        }

        double baseMid = indicator.Midline.Value;

        // Multiple corrections
        for (int j = 0; j < 5; j++)
        {
            indicator.Update(new TValue(now.AddMinutes(7), 150 + j * 10), isNew: false);
        }

        // Revert to original
        indicator.Update(new TValue(now.AddMinutes(7), 100 + 7 * 2), isNew: false);

        Assert.Equal(baseMid, indicator.Midline.Value, 10);
    }

    [Fact]
    public void BarCorrection_AfterCorrection_NextNewBarUsesCorrectedState()
    {
        var indicator = new TtmLrc(5);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 6; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + i), isNew: true);
        }

        // Correct last bar
        indicator.Update(new TValue(now.AddMinutes(5), 150), isNew: false);

        // Verify correction applied
        Assert.True(indicator.Midline.Value > 100, "Midline should reflect corrected spike value");

        // Add new bar
        indicator.Update(new TValue(now.AddMinutes(6), 160), isNew: true);

        // Verify the correction persisted - the new state should be based on the corrected value
        // By checking slope direction changed due to spike
        Assert.True(indicator.Slope > 0, "Slope should be positive after spike correction");
    }

    #endregion

    #region Batch vs Streaming Consistency

    [Fact]
    public void BatchVsStreaming_SameResults()
    {
        var streamingIndicator = new TtmLrc(20);
        var now = DateTime.UtcNow;
        var rng = new Random(42);
        int count = 50;

        var times = new List<long>(count);
        var values = new List<double>(count);

        for (int i = 0; i < count; i++)
        {
            long t = (now.AddMinutes(i)).Ticks;
            double v = 100 + rng.NextDouble() * 20;
            times.Add(t);
            values.Add(v);
            streamingIndicator.Update(new TValue(new DateTime(t, DateTimeKind.Utc), v), isNew: true);
        }

        var source = new TSeries(times, values);
        var (bMid, bU1, bL1, bU2, bL2) = TtmLrc.Batch(source, 20);

        // Compare streaming final values to batch final values
        Assert.Equal(streamingIndicator.Midline.Value, bMid.Values[^1], 10);
        Assert.Equal(streamingIndicator.Upper1.Value, bU1.Values[^1], 10);
        Assert.Equal(streamingIndicator.Lower1.Value, bL1.Values[^1], 10);
        Assert.Equal(streamingIndicator.Upper2.Value, bU2.Values[^1], 10);
        Assert.Equal(streamingIndicator.Lower2.Value, bL2.Values[^1], 10);
    }

    [Fact]
    public void Update_TSeries_ReturnsAllFiveBands()
    {
        var indicator = new TtmLrc(10);
        var now = DateTime.UtcNow;
        var rng = new Random(42);
        int count = 20;

        var times = new List<long>(count);
        var values = new List<double>(count);

        for (int i = 0; i < count; i++)
        {
            times.Add(now.AddMinutes(i).Ticks);
            values.Add(100 + rng.NextDouble() * 10);
        }

        var source = new TSeries(times, values);
        var (mid, u1, l1, u2, l2) = indicator.Update(source);

        Assert.Equal(count, mid.Count);
        Assert.Equal(count, u1.Count);
        Assert.Equal(count, l1.Count);
        Assert.Equal(count, u2.Count);
        Assert.Equal(count, l2.Count);
    }

    [Fact]
    public void Calculate_ReturnsIndicatorAndResults()
    {
        var now = DateTime.UtcNow;
        var rng = new Random(42);
        int count = 30;

        var times = new List<long>(count);
        var values = new List<double>(count);

        for (int i = 0; i < count; i++)
        {
            times.Add(now.AddMinutes(i).Ticks);
            values.Add(100 + rng.NextDouble() * 15);
        }

        var source = new TSeries(times, values);
        var (results, indicator) = TtmLrc.Calculate(source, 15);

        Assert.NotNull(indicator);
        Assert.True(indicator.IsHot);
        Assert.Equal(count, results.Midline.Count);
        Assert.Equal(count, results.Upper1.Count);
        Assert.Equal(count, results.Lower1.Count);
        Assert.Equal(count, results.Upper2.Count);
        Assert.Equal(count, results.Lower2.Count);
    }

    #endregion

    #region NaN/Infinity Handling

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var indicator = new TtmLrc(5);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + i), isNew: true);
        }

        // Capture pre-NaN state for verification
        Assert.True(double.IsFinite(indicator.Midline.Value), "Midline should be finite before NaN");

        // Add NaN
        indicator.Update(new TValue(now.AddMinutes(5), double.NaN), isNew: true);

        // Should still have valid output (using last valid value)
        Assert.True(double.IsFinite(indicator.Midline.Value), "Midline should still be finite after NaN input");
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var indicator = new TtmLrc(5);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 6; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + i), isNew: true);
        }

        // Add positive infinity
        indicator.Update(new TValue(now.AddMinutes(6), double.PositiveInfinity), isNew: true);

        Assert.True(double.IsFinite(indicator.Midline.Value), "Midline should still be finite after Infinity input");
    }

    [Fact]
    public void Batch_NaN_HandledGracefully()
    {
        var source = new List<double> { 100, 101, double.NaN, 103, 104, 105, 106 };
        int len = source.Count;

        Span<double> mid = stackalloc double[len];
        Span<double> u1 = stackalloc double[len];
        Span<double> l1 = stackalloc double[len];
        Span<double> u2 = stackalloc double[len];
        Span<double> l2 = stackalloc double[len];

        TtmLrc.Batch(source.ToArray(), mid, u1, l1, u2, l2, 3);

        // All outputs after first few should be finite
        for (int i = 2; i < len; i++)
        {
            Assert.True(double.IsFinite(mid[i]), $"Midline[{i}] should be finite");
        }
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new TtmLrc(5);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + i * 2), isNew: true);
        }

        Assert.True(indicator.IsHot);
        Assert.True(indicator.Slope > 0);

        indicator.Reset();

        Assert.False(indicator.IsHot);
        Assert.Equal(0, indicator.Slope);
        Assert.Equal(0, indicator.StdDev);
        Assert.Equal(0, indicator.RSquared);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var indicator = new TtmLrc(5);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + i), isNew: true);
        }

        double firstRunMid = indicator.Midline.Value;

        indicator.Reset();

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + i), isNew: true);
        }

        // Results should be identical after reuse
        Assert.Equal(firstRunMid, indicator.Midline.Value, 10);
    }

    #endregion

    #region Prime Tests

    [Fact]
    public void Prime_InitializesFromSeries()
    {
        var indicator = new TtmLrc(10);
        var now = DateTime.UtcNow;

        var times = new List<long>(15);
        var values = new List<double>(15);

        for (int i = 0; i < 15; i++)
        {
            times.Add(now.AddMinutes(i).Ticks);
            values.Add(100 + i * 2);
        }

        var source = new TSeries(times, values);
        indicator.Prime(source);

        Assert.True(indicator.IsHot);
        Assert.True(Math.Abs(indicator.Slope - 2.0) < 1e-9);
    }

    [Fact]
    public void Constructor_WithSource_AutoSubscribes()
    {
        var source = new TSeries();
        var indicator = new TtmLrc(source, 5);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 8; i++)
        {
            source.Add(new TValue(now.AddMinutes(i), 100 + i * 3), isNew: true);
        }

        Assert.True(indicator.IsHot);
        Assert.True(Math.Abs(indicator.Slope - 3.0) < 1e-9);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EmptySeries_ReturnsEmptyResults()
    {
        var indicator = new TtmLrc(10);
        var source = new TSeries();

        var (mid, u1, l1, u2, l2) = indicator.Update(source);

        Assert.True(mid.Count == 0, "Midline should be empty for empty source");
        Assert.True(u1.Count == 0, "Upper1 should be empty for empty source");
        Assert.True(l1.Count == 0, "Lower1 should be empty for empty source");
        Assert.True(u2.Count == 0, "Upper2 should be empty for empty source");
        Assert.True(l2.Count == 0, "Lower2 should be empty for empty source");
    }

    [Fact]
    public void SingleValue_AllBandsEqual()
    {
        var indicator = new TtmLrc(10);
        var now = DateTime.UtcNow;

        indicator.Update(new TValue(now, 100), isNew: true);

        Assert.Equal(100, indicator.Midline.Value);
        Assert.Equal(100, indicator.Upper1.Value);
        Assert.Equal(100, indicator.Lower1.Value);
        Assert.Equal(100, indicator.Upper2.Value);
        Assert.Equal(100, indicator.Lower2.Value);
    }

    [Fact]
    public void TwoValues_CalculatesRegression()
    {
        var indicator = new TtmLrc(10);
        var now = DateTime.UtcNow;

        indicator.Update(new TValue(now, 100), isNew: true);
        indicator.Update(new TValue(now.AddMinutes(1), 110), isNew: true);

        // Slope should be 10 (rise of 10 over run of 1)
        Assert.True(Math.Abs(indicator.Slope - 10.0) < 1e-9, $"Slope should be 10, got {indicator.Slope}");

        // Midline at x=1 should be 110
        Assert.True(Math.Abs(indicator.Midline.Value - 110.0) < 1e-9, $"Midline should be 110, got {indicator.Midline.Value}");
    }

    [Fact]
    public void VerySmallPeriod_Period2_Works()
    {
        var indicator = new TtmLrc(2);
        var now = DateTime.UtcNow;

        indicator.Update(new TValue(now, 100), isNew: true);
        indicator.Update(new TValue(now.AddMinutes(1), 120), isNew: true);
        indicator.Update(new TValue(now.AddMinutes(2), 130), isNew: true);

        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Midline.Value));
        Assert.True(double.IsFinite(indicator.Slope));
    }

    [Fact]
    public void LargePeriod_HandlesCorrectly()
    {
        var indicator = new TtmLrc(200);
        var now = DateTime.UtcNow;
        var rng = new Random(42);

        for (int i = 0; i < 250; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + rng.NextDouble() * 50), isNew: true);
        }

        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Midline.Value));
        Assert.True(double.IsFinite(indicator.Slope));
        Assert.True(indicator.RSquared >= 0 && indicator.RSquared <= 1);
    }

    #endregion

    #region Batch Validation Tests

    [Fact]
    public void Batch_InvalidPeriod_ThrowsException()
    {
        double[] source = new double[10];
        double[] mid = new double[10];
        double[] u1 = new double[10];
        double[] l1 = new double[10];
        double[] u2 = new double[10];
        double[] l2 = new double[10];

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TtmLrc.Batch(source, mid, u1, l1, u2, l2, 1));
    }

    [Fact]
    public void Batch_OutputTooShort_ThrowsException()
    {
        double[] source = new double[10];
        double[] mid = new double[5]; // Too short
        double[] u1 = new double[10];
        double[] l1 = new double[10];
        double[] u2 = new double[10];
        double[] l2 = new double[10];

        Assert.Throws<ArgumentException>(() =>
            TtmLrc.Batch(source, mid, u1, l1, u2, l2, 3));
    }

    #endregion

    #region Pub/Sub Tests

    [Fact]
    public void Pub_FiredOnUpdate()
    {
        var indicator = new TtmLrc(5);
        var now = DateTime.UtcNow;
        int eventCount = 0;

        void OnPub(object? sender, in TValueEventArgs args) { eventCount = eventCount + 1; }
        indicator.Pub += OnPub;

        for (int i = 0; i < 8; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + i), isNew: true);
        }

        Assert.Equal(8, eventCount);
    }

    [Fact]
    public void Pub_ReceivesCorrectValue()
    {
        var indicator = new TtmLrc(5);
        var now = DateTime.UtcNow;
        TValue? lastPubValue = null;

        void OnPub(object? sender, in TValueEventArgs args) => lastPubValue = args.Value;
        indicator.Pub += OnPub;

        for (int i = 0; i < 8; i++)
        {
            indicator.Update(new TValue(now.AddMinutes(i), 100 + i * 2), isNew: true);
        }

        Assert.NotNull(lastPubValue);
        Assert.Equal(indicator.Midline.Value, lastPubValue.Value.Value, 10);
    }

    #endregion
}
