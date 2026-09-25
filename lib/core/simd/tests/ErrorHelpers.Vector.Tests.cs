using System.Numerics;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class ErrorHelpersVectorTests
{
    private readonly ITestOutputHelper _output;

    public ErrorHelpersVectorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SignedErrors_VectorCore_MatchesScalarCore_AllFinite()
    {
        var (actual, predicted) = BuildPair(1024, seed: 101, withNonFinite: false);
        VerifySigned(actual, predicted);
    }

    [Fact]
    public void SignedErrors_VectorCore_MatchesScalarCore_WithNonFinite()
    {
        var (actual, predicted) = BuildPair(1024, seed: 102, withNonFinite: true);
        VerifySigned(actual, predicted);
    }

    [Fact]
    public void SignedErrors_VectorCore_MatchesScalarCore_EdgeValues()
    {
        VerifySigned([0.0, -0.0, 1e-12, -1.5, 3.25, 0.0, 1e-300, -1e-300], [0.0, 0.0, 2e-12, 1.5, -3.25, 1e-300, -1e-300, 1e-300]);
        VerifySigned([double.NaN, double.NaN, 5.0, double.NaN], [1.0, 2.0, 3.0, 4.0]);
        VerifySigned([double.PositiveInfinity, -5.0, double.NegativeInfinity, 2.0], [1.0, 2.0, 1.0, 1.0]);
        VerifySigned([-1.0, -2.0, -3.0, -4.0, -5.0, -6.0, -7.0], [1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0]);
    }

    [Fact]
    public void SignedErrors_VectorCore_MatchesScalarCore_ShortArrays()
    {
        int limit = Vector<double>.Count + 2;
        for (int n = 0; n <= limit; n++)
        {
            var (actual, predicted) = BuildPair(n, seed: 1200 + n, withNonFinite: false);
            VerifySigned(actual, predicted);
            (actual, predicted) = BuildPair(n, seed: 2200 + n, withNonFinite: true);
            VerifySigned(actual, predicted);
        }
    }

    [Fact]
    public void AbsoluteErrors_VectorCore_MatchesScalarCore_AllFinite()
    {
        var (actual, predicted) = BuildPair(1024, seed: 201, withNonFinite: false);
        VerifyAbsolute(actual, predicted);
    }

    [Fact]
    public void AbsoluteErrors_VectorCore_MatchesScalarCore_WithNonFinite()
    {
        var (actual, predicted) = BuildPair(1024, seed: 202, withNonFinite: true);
        VerifyAbsolute(actual, predicted);
    }

    [Fact]
    public void AbsoluteErrors_VectorCore_MatchesScalarCore_EdgeValues()
    {
        VerifyAbsolute([0.0, -0.0, 1e-12, -1.5, 3.25, 0.0, 1e-300, -1e-300], [0.0, 0.0, 2e-12, 1.5, -3.25, 1e-300, -1e-300, 1e-300]);
        VerifyAbsolute([double.NaN, 5.0, double.NaN], [1.0, 3.0, 4.0]);
        VerifyAbsolute([double.PositiveInfinity, -5.0, double.NegativeInfinity], [1.0, 2.0, 1.0]);
        VerifyAbsolute([-1.0, -2.0, -3.0, -4.0, -5.0, -6.0, -7.0, -8.0], [8.0, 7.0, 6.0, 5.0, 4.0, 3.0, 2.0, 1.0]);
    }

    [Fact]
    public void AbsoluteErrors_VectorCore_MatchesScalarCore_ShortArrays()
    {
        int limit = Vector<double>.Count + 2;
        for (int n = 0; n <= limit; n++)
        {
            var (actual, predicted) = BuildPair(n, seed: 1300 + n, withNonFinite: false);
            VerifyAbsolute(actual, predicted);
            (actual, predicted) = BuildPair(n, seed: 2300 + n, withNonFinite: true);
            VerifyAbsolute(actual, predicted);
        }
    }

    [Fact]
    public void SquaredErrors_VectorCore_MatchesScalarCore_AllFinite()
    {
        var (actual, predicted) = BuildPair(1024, seed: 301, withNonFinite: false);
        VerifySquared(actual, predicted);
    }

    [Fact]
    public void SquaredErrors_VectorCore_MatchesScalarCore_WithNonFinite()
    {
        var (actual, predicted) = BuildPair(1024, seed: 302, withNonFinite: true);
        VerifySquared(actual, predicted);
    }

    [Fact]
    public void SquaredErrors_VectorCore_MatchesScalarCore_EdgeValues()
    {
        VerifySquared([0.0, -0.0, 1e-12, -1.5, 3.25, 1e-160, -1e160], [1e-300, 0.0, 2e-12, 1.5, -3.25, -1e160, 1e160]);
        VerifySquared([double.NaN, 5.0, double.NaN], [1.0, 3.0, 4.0]);
        VerifySquared([double.PositiveInfinity, -5.0, double.NegativeInfinity], [1.0, 2.0, 1.0]);
        VerifySquared([-1.0, -2.0, -3.0, -4.0, -5.0, -6.0], [1.0, 1.0, 1.0, 1.0, 1.0, 1.0]);
    }

    [Fact]
    public void SquaredErrors_VectorCore_MatchesScalarCore_ShortArrays()
    {
        int limit = Vector<double>.Count + 2;
        for (int n = 0; n <= limit; n++)
        {
            var (actual, predicted) = BuildPair(n, seed: 1400 + n, withNonFinite: false);
            VerifySquared(actual, predicted);
            (actual, predicted) = BuildPair(n, seed: 2400 + n, withNonFinite: true);
            VerifySquared(actual, predicted);
        }
    }

    [Fact]
    public void WeightedErrors_VectorCore_MatchesScalarCore_AllFinite()
    {
        var (actual, predicted, weights) = BuildTriple(1024, seed: 401, withNonFinite: false);
        VerifyWeighted(actual, predicted, weights);
    }

    [Fact]
    public void WeightedErrors_VectorCore_MatchesScalarCore_WithNonFinite()
    {
        var (actual, predicted, weights) = BuildTriple(1024, seed: 402, withNonFinite: true);
        VerifyWeighted(actual, predicted, weights);
    }

    [Fact]
    public void WeightedErrors_VectorCore_MatchesScalarCore_EdgeValues()
    {
        VerifyWeighted([10.0, 20.0, -30.0, 0.0], [8.0, 18.0, -28.0, 5.0], [0.0, 1.0, -1.0, 0.5]);
        VerifyWeighted([double.NaN, 5.0, double.NaN], [1.0, 3.0, 4.0], [1.0, double.NaN, 2.0]);
        VerifyWeighted([1e160, -1e160, 0.0], [0.0, 0.0, 0.0], [1.0, 1.0, 2.0]);
    }

    [Fact]
    public void WeightedErrors_VectorCore_MatchesScalarCore_ShortArrays()
    {
        int limit = Vector<double>.Count + 2;
        for (int n = 0; n <= limit; n++)
        {
            var (actual, predicted, weights) = BuildTriple(n, seed: 1500 + n, withNonFinite: false);
            VerifyWeighted(actual, predicted, weights);
            (actual, predicted, weights) = BuildTriple(n, seed: 2500 + n, withNonFinite: true);
            VerifyWeighted(actual, predicted, weights);
        }
    }

    [Fact]
    public void PercentageErrors_VectorCore_MatchesScalarCore_AllFinite()
    {
        var (actual, predicted) = BuildPair(1024, seed: 501, withNonFinite: false);
        VerifyPercentage(actual, predicted, 1e-10);
    }

    [Fact]
    public void PercentageErrors_VectorCore_MatchesScalarCore_WithNonFinite()
    {
        var (actual, predicted) = BuildPair(1024, seed: 502, withNonFinite: true);
        VerifyPercentage(actual, predicted, 1e-10);
    }

    [Fact]
    public void PercentageErrors_VectorCore_MatchesScalarCore_EdgeValues()
    {
        VerifyPercentage([1e-13, 1e-12, 1e-11, 5e-11, 1e-10, 2e-10, -1e-12, double.Epsilon], [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, -1.0, 1e-13], 1e-10);
        VerifyPercentage([-50.0, 200.0, -3.0, 0.0], [47.0, 210.0, -7.0, 1e-13], 1e-10);
        VerifyPercentage([1e308, -1e308, 7.0], [1e308, 1e308, -200.0], 1e-10);
        VerifyPercentage([double.NaN, 100.0, double.PositiveInfinity], [90.0, 80.0, 50.0], 1e-10);
    }

    [Fact]
    public void PercentageErrors_VectorCore_MatchesScalarCore_CustomEpsilon()
    {
        var (actual, predicted) = BuildPair(256, seed: 503, withNonFinite: false);
        VerifyPercentage(actual, predicted, 5e-11);
        VerifyPercentage([1e-13, 1e-12, 5e-11, 1e-10, -5e-11, 0.0], [1.0, 2.0, 3.0, 4.0, -5e-11, 1e-13], 5e-11);
    }

    [Fact]
    public void PercentageErrors_VectorCore_MatchesScalarCore_ShortArrays()
    {
        int limit = Vector<double>.Count + 2;
        for (int n = 0; n <= limit; n++)
        {
            var (actual, predicted) = BuildPair(n, seed: 1600 + n, withNonFinite: false);
            VerifyPercentage(actual, predicted, 1e-10);
            (actual, predicted) = BuildPair(n, seed: 2600 + n, withNonFinite: true);
            VerifyPercentage(actual, predicted, 1e-10);
        }
    }

    [Fact]
    public void SymmetricPercentageErrors_VectorCore_MatchesScalarCore_AllFinite()
    {
        var (actual, predicted) = BuildPair(1024, seed: 601, withNonFinite: false);
        VerifySymmetricPercentage(actual, predicted, 1e-10);
    }

    [Fact]
    public void SymmetricPercentageErrors_VectorCore_MatchesScalarCore_WithNonFinite()
    {
        var (actual, predicted) = BuildPair(1024, seed: 602, withNonFinite: true);
        VerifySymmetricPercentage(actual, predicted, 1e-10);
    }

    [Fact]
    public void SymmetricPercentageErrors_VectorCore_MatchesScalarCore_EdgeValues()
    {
        VerifySymmetricPercentage([1e-13, 1e-11, 0.0, 2e-10], [2e-13, 1e-9, 1e-14, 0.0], 1e-10);
        VerifySymmetricPercentage([100.0, 80.0, -50.0, 0.0], [80.0, 90.0, -60.0, 5.0], 1e-10);
        VerifySymmetricPercentage([1e300, 1.0, -0.0], [1e300, 1e300, 0.0], 1e-10);
        VerifySymmetricPercentage([double.NaN, 100.0, double.NegativeInfinity], [80.0, 90.0, 50.0], 1e-10);
    }

    [Fact]
    public void SymmetricPercentageErrors_VectorCore_MatchesScalarCore_CustomEpsilon()
    {
        var (actual, predicted) = BuildPair(256, seed: 603, withNonFinite: false);
        VerifySymmetricPercentage(actual, predicted, 5e-11);
        VerifySymmetricPercentage([1e-14, 5e-11, 1e-10, -5e-11, 0.0], [1e-13, 1e-11, 1e-9, 5e-11, 1e-14], 5e-11);
    }

    [Fact]
    public void SymmetricPercentageErrors_VectorCore_MatchesScalarCore_ShortArrays()
    {
        int limit = Vector<double>.Count + 2;
        for (int n = 0; n <= limit; n++)
        {
            var (actual, predicted) = BuildPair(n, seed: 1700 + n, withNonFinite: false);
            VerifySymmetricPercentage(actual, predicted, 1e-10);
            (actual, predicted) = BuildPair(n, seed: 2700 + n, withNonFinite: true);
            VerifySymmetricPercentage(actual, predicted, 1e-10);
        }
    }

    [Fact]
    public void PseudoHuberErrors_VectorCore_MatchesScalarCore_AllFinite()
    {
        var (actual, predicted) = BuildPair(1024, seed: 701, withNonFinite: false);
        VerifyPseudoHuber(actual, predicted, 1.0);
    }

    [Fact]
    public void PseudoHuberErrors_VectorCore_MatchesScalarCore_WithNonFinite()
    {
        var (actual, predicted) = BuildPair(1024, seed: 702, withNonFinite: true);
        VerifyPseudoHuber(actual, predicted, 1.0);
    }

    [Fact]
    public void PseudoHuberErrors_VectorCore_MatchesScalarCore_EdgeValues()
    {
        VerifyPseudoHuber([100.0, -100.0, 50.0, 0.0], [0.0, 0.0, 0.0, 0.0], 1.0);
        VerifyPseudoHuber([10.0, 10.5, -8.0, 0.001], [8.0, 8.0, -8.0, 0.0], 0.25);
        VerifyPseudoHuber([5.0, -0.0, 2.5], [5.0, 0.0, 2.5], 2.0);
        VerifyPseudoHuber([double.NaN, 10.0, double.PositiveInfinity], [8.0, 8.0, 8.0], 1.0);
    }

    [Fact]
    public void PseudoHuberErrors_VectorCore_MatchesScalarCore_ShortArrays()
    {
        int limit = Vector<double>.Count + 2;
        for (int n = 0; n <= limit; n++)
        {
            var (actual, predicted) = BuildPair(n, seed: 1800 + n, withNonFinite: false);
            VerifyPseudoHuber(actual, predicted, 1.0);
            (actual, predicted) = BuildPair(n, seed: 2800 + n, withNonFinite: true);
            VerifyPseudoHuber(actual, predicted, 1.0);
        }
    }

    [Fact]
    public void TukeyBiweightErrors_VectorCore_MatchesScalarCore_AllFinite()
    {
        var (actual, predicted) = BuildPair(1024, seed: 801, withNonFinite: false);
        VerifyTukey(actual, predicted, 4.685);
    }

    [Fact]
    public void TukeyBiweightErrors_VectorCore_MatchesScalarCore_WithNonFinite()
    {
        var (actual, predicted) = BuildPair(1024, seed: 802, withNonFinite: true);
        VerifyTukey(actual, predicted, 4.685);
    }

    [Fact]
    public void TukeyBiweightErrors_VectorCore_MatchesScalarCore_EdgeValues()
    {
        VerifyTukey([4.685, 4.686, 4.684, 2.0, -4.685], [0.0, 0.0, 0.0, 0.0, 0.0], 4.685);
        VerifyTukey([2.0, 2.000001, 1.999999, -2.0], [0.0, 0.0, 0.0, 0.0], 2.0);
        VerifyTukey([100.0, 0.0, -0.0, 1e-300], [0.0, 0.0, 0.0, 0.0], 4.685);
        VerifyTukey([double.NaN, 10.0, double.NegativeInfinity], [9.0, 9.0, 9.0], 4.685);
    }

    [Fact]
    public void TukeyBiweightErrors_VectorCore_MatchesScalarCore_ShortArrays()
    {
        int limit = Vector<double>.Count + 2;
        for (int n = 0; n <= limit; n++)
        {
            var (actual, predicted) = BuildPair(n, seed: 1900 + n, withNonFinite: false);
            VerifyTukey(actual, predicted, 4.685);
            (actual, predicted) = BuildPair(n, seed: 2900 + n, withNonFinite: true);
            VerifyTukey(actual, predicted, 4.685);
        }
    }

    [Fact]
    public void HuberErrors_VectorCore_MatchesScalarCore_AllFinite()
    {
        var (actual, predicted) = BuildPair(1024, seed: 901, withNonFinite: false);
        VerifyHuber(actual, predicted, 1.0);
    }

    [Fact]
    public void HuberErrors_VectorCore_MatchesScalarCore_WithNonFinite()
    {
        var (actual, predicted) = BuildPair(1024, seed: 902, withNonFinite: true);
        VerifyHuber(actual, predicted, 1.0);
    }

    [Fact]
    public void HuberErrors_VectorCore_MatchesScalarCore_EdgeValues()
    {
        VerifyHuber([1.0, 2.0, -1.0, -2.0, 0.5], [0.0, 0.0, 0.0, 0.0, 0.0], 1.0);
        VerifyHuber([2.0, 2.000001, 1.999999, -2.0, 0.0], [0.0, 0.0, 0.0, 0.0, 0.0], 2.0);
        VerifyHuber([100.0, 1e-300, -0.0], [0.0, 0.0, 1e-300], 1.0);
        VerifyHuber([double.NaN, 10.0, double.PositiveInfinity], [9.5, 9.5, 9.5], 1.0);
    }

    [Fact]
    public void HuberErrors_VectorCore_MatchesScalarCore_ShortArrays()
    {
        int limit = Vector<double>.Count + 2;
        for (int n = 0; n <= limit; n++)
        {
            var (actual, predicted) = BuildPair(n, seed: 2000 + n, withNonFinite: false);
            VerifyHuber(actual, predicted, 1.0);
            (actual, predicted) = BuildPair(n, seed: 3000 + n, withNonFinite: true);
            VerifyHuber(actual, predicted, 1.0);
        }
    }

    [Fact]
    public void SanitizeInputs_VectorCore_MatchesScalarCore_AllFinite()
    {
        var (actual, predicted) = BuildPair(1024, seed: 1001, withNonFinite: false);
        VerifySanitize(actual, predicted);
    }

    [Fact]
    public void SanitizeInputs_VectorCore_MatchesScalarCore_WithNonFinite()
    {
        var (actual, predicted) = BuildPair(1024, seed: 1002, withNonFinite: true);
        VerifySanitize(actual, predicted);
    }

    [Fact]
    public void SanitizeInputs_VectorCore_MatchesScalarCore_EdgeValues()
    {
        VerifySanitize([0.0, -0.0, 1e-12, -1.5, 3.25, 0.0, 1e-300, -1e-300], [0.0, 0.0, 2e-12, 1.5, -3.25, 1e-300, -1e-300, 1e-300]);
        VerifySanitize([double.NaN, double.NaN, 5.0, double.NaN], [1.0, double.NaN, 3.0, 4.0]);
        VerifySanitize([double.PositiveInfinity, -5.0, double.NegativeInfinity, 2.0], [1.0, double.NegativeInfinity, 1.0, 1.0]);
        VerifySanitize([double.NaN, double.NaN, double.NaN], [double.NaN, double.NaN, double.NaN]);
    }

    [Fact]
    public void SanitizeInputs_VectorCore_MatchesScalarCore_ShortArrays()
    {
        int limit = Vector<double>.Count + 2;
        for (int n = 0; n <= limit; n++)
        {
            var (actual, predicted) = BuildPair(n, seed: 2100 + n, withNonFinite: false);
            VerifySanitize(actual, predicted);
            (actual, predicted) = BuildPair(n, seed: 3100 + n, withNonFinite: true);
            VerifySanitize(actual, predicted);
        }
    }

    private void VerifySigned(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted)
    {
        double lastValidActual = ErrorHelpers.FindFirstValidValue(actual);
        double lastValidPredicted = ErrorHelpers.FindFirstValidValue(predicted);
        double[] scalar = new double[actual.Length];
        double[] vector = new double[actual.Length];
        ErrorHelpers.ComputeSignedErrorsScalar(actual, predicted, scalar, lastValidActual, lastValidPredicted);
        ErrorHelpers.ComputeSignedErrorsVectorCore(actual, predicted, vector, lastValidActual, lastValidPredicted);
        double maxDiff = Compare(scalar, vector);
        _output.WriteLine($"signed n={actual.Length} maxAbsDiff={maxDiff:G17}");
    }

    private void VerifyAbsolute(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted)
    {
        double lastValidActual = ErrorHelpers.FindFirstValidValue(actual);
        double lastValidPredicted = ErrorHelpers.FindFirstValidValue(predicted);
        double[] scalar = new double[actual.Length];
        double[] vector = new double[actual.Length];
        ErrorHelpers.ComputeAbsoluteErrorsScalar(actual, predicted, scalar, lastValidActual, lastValidPredicted);
        ErrorHelpers.ComputeAbsoluteErrorsVectorCore(actual, predicted, vector, lastValidActual, lastValidPredicted);
        double maxDiff = Compare(scalar, vector);
        _output.WriteLine($"absolute n={actual.Length} maxAbsDiff={maxDiff:G17}");
    }

    private void VerifySquared(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted)
    {
        double lastValidActual = ErrorHelpers.FindFirstValidValue(actual);
        double lastValidPredicted = ErrorHelpers.FindFirstValidValue(predicted);
        double[] scalar = new double[actual.Length];
        double[] vector = new double[actual.Length];
        ErrorHelpers.ComputeSquaredErrorsScalar(actual, predicted, scalar, lastValidActual, lastValidPredicted);
        ErrorHelpers.ComputeSquaredErrorsVectorCore(actual, predicted, vector, lastValidActual, lastValidPredicted);
        double maxDiff = Compare(scalar, vector);
        _output.WriteLine($"squared n={actual.Length} maxAbsDiff={maxDiff:G17}");
    }

    private void VerifyWeighted(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, ReadOnlySpan<double> weights)
    {
        double lastValidActual = ErrorHelpers.FindFirstValidValue(actual);
        double lastValidPredicted = ErrorHelpers.FindFirstValidValue(predicted);
        double lastValidWeight = ErrorHelpers.FindFirstValidValue(weights);
        double[] scalar = new double[actual.Length];
        double[] vector = new double[actual.Length];
        ErrorHelpers.ComputeWeightedErrorsScalarCore(actual, predicted, weights, scalar, lastValidActual, lastValidPredicted, lastValidWeight);
        ErrorHelpers.ComputeWeightedErrorsVectorCore(actual, predicted, weights, vector, lastValidActual, lastValidPredicted, lastValidWeight);
        double maxDiff = Compare(scalar, vector);
        _output.WriteLine($"weighted n={actual.Length} maxAbsDiff={maxDiff:G17}");
    }

    private void VerifyPercentage(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, double epsilon)
    {
        double lastValidActual = ErrorHelpers.FindFirstValidValue(actual);
        double lastValidPredicted = ErrorHelpers.FindFirstValidValue(predicted);
        double[] scalar = new double[actual.Length];
        double[] vector = new double[actual.Length];
        ErrorHelpers.ComputePercentageErrorsScalarCore(actual, predicted, scalar, lastValidActual, lastValidPredicted, epsilon);
        ErrorHelpers.ComputePercentageErrorsVectorCore(actual, predicted, vector, lastValidActual, lastValidPredicted, epsilon);
        double maxDiff = Compare(scalar, vector);
        _output.WriteLine($"percentage n={actual.Length} eps={epsilon:G17} maxAbsDiff={maxDiff:G17}");
    }

    private void VerifySymmetricPercentage(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, double epsilon)
    {
        double lastValidActual = ErrorHelpers.FindFirstValidValue(actual);
        double lastValidPredicted = ErrorHelpers.FindFirstValidValue(predicted);
        double[] scalar = new double[actual.Length];
        double[] vector = new double[actual.Length];
        ErrorHelpers.ComputeSymmetricPercentageErrorsScalarCore(actual, predicted, scalar, lastValidActual, lastValidPredicted, epsilon);
        ErrorHelpers.ComputeSymmetricPercentageErrorsVectorCore(actual, predicted, vector, lastValidActual, lastValidPredicted, epsilon);
        double maxDiff = Compare(scalar, vector);
        _output.WriteLine($"symmetric n={actual.Length} eps={epsilon:G17} maxAbsDiff={maxDiff:G17}");
    }

    private void VerifyPseudoHuber(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, double delta)
    {
        double lastValidActual = ErrorHelpers.FindFirstValidValue(actual);
        double lastValidPredicted = ErrorHelpers.FindFirstValidValue(predicted);
        double[] scalar = new double[actual.Length];
        double[] vector = new double[actual.Length];
        ErrorHelpers.ComputePseudoHuberErrorsScalarCore(actual, predicted, scalar, lastValidActual, lastValidPredicted, delta);
        ErrorHelpers.ComputePseudoHuberErrorsVectorCore(actual, predicted, vector, lastValidActual, lastValidPredicted, delta);
        double maxDiff = Compare(scalar, vector);
        _output.WriteLine($"pseudohuber n={actual.Length} delta={delta:G17} maxAbsDiff={maxDiff:G17}");
    }

    private void VerifyTukey(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, double c)
    {
        double lastValidActual = ErrorHelpers.FindFirstValidValue(actual);
        double lastValidPredicted = ErrorHelpers.FindFirstValidValue(predicted);
        double[] scalar = new double[actual.Length];
        double[] vector = new double[actual.Length];
        ErrorHelpers.ComputeTukeyBiweightErrorsScalarCore(actual, predicted, scalar, lastValidActual, lastValidPredicted, c);
        ErrorHelpers.ComputeTukeyBiweightErrorsVectorCore(actual, predicted, vector, lastValidActual, lastValidPredicted, c);
        double maxDiff = Compare(scalar, vector);
        _output.WriteLine($"tukey n={actual.Length} c={c:G17} maxAbsDiff={maxDiff:G17}");
    }

    private void VerifyHuber(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, double delta)
    {
        double lastValidActual = ErrorHelpers.FindFirstValidValue(actual);
        double lastValidPredicted = ErrorHelpers.FindFirstValidValue(predicted);
        double[] scalar = new double[actual.Length];
        double[] vector = new double[actual.Length];
        ErrorHelpers.ComputeHuberErrorsScalarCore(actual, predicted, scalar, lastValidActual, lastValidPredicted, delta);
        ErrorHelpers.ComputeHuberErrorsVectorCore(actual, predicted, vector, lastValidActual, lastValidPredicted, delta);
        double maxDiff = Compare(scalar, vector);
        _output.WriteLine($"huber n={actual.Length} delta={delta:G17} maxAbsDiff={maxDiff:G17}");
    }

    private void VerifySanitize(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted)
    {
        double lastValidActual = ErrorHelpers.FindFirstValidValue(actual);
        double lastValidPredicted = ErrorHelpers.FindFirstValidValue(predicted);
        double[] scalarActual = new double[actual.Length];
        double[] scalarPredicted = new double[actual.Length];
        double[] vectorActual = new double[actual.Length];
        double[] vectorPredicted = new double[actual.Length];
        ErrorHelpers.SanitizeInputsScalarCore(actual, predicted, scalarActual, scalarPredicted, lastValidActual, lastValidPredicted);
        ErrorHelpers.SanitizeInputsVectorCore(actual, predicted, vectorActual, vectorPredicted, lastValidActual, lastValidPredicted);
        double maxDiff = Math.Max(Compare(scalarActual, vectorActual), Compare(scalarPredicted, vectorPredicted));
        _output.WriteLine($"sanitize n={actual.Length} maxAbsDiff={maxDiff:G17}");
    }

    private static double Compare(ReadOnlySpan<double> scalar, ReadOnlySpan<double> vector)
    {
        Assert.Equal(scalar.Length, vector.Length);
        double maxDiff = 0.0;
        for (int i = 0; i < scalar.Length; i++)
        {
            double a = scalar[i];
            double b = vector[i];
            if (double.IsNaN(a) || double.IsNaN(b))
            {
                Assert.True(double.IsNaN(a) && double.IsNaN(b), $"NaN mismatch at {i}: scalar={a:R}, vector={b:R}");
                continue;
            }

            if (double.IsInfinity(a) || double.IsInfinity(b))
            {
                Assert.True(a == b, $"Infinity mismatch at {i}: scalar={a:R}, vector={b:R}");
                continue;
            }

            double diff = Math.Abs(a - b);
            double scale = Math.Max(Math.Abs(a), Math.Abs(b));
            double allowed = (1e-9 * scale) + 1e-12;
            Assert.True(diff <= allowed, $"Mismatch at {i}: scalar={a:R}, vector={b:R}, diff={diff:R}");
            maxDiff = Math.Max(maxDiff, diff);
        }

        return maxDiff;
    }

    private static (double[] Actual, double[] Predicted) BuildPair(int n, int seed, bool withNonFinite)
    {
        var rng = new Random(seed);
        double[] actual = new double[n];
        double[] predicted = new double[n];
        for (int i = 0; i < n; i++)
        {
            actual[i] = (rng.NextDouble() - 0.5) * 200.0;
            predicted[i] = (rng.NextDouble() - 0.5) * 200.0;
        }

        if (n > 0)
        {
            actual[0] = 0.0;
            predicted[0] = 0.0;
        }

        if (n > 1)
        {
            actual[n / 2] = -0.0;
            predicted[n / 2] = -0.0;
        }

        if (withNonFinite && n > 0)
        {
            actual[0] = double.NaN;
            if (n > 1)
            {
                predicted[1] = double.NaN;
            }

            if (n > 2)
            {
                actual[n / 3] = double.PositiveInfinity;
            }

            if (n > 3)
            {
                predicted[(2 * n) / 3] = double.NegativeInfinity;
            }

            if (n > 4)
            {
                actual[n - 1] = double.NaN;
            }

            if (n > 5)
            {
                predicted[n - 1] = double.PositiveInfinity;
            }
        }

        return (actual, predicted);
    }

    private static (double[] Actual, double[] Predicted, double[] Weights) BuildTriple(int n, int seed, bool withNonFinite)
    {
        var rng = new Random(seed);
        double[] actual = new double[n];
        double[] predicted = new double[n];
        double[] weights = new double[n];
        for (int i = 0; i < n; i++)
        {
            actual[i] = (rng.NextDouble() - 0.5) * 200.0;
            predicted[i] = (rng.NextDouble() - 0.5) * 200.0;
            weights[i] = rng.NextDouble() * 10.0;
        }

        if (n > 0)
        {
            actual[0] = 0.0;
            predicted[0] = 0.0;
            weights[0] = 0.0;
        }

        if (n > 1)
        {
            actual[n / 2] = -0.0;
            weights[1] = 1.0;
        }

        if (n > 2)
        {
            weights[n - 1] = 0.0;
        }

        if (withNonFinite && n > 0)
        {
            actual[0] = double.NaN;
            if (n > 1)
            {
                predicted[1] = double.NaN;
            }

            if (n > 2)
            {
                weights[n / 2] = double.PositiveInfinity;
            }

            if (n > 3)
            {
                actual[n - 1] = double.NaN;
            }

            if (n > 4)
            {
                weights[n - 1] = double.NaN;
            }
        }

        return (actual, predicted, weights);
    }
}