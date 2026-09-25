using System.Numerics;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class JerkVectorTests
{
    private readonly ITestOutputHelper _output;

    public JerkVectorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void VectorCore_MatchesScalarCore_AllFinite()
    {
        Verify(BuildInput(1024, seed: 1901, withNonFinite: false));
    }

    [Fact]
    public void VectorCore_MatchesScalarCore_WithNonFinite()
    {
        Verify(BuildInput(1024, seed: 2001, withNonFinite: true));
    }

    [Fact]
    public void VectorCore_MatchesScalarCore_EdgeValues()
    {
        Verify([0.0, -0.0, 0.0, -1.5, 3.25, 0.0, 12.0, -8.0]);
        Verify([double.NaN, double.NaN, double.NaN, double.NaN, double.NaN]);
        Verify([double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity]);
        Verify([double.NaN, 1.0, 2.0, double.NaN, 4.0, 5.0, double.NegativeInfinity, 7.0]);
        Verify([1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0, 11.0, 12.0]);
    }

    [Fact]
    public void VectorCore_MatchesScalarCore_ShortArrays()
    {
        int limit = Vector<double>.Count + 5;
        for (int n = 0; n <= limit; n++)
        {
            Verify(BuildInput(n, seed: 2500 + n, withNonFinite: false));
            Verify(BuildInput(n, seed: 3500 + n, withNonFinite: true));
        }
    }

    private void Verify(double[] source)
    {
        double[] scalar = new double[source.Length];
        double[] vector = new double[source.Length];
        Jerk.CalculateScalarCore(source, scalar);
        Jerk.CalculateVectorCore(source, vector);
        double maxDiff = Compare(scalar, vector);
        _output.WriteLine($"n={source.Length} maxAbsDiff={maxDiff:G17}");
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
                Assert.True(double.IsNaN(a) && double.IsNaN(b), $"NaN mismatch at {i}: scalar={a}, vector={b}");
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

    private static double[] BuildInput(int n, int seed, bool withNonFinite)
    {
        var rng = new Random(seed);
        double[] data = new double[n];
        for (int i = 0; i < n; i++)
        {
            data[i] = (rng.NextDouble() - 0.5) * 200.0;
        }

        if (n > 0)
        {
            data[0] = 0.0;
        }

        if (n > 1)
        {
            data[n / 2] = -0.0;
        }

        if (withNonFinite && n > 0)
        {
            data[0] = double.NaN;
            if (n > 1)
            {
                data[n / 3] = double.PositiveInfinity;
            }

            if (n > 2)
            {
                data[(2 * n) / 3] = double.NegativeInfinity;
            }

            if (n > 3)
            {
                data[n - 1] = double.NaN;
            }
        }

        return data;
    }
}
