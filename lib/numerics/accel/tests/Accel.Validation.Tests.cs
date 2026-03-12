namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Accel using synthetic data with known mathematical results.
/// </summary>
public class AccelValidationTests
{
    [Fact]
    public void QuadraticSequence_ProducesConstantAccel()
    {
        // Quadratic sequence: 0, 1, 4, 9, 16, 25 (x^2)
        // Accel = second difference = 2 (constant for quadratic)
        // f(n) = n², slope(n) = 2n-1, accel = 2
        double[] data = [0, 1, 4, 9, 16, 25];
        double[] expected = [0, 0, 2, 2, 2, 2]; // First two are warmup (0), rest are 2

        var accel = new Accel();
        for (int i = 0; i < data.Length; i++)
        {
            var result = accel.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void LinearSequence_ProducesZeroAccel()
    {
        // Linear sequence: 0, 2, 4, 6, 8, 10 (slope = 2, accel = 0)
        double[] data = [0, 2, 4, 6, 8, 10];
        double[] expected = [0, 0, 0, 0, 0, 0];

        var accel = new Accel();
        for (int i = 0; i < data.Length; i++)
        {
            var result = accel.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void ConstantSequence_ProducesZeroAccel()
    {
        // Constant sequence: 5, 5, 5, 5, 5 (slope = 0, accel = 0)
        double[] data = [5, 5, 5, 5, 5];
        double[] expected = [0, 0, 0, 0, 0];

        var accel = new Accel();
        for (int i = 0; i < data.Length; i++)
        {
            var result = accel.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void CubicSequence_ProducesLinearAccel()
    {
        // Cubic sequence: 0, 1, 8, 27, 64, 125 (x^3)
        // First diff: 1, 7, 19, 37, 61
        // Second diff (accel): 6, 12, 18, 24 (linear, step of 6)
        double[] data = [0, 1, 8, 27, 64, 125];
        double[] expected = [0, 0, 6, 12, 18, 24];

        var accel = new Accel();
        for (int i = 0; i < data.Length; i++)
        {
            var result = accel.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void NegativeQuadratic_ProducesNegativeAccel()
    {
        // Negative quadratic: -x² → 0, -1, -4, -9, -16
        // Accel = -2 (constant)
        double[] data = [0, -1, -4, -9, -16];
        double[] expected = [0, 0, -2, -2, -2];

        var accel = new Accel();
        for (int i = 0; i < data.Length; i++)
        {
            var result = accel.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void AlternatingSequence_ProducesAlternatingAccel()
    {
        // Alternating: 0, 10, 0, 10, 0
        // Slope: 10, -10, 10, -10
        // Accel: -20, 20, -20
        double[] data = [0, 10, 0, 10, 0];
        double[] expected = [0, 0, -20, 20, -20];

        var accel = new Accel();
        for (int i = 0; i < data.Length; i++)
        {
            var result = accel.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void BatchCalculation_MatchesSyntheticData()
    {
        double[] data = [0, 1, 4, 9, 16, 25];
        double[] expected = [0, 0, 2, 2, 2, 2];
        double[] output = new double[data.Length];

        Accel.Batch(data, output);

        for (int i = 0; i < data.Length; i++)
        {
            Assert.Equal(expected[i], output[i], precision: 9);
        }
    }

    [Fact]
    public void LargeQuadraticSequence_ProducesConstantAccel()
    {
        // Generate 1000 points: f(n) = n² with coefficient 0.5 → accel = 1
        int count = 1000;
        double[] data = new double[count];
        for (int i = 0; i < count; i++)
        {
            data[i] = 0.5 * i * i;
        }

        var accel = new Accel();
        // Skip warmup period (first 2 bars)
        _ = accel.Update(new TValue(DateTime.UtcNow, data[0]));
        _ = accel.Update(new TValue(DateTime.UtcNow, data[1]));

        for (int i = 2; i < count; i++)
        {
            accel.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(1.0, accel.Last.Value, precision: 9);
        }
    }
}
