using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// ReLU validation tests - validates against known mathematical properties
/// since no external library implementations exist for this activation function.
/// </summary>
public class ReluValidationTests
{
    private const double Tolerance = 1e-10;

    [Fact]
    public void Relu_MathematicalDefinition_Streaming()
    {
        // ReLU: f(x) = max(0, x)
        var indicator = new Relu();
        var time = DateTime.UtcNow;
        double[] testValues = { -10.0, -5.0, -1.0, -0.5, 0.0, 0.5, 1.0, 5.0, 10.0 };

        foreach (var x in testValues)
        {
            indicator.Update(new TValue(time, x));
            double expected = Math.Max(0.0, x);
            Assert.Equal(expected, indicator.Last.Value, Tolerance);
            time = time.AddMinutes(1);
        }
    }

    [Fact]
    public void Relu_MathematicalDefinition_Batch()
    {
        double[] testValues = { -10.0, -5.0, -1.0, -0.5, 0.0, 0.5, 1.0, 5.0, 10.0 };
        var source = new TSeries();
        var time = DateTime.UtcNow;
        foreach (var v in testValues)
        {
            source.Add(new TValue(time, v), true);
            time = time.AddMinutes(1);
        }

        var result = Relu.Calculate(source);

        for (int i = 0; i < testValues.Length; i++)
        {
            double expected = Math.Max(0.0, testValues[i]);
            Assert.Equal(expected, result[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Relu_MathematicalDefinition_Span()
    {
        double[] testValues = { -10.0, -5.0, -1.0, -0.5, 0.0, 0.5, 1.0, 5.0, 10.0 };
        double[] output = new double[testValues.Length];

        Relu.Calculate(testValues, output);

        for (int i = 0; i < testValues.Length; i++)
        {
            double expected = Math.Max(0.0, testValues[i]);
            Assert.Equal(expected, output[i], Tolerance);
        }
    }

    [Fact]
    public void Relu_Property_NonNegative()
    {
        // Property: ReLU output is always >= 0
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.5, seed: 43000);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = Change.Calculate(bars.Close);

        var result = Relu.Calculate(source);

        for (int i = 0; i < result.Count; i++)
        {
            Assert.True(result[i].Value >= 0, $"ReLU output at index {i} should be non-negative");
        }
    }

    [Fact]
    public void Relu_Property_PositivePassthrough()
    {
        // Property: For x > 0, ReLU(x) = x
        double[] positiveValues = { 0.001, 0.1, 1.0, 10.0, 100.0, 1000.0 };
        double[] output = new double[positiveValues.Length];

        Relu.Calculate(positiveValues, output);

        for (int i = 0; i < positiveValues.Length; i++)
        {
            Assert.Equal(positiveValues[i], output[i], Tolerance);
        }
    }

    [Fact]
    public void Relu_Property_NegativeZero()
    {
        // Property: For x < 0, ReLU(x) = 0
        double[] negativeValues = { -0.001, -0.1, -1.0, -10.0, -100.0, -1000.0 };
        double[] output = new double[negativeValues.Length];

        Relu.Calculate(negativeValues, output);

        for (int i = 0; i < negativeValues.Length; i++)
        {
            Assert.Equal(0.0, output[i], Tolerance);
        }
    }

    [Fact]
    public void Relu_Property_ZeroAtZero()
    {
        // Property: ReLU(0) = 0
        var indicator = new Relu();
        indicator.Update(new TValue(DateTime.UtcNow, 0.0));
        Assert.Equal(0.0, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Relu_StreamingVsBatch_Consistency()
    {
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 43001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = Change.Calculate(bars.Close);

        // Streaming
        var streaming = new Relu();
        var streamingResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
            streamingResults[i] = streaming.Last.Value;
        }

        // Batch
        var batch = Relu.Calculate(source);

        // Span
        var spanOutput = new double[source.Count];
        Relu.Calculate(source.Values.ToArray(), spanOutput);

        // All three should match
        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamingResults[i], batch[i].Value, Tolerance);
            Assert.Equal(streamingResults[i], spanOutput[i], Tolerance);
        }
    }
}
