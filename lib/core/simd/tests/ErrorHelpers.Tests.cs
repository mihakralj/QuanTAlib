using Xunit;

namespace QuanTAlib.Tests;

public class ErrorHelpersTests
{
    private const double Tolerance = 1e-10;

    // ── Constants ───────────────────────────────────────────────────────

    [Fact]
    public void StackAllocThreshold_Is256()
    {
        Assert.Equal(256, ErrorHelpers.StackAllocThreshold);
    }

    [Fact]
    public void DefaultResyncInterval_Is1000()
    {
        Assert.Equal(1000, ErrorHelpers.DefaultResyncInterval);
    }

    // ── FindFirstValidValue ─────────────────────────────────────────────

    [Fact]
    public void FindFirstValidValue_AllFinite_ReturnsFirst()
    {
        double[] data = [10.0, 20.0, 30.0];
        Assert.Equal(10.0, ErrorHelpers.FindFirstValidValue(data));
    }

    [Fact]
    public void FindFirstValidValue_LeadingNaN_SkipsToFirstFinite()
    {
        double[] data = [double.NaN, double.NaN, 42.0, 50.0];
        Assert.Equal(42.0, ErrorHelpers.FindFirstValidValue(data));
    }

    [Fact]
    public void FindFirstValidValue_AllNaN_ReturnsZero()
    {
        double[] data = [double.NaN, double.NaN, double.NaN];
        Assert.Equal(0.0, ErrorHelpers.FindFirstValidValue(data));
    }

    [Fact]
    public void FindFirstValidValue_EmptySpan_ReturnsZero()
    {
        Assert.Equal(0.0, ErrorHelpers.FindFirstValidValue(ReadOnlySpan<double>.Empty));
    }

    [Fact]
    public void FindFirstValidValue_InfinitySkipped_ReturnsFirstFinite()
    {
        double[] data = [double.PositiveInfinity, double.NegativeInfinity, 7.0];
        Assert.Equal(7.0, ErrorHelpers.FindFirstValidValue(data));
    }

    // ── ComputeSignedErrors ─────────────────────────────────────────────

    [Fact]
    public void SignedErrors_BasicComputation()
    {
        double[] actual = [10.0, 20.0, 30.0];
        double[] predicted = [8.0, 25.0, 29.0];
        double[] output = new double[3];

        ErrorHelpers.ComputeSignedErrors(actual, predicted, output);

        Assert.Equal(2.0, output[0], Tolerance);
        Assert.Equal(-5.0, output[1], Tolerance);
        Assert.Equal(1.0, output[2], Tolerance);
    }

    [Fact]
    public void SignedErrors_EmptySpan_NoOp()
    {
        ErrorHelpers.ComputeSignedErrors(
            ReadOnlySpan<double>.Empty,
            ReadOnlySpan<double>.Empty,
            Span<double>.Empty);
        Assert.True(true); // No exception = pass
    }

    [Fact]
    public void SignedErrors_LengthMismatch_Throws()
    {
        double[] a = [1.0, 2.0];
        double[] b = [1.0];
        double[] o = [0.0, 0.0];

        Assert.Throws<ArgumentException>(() =>
            ErrorHelpers.ComputeSignedErrors(a, b, o));
    }

    [Fact]
    public void SignedErrors_WithNaN_SubstitutesLastValid()
    {
        double[] actual = [10.0, double.NaN, 30.0];
        double[] predicted = [5.0, 15.0, 25.0];
        double[] output = new double[3];

        ErrorHelpers.ComputeSignedErrors(actual, predicted, output);

        Assert.Equal(5.0, output[0], Tolerance);    // 10 - 5
        Assert.Equal(-5.0, output[1], Tolerance);   // 10 (last valid) - 15
        Assert.Equal(5.0, output[2], Tolerance);     // 30 - 25
    }

    [Fact]
    public void SignedErrors_LargeCleanArray_ProducesCorrectResults()
    {
        // ≥ 8 elements to exercise SIMD path (Vector256<double>.Count = 4)
        double[] actual = [1, 2, 3, 4, 5, 6, 7, 8];
        double[] predicted = [0, 1, 2, 3, 4, 5, 6, 7];
        double[] output = new double[8];

        ErrorHelpers.ComputeSignedErrors(actual, predicted, output);

        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(1.0, output[i], Tolerance);
        }
    }

    [Fact]
    public void SignedErrors_LargeArrayWithNaN_FallsBackCorrectly()
    {
        double[] actual = [1, 2, 3, double.NaN, 5, 6, 7, 8];
        double[] predicted = [0, 0, 0, 0, 0, 0, 0, 0];
        double[] output = new double[8];

        ErrorHelpers.ComputeSignedErrors(actual, predicted, output);

        Assert.Equal(1.0, output[0], Tolerance);
        Assert.Equal(2.0, output[1], Tolerance);
        Assert.Equal(3.0, output[2], Tolerance);
        Assert.Equal(3.0, output[3], Tolerance); // NaN → last valid (3)
        Assert.Equal(5.0, output[4], Tolerance);
    }

    [Fact]
    public void SignedErrors_SingleElement()
    {
        double[] actual = [7.0];
        double[] predicted = [3.0];
        double[] output = new double[1];

        ErrorHelpers.ComputeSignedErrors(actual, predicted, output);

        Assert.Equal(4.0, output[0], Tolerance);
    }

    // ── ComputeAbsoluteErrors ───────────────────────────────────────────

    [Fact]
    public void AbsoluteErrors_BasicComputation()
    {
        double[] actual = [10.0, 20.0, 30.0];
        double[] predicted = [12.0, 15.0, 35.0];
        double[] output = new double[3];

        ErrorHelpers.ComputeAbsoluteErrors(actual, predicted, output);

        Assert.Equal(2.0, output[0], Tolerance);
        Assert.Equal(5.0, output[1], Tolerance);
        Assert.Equal(5.0, output[2], Tolerance);
    }

    [Fact]
    public void AbsoluteErrors_EmptySpan_NoOp()
    {
        ErrorHelpers.ComputeAbsoluteErrors(
            ReadOnlySpan<double>.Empty,
            ReadOnlySpan<double>.Empty,
            Span<double>.Empty);
        Assert.True(true);
    }

    [Fact]
    public void AbsoluteErrors_LengthMismatch_Throws()
    {
        double[] a = [1.0];
        double[] b = [1.0, 2.0];
        double[] o = [0.0];

        Assert.Throws<ArgumentException>(() =>
            ErrorHelpers.ComputeAbsoluteErrors(a, b, o));
    }

    [Fact]
    public void AbsoluteErrors_AlwaysNonNegative()
    {
        double[] actual = [5.0, -3.0, 10.0, 0.0];
        double[] predicted = [8.0, 2.0, 10.0, -5.0];
        double[] output = new double[4];

        ErrorHelpers.ComputeAbsoluteErrors(actual, predicted, output);

        for (int i = 0; i < 4; i++)
        {
            Assert.True(output[i] >= 0.0, $"AbsoluteError at {i} was {output[i]}");
        }
    }

    [Fact]
    public void AbsoluteErrors_WithNaN_SubstitutesLastValid()
    {
        double[] actual = [10.0, double.NaN, 30.0];
        double[] predicted = [5.0, 15.0, 25.0];
        double[] output = new double[3];

        ErrorHelpers.ComputeAbsoluteErrors(actual, predicted, output);

        Assert.Equal(5.0, output[0], Tolerance);   // |10 - 5|
        Assert.Equal(5.0, output[1], Tolerance);   // |10 - 15|
        Assert.Equal(5.0, output[2], Tolerance);   // |30 - 25|
    }

    [Fact]
    public void AbsoluteErrors_LargeCleanArray_SimdPath()
    {
        double[] actual = [10, 20, 30, 40, 50, 60, 70, 80];
        double[] predicted = [12, 18, 33, 37, 55, 58, 73, 77];
        double[] output = new double[8];

        ErrorHelpers.ComputeAbsoluteErrors(actual, predicted, output);

        Assert.Equal(2.0, output[0], Tolerance);
        Assert.Equal(2.0, output[1], Tolerance);
        Assert.Equal(3.0, output[2], Tolerance);
        Assert.Equal(3.0, output[3], Tolerance);
        for (int i = 0; i < 8; i++)
        {
            Assert.True(output[i] >= 0.0);
        }
    }

    // ── ComputeSquaredErrors ────────────────────────────────────────────

    [Fact]
    public void SquaredErrors_BasicComputation()
    {
        double[] actual = [10.0, 20.0, 30.0];
        double[] predicted = [8.0, 25.0, 27.0];
        double[] output = new double[3];

        ErrorHelpers.ComputeSquaredErrors(actual, predicted, output);

        Assert.Equal(4.0, output[0], Tolerance);    // (10-8)² = 4
        Assert.Equal(25.0, output[1], Tolerance);   // (20-25)² = 25
        Assert.Equal(9.0, output[2], Tolerance);    // (30-27)² = 9
    }

    [Fact]
    public void SquaredErrors_EmptySpan_NoOp()
    {
        ErrorHelpers.ComputeSquaredErrors(
            ReadOnlySpan<double>.Empty,
            ReadOnlySpan<double>.Empty,
            Span<double>.Empty);
        Assert.True(true);
    }

    [Fact]
    public void SquaredErrors_LengthMismatch_Throws()
    {
        double[] a = [1.0, 2.0, 3.0];
        double[] b = [1.0, 2.0];
        double[] o = [0.0, 0.0, 0.0];

        Assert.Throws<ArgumentException>(() =>
            ErrorHelpers.ComputeSquaredErrors(a, b, o));
    }

    [Fact]
    public void SquaredErrors_AlwaysNonNegative()
    {
        double[] actual = [-5.0, 0.0, 3.0, -1.0];
        double[] predicted = [2.0, -3.0, 7.0, -1.0];
        double[] output = new double[4];

        ErrorHelpers.ComputeSquaredErrors(actual, predicted, output);

        for (int i = 0; i < 4; i++)
        {
            Assert.True(output[i] >= 0.0);
        }
    }

    [Fact]
    public void SquaredErrors_WithNaN_SubstitutesLastValid()
    {
        double[] actual = [10.0, double.NaN, 30.0];
        double[] predicted = [7.0, 20.0, 27.0];
        double[] output = new double[3];

        ErrorHelpers.ComputeSquaredErrors(actual, predicted, output);

        Assert.Equal(9.0, output[0], Tolerance);     // (10-7)² = 9
        Assert.Equal(100.0, output[1], Tolerance);   // (10-20)² = 100, NaN actual → 10
        Assert.Equal(9.0, output[2], Tolerance);     // (30-27)² = 9
    }

    [Fact]
    public void SquaredErrors_LargeCleanArray_SimdPath()
    {
        double[] actual = [1, 2, 3, 4, 5, 6, 7, 8];
        double[] predicted = [2, 3, 4, 5, 6, 7, 8, 9];
        double[] output = new double[8];

        ErrorHelpers.ComputeSquaredErrors(actual, predicted, output);

        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(1.0, output[i], Tolerance); // Each diff is -1, squared = 1
        }
    }

    // ── ComputeWeightedErrors ───────────────────────────────────────────

    [Fact]
    public void WeightedErrors_BasicComputation()
    {
        double[] actual = [10.0, 20.0, 30.0];
        double[] predicted = [8.0, 18.0, 28.0];
        double[] weights = [1.0, 2.0, 3.0];
        double[] output = new double[3];

        ErrorHelpers.ComputeWeightedErrors(actual, predicted, weights, output);

        // weight * (act - pred)²
        Assert.Equal(1.0 * 4.0, output[0], Tolerance);   // 1 * (10-8)² = 4
        Assert.Equal(2.0 * 4.0, output[1], Tolerance);   // 2 * (20-18)² = 8
        Assert.Equal(3.0 * 4.0, output[2], Tolerance);   // 3 * (30-28)² = 12
    }

    [Fact]
    public void WeightedErrors_EmptySpan_NoOp()
    {
        ErrorHelpers.ComputeWeightedErrors(
            ReadOnlySpan<double>.Empty,
            ReadOnlySpan<double>.Empty,
            ReadOnlySpan<double>.Empty,
            Span<double>.Empty);
        Assert.True(true);
    }

    [Fact]
    public void WeightedErrors_LengthMismatch_Throws()
    {
        double[] a = [1.0];
        double[] b = [1.0];
        double[] w = [1.0, 2.0]; // mismatched
        double[] o = [0.0];

        Assert.Throws<ArgumentException>(() =>
            ErrorHelpers.ComputeWeightedErrors(a, b, w, o));
    }

    [Fact]
    public void WeightedErrors_WithNaN_SubstitutesLastValid()
    {
        double[] actual = [10.0, double.NaN];
        double[] predicted = [8.0, 6.0];
        double[] weights = [1.0, double.NaN];
        double[] output = new double[2];

        ErrorHelpers.ComputeWeightedErrors(actual, predicted, weights, output);

        // [0]: 1.0 * (10-8)² = 4.0
        Assert.Equal(4.0, output[0], Tolerance);
        // [1]: NaN act→10, NaN wgt→1.0: 1.0 * (10-6)² = 16.0
        Assert.Equal(16.0, output[1], Tolerance);
    }

    [Fact]
    public void WeightedErrors_ZeroWeight_ProducesZero()
    {
        double[] actual = [100.0];
        double[] predicted = [0.0];
        double[] weights = [0.0];
        double[] output = new double[1];

        ErrorHelpers.ComputeWeightedErrors(actual, predicted, weights, output);

        Assert.Equal(0.0, output[0], Tolerance);
    }

    // ── ComputePercentageErrors ─────────────────────────────────────────

    [Fact]
    public void PercentageErrors_BasicComputation()
    {
        double[] actual = [100.0, 200.0];
        double[] predicted = [90.0, 210.0];
        double[] output = new double[2];

        ErrorHelpers.ComputePercentageErrors(actual, predicted, output);

        Assert.Equal(10.0, output[0], Tolerance);  // |100-90|/|100|*100 = 10%
        Assert.Equal(5.0, output[1], Tolerance);   // |200-210|/|200|*100 = 5%
    }

    [Fact]
    public void PercentageErrors_EmptySpan_NoOp()
    {
        ErrorHelpers.ComputePercentageErrors(
            ReadOnlySpan<double>.Empty,
            ReadOnlySpan<double>.Empty,
            Span<double>.Empty);
        Assert.True(true);
    }

    [Fact]
    public void PercentageErrors_LengthMismatch_Throws()
    {
        double[] a = [1.0, 2.0];
        double[] b = [1.0];
        double[] o = [0.0, 0.0];

        Assert.Throws<ArgumentException>(() =>
            ErrorHelpers.ComputePercentageErrors(a, b, o));
    }

    [Fact]
    public void PercentageErrors_NearZeroActual_UsesAbsoluteError()
    {
        // When |actual| < epsilon, falls back to |actual - predicted|
        double[] actual = [1e-15];
        double[] predicted = [5.0];
        double[] output = new double[1];

        ErrorHelpers.ComputePercentageErrors(actual, predicted, output);

        // absActual ~ 0 < epsilon (1e-10), so output = |act - pred| = 5.0
        Assert.Equal(5.0, output[0], 1e-5);
    }

    [Fact]
    public void PercentageErrors_WithNaN_SubstitutesLastValid()
    {
        double[] actual = [100.0, double.NaN];
        double[] predicted = [90.0, 80.0];
        double[] output = new double[2];

        ErrorHelpers.ComputePercentageErrors(actual, predicted, output);

        Assert.Equal(10.0, output[0], Tolerance); // |100-90|/100*100
        Assert.Equal(20.0, output[1], Tolerance); // NaN→100: |100-80|/100*100
    }

    // ── ComputeSymmetricPercentageErrors ────────────────────────────────

    [Fact]
    public void SymmetricPercentageErrors_BasicComputation()
    {
        double[] actual = [100.0];
        double[] predicted = [80.0];
        double[] output = new double[1];

        ErrorHelpers.ComputeSymmetricPercentageErrors(actual, predicted, output);

        // |100-80| / ((|100|+|80|)/2) * 100 = 20 / 90 * 100 ≈ 22.222
        double expected = 20.0 / 90.0 * 100.0;
        Assert.Equal(expected, output[0], Tolerance);
    }

    [Fact]
    public void SymmetricPercentageErrors_EmptySpan_NoOp()
    {
        ErrorHelpers.ComputeSymmetricPercentageErrors(
            ReadOnlySpan<double>.Empty,
            ReadOnlySpan<double>.Empty,
            Span<double>.Empty);
        Assert.True(true);
    }

    [Fact]
    public void SymmetricPercentageErrors_LengthMismatch_Throws()
    {
        double[] a = [1.0];
        double[] b = [1.0, 2.0];
        double[] o = [0.0];

        Assert.Throws<ArgumentException>(() =>
            ErrorHelpers.ComputeSymmetricPercentageErrors(a, b, o));
    }

    [Fact]
    public void SymmetricPercentageErrors_BothNearZero_ReturnsZero()
    {
        double[] actual = [1e-15];
        double[] predicted = [1e-15];
        double[] output = new double[1];

        ErrorHelpers.ComputeSymmetricPercentageErrors(actual, predicted, output);

        Assert.Equal(0.0, output[0], Tolerance);
    }

    [Fact]
    public void SymmetricPercentageErrors_WithNaN_SubstitutesLastValid()
    {
        double[] actual = [100.0, double.NaN];
        double[] predicted = [80.0, 90.0];
        double[] output = new double[2];

        ErrorHelpers.ComputeSymmetricPercentageErrors(actual, predicted, output);

        // [1]: NaN→100: |100-90| / ((100+90)/2) * 100 = 10/95*100
        double expected1 = 10.0 / 95.0 * 100.0;
        Assert.Equal(expected1, output[1], Tolerance);
    }

    // ── ComputeLogCoshErrors ────────────────────────────────────────────

    [Fact]
    public void LogCoshErrors_ZeroError_ReturnsZero()
    {
        double[] actual = [5.0];
        double[] predicted = [5.0];
        double[] output = new double[1];

        ErrorHelpers.ComputeLogCoshErrors(actual, predicted, output);

        Assert.Equal(0.0, output[0], Tolerance); // log(cosh(0)) = 0
    }

    [Fact]
    public void LogCoshErrors_SmallError_UsesExactFormula()
    {
        double[] actual = [10.0];
        double[] predicted = [7.0];
        double[] output = new double[1];

        ErrorHelpers.ComputeLogCoshErrors(actual, predicted, output);

        double expected = Math.Log(Math.Cosh(3.0));
        Assert.Equal(expected, output[0], Tolerance);
    }

    [Fact]
    public void LogCoshErrors_LargeError_UsesApproximation()
    {
        // |x| > 20 triggers approximation: |x| - log(2)
        double[] actual = [100.0];
        double[] predicted = [50.0];
        double[] output = new double[1];

        ErrorHelpers.ComputeLogCoshErrors(actual, predicted, output);

        double expected = 50.0 - Math.Log(2.0);
        Assert.Equal(expected, output[0], 1e-6);
    }

    [Fact]
    public void LogCoshErrors_EmptySpan_NoOp()
    {
        ErrorHelpers.ComputeLogCoshErrors(
            ReadOnlySpan<double>.Empty,
            ReadOnlySpan<double>.Empty,
            Span<double>.Empty);
        Assert.True(true);
    }

    [Fact]
    public void LogCoshErrors_LengthMismatch_Throws()
    {
        double[] a = [1.0, 2.0];
        double[] b = [1.0];
        double[] o = [0.0, 0.0];

        Assert.Throws<ArgumentException>(() =>
            ErrorHelpers.ComputeLogCoshErrors(a, b, o));
    }

    [Fact]
    public void LogCoshErrors_AlwaysNonNegative()
    {
        double[] actual = [5.0, -3.0, 10.0];
        double[] predicted = [8.0, -1.0, 10.0];
        double[] output = new double[3];

        ErrorHelpers.ComputeLogCoshErrors(actual, predicted, output);

        for (int i = 0; i < 3; i++)
        {
            Assert.True(output[i] >= 0.0, $"LogCosh at {i} was {output[i]}");
        }
    }

    [Fact]
    public void LogCoshErrors_WithNaN_SubstitutesLastValid()
    {
        double[] actual = [10.0, double.NaN];
        double[] predicted = [7.0, 7.0];
        double[] output = new double[2];

        ErrorHelpers.ComputeLogCoshErrors(actual, predicted, output);

        double expected = Math.Log(Math.Cosh(3.0));
        Assert.Equal(expected, output[0], Tolerance);
        Assert.Equal(expected, output[1], Tolerance); // NaN→10, same result
    }

    // ── ComputePseudoHuberErrors ────────────────────────────────────────

    [Fact]
    public void PseudoHuberErrors_ZeroError_ReturnsZero()
    {
        double[] actual = [5.0];
        double[] predicted = [5.0];
        double[] output = new double[1];

        ErrorHelpers.ComputePseudoHuberErrors(actual, predicted, output);

        Assert.Equal(0.0, output[0], Tolerance); // δ²(√(1+0)-1) = 0
    }

    [Fact]
    public void PseudoHuberErrors_BasicComputation()
    {
        double[] actual = [10.0];
        double[] predicted = [8.0];
        double[] output = new double[1];
        double delta = 1.0;

        ErrorHelpers.ComputePseudoHuberErrors(actual, predicted, output, delta);

        // δ²(√(1+(2/1)²)-1) = 1*(√5-1) ≈ 1.2360679...
        double expected = Math.Sqrt(1.0 + 4.0) - 1.0;
        Assert.Equal(expected, output[0], Tolerance);
    }

    [Fact]
    public void PseudoHuberErrors_CustomDelta()
    {
        double[] actual = [10.0];
        double[] predicted = [8.0];
        double[] output = new double[1];
        double delta = 2.0;

        ErrorHelpers.ComputePseudoHuberErrors(actual, predicted, output, delta);

        // δ²(√(1+(2/2)²)-1) = 4*(√2-1) ≈ 1.6568...
        double expected = 4.0 * (Math.Sqrt(2.0) - 1.0);
        Assert.Equal(expected, output[0], Tolerance);
    }

    [Fact]
    public void PseudoHuberErrors_EmptySpan_NoOp()
    {
        ErrorHelpers.ComputePseudoHuberErrors(
            ReadOnlySpan<double>.Empty,
            ReadOnlySpan<double>.Empty,
            Span<double>.Empty);
        Assert.True(true);
    }

    [Fact]
    public void PseudoHuberErrors_LengthMismatch_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ErrorHelpers.ComputePseudoHuberErrors([1.0], [1.0, 2.0], new double[1]));
    }

    [Fact]
    public void PseudoHuberErrors_WithNaN_SubstitutesLastValid()
    {
        double[] actual = [10.0, double.NaN];
        double[] predicted = [8.0, 8.0];
        double[] output = new double[2];

        ErrorHelpers.ComputePseudoHuberErrors(actual, predicted, output);

        // Both should compute same result since NaN→10
        Assert.Equal(output[0], output[1], Tolerance);
    }

    // ── ComputeTukeyBiweightErrors ──────────────────────────────────────

    [Fact]
    public void TukeyBiweightErrors_SmallError_InlierFormula()
    {
        double[] actual = [10.0];
        double[] predicted = [9.0];
        double[] output = new double[1];
        double c = 4.685;

        ErrorHelpers.ComputeTukeyBiweightErrors(actual, predicted, output, c);

        // diff = 1.0, |diff| ≤ c
        double ratio = 1.0 / c;
        double ratioSq = ratio * ratio;
        double oneMinusRatioSq = 1.0 - ratioSq;
        double cubed = oneMinusRatioSq * oneMinusRatioSq * oneMinusRatioSq;
        double expected = (c * c / 6.0) * (1.0 - cubed);
        Assert.Equal(expected, output[0], Tolerance);
    }

    [Fact]
    public void TukeyBiweightErrors_LargeError_OutlierRejection()
    {
        double[] actual = [100.0];
        double[] predicted = [0.0];
        double[] output = new double[1];
        double c = 4.685;

        ErrorHelpers.ComputeTukeyBiweightErrors(actual, predicted, output, c);

        // |diff| = 100 > c, so output = c²/6
        double expected = c * c / 6.0;
        Assert.Equal(expected, output[0], Tolerance);
    }

    [Fact]
    public void TukeyBiweightErrors_EmptySpan_NoOp()
    {
        ErrorHelpers.ComputeTukeyBiweightErrors(
            ReadOnlySpan<double>.Empty,
            ReadOnlySpan<double>.Empty,
            Span<double>.Empty);
        Assert.True(true);
    }

    [Fact]
    public void TukeyBiweightErrors_LengthMismatch_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ErrorHelpers.ComputeTukeyBiweightErrors([1.0, 2.0], [1.0], new double[2]));
    }

    [Fact]
    public void TukeyBiweightErrors_CustomC()
    {
        double[] actual = [10.0];
        double[] predicted = [8.0];
        double[] output = new double[1];
        double c = 2.0; // Small c so diff=2 is right at boundary

        ErrorHelpers.ComputeTukeyBiweightErrors(actual, predicted, output, c);

        // |diff| = 2.0 = c, so ratio = 1, ratioSq = 1, 1-ratioSq = 0, cubed = 0
        // output = c²/6 * (1-0) = c²/6
        double expected = c * c / 6.0;
        Assert.Equal(expected, output[0], Tolerance);
    }

    [Fact]
    public void TukeyBiweightErrors_WithNaN_SubstitutesLastValid()
    {
        double[] actual = [10.0, double.NaN];
        double[] predicted = [9.0, 9.0];
        double[] output = new double[2];

        ErrorHelpers.ComputeTukeyBiweightErrors(actual, predicted, output);

        // Both should compute same result since NaN→10
        Assert.Equal(output[0], output[1], Tolerance);
    }

    // ── ComputeHuberErrors ──────────────────────────────────────────────

    [Fact]
    public void HuberErrors_SmallError_QuadraticRegion()
    {
        double[] actual = [10.0];
        double[] predicted = [9.5];
        double[] output = new double[1];
        double delta = 1.0;

        ErrorHelpers.ComputeHuberErrors(actual, predicted, output, delta);

        // |diff| = 0.5 ≤ delta → 0.5 * diff² = 0.5 * 0.25 = 0.125
        Assert.Equal(0.125, output[0], Tolerance);
    }

    [Fact]
    public void HuberErrors_LargeError_LinearRegion()
    {
        double[] actual = [10.0];
        double[] predicted = [5.0];
        double[] output = new double[1];
        double delta = 1.0;

        ErrorHelpers.ComputeHuberErrors(actual, predicted, output, delta);

        // |diff| = 5 > delta → delta * (|diff| - 0.5*delta) = 1*(5-0.5) = 4.5
        Assert.Equal(4.5, output[0], Tolerance);
    }

    [Fact]
    public void HuberErrors_EmptySpan_NoOp()
    {
        ErrorHelpers.ComputeHuberErrors(
            ReadOnlySpan<double>.Empty,
            ReadOnlySpan<double>.Empty,
            Span<double>.Empty);
        Assert.True(true);
    }

    [Fact]
    public void HuberErrors_LengthMismatch_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ErrorHelpers.ComputeHuberErrors([1.0], [1.0, 2.0], new double[1]));
    }

    [Fact]
    public void HuberErrors_CustomDelta()
    {
        double[] actual = [10.0];
        double[] predicted = [7.0];
        double[] output = new double[1];
        double delta = 2.0;

        ErrorHelpers.ComputeHuberErrors(actual, predicted, output, delta);

        // |diff| = 3 > delta=2 → 2*(3-1) = 4.0
        Assert.Equal(4.0, output[0], Tolerance);
    }

    [Fact]
    public void HuberErrors_ExactlyAtDelta_UsesQuadratic()
    {
        double[] actual = [10.0];
        double[] predicted = [9.0];
        double[] output = new double[1];
        double delta = 1.0;

        ErrorHelpers.ComputeHuberErrors(actual, predicted, output, delta);

        // |diff| = 1.0 = delta → 0.5 * 1² = 0.5
        Assert.Equal(0.5, output[0], Tolerance);
    }

    [Fact]
    public void HuberErrors_WithNaN_SubstitutesLastValid()
    {
        double[] actual = [10.0, double.NaN];
        double[] predicted = [9.5, 9.5];
        double[] output = new double[2];

        ErrorHelpers.ComputeHuberErrors(actual, predicted, output);

        Assert.Equal(output[0], output[1], Tolerance);
    }

    // ── ApplyRollingMean ────────────────────────────────────────────────

    [Fact]
    public void RollingMean_BasicComputation()
    {
        double[] errors = [2.0, 4.0, 6.0, 8.0, 10.0];
        double[] output = new double[5];

        ErrorHelpers.ApplyRollingMean(errors, output, period: 3);

        // Warmup: output[0]=2/1=2, output[1]=(2+4)/2=3, output[2]=(2+4+6)/3=4
        Assert.Equal(2.0, output[0], Tolerance);
        Assert.Equal(3.0, output[1], Tolerance);
        Assert.Equal(4.0, output[2], Tolerance);
        // Main: output[3]=(4+6+8)/3=6, output[4]=(6+8+10)/3=8
        Assert.Equal(6.0, output[3], Tolerance);
        Assert.Equal(8.0, output[4], Tolerance);
    }

    [Fact]
    public void RollingMean_EmptySpan_NoOp()
    {
        ErrorHelpers.ApplyRollingMean(
            ReadOnlySpan<double>.Empty,
            Span<double>.Empty,
            period: 3);
        Assert.True(true);
    }

    [Fact]
    public void RollingMean_LengthMismatch_Throws()
    {
        double[] a = [1.0, 2.0];
        double[] b = [0.0];

        Assert.Throws<ArgumentException>(() =>
            ErrorHelpers.ApplyRollingMean(a, b, period: 2));
    }

    [Fact]
    public void RollingMean_PeriodZero_Throws()
    {
        double[] a = [1.0];
        double[] b = [0.0];

        Assert.Throws<ArgumentException>(() =>
            ErrorHelpers.ApplyRollingMean(a, b, period: 0));
    }

    [Fact]
    public void RollingMean_NegativePeriod_Throws()
    {
        double[] a = [1.0];
        double[] b = [0.0];

        Assert.Throws<ArgumentException>(() =>
            ErrorHelpers.ApplyRollingMean(a, b, period: -1));
    }

    [Fact]
    public void RollingMean_PeriodGreaterThanLength_WarmupOnly()
    {
        double[] errors = [2.0, 4.0, 6.0];
        double[] output = new double[3];

        // Period 10 > length 3 → all in warmup phase
        ErrorHelpers.ApplyRollingMean(errors, output, period: 10);

        Assert.Equal(2.0, output[0], Tolerance);        // 2/1
        Assert.Equal(3.0, output[1], Tolerance);        // (2+4)/2
        Assert.Equal(4.0, output[2], Tolerance);        // (2+4+6)/3
    }

    [Fact]
    public void RollingMean_ResyncCorrectsDrift()
    {
        // Use a short resync interval to trigger the resync path
        int period = 3;
        int len = 10;
        double[] errors = new double[len];
        double[] output = new double[len];
        for (int i = 0; i < len; i++)
        {
            errors[i] = 1.0; // Constant 1.0
        }

        ErrorHelpers.ApplyRollingMean(errors, output, period, resyncInterval: 3);

        // After warmup, all values should be 1.0 (mean of three 1.0s)
        for (int i = period - 1; i < len; i++)
        {
            Assert.Equal(1.0, output[i], 1e-8);
        }
    }

    [Fact]
    public void RollingMean_LargePeriod_UsesArrayPool()
    {
        // Period > 256 triggers ArrayPool path
        int period = 300;
        int len = period + 10;
        double[] errors = new double[len];
        double[] output = new double[len];
        for (int i = 0; i < len; i++)
        {
            errors[i] = 2.0;
        }

        ErrorHelpers.ApplyRollingMean(errors, output, period);

        // After warmup, should be 2.0 (mean of constant 2.0)
        Assert.Equal(2.0, output[len - 1], 1e-8);
    }

    // ── ApplyRollingMeanSqrt ────────────────────────────────────────────

    [Fact]
    public void RollingMeanSqrt_BasicComputation()
    {
        double[] squaredErrors = [4.0, 9.0, 16.0];
        double[] output = new double[3];

        ErrorHelpers.ApplyRollingMeanSqrt(squaredErrors, output, period: 2);

        // Warmup: output[0] = √(4/1) = 2
        Assert.Equal(2.0, output[0], Tolerance);
        // output[1] = √((4+9)/2) = √6.5
        Assert.Equal(Math.Sqrt(6.5), output[1], Tolerance);
        // Main: output[2] = √((9+16)/2) = √12.5
        Assert.Equal(Math.Sqrt(12.5), output[2], Tolerance);
    }

    [Fact]
    public void RollingMeanSqrt_EmptySpan_NoOp()
    {
        ErrorHelpers.ApplyRollingMeanSqrt(
            ReadOnlySpan<double>.Empty,
            Span<double>.Empty,
            period: 3);
        Assert.True(true);
    }

    [Fact]
    public void RollingMeanSqrt_LengthMismatch_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ErrorHelpers.ApplyRollingMeanSqrt([1.0, 2.0], new double[1], period: 2));
    }

    [Fact]
    public void RollingMeanSqrt_PeriodZero_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ErrorHelpers.ApplyRollingMeanSqrt([1.0], new double[1], period: 0));
    }

    [Fact]
    public void RollingMeanSqrt_LargePeriod_UsesArrayPool()
    {
        int period = 300;
        int len = period + 5;
        double[] errors = new double[len];
        double[] output = new double[len];
        for (int i = 0; i < len; i++)
        {
            errors[i] = 9.0;
        }

        ErrorHelpers.ApplyRollingMeanSqrt(errors, output, period);

        // √(9) = 3
        Assert.Equal(3.0, output[len - 1], 1e-8);
    }

    [Fact]
    public void RollingMeanSqrt_ResyncCorrectsDrift()
    {
        int period = 3;
        int len = 10;
        double[] errors = new double[len];
        double[] output = new double[len];
        for (int i = 0; i < len; i++)
        {
            errors[i] = 4.0;
        }

        ErrorHelpers.ApplyRollingMeanSqrt(errors, output, period, resyncInterval: 3);

        for (int i = period - 1; i < len; i++)
        {
            Assert.Equal(2.0, output[i], 1e-8); // √(4) = 2
        }
    }

    // ── ApplyRollingWeightedMeanSqrt ────────────────────────────────────

    [Fact]
    public void RollingWeightedMeanSqrt_BasicComputation()
    {
        double[] wse = [4.0, 8.0, 12.0];
        double[] weights = [1.0, 2.0, 3.0];
        double[] output = new double[3];

        ErrorHelpers.ApplyRollingWeightedMeanSqrt(wse, weights, output, period: 2);

        // Warmup[0]: √(4/1) = 2
        Assert.Equal(2.0, output[0], Tolerance);
        // Warmup[1]: √((4+8)/(1+2)) = √(12/3) = √4 = 2
        Assert.Equal(2.0, output[1], Tolerance);
        // Main[2]: √((8+12)/(2+3)) = √(20/5) = √4 = 2
        Assert.Equal(2.0, output[2], Tolerance);
    }

    [Fact]
    public void RollingWeightedMeanSqrt_EmptySpan_NoOp()
    {
        ErrorHelpers.ApplyRollingWeightedMeanSqrt(
            ReadOnlySpan<double>.Empty,
            ReadOnlySpan<double>.Empty,
            Span<double>.Empty,
            period: 3);
        Assert.True(true);
    }

    [Fact]
    public void RollingWeightedMeanSqrt_LengthMismatch_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ErrorHelpers.ApplyRollingWeightedMeanSqrt(
                [1.0, 2.0], [1.0], new double[2], period: 2));
    }

    [Fact]
    public void RollingWeightedMeanSqrt_PeriodZero_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ErrorHelpers.ApplyRollingWeightedMeanSqrt(
                [1.0], [1.0], new double[1], period: 0));
    }

    [Fact]
    public void RollingWeightedMeanSqrt_ZeroWeights_ReturnsZero()
    {
        double[] wse = [10.0, 20.0, 30.0];
        double[] weights = [0.0, 0.0, 0.0];
        double[] output = new double[3];

        ErrorHelpers.ApplyRollingWeightedMeanSqrt(wse, weights, output, period: 2);

        // sumWeights ≤ 1e-10 → returns 0.0
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(0.0, output[i], Tolerance);
        }
    }

    [Fact]
    public void RollingWeightedMeanSqrt_LargePeriod_UsesArrayPool()
    {
        int period = 300;
        int len = period + 5;
        double[] wse = new double[len];
        double[] weights = new double[len];
        double[] output = new double[len];
        for (int i = 0; i < len; i++)
        {
            wse[i] = 9.0;
            weights[i] = 1.0;
        }

        ErrorHelpers.ApplyRollingWeightedMeanSqrt(wse, weights, output, period);

        // √(9*period / period) = √9 = 3
        Assert.Equal(3.0, output[len - 1], 1e-8);
    }

    [Fact]
    public void RollingWeightedMeanSqrt_ResyncCorrectsDrift()
    {
        int period = 2;
        int len = 10;
        double[] wse = new double[len];
        double[] weights = new double[len];
        double[] output = new double[len];
        for (int i = 0; i < len; i++)
        {
            wse[i] = 16.0;
            weights[i] = 1.0;
        }

        ErrorHelpers.ApplyRollingWeightedMeanSqrt(wse, weights, output, period, resyncInterval: 3);

        for (int i = period - 1; i < len; i++)
        {
            Assert.Equal(4.0, output[i], 1e-8); // √(16) = 4
        }
    }

    // ── SanitizeInputs ──────────────────────────────────────────────────

    [Fact]
    public void SanitizeInputs_CleanData_CopiesAsIs()
    {
        double[] actual = [1.0, 2.0, 3.0];
        double[] predicted = [4.0, 5.0, 6.0];
        double[] actualOut = new double[3];
        double[] predictedOut = new double[3];

        ErrorHelpers.SanitizeInputs(actual, predicted, actualOut, predictedOut);

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(actual[i], actualOut[i], Tolerance);
            Assert.Equal(predicted[i], predictedOut[i], Tolerance);
        }
    }

    [Fact]
    public void SanitizeInputs_WithNaN_ReplacesWithLastValid()
    {
        double[] actual = [10.0, double.NaN, 30.0];
        double[] predicted = [5.0, double.NaN, 15.0];
        double[] actualOut = new double[3];
        double[] predictedOut = new double[3];

        ErrorHelpers.SanitizeInputs(actual, predicted, actualOut, predictedOut);

        Assert.Equal(10.0, actualOut[0], Tolerance);
        Assert.Equal(10.0, actualOut[1], Tolerance); // NaN → 10
        Assert.Equal(30.0, actualOut[2], Tolerance);
        Assert.Equal(5.0, predictedOut[0], Tolerance);
        Assert.Equal(5.0, predictedOut[1], Tolerance); // NaN → 5
        Assert.Equal(15.0, predictedOut[2], Tolerance);
    }

    [Fact]
    public void SanitizeInputs_EmptySpan_NoOp()
    {
        ErrorHelpers.SanitizeInputs(
            ReadOnlySpan<double>.Empty,
            ReadOnlySpan<double>.Empty,
            Span<double>.Empty,
            Span<double>.Empty);
        Assert.True(true);
    }

    [Fact]
    public void SanitizeInputs_LengthMismatch_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ErrorHelpers.SanitizeInputs([1.0], [1.0, 2.0], new double[1], new double[1]));
    }

    [Fact]
    public void SanitizeInputs_AllNaN_UsesZero()
    {
        double[] actual = [double.NaN, double.NaN];
        double[] predicted = [double.NaN, double.NaN];
        double[] actualOut = new double[2];
        double[] predictedOut = new double[2];

        ErrorHelpers.SanitizeInputs(actual, predicted, actualOut, predictedOut);

        // FindFirstValidValue returns 0.0 when all NaN
        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(0.0, actualOut[i], Tolerance);
            Assert.Equal(0.0, predictedOut[i], Tolerance);
        }
    }

    [Fact]
    public void SanitizeInputs_InfinityReplacedWithLastValid()
    {
        double[] actual = [10.0, double.PositiveInfinity, 30.0];
        double[] predicted = [5.0, double.NegativeInfinity, 15.0];
        double[] actualOut = new double[3];
        double[] predictedOut = new double[3];

        ErrorHelpers.SanitizeInputs(actual, predicted, actualOut, predictedOut);

        Assert.Equal(10.0, actualOut[1], Tolerance);  // Inf → 10
        Assert.Equal(5.0, predictedOut[1], Tolerance); // -Inf → 5
    }

    // ── Cross-method consistency ────────────────────────────────────────

    [Fact]
    public void SignedErrors_AbsoluteErrors_Consistency()
    {
        // |signed| should equal absolute
        double[] actual = [10.0, -5.0, 20.0, 0.0, -3.0];
        double[] predicted = [7.0, -2.0, 25.0, -1.0, 3.0];
        double[] signedOut = new double[5];
        double[] absOut = new double[5];

        ErrorHelpers.ComputeSignedErrors(actual, predicted, signedOut);
        ErrorHelpers.ComputeAbsoluteErrors(actual, predicted, absOut);

        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(Math.Abs(signedOut[i]), absOut[i], Tolerance);
        }
    }

    [Fact]
    public void SquaredErrors_EqualsSignedErrorsSquared()
    {
        double[] actual = [10.0, 5.0, -3.0];
        double[] predicted = [8.0, 7.0, -1.0];
        double[] signedOut = new double[3];
        double[] sqOut = new double[3];

        ErrorHelpers.ComputeSignedErrors(actual, predicted, signedOut);
        ErrorHelpers.ComputeSquaredErrors(actual, predicted, sqOut);

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(signedOut[i] * signedOut[i], sqOut[i], Tolerance);
        }
    }

    [Fact]
    public void PerfectPrediction_AllErrorsZero()
    {
        double[] actual = [1.0, 2.0, 3.0, 4.0];
        double[] predicted = [1.0, 2.0, 3.0, 4.0];

        double[] signed = new double[4];
        double[] abs = new double[4];
        double[] sq = new double[4];
        double[] logcosh = new double[4];

        ErrorHelpers.ComputeSignedErrors(actual, predicted, signed);
        ErrorHelpers.ComputeAbsoluteErrors(actual, predicted, abs);
        ErrorHelpers.ComputeSquaredErrors(actual, predicted, sq);
        ErrorHelpers.ComputeLogCoshErrors(actual, predicted, logcosh);

        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(0.0, signed[i], Tolerance);
            Assert.Equal(0.0, abs[i], Tolerance);
            Assert.Equal(0.0, sq[i], Tolerance);
            Assert.Equal(0.0, logcosh[i], Tolerance);
        }
    }

    [Fact]
    public void HuberErrors_ApproachesQuadratic_ForSmallErrors()
    {
        // For very small errors, Huber ≈ 0.5 * error²
        double[] actual = [10.0];
        double[] predicted = [10.001];
        double[] huberOut = new double[1];
        double[] sqOut = new double[1];

        ErrorHelpers.ComputeHuberErrors(actual, predicted, huberOut, delta: 1.0);
        ErrorHelpers.ComputeSquaredErrors(actual, predicted, sqOut);

        // Huber should be 0.5 * squared for small |diff|
        Assert.Equal(0.5 * sqOut[0], huberOut[0], 1e-8);
    }

    [Fact]
    public void SymmetricPercentageErrors_Symmetric()
    {
        // SMAPE should give same result regardless of which is actual/predicted
        double[] a = [100.0];
        double[] b = [80.0];
        double[] out1 = new double[1];
        double[] out2 = new double[1];

        ErrorHelpers.ComputeSymmetricPercentageErrors(a, b, out1);
        ErrorHelpers.ComputeSymmetricPercentageErrors(b, a, out2);

        Assert.Equal(out1[0], out2[0], Tolerance);
    }
}
