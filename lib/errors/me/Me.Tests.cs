namespace QuanTAlib.Tests;

public class MeTests
{
    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Me(0));
        Assert.Throws<ArgumentException>(() => new Me(-1));

        var me = new Me(10);
        Assert.NotNull(me);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var me = new Me(10);

        Assert.Equal(0, me.Last.Value);
        Assert.False(me.IsHot);
        Assert.Contains("Me", me.Name, StringComparison.Ordinal);

        me.Update(100, 105);
        Assert.NotEqual(0, me.Last.Time);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        int period = 5;
        var me = new Me(period);

        for (int i = 0; i < period - 1; i++)
        {
            Assert.False(me.IsHot, $"IsHot should be false at index {i}");
            me.Update(i * 10, i * 10 + 5);
        }

        me.Update((period - 1) * 10, (period - 1) * 10 + 5);
        Assert.True(me.IsHot, "IsHot should be true after period updates");
    }

    [Fact]
    public void Me_CalculatesCorrectly()
    {
        var me = new Me(3);

        // 10 - 15 = -5
        var res1 = me.Update(10, 15);
        Assert.Equal(-5.0, res1.Value, 10);

        // 20 - 30 = -10, Mean = (-5 + -10) / 2 = -7.5
        var res2 = me.Update(20, 30);
        Assert.Equal(-7.5, res2.Value, 10);

        // 30 - 25 = 5, Mean = (-5 + -10 + 5) / 3 = -10/3
        var res3 = me.Update(30, 25);
        Assert.Equal(-10.0 / 3.0, res3.Value, 10);

        // 40 - 35 = 5, Window slides: (-10 + 5 + 5) / 3 = 0
        var res4 = me.Update(40, 35);
        Assert.Equal(0.0, res4.Value, 10);
    }

    [Fact]
    public void Me_PerfectPrediction_ReturnsZero()
    {
        var me = new Me(5);

        for (int i = 0; i < 10; i++)
        {
            me.Update(i * 10, i * 10); // Perfect prediction
        }

        Assert.Equal(0.0, me.Last.Value, 10);
    }

    [Fact]
    public void Me_ConstantUnderPrediction_ReturnsPositive()
    {
        var me = new Me(5);

        for (int i = 0; i < 10; i++)
        {
            me.Update(110, 100); // Actual > predicted (under-prediction)
        }

        Assert.Equal(10.0, me.Last.Value, 10);
    }

    [Fact]
    public void Me_ConstantOverPrediction_ReturnsNegative()
    {
        var me = new Me(5);

        for (int i = 0; i < 10; i++)
        {
            me.Update(100, 110); // Actual < predicted (over-prediction)
        }

        Assert.Equal(-10.0, me.Last.Value, 10);
    }

    [Fact]
    public void Me_BalancedErrors_CancelOut()
    {
        var me = new Me(4);

        // Errors: +10, -10, +10, -10 should cancel out
        me.Update(110, 100); // +10
        me.Update(90, 100);  // -10
        me.Update(110, 100); // +10
        me.Update(90, 100);  // -10

        Assert.Equal(0.0, me.Last.Value, 10);
    }

    [Fact]
    public void Me_PreservesSign()
    {
        var me = new Me(3);

        // Error = 15 - 10 = 5 (under-prediction)
        me.Update(15, 10);
        Assert.True(me.Last.Value > 0, "ME should be positive for under-prediction");

        var me2 = new Me(3);
        // Error = 10 - 15 = -5 (over-prediction)
        me2.Update(10, 15);
        Assert.True(me2.Last.Value < 0, "ME should be negative for over-prediction");
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var me = new Me(10);

        me.Update(100, 110, isNew: true);
        double value1 = me.Last.Value;

        me.Update(100, 120, isNew: true);
        double value2 = me.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var me = new Me(10);

        me.Update(100, 110);
        me.Update(100, 120, isNew: true);
        double beforeUpdate = me.Last.Value;

        me.Update(100, 130, isNew: false);
        double afterUpdate = me.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var me = new Me(5);

        double tenthActual = 0;
        double tenthPredicted = 0;

        // Feed 10 updates
        for (int i = 0; i < 10; i++)
        {
            tenthActual = i * 10;
            tenthPredicted = i * 10 + 5;
            me.Update(tenthActual, tenthPredicted);
        }

        double stateAfterTen = me.Last.Value;

        // Apply 5 corrections with isNew=false
        for (int i = 0; i < 5; i++)
        {
            me.Update(100 + i, 200 + i, isNew: false);
        }

        // Restore to original values
        me.Update(tenthActual, tenthPredicted, isNew: false);

        Assert.Equal(stateAfterTen, me.Last.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var me = new Me(5);

        for (int i = 0; i < 10; i++)
        {
            me.Update(i * 10, i * 10 + 5);
        }

        Assert.True(me.IsHot);

        me.Reset();

        Assert.False(me.IsHot);
        Assert.Equal(0, me.Last.Value);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var me = new Me(5);

        me.Update(100, 110);
        me.Update(110, 120);
        me.Update(120, 130);

        var result = me.Update(double.NaN, double.NaN);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var me = new Me(5);

        me.Update(100, 110);
        me.Update(110, 120);

        var result = me.Update(double.PositiveInfinity, double.NegativeInfinity);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var me = new Me(5);

        me.Update(100, 110);
        me.Update(110, 120);
        me.Update(120, 130);

        var r1 = me.Update(double.NaN, double.NaN);
        var r2 = me.Update(double.NaN, double.NaN);
        var r3 = me.Update(double.NaN, double.NaN);

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Me_Throws_On_Single_Input()
    {
        var me = new Me(10);
        Assert.Throws<NotSupportedException>(() => me.Update(new TValue(DateTime.UtcNow, 1)));
        Assert.Throws<NotSupportedException>(() => me.Update(new TSeries()));
        Assert.Throws<NotSupportedException>(() => me.Prime([1, 2, 3]));
    }

    [Fact]
    public void BatchSpan_MatchesStreaming()
    {
        int period = 5;
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
        var me = new Me(period);
        var streamingResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamingResults[i] = me.Update(actual[i], predicted[i]).Value;
        }

        // Batch
        double[] batchResults = new double[count];
        Me.Batch(actual, predicted, batchResults, period);

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
            Me.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() =>
            Me.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), -1));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() =>
            Me.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), 3));

        // Predicted must be same length as actual
        Assert.Throws<ArgumentException>(() =>
            Me.Batch(actual.AsSpan(), wrongSizePredicted.AsSpan(), output.AsSpan(), 3));
    }

    [Fact]
    public void Calculate_Works()
    {
        var actual = new TSeries();
        var predicted = new TSeries();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            actual.Add(now.AddMinutes(i), i * 10);
            predicted.Add(now.AddMinutes(i), i * 10 + 5);
        }

        var results = Me.Calculate(actual, predicted, 3);

        Assert.Equal(10, results.Count);
        // All errors are -5, so ME should be -5
        Assert.Equal(-5.0, results.Last.Value, 10);
    }

    [Fact]
    public void Calculate_ValidatesMismatchedLengths()
    {
        var actual = new TSeries();
        var predicted = new TSeries();

        for (int i = 0; i < 10; i++) actual.Add(DateTime.UtcNow, i);
        for (int i = 0; i < 5; i++) predicted.Add(DateTime.UtcNow, i);

        Assert.Throws<ArgumentException>(() => Me.Calculate(actual, predicted, 3));
    }

    [Fact]
    public void BatchSpan_HandlesNaN()
    {
        double[] actual = [100, 110, double.NaN, 130, 140];
        double[] predicted = [105, 115, 125, double.NaN, 145];
        double[] output = new double[5];

        Me.Batch(actual, predicted, output, 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Me_Resync_Works()
    {
        var me = new Me(5);

        // Force many updates to trigger resync (ResyncInterval = 1000)
        for (int i = 0; i < 1100; i++)
        {
            me.Update(110, 100); // Constant error of +10
        }

        // After resync, result should still be correct
        Assert.Equal(10.0, me.Last.Value, 10);
    }
}
