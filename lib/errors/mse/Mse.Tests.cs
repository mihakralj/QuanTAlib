namespace QuanTAlib.Tests;

public class MseTests
{
    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Mse(0));
        Assert.Throws<ArgumentException>(() => new Mse(-1));

        var mse = new Mse(10);
        Assert.NotNull(mse);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var mse = new Mse(10);

        Assert.Equal(0, mse.Last.Value);
        Assert.False(mse.IsHot);
        Assert.Contains("Mse", mse.Name, StringComparison.Ordinal);

        mse.Update(100, 105);
        Assert.NotEqual(0, mse.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        const int period = 5;
        var mse = new Mse(period);

        for (int i = 0; i < period - 1; i++)
        {
            Assert.False(mse.IsHot, $"IsHot should be false at index {i}");
            mse.Update(i * 10, i * 10 + 5);
        }

        mse.Update((period - 1) * 10, (period - 1) * 10 + 5);
        Assert.True(mse.IsHot, "IsHot should be true after period updates");
    }

    [Fact]
    public void Mse_CalculatesCorrectly()
    {
        var mse = new Mse(3);

        // (10 - 15)² = 25
        var res1 = mse.Update(10, 15);
        Assert.Equal(25.0, res1.Value, 10);

        // (20 - 30)² = 100, Mean = (25 + 100) / 2 = 62.5
        var res2 = mse.Update(20, 30);
        Assert.Equal(62.5, res2.Value, 10);

        // (30 - 25)² = 25, Mean = (25 + 100 + 25) / 3 = 50
        var res3 = mse.Update(30, 25);
        Assert.Equal(50.0, res3.Value, 10);

        // (40 - 35)² = 25, Window slides: (100 + 25 + 25) / 3 = 50
        var res4 = mse.Update(40, 35);
        Assert.Equal(50.0, res4.Value, 10);
    }

    [Fact]
    public void Mse_PerfectPrediction_ReturnsZero()
    {
        var mse = new Mse(5);

        for (int i = 0; i < 10; i++)
        {
            mse.Update(i * 10, i * 10); // Perfect prediction
        }

        Assert.Equal(0.0, mse.Last.Value, 10);
    }

    [Fact]
    public void Mse_ConstantError_ReturnsSquaredConstant()
    {
        var mse = new Mse(5);

        for (int i = 0; i < 10; i++)
        {
            mse.Update(100, 110); // Constant error of 10, squared = 100
        }

        Assert.Equal(100.0, mse.Last.Value, 10);
    }

    [Fact]
    public void Mse_PenalizesLargeErrors()
    {
        var mse = new Mse(3);

        // Small errors: (1-2)² = 1, (2-3)² = 1, (3-4)² = 1
        // Mean = 1
        mse.Update(1, 2);
        mse.Update(2, 3);
        var smallResult = mse.Update(3, 4);
        Assert.Equal(1.0, smallResult.Value, 10);

        mse.Reset();

        // Large error: (1-11)² = 100, (2-3)² = 1, (3-4)² = 1
        // Mean = 102/3 = 34
        mse.Update(1, 11); // Large error
        mse.Update(2, 3);
        var largeResult = mse.Update(3, 4);
        Assert.Equal(102.0 / 3.0, largeResult.Value, 10);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var mse = new Mse(10);

        mse.Update(100, 110, isNew: true);
        double value1 = mse.Last.Value;

        mse.Update(100, 120, isNew: true);
        double value2 = mse.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var mse = new Mse(10);

        mse.Update(100, 110);
        mse.Update(100, 120, isNew: true);
        double beforeUpdate = mse.Last.Value;

        mse.Update(100, 130, isNew: false);
        double afterUpdate = mse.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var mse = new Mse(5);

        double tenthActual = 0;
        double tenthPredicted = 0;

        // Feed 10 updates
        for (int i = 0; i < 10; i++)
        {
            tenthActual = i * 10;
            tenthPredicted = i * 10 + 5;
            mse.Update(tenthActual, tenthPredicted);
        }

        double stateAfterTen = mse.Last.Value;

        // Apply 5 corrections with isNew=false
        for (int i = 0; i < 5; i++)
        {
            mse.Update(100 + i, 200 + i, isNew: false);
        }

        // Restore to original values
        mse.Update(tenthActual, tenthPredicted, isNew: false);

        Assert.Equal(stateAfterTen, mse.Last.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var mse = new Mse(5);

        for (int i = 0; i < 10; i++)
        {
            mse.Update(i * 10, i * 10 + 5);
        }

        Assert.True(mse.IsHot);

        mse.Reset();

        Assert.False(mse.IsHot);
        Assert.Equal(0, mse.Last.Value);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var mse = new Mse(5);

        mse.Update(100, 110);
        mse.Update(110, 120);
        mse.Update(120, 130);

        var result = mse.Update(double.NaN, double.NaN);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var mse = new Mse(5);

        mse.Update(100, 110);
        mse.Update(110, 120);

        var result = mse.Update(double.PositiveInfinity, double.NegativeInfinity);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Mse_Throws_On_Single_Input()
    {
        var mse = new Mse(10);
        Assert.Throws<NotSupportedException>(() => mse.Update(new TValue(DateTime.UtcNow, 1)));
        Assert.Throws<NotSupportedException>(() => mse.Update(new TSeries()));
        Assert.Throws<NotSupportedException>(() => mse.Prime([1, 2, 3]));
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
            predicted[i] = bar.Close * 1.05 + 2;
        }

        // Streaming
        var mse = new Mse(period);
        var streamingResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamingResults[i] = mse.Update(actual[i], predicted[i]).Value;
        }

        // Batch
        double[] batchResults = new double[count];
        Mse.Batch(actual, predicted, batchResults, period);

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

        Assert.Throws<ArgumentException>(() =>
            Mse.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() =>
            Mse.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), -1));
        Assert.Throws<ArgumentException>(() =>
            Mse.Batch(actual.AsSpan(), predicted.AsSpan(), new double[3].AsSpan(), 3));
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

        var results = Mse.Calculate(actual, predicted, 3);

        Assert.Equal(10, results.Count);
        // All errors are 5², so MSE should be 25
        Assert.Equal(25.0, results.Last.Value, 10);
    }

    [Fact]
    public void BatchSpan_HandlesNaN()
    {
        double[] actual = [100, 110, double.NaN, 130, 140];
        double[] predicted = [105, 115, 125, double.NaN, 145];
        double[] output = new double[5];

        Mse.Batch(actual, predicted, output, 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }
}