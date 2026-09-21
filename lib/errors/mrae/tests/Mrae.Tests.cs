namespace QuanTAlib.Tests;

public class MraeTests
{
    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Mrae(0));
        Assert.Throws<ArgumentException>(() => new Mrae(-1));

        var mrae = new Mrae(10);
        Assert.NotNull(mrae);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var mrae = new Mrae(10);

        Assert.Equal(0, mrae.Last.Value);
        Assert.False(mrae.IsHot);
        Assert.Contains("Mrae", mrae.Name, StringComparison.Ordinal);

        mrae.Update(100, 105);
        Assert.NotEqual(0, mrae.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        const int period = 5;
        var mrae = new Mrae(period);

        for (int i = 1; i <= period - 1; i++)
        {
            Assert.False(mrae.IsHot, $"IsHot should be false at index {i}");
            mrae.Update(i * 10, (i * 10) + 5);
        }

        mrae.Update(period * 10, (period * 10) + 5);
        Assert.True(mrae.IsHot, "IsHot should be true after period updates");
    }

    [Fact]
    public void Mrae_CalculatesCorrectly()
    {
        var mrae = new Mrae(3);

        // |100 - 110| / |100| = 10/100 = 0.1
        var res1 = mrae.Update(100, 110);
        Assert.Equal(0.1, res1.Value, 10);

        // |200 - 220| / |200| = 20/200 = 0.1, Mean = (0.1 + 0.1) / 2 = 0.1
        var res2 = mrae.Update(200, 220);
        Assert.Equal(0.1, res2.Value, 10);

        // |50 - 60| / |50| = 10/50 = 0.2, Mean = (0.1 + 0.1 + 0.2) / 3 = 0.133...
        var res3 = mrae.Update(50, 60);
        Assert.Equal(0.4 / 3.0, res3.Value, 10);
    }

    [Fact]
    public void Mrae_PerfectPrediction_ReturnsZero()
    {
        var mrae = new Mrae(5);

        for (int i = 1; i <= 10; i++)
        {
            mrae.Update(i * 10, i * 10); // Perfect prediction
        }

        Assert.Equal(0.0, mrae.Last.Value, 10);
    }

    [Fact]
    public void Mrae_ProportionalError_ReturnsConstant()
    {
        var mrae = new Mrae(5);

        // 10% error for all
        for (int i = 1; i <= 10; i++)
        {
            mrae.Update(i * 100, i * 110); // 10% overestimate
        }

        Assert.Equal(0.1, mrae.Last.Value, 10);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var mrae = new Mrae(10);

        mrae.Update(100, 110, isNew: true);
        double value1 = mrae.Last.Value;

        mrae.Update(100, 120, isNew: true);
        double value2 = mrae.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var mrae = new Mrae(10);

        mrae.Update(100, 110);
        mrae.Update(100, 120, isNew: true);
        double beforeUpdate = mrae.Last.Value;

        mrae.Update(100, 130, isNew: false);
        double afterUpdate = mrae.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var mrae = new Mrae(5);

        double tenthActual = 0;
        double tenthPredicted = 0;

        // Feed 10 updates
        for (int i = 1; i <= 10; i++)
        {
            tenthActual = i * 100;
            tenthPredicted = (i * 100) + 10;
            mrae.Update(tenthActual, tenthPredicted);
        }

        double stateAfterTen = mrae.Last.Value;

        // Apply 5 corrections with isNew=false
        for (int i = 0; i < 5; i++)
        {
            mrae.Update(100 + i, 200 + i, isNew: false);
        }

        // Restore to original values
        mrae.Update(tenthActual, tenthPredicted, isNew: false);

        Assert.Equal(stateAfterTen, mrae.Last.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var mrae = new Mrae(5);

        for (int i = 1; i <= 10; i++)
        {
            mrae.Update(i * 10, (i * 10) + 5);
        }

        Assert.True(mrae.IsHot);

        mrae.Reset();

        Assert.False(mrae.IsHot);
        Assert.Equal(0, mrae.Last.Value);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var mrae = new Mrae(5);

        mrae.Update(100, 110);
        mrae.Update(110, 120);
        mrae.Update(120, 130);

        var result = mrae.Update(double.NaN, double.NaN);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var mrae = new Mrae(5);

        mrae.Update(100, 110);
        mrae.Update(110, 120);

        var result = mrae.Update(double.PositiveInfinity, double.NegativeInfinity);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var mrae = new Mrae(5);

        mrae.Update(100, 110);
        mrae.Update(110, 120);
        mrae.Update(120, 130);

        var r1 = mrae.Update(double.NaN, double.NaN);
        var r2 = mrae.Update(double.NaN, double.NaN);
        var r3 = mrae.Update(double.NaN, double.NaN);

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Mrae_Throws_On_Single_Input()
    {
        var mrae = new Mrae(10);
        Assert.Throws<NotSupportedException>(() => mrae.Update(new TValue(DateTime.UtcNow, 1)));
        Assert.Throws<NotSupportedException>(() => mrae.Update(new TSeries()));
        Assert.Throws<NotSupportedException>(() => mrae.Prime([1, 2, 3]));
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
            predicted[i] = (bar.Close * 1.05) + 2;
        }

        // Streaming
        var mrae = new Mrae(period);
        var streamingResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamingResults[i] = mrae.Update(actual[i], predicted[i]).Value;
        }

        // Batch
        double[] batchResults = new double[count];
        Mrae.Batch(actual, predicted, batchResults, period);

        // Compare
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i], 9);
        }
    }

    [Fact]
    public void BatchSpan_ValidatesInput()
    {
        double[] actual = [10, 20, 30, 40, 50];
        double[] predicted = [11, 22, 33, 44, 55];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];
        double[] wrongSizePredicted = new double[3];

        Assert.Throws<ArgumentException>(() =>
            Mrae.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() =>
            Mrae.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), -1));
        Assert.Throws<ArgumentException>(() =>
            Mrae.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), 3));
        Assert.Throws<ArgumentException>(() =>
            Mrae.Batch(actual.AsSpan(), wrongSizePredicted.AsSpan(), output.AsSpan(), 3));
    }

    [Fact]
    public void Calculate_Works()
    {
        var actual = new TSeries();
        var predicted = new TSeries();
        var now = DateTime.UtcNow;

        for (int i = 1; i <= 10; i++)
        {
            actual.Add(now.AddMinutes(i), i * 100);
            predicted.Add(now.AddMinutes(i), i * 110); // 10% error
        }

        var results = Mrae.Batch(actual, predicted, 3);

        Assert.Equal(10, results.Count);
        Assert.Equal(0.1, results.Last.Value, 10);
    }

    [Fact]
    public void Calculate_ValidatesMismatchedLengths()
    {
        var actual = new TSeries();
        var predicted = new TSeries();

        for (int i = 1; i <= 10; i++)
        {
            actual.Add(DateTime.UtcNow, i * 10);
        }

        for (int i = 1; i <= 5; i++)
        {
            predicted.Add(DateTime.UtcNow, i * 10);
        }

        Assert.Throws<ArgumentException>(() => Mrae.Batch(actual, predicted, 3));
    }

    [Fact]
    public void BatchSpan_HandlesNaN()
    {
        double[] actual = [100, 110, double.NaN, 130, 140];
        double[] predicted = [105, 115, 125, double.NaN, 145];
        double[] output = new double[5];

        Mrae.Batch(actual, predicted, output, 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Mrae_Resync_Works()
    {
        var mrae = new Mrae(5);

        // Force many updates to trigger resync
        for (int i = 1; i <= 1100; i++)
        {
            mrae.Update(100, 110); // 10% error
        }

        Assert.Equal(0.1, mrae.Last.Value, 10);
    }
}
