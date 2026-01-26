using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Sigmoid indicator against mathematical properties.
/// Sigmoid has no direct external library equivalents, so we validate against
/// the mathematical definition: S(x) = 1 / (1 + exp(-k * (x - x0)))
/// </summary>
public class SigmoidValidationTests
{
    private const double Epsilon = 1e-10;

    // ═══════════════════════════════════════════════════════════════════════════════
    // Mathematical Definition Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0.0, 1.0, 0.0)]    // S(0) with k=1, x0=0
    [InlineData(1.0, 1.0, 0.0)]    // S(1) with k=1, x0=0
    [InlineData(-1.0, 1.0, 0.0)]   // S(-1) with k=1, x0=0
    [InlineData(5.0, 1.0, 0.0)]    // S(5) with k=1, x0=0
    [InlineData(-5.0, 1.0, 0.0)]   // S(-5) with k=1, x0=0
    [InlineData(0.0, 2.0, 0.0)]    // Different steepness
    [InlineData(100.0, 1.0, 100.0)] // Shifted midpoint
    public void Sigmoid_MatchesMathematicalDefinition(double x, double k, double x0)
    {
        var sigmoid = new Sigmoid(k, x0);
        var result = sigmoid.Update(new TValue(DateTime.UtcNow, x));

        // Mathematical definition: S(x) = 1 / (1 + exp(-k * (x - x0)))
        double expected = 1.0 / (1.0 + Math.Exp(-k * (x - x0)));

        Assert.Equal(expected, result.Value, Epsilon);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Symmetry Property Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(5.0)]
    [InlineData(10.0)]
    public void Sigmoid_Symmetry_AroundMidpoint(double offset)
    {
        // Property: S(x0 + d) + S(x0 - d) = 1
        var sigmoid = new Sigmoid(k: 1.0, x0: 0.0);

        var resultPlus = sigmoid.Update(new TValue(DateTime.UtcNow, offset));
        sigmoid.Reset();
        var resultMinus = sigmoid.Update(new TValue(DateTime.UtcNow, -offset));

        Assert.Equal(1.0, resultPlus.Value + resultMinus.Value, Epsilon);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Midpoint Property Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0.0)]
    [InlineData(50.0)]
    [InlineData(-50.0)]
    [InlineData(100.0)]
    public void Sigmoid_AtMidpoint_ReturnsHalf(double x0)
    {
        // Property: S(x0) = 0.5 for any x0
        var sigmoid = new Sigmoid(k: 1.0, x0: x0);
        var result = sigmoid.Update(new TValue(DateTime.UtcNow, x0));

        Assert.Equal(0.5, result.Value, Epsilon);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Range Property Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Sigmoid_OutputAlwaysBetweenZeroAndOne()
    {
        var sigmoid = new Sigmoid();
        var rng = new Random(42);

        for (int i = 0; i < 1000; i++)
        {
            double x = rng.NextDouble() * 2000 - 1000;  // Range [-1000, 1000]
            var result = sigmoid.Update(new TValue(DateTime.UtcNow.AddSeconds(i), x), true);

            Assert.True(result.Value >= 0.0, $"Output {result.Value} should be >= 0 for input {x}");
            Assert.True(result.Value <= 1.0, $"Output {result.Value} should be <= 1 for input {x}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Monotonicity Property Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Sigmoid_IsStrictlyIncreasing()
    {
        // Property: if x1 < x2 then S(x1) < S(x2)
        var sigmoid = new Sigmoid();

        double prevValue = double.NegativeInfinity;
        for (double x = -10; x <= 10; x += 0.5)
        {
            sigmoid.Reset();
            var result = sigmoid.Update(new TValue(DateTime.UtcNow, x));

            Assert.True(result.Value > prevValue, $"S({x}) = {result.Value} should be > {prevValue}");
            prevValue = result.Value;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Steepness Property Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Sigmoid_HigherK_SteeperTransition()
    {
        // At x = x0 + 1, higher k should produce values closer to 1
        double x = 1.0;

        var sigmoidK1 = new Sigmoid(k: 1.0);
        var sigmoidK5 = new Sigmoid(k: 5.0);
        var sigmoidK10 = new Sigmoid(k: 10.0);

        var result1 = sigmoidK1.Update(new TValue(DateTime.UtcNow, x));
        var result5 = sigmoidK5.Update(new TValue(DateTime.UtcNow, x));
        var result10 = sigmoidK10.Update(new TValue(DateTime.UtcNow, x));

        Assert.True(result10.Value > result5.Value);
        Assert.True(result5.Value > result1.Value);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Derivative Property Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Sigmoid_DerivativeMaximumAtMidpoint()
    {
        // Property: The derivative of sigmoid is maximum at x0
        // S'(x) = k * S(x) * (1 - S(x))
        // At x0, S(x0) = 0.5, so S'(x0) = k * 0.5 * 0.5 = k/4
        double k = 2.0;
        var sigmoid = new Sigmoid(k: k, x0: 0.0);

        // Numerical derivative using central difference
        double h = 0.0001;
        sigmoid.Reset();
        double sPlus = sigmoid.Update(new TValue(DateTime.UtcNow, h)).Value;
        sigmoid.Reset();
        double sMinus = sigmoid.Update(new TValue(DateTime.UtcNow, -h)).Value;

        double numericalDerivative = (sPlus - sMinus) / (2 * h);
        double expectedDerivative = k / 4.0;

        Assert.Equal(expectedDerivative, numericalDerivative, 1e-4);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Limit Property Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Sigmoid_ApproachesOneForLargePositive()
    {
        // lim(x→∞) S(x) = 1
        var sigmoid = new Sigmoid();
        var result = sigmoid.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(result.Value > 0.99999);
    }

    [Fact]
    public void Sigmoid_ApproachesZeroForLargeNegative()
    {
        // lim(x→-∞) S(x) = 0
        var sigmoid = new Sigmoid();
        var result = sigmoid.Update(new TValue(DateTime.UtcNow, -100.0));

        Assert.True(result.Value < 0.00001);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Inverse Relationship Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(0.9)]
    public void Sigmoid_InverseIsLogit(double y)
    {
        // Logit(y) = ln(y / (1-y)) = x (inverse of sigmoid with k=1, x0=0)
        var sigmoid = new Sigmoid(k: 1.0, x0: 0.0);

        // Calculate x from y using logit
        double x = Math.Log(y / (1 - y));

        // Sigmoid of x should give y
        var result = sigmoid.Update(new TValue(DateTime.UtcNow, x));

        Assert.Equal(y, result.Value, Epsilon);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Span vs Streaming Consistency Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Sigmoid_SpanAndStreaming_ProduceSameResults()
    {
        double k = 0.5;
        double x0 = 50.0;
        double[] source = new double[500];
        var rng = new Random(42);
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = rng.NextDouble() * 200 - 50;  // Range [-50, 150]
        }

        // Span calculation
        double[] spanOutput = new double[source.Length];
        Sigmoid.Calculate(source.AsSpan(), spanOutput.AsSpan(), k, x0);

        // Streaming calculation
        var sigmoid = new Sigmoid(k, x0);
        for (int i = 0; i < source.Length; i++)
        {
            var result = sigmoid.Update(new TValue(DateTime.UtcNow.AddSeconds(i), source[i]), true);
            Assert.Equal(spanOutput[i], result.Value, Epsilon);
        }
    }
}
