namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Jerk using synthetic data with known mathematical results.
/// </summary>
public class JerkValidationTests
{
    [Fact]
    public void CubicSequence_ProducesConstantJerk()
    {
        // Cubic sequence: 0, 1, 8, 27, 64, 125 (x^3)
        // First diff (slope): 1, 7, 19, 37, 61
        // Second diff (accel): 6, 12, 18, 24
        // Third diff (jerk): 6, 6, 6 (constant for cubic)
        double[] data = [0, 1, 8, 27, 64, 125];
        double[] expected = [0, 0, 0, 6, 6, 6]; // First three are warmup (0), rest are 6

        var jerk = new Jerk();
        for (int i = 0; i < data.Length; i++)
        {
            var result = jerk.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void QuadraticSequence_ProducesZeroJerk()
    {
        // Quadratic sequence: 0, 1, 4, 9, 16, 25 (x^2)
        // Accel = 2 (constant), so Jerk = 0
        double[] data = [0, 1, 4, 9, 16, 25];
        double[] expected = [0, 0, 0, 0, 0, 0];

        var jerk = new Jerk();
        for (int i = 0; i < data.Length; i++)
        {
            var result = jerk.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void LinearSequence_ProducesZeroJerk()
    {
        // Linear sequence: 0, 2, 4, 6, 8, 10 (slope = 2, accel = 0, jerk = 0)
        double[] data = [0, 2, 4, 6, 8, 10];
        double[] expected = [0, 0, 0, 0, 0, 0];

        var jerk = new Jerk();
        for (int i = 0; i < data.Length; i++)
        {
            var result = jerk.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void ConstantSequence_ProducesZeroJerk()
    {
        // Constant sequence: 5, 5, 5, 5, 5 (all derivatives = 0)
        double[] data = [5, 5, 5, 5, 5];
        double[] expected = [0, 0, 0, 0, 0];

        var jerk = new Jerk();
        for (int i = 0; i < data.Length; i++)
        {
            var result = jerk.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void QuarticSequence_ProducesLinearJerk()
    {
        // Quartic sequence: 0, 1, 16, 81, 256, 625 (x^4)
        // First diff: 1, 15, 65, 175, 369
        // Second diff: 14, 50, 110, 194
        // Third diff (jerk): 36, 60, 84 (linear, step of 24)
        double[] data = [0, 1, 16, 81, 256, 625];
        double[] expected = [0, 0, 0, 36, 60, 84];

        var jerk = new Jerk();
        for (int i = 0; i < data.Length; i++)
        {
            var result = jerk.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void NegativeCubic_ProducesNegativeJerk()
    {
        // Negative cubic: -x³ → 0, -1, -8, -27, -64
        // Jerk = -6 (constant)
        double[] data = [0, -1, -8, -27, -64];
        double[] expected = [0, 0, 0, -6, -6];

        var jerk = new Jerk();
        for (int i = 0; i < data.Length; i++)
        {
            var result = jerk.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void AlternatingSequence_ProducesAlternatingJerk()
    {
        // Alternating: 0, 10, 0, 10, 0, 10
        // Slope: 10, -10, 10, -10, 10
        // Accel: -20, 20, -20, 20
        // Jerk: 40, -40, 40
        double[] data = [0, 10, 0, 10, 0, 10];
        double[] expected = [0, 0, 0, 40, -40, 40];

        var jerk = new Jerk();
        for (int i = 0; i < data.Length; i++)
        {
            var result = jerk.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void BatchCalculation_MatchesSyntheticData()
    {
        double[] data = [0, 1, 8, 27, 64, 125];
        double[] expected = [0, 0, 0, 6, 6, 6];
        double[] output = new double[data.Length];

        Jerk.Batch(data, output);

        for (int i = 0; i < data.Length; i++)
        {
            Assert.Equal(expected[i], output[i], precision: 9);
        }
    }

    [Fact]
    public void LargeCubicSequence_ProducesConstantJerk()
    {
        // Generate 1000 points: f(n) = n³ with coefficient 1/6 → jerk = 1
        // f(n) = n³/6, f'(n) = n²/2, f''(n) = n, f'''(n) = 1
        // Discrete: jerk = 1 (after warmup)
        // Note: Large cubic values accumulate floating-point error, use precision: 8
        int count = 1000;
        double[] data = new double[count];
        for (int i = 0; i < count; i++)
        {
            data[i] = (double)(i * i * i) / 6.0;
        }

        var jerk = new Jerk();
        // Skip warmup period (first 3 bars)
        _ = jerk.Update(new TValue(DateTime.UtcNow, data[0]));
        _ = jerk.Update(new TValue(DateTime.UtcNow, data[1]));
        _ = jerk.Update(new TValue(DateTime.UtcNow, data[2]));

        for (int i = 3; i < count; i++)
        {
            jerk.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(1.0, jerk.Last.Value, precision: 6);
        }
    }
}
