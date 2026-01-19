namespace QuanTAlib.Tests;

public class HuberTests
{
    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Huber(0));
        Assert.Throws<ArgumentException>(() => new Huber(-1));
        Assert.Throws<ArgumentException>(() => new Huber(10, 0));
        Assert.Throws<ArgumentException>(() => new Huber(10, -1));

        var huber = new Huber(10);
        Assert.NotNull(huber);

        var huberWithDelta = new Huber(10, 2.0);
        Assert.NotNull(huberWithDelta);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var huber = new Huber(10);

        Assert.Equal(0, huber.Last.Value);
        Assert.False(huber.IsHot);
        Assert.Contains("Huber", huber.Name, StringComparison.Ordinal);

        huber.Update(100, 105);
        Assert.NotEqual(0, huber.Last.Time);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        const int period = 5;
        var huber = new Huber(period);

        for (int i = 0; i < period - 1; i++)
        {
            Assert.False(huber.IsHot, $"IsHot should be false at index {i}");
            huber.Update(i * 10, i * 10 + 5);
        }

        huber.Update((period - 1) * 10, (period - 1) * 10 + 5);
        Assert.True(huber.IsHot, "IsHot should be true after period updates");
    }

    [Fact]
    public void Huber_SmallErrors_BehavesLikeMSE()
    {
        double delta = 10.0; // Large delta so all errors are "small"
        var huber = new Huber(3, delta);

        // Error = 0.5 (small), Huber = 0.5 * 0.5^2 = 0.125
        var res1 = huber.Update(100, 99.5);
        Assert.Equal(0.125, res1.Value, 10);

        // Error = 1.0, Huber = 0.5 * 1^2 = 0.5, Mean = (0.125 + 0.5) / 2 = 0.3125
        var res2 = huber.Update(100, 99);
        Assert.Equal(0.3125, res2.Value, 10);
    }

    [Fact]
    public void Huber_LargeErrors_BehavesLikeMAE()
    {
        double delta = 1.0; // Small delta so large errors get linear treatment
        var huber = new Huber(1, delta);
        double halfDeltaSquared = 0.5 * delta * delta;

        // Error = 10 (large), Huber = delta * |error| - 0.5 * delta^2 = 1 * 10 - 0.5 = 9.5
        var res1 = huber.Update(110, 100);
        Assert.Equal(delta * 10 - halfDeltaSquared, res1.Value, 10);
    }

    [Fact]
    public void Huber_TransitionPoint()
    {
        double delta = 5.0;
        var huber1 = new Huber(1, delta);
        var huber2 = new Huber(1, delta);

        // Error exactly at delta boundary
        var atDelta = huber1.Update(105, 100);
        // 0.5 * 5^2 = 12.5
        Assert.Equal(0.5 * delta * delta, atDelta.Value, 10);

        // Error just above delta
        var aboveDelta = huber2.Update(105.1, 100);
        // Should be very close to quadratic at transition
        // delta * 5.1 - 0.5 * delta^2 = 5 * 5.1 - 12.5 = 25.5 - 12.5 = 13
        double expected = delta * 5.1 - 0.5 * delta * delta;
        Assert.Equal(expected, aboveDelta.Value, 5);
    }

    [Fact]
    public void Huber_PerfectPrediction_ReturnsZero()
    {
        var huber = new Huber(5);

        for (int i = 0; i < 10; i++)
        {
            huber.Update(i * 10, i * 10); // Perfect prediction
        }

        Assert.Equal(0.0, huber.Last.Value, 10);
    }

    [Fact]
    public void Huber_SymmetricForPositiveNegativeErrors()
    {
        double delta = 2.0;
        var huber1 = new Huber(1, delta);
        var huber2 = new Huber(1, delta);

        // Positive error
        var positive = huber1.Update(105, 100);

        // Negative error (same magnitude)
        var negative = huber2.Update(95, 100);

        Assert.Equal(positive.Value, negative.Value, 10);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var huber = new Huber(10);

        huber.Update(100, 110, isNew: true);
        double value1 = huber.Last.Value;

        huber.Update(100, 120, isNew: true);
        double value2 = huber.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var huber = new Huber(10);

        huber.Update(100, 110);
        huber.Update(100, 120, isNew: true);
        double beforeUpdate = huber.Last.Value;

        huber.Update(100, 130, isNew: false);
        double afterUpdate = huber.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var huber = new Huber(5);

        double tenthActual = 0;
        double tenthPredicted = 0;

        // Feed 10 updates
        for (int i = 0; i < 10; i++)
        {
            tenthActual = i * 10;
            tenthPredicted = i * 10 + 5;
            huber.Update(tenthActual, tenthPredicted);
        }

        double stateAfterTen = huber.Last.Value;

        // Apply 5 corrections with isNew=false
        for (int i = 0; i < 5; i++)
        {
            huber.Update(100 + i, 200 + i, isNew: false);
        }

        // Restore to original values
        huber.Update(tenthActual, tenthPredicted, isNew: false);

        Assert.Equal(stateAfterTen, huber.Last.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var huber = new Huber(5);

        for (int i = 0; i < 10; i++)
        {
            huber.Update(i * 10, i * 10 + 5);
        }

        Assert.True(huber.IsHot);

        huber.Reset();

        Assert.False(huber.IsHot);
        Assert.Equal(0, huber.Last.Value);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var huber = new Huber(5);

        huber.Update(100, 110);
        huber.Update(110, 120);
        huber.Update(120, 130);

        var result = huber.Update(double.NaN, double.NaN);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var huber = new Huber(5);

        huber.Update(100, 110);
        huber.Update(110, 120);

        var result = huber.Update(double.PositiveInfinity, double.NegativeInfinity);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var huber = new Huber(5);

        huber.Update(100, 110);
        huber.Update(110, 120);
        huber.Update(120, 130);

        var r1 = huber.Update(double.NaN, double.NaN);
        var r2 = huber.Update(double.NaN, double.NaN);
        var r3 = huber.Update(double.NaN, double.NaN);

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Huber_Throws_On_Single_Input()
    {
        var huber = new Huber(10);
        Assert.Throws<NotSupportedException>(() => huber.Update(new TValue(DateTime.UtcNow, 1)));
        Assert.Throws<NotSupportedException>(() => huber.Update(new TSeries()));
        Assert.Throws<NotSupportedException>(() => huber.Prime([1, 2, 3]));
    }

    [Fact]
    public void BatchSpan_MatchesStreaming()
    {
        int period = 5;
        double delta = 1.345;
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        double[] actual = new double[count];
        double[] predicted = new double[count];
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            actual[i] = bar.Close;
            predicted[i] = bar.Close * 1.05 + 2; // Offset prediction
        }

        // Streaming
        var huber = new Huber(period, delta);
        var streamingResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamingResults[i] = huber.Update(actual[i], predicted[i]).Value;
        }

        // Batch
        double[] batchResults = new double[count];
        Huber.Batch(actual, predicted, batchResults, period, delta);

        // Compare
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i], 9);
        }
    }

    [Fact]
    public void BatchSpan_ValidatesInput()
    {
        double[] actual = [1, 2, 3, 4, 5];
        double[] predicted = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];
        double[] wrongSizePredicted = new double[3];

        // Period must be > 0
        Assert.Throws<ArgumentException>(() =>
            Huber.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() =>
            Huber.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), -1));

        // Delta must be > 0
        Assert.Throws<ArgumentException>(() =>
            Huber.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 3, 0));
        Assert.Throws<ArgumentException>(() =>
            Huber.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 3, -1));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() =>
            Huber.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), 3));

        // Predicted must be same length as actual
        Assert.Throws<ArgumentException>(() =>
            Huber.Batch(actual.AsSpan(), wrongSizePredicted.AsSpan(), output.AsSpan(), 3));
    }

    [Fact]
    public void Calculate_Works()
    {
        var actual = new TSeries();
        var predicted = new TSeries();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            actual.Add(now.AddMinutes(i), 100);
            predicted.Add(now.AddMinutes(i), 100.5); // Small constant error
        }

        var results = Huber.Calculate(actual, predicted, 3);

        Assert.Equal(10, results.Count);
        // Error = 0.5, Huber (small error) = 0.5 * 0.5^2 = 0.125
        Assert.Equal(0.125, results.Last.Value, 10);
    }

    [Fact]
    public void Calculate_ValidatesMismatchedLengths()
    {
        var actual = new TSeries();
        var predicted = new TSeries();

        for (int i = 0; i < 10; i++) actual.Add(DateTime.UtcNow, i);
        for (int i = 0; i < 5; i++) predicted.Add(DateTime.UtcNow, i);

        Assert.Throws<ArgumentException>(() => Huber.Calculate(actual, predicted, 3));
    }

    [Fact]
    public void BatchSpan_HandlesNaN()
    {
        double[] actual = [100, 110, double.NaN, 130, 140];
        double[] predicted = [105, 115, 125, double.NaN, 145];
        double[] output = new double[5];

        Huber.Batch(actual, predicted, output, 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Huber_Resync_Works()
    {
        double delta = 10.0; // Large delta for quadratic behavior
        var huber = new Huber(5, delta);

        // Force many updates to trigger resync (ResyncInterval = 1000)
        for (int i = 0; i < 1100; i++)
        {
            huber.Update(100, 102); // Constant error of 2
        }

        // Error = 2, Huber = 0.5 * 2^2 = 2.0
        Assert.Equal(2.0, huber.Last.Value, 10);
    }

    [Fact]
    public void Huber_DefaultDelta_Is1_345()
    {
        var huber = new Huber(5);
        Assert.Contains("1.345", huber.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Huber_DifferentDeltas_ProduceDifferentResults()
    {
        var huber1 = new Huber(5, 1.0);
        var huber2 = new Huber(5, 5.0);

        // Large error that exceeds both deltas differently
        huber1.Update(100, 110); // Error = 10
        huber2.Update(100, 110); // Error = 10

        // With delta=1: linear region -> 1*10 - 0.5 = 9.5
        // With delta=5: linear region -> 5*10 - 12.5 = 37.5
        Assert.NotEqual(huber1.Last.Value, huber2.Last.Value);
    }
}
