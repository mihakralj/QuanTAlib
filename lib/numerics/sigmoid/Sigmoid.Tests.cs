using Xunit;

namespace QuanTAlib.Tests;

public class SigmoidTests
{
    private readonly GBM _gbm = new(1000, 0.05, 0.2, seed: 100);
    private const double Epsilon = 1e-10;

    // ═══════════════════════════════════════════════════════════════════════════════
    // Constructor Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Constructor_WithDefaultParameters_SetsCorrectName()
    {
        var sigmoid = new Sigmoid();
        Assert.Equal("Sigmoid(1.00,0.00)", sigmoid.Name);
    }

    [Fact]
    public void Constructor_WithCustomParameters_SetsCorrectName()
    {
        var sigmoid = new Sigmoid(k: 0.5, x0: 100.0);
        Assert.Equal("Sigmoid(0.50,100.00)", sigmoid.Name);
    }

    [Fact]
    public void Constructor_WithZeroK_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Sigmoid(k: 0));
    }

    [Fact]
    public void Constructor_WithNegativeK_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Sigmoid(k: -1));
    }

    [Fact]
    public void Constructor_WarmupPeriod_IsZero()
    {
        var sigmoid = new Sigmoid();
        Assert.Equal(0, sigmoid.WarmupPeriod);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Basic Update Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_ReturnsTValue()
    {
        var sigmoid = new Sigmoid();
        var input = new TValue(DateTime.UtcNow, 0.0);

        var result = sigmoid.Update(input);

        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_AtMidpoint_ReturnsHalf()
    {
        var sigmoid = new Sigmoid(k: 1.0, x0: 0.0);
        var input = new TValue(DateTime.UtcNow, 0.0);

        var result = sigmoid.Update(input);

        Assert.Equal(0.5, result.Value, Epsilon);
    }

    [Fact]
    public void Update_AtMidpointWithOffset_ReturnsHalf()
    {
        var sigmoid = new Sigmoid(k: 1.0, x0: 100.0);
        var input = new TValue(DateTime.UtcNow, 100.0);

        var result = sigmoid.Update(input);

        Assert.Equal(0.5, result.Value, Epsilon);
    }

    [Fact]
    public void Update_Last_IsUpdated()
    {
        var sigmoid = new Sigmoid();
        var input = new TValue(DateTime.UtcNow, 1.0);

        sigmoid.Update(input);

        Assert.Equal(input.Time, sigmoid.Last.Time);
    }

    [Fact]
    public void Update_IsHot_IsAlwaysTrue()
    {
        var sigmoid = new Sigmoid();
        Assert.True(sigmoid.IsHot);

        sigmoid.Update(new TValue(DateTime.UtcNow, 0.0));
        Assert.True(sigmoid.IsHot);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // isNew State Management Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_WithIsNewTrue_AdvancesState()
    {
        var sigmoid = new Sigmoid();
        var input1 = new TValue(DateTime.UtcNow, 1.0);
        var input2 = new TValue(DateTime.UtcNow.AddSeconds(1), 2.0);

        var result1 = sigmoid.Update(input1, isNew: true);
        var result2 = sigmoid.Update(input2, isNew: true);

        Assert.NotEqual(result1.Value, result2.Value);
    }

    [Fact]
    public void Update_WithIsNewFalse_ReplacesCurrentBar()
    {
        var sigmoid = new Sigmoid();
        var input1 = new TValue(DateTime.UtcNow, 1.0);
        var input2 = new TValue(DateTime.UtcNow, 2.0);

        sigmoid.Update(input1, isNew: true);
        var result = sigmoid.Update(input2, isNew: false);

        Assert.Equal(sigmoid.Last.Value, result.Value);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoreState()
    {
        var sigmoid = new Sigmoid();

        // Initial value
        sigmoid.Update(new TValue(DateTime.UtcNow, 1.0), isNew: true);
        double afterFirst = sigmoid.Last.Value;

        // Multiple corrections (isNew = false)
        sigmoid.Update(new TValue(DateTime.UtcNow, 2.0), isNew: false);
        sigmoid.Update(new TValue(DateTime.UtcNow, 3.0), isNew: false);
        sigmoid.Update(new TValue(DateTime.UtcNow, 1.0), isNew: false);

        // Should restore to same state as after first update with same input
        Assert.Equal(afterFirst, sigmoid.Last.Value, Epsilon);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // NaN/Infinity Handling Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var sigmoid = new Sigmoid();

        sigmoid.Update(new TValue(DateTime.UtcNow, 1.0), isNew: true);
        double lastValid = sigmoid.Last.Value;

        var nanResult = sigmoid.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.NaN), isNew: true);

        Assert.Equal(lastValid, nanResult.Value);
    }

    [Fact]
    public void Update_PositiveInfinity_UsesLastValidValue()
    {
        var sigmoid = new Sigmoid();

        sigmoid.Update(new TValue(DateTime.UtcNow, 0.0), isNew: true);
        double lastValid = sigmoid.Last.Value;

        var infResult = sigmoid.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.PositiveInfinity), isNew: true);

        Assert.Equal(lastValid, infResult.Value);
    }

    [Fact]
    public void Update_NegativeInfinity_UsesLastValidValue()
    {
        var sigmoid = new Sigmoid();

        sigmoid.Update(new TValue(DateTime.UtcNow, 0.0), isNew: true);
        double lastValid = sigmoid.Last.Value;

        var infResult = sigmoid.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.NegativeInfinity), isNew: true);

        Assert.Equal(lastValid, infResult.Value);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Reset Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Reset_ClearsState()
    {
        var sigmoid = new Sigmoid();

        sigmoid.Update(new TValue(DateTime.UtcNow, 1.0), isNew: true);
        sigmoid.Reset();

        Assert.Equal(default, sigmoid.Last);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // TSeries Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_TSeries_ReturnsCorrectLength()
    {
        var sigmoid = new Sigmoid();
        var series = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            series.Add(new TValue(DateTime.UtcNow.AddSeconds(i), i - 50), isNew: true);
        }

        var result = sigmoid.Update(series);

        Assert.Equal(series.Count, result.Count);
    }

    [Fact]
    public void Calculate_TSeries_ReturnsCorrectLength()
    {
        var series = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            series.Add(new TValue(DateTime.UtcNow.AddSeconds(i), i - 50), isNew: true);
        }

        var result = Sigmoid.Batch(series);

        Assert.Equal(series.Count, result.Count);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Span API Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Calculate_Span_EmptySource_ThrowsArgumentException()
    {
        double[] output = new double[10];
        Assert.Throws<ArgumentException>(() => Sigmoid.Batch(ReadOnlySpan<double>.Empty, output.AsSpan()));
    }

    [Fact]
    public void Calculate_Span_OutputTooSmall_ThrowsArgumentException()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[3];

        Assert.Throws<ArgumentException>(() => Sigmoid.Batch(source.AsSpan(), output.AsSpan()));
    }

    [Fact]
    public void Calculate_Span_InvalidK_ThrowsArgumentException()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];

        Assert.Throws<ArgumentException>(() => Sigmoid.Batch(source.AsSpan(), output.AsSpan(), k: 0));
    }

    [Fact]
    public void Calculate_Span_MatchesStreaming()
    {
        double[] source = new double[100];
        var rng = new Random(42);
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = rng.NextDouble() * 200 - 100;
        }

        double[] spanOutput = new double[source.Length];
        Sigmoid.Batch(source.AsSpan(), spanOutput.AsSpan());

        var sigmoid = new Sigmoid();
        double[] streamOutput = new double[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            streamOutput[i] = sigmoid.Update(new TValue(DateTime.UtcNow.AddSeconds(i), source[i]), true).Value;
        }

        for (int i = 0; i < source.Length; i++)
        {
            Assert.Equal(streamOutput[i], spanOutput[i], Epsilon);
        }
    }

    [Fact]
    public void Calculate_Span_HandlesNaN()
    {
        double[] source = [1.0, double.NaN, 2.0];
        double[] output = new double[3];

        Sigmoid.Batch(source.AsSpan(), output.AsSpan());

        Assert.True(double.IsFinite(output[0]));
        Assert.True(double.IsFinite(output[1]));  // NaN replaced with last valid
        Assert.True(double.IsFinite(output[2]));
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Chaining Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Chaining_PublishesEvents()
    {
        var source = new TSeries();
        var sigmoid = new Sigmoid(source);
        int eventCount = 0;

        sigmoid.Pub += (_, in _) => eventCount++;

        for (int i = 0; i < 10; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), i), isNew: true);
        }

        Assert.Equal(10, eventCount);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Steepness Parameter Tests
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_HigherK_CreatesSteeperTransition()
    {
        var sigmoidLow = new Sigmoid(k: 0.5);
        var sigmoidHigh = new Sigmoid(k: 5.0);

        // At x=1, higher k should give value closer to 1
        var resultLow = sigmoidLow.Update(new TValue(DateTime.UtcNow, 1.0));
        var resultHigh = sigmoidHigh.Update(new TValue(DateTime.UtcNow, 1.0));

        Assert.True(resultHigh.Value > resultLow.Value);
    }

    [Fact]
    public void Update_DifferentX0_ShiftsMidpoint()
    {
        var sigmoid0 = new Sigmoid(k: 1.0, x0: 0.0);
        var sigmoid100 = new Sigmoid(k: 1.0, x0: 100.0);

        // At x=0, sigmoid with x0=0 should be 0.5
        var result0 = sigmoid0.Update(new TValue(DateTime.UtcNow, 0.0));
        // At x=100, sigmoid with x0=100 should be 0.5
        var result100 = sigmoid100.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.Equal(0.5, result0.Value, Epsilon);
        Assert.Equal(0.5, result100.Value, Epsilon);
    }
}
