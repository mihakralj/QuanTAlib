namespace QuanTAlib.Tests;

public class MaeTests
{
    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Mae(0));
        Assert.Throws<ArgumentException>(() => new Mae(-1));

        var mae = new Mae(10);
        Assert.NotNull(mae);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var mae = new Mae(10);

        Assert.Equal(0, mae.Last.Value);
        Assert.False(mae.IsHot);
        Assert.Contains("Mae", mae.Name, StringComparison.Ordinal);

        mae.Update(100, 105);
        Assert.NotEqual(0, mae.Last.Time);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        const int period = 5;
        var mae = new Mae(period);

        for (int i = 0; i < period - 1; i++)
        {
            Assert.False(mae.IsHot, $"IsHot should be false at index {i}");
            mae.Update(i * 10, i * 10 + 5);
        }

        mae.Update((period - 1) * 10, (period - 1) * 10 + 5);
        Assert.True(mae.IsHot, "IsHot should be true after period updates");
    }

    [Fact]
    public void Mae_CalculatesCorrectly()
    {
        var mae = new Mae(3);

        // |10 - 15| = 5
        var res1 = mae.Update(10, 15);
        Assert.Equal(5.0, res1.Value, 10);

        // |20 - 30| = 10, Mean = (5 + 10) / 2 = 7.5
        var res2 = mae.Update(20, 30);
        Assert.Equal(7.5, res2.Value, 10);

        // |30 - 25| = 5, Mean = (5 + 10 + 5) / 3 = 6.666...
        var res3 = mae.Update(30, 25);
        Assert.Equal(20.0 / 3.0, res3.Value, 10);

        // |40 - 35| = 5, Window slides: (10 + 5 + 5) / 3 = 6.666...
        var res4 = mae.Update(40, 35);
        Assert.Equal(20.0 / 3.0, res4.Value, 10);
    }

    [Fact]
    public void Mae_PerfectPrediction_ReturnsZero()
    {
        var mae = new Mae(5);

        for (int i = 0; i < 10; i++)
        {
            mae.Update(i * 10, i * 10); // Perfect prediction
        }

        Assert.Equal(0.0, mae.Last.Value, 10);
    }

    [Fact]
    public void Mae_ConstantError_ReturnsConstant()
    {
        var mae = new Mae(5);

        for (int i = 0; i < 10; i++)
        {
            mae.Update(100, 110); // Constant error of 10
        }

        Assert.Equal(10.0, mae.Last.Value, 10);
    }

    [Fact]
    public void Mae_NegativeError_TakesAbsoluteValue()
    {
        var mae = new Mae(3);

        // Error = |15 - 10| = 5 (predicted > actual)
        mae.Update(10, 15);
        // Error = |20 - 30| = 10 (predicted > actual)
        mae.Update(20, 30);
        // Error = |50 - 25| = 25 (predicted < actual)
        mae.Update(50, 25);

        // Mean = (5 + 10 + 25) / 3 = 40 / 3
        Assert.Equal(40.0 / 3.0, mae.Last.Value, 10);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var mae = new Mae(10);

        mae.Update(100, 110, isNew: true);
        double value1 = mae.Last.Value;

        mae.Update(100, 120, isNew: true);
        double value2 = mae.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var mae = new Mae(10);

        mae.Update(100, 110);
        mae.Update(100, 120, isNew: true);
        double beforeUpdate = mae.Last.Value;

        mae.Update(100, 130, isNew: false);
        double afterUpdate = mae.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var mae = new Mae(5);

        double tenthActual = 0;
        double tenthPredicted = 0;

        // Feed 10 updates
        for (int i = 0; i < 10; i++)
        {
            tenthActual = i * 10;
            tenthPredicted = i * 10 + 5;
            mae.Update(tenthActual, tenthPredicted);
        }

        double stateAfterTen = mae.Last.Value;

        // Apply 5 corrections with isNew=false
        for (int i = 0; i < 5; i++)
        {
            mae.Update(100 + i, 200 + i, isNew: false);
        }

        // Restore to original values
        mae.Update(tenthActual, tenthPredicted, isNew: false);

        Assert.Equal(stateAfterTen, mae.Last.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var mae = new Mae(5);

        for (int i = 0; i < 10; i++)
        {
            mae.Update(i * 10, i * 10 + 5);
        }

        Assert.True(mae.IsHot);

        mae.Reset();

        Assert.False(mae.IsHot);
        Assert.Equal(0, mae.Last.Value);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var mae = new Mae(5);

        mae.Update(100, 110);
        mae.Update(110, 120);
        mae.Update(120, 130);

        var result = mae.Update(double.NaN, double.NaN);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var mae = new Mae(5);

        mae.Update(100, 110);
        mae.Update(110, 120);

        var result = mae.Update(double.PositiveInfinity, double.NegativeInfinity);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var mae = new Mae(5);

        mae.Update(100, 110);
        mae.Update(110, 120);
        mae.Update(120, 130);

        var r1 = mae.Update(double.NaN, double.NaN);
        var r2 = mae.Update(double.NaN, double.NaN);
        var r3 = mae.Update(double.NaN, double.NaN);

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Mae_Throws_On_Single_Input()
    {
        var mae = new Mae(10);
        Assert.Throws<NotSupportedException>(() => mae.Update(new TValue(DateTime.UtcNow, 1)));
        Assert.Throws<NotSupportedException>(() => mae.Update(new TSeries()));
        Assert.Throws<NotSupportedException>(() => mae.Prime([1, 2, 3]));
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
        var mae = new Mae(period);
        var streamingResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamingResults[i] = mae.Update(actual[i], predicted[i]).Value;
        }

        // Batch
        double[] batchResults = new double[count];
        Mae.Batch(actual, predicted, batchResults, period);

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
            Mae.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() =>
            Mae.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), -1));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() =>
            Mae.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), 3));

        // Predicted must be same length as actual
        Assert.Throws<ArgumentException>(() =>
            Mae.Batch(actual.AsSpan(), wrongSizePredicted.AsSpan(), output.AsSpan(), 3));
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

        var results = Mae.Calculate(actual, predicted, 3);

        Assert.Equal(10, results.Count);
        // All errors are 5, so MAE should be 5
        Assert.Equal(5.0, results.Last.Value, 10);
    }

    [Fact]
    public void Calculate_ValidatesMismatchedLengths()
    {
        var actual = new TSeries();
        var predicted = new TSeries();

        for (int i = 0; i < 10; i++) actual.Add(DateTime.UtcNow, i);
        for (int i = 0; i < 5; i++) predicted.Add(DateTime.UtcNow, i);

        Assert.Throws<ArgumentException>(() => Mae.Calculate(actual, predicted, 3));
    }

    [Fact]
    public void BatchSpan_HandlesNaN()
    {
        double[] actual = [100, 110, double.NaN, 130, 140];
        double[] predicted = [105, 115, 125, double.NaN, 145];
        double[] output = new double[5];

        Mae.Batch(actual, predicted, output, 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Mae_Resync_Works()
    {
        var mae = new Mae(5);

        // Force many updates to trigger resync (ResyncInterval = 1000)
        for (int i = 0; i < 1100; i++)
        {
            mae.Update(i, i + 10); // Constant error of 10
        }

        // After resync, result should still be correct
        Assert.Equal(10.0, mae.Last.Value, 10);
    }
}
