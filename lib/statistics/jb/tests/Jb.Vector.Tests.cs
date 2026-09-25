using System.Numerics;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class JbVectorTests
{
    private static readonly int[] Periods = [3, 4, 5, 8, 16];

    private readonly ITestOutputHelper _output;

    public JbVectorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void VectorCore_MatchesScalarCore_AllFinite()
    {
        foreach (int period in Periods)
        {
            Verify(BuildInput(1024, seed: 1001, withNonFinite: false), period);
        }
    }

    [Fact]
    public void VectorCore_MatchesScalarCore_WithNonFinite()
    {
        foreach (int period in Periods)
        {
            Verify(BuildInput(1024, seed: 1002, withNonFinite: true), period);
        }
    }

    [Fact]
    public void VectorCore_MatchesScalarCore_EdgeValues()
    {
        double[][] cases =
        [
            [0.0, -0.0, 0.0, -1.5, 3.25, 0.0, 12.0, -8.0],
            [double.NaN, double.NaN, double.NaN, double.NaN, double.NaN],
            [double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity],
            [-1.0, -2.0, -3.0, -4.0, -5.0, -6.0],
            [7.0, double.NaN, 8.0, double.PositiveInfinity, 9.0, 0.0, -4.0, double.NegativeInfinity],
            [5.0, 5.0, 5.0, 5.0, 5.0, 5.0],
        ];

        foreach (double[] source in cases)
        {
            foreach (int period in Periods)
            {
                Verify(source, period);
            }
        }
    }

    [Fact]
    public void VectorCore_MatchesScalarCore_ShortArrays()
    {
        int limit = Vector<double>.Count + 5;
        for (int n = 0; n <= limit; n++)
        {
            foreach (int period in Periods)
            {
                Verify(BuildInput(n, seed: 2000 + n, withNonFinite: false), period);
                Verify(BuildInput(n, seed: 3000 + n, withNonFinite: true), period);
            }
        }
    }

    private void Verify(double[] source, int period)
    {
        double[] scalar = new double[source.Length];
        double[] vector = new double[source.Length];
        Jb.CalculateScalarCore(source, scalar, period);
        Jb.CalculateVectorCore(source, vector, period);
        double maxDiff = Compare(scalar, vector);
        _output.WriteLine($"n={source.Length} period={period} maxAbsDiff={maxDiff:G17}");
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

            if (double.IsInfinity(a) || double.IsInfinity(b))
            {
                Assert.True(double.IsInfinity(a) && double.IsInfinity(b) && Math.Sign(a) == Math.Sign(b),
                    $"Infinity mismatch at {i}: scalar={a}, vector={b}");
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
            data[i] = (rng.Next(0, 4) - 1.5) * 2.0;
        }

        if (n > 0)
        {
            data[0] = 0.0;
        }

        if (n > 1)
        {
            data[1] = -0.0;
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
