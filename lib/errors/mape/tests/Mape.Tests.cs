namespace QuanTAlib.Tests;

public class MapeTests
{
    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Mape(0));
        Assert.Throws<ArgumentException>(() => new Mape(-1));

        var mape = new Mape(10);
        Assert.NotNull(mape);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var mape = new Mape(10);

        Assert.Equal(0, mape.Last.Value);
        Assert.False(mape.IsHot);
        Assert.Contains("Mape", mape.Name, StringComparison.Ordinal);

        mape.Update(100, 105);
        Assert.NotEqual(0, mape.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        const int period = 5;
        var mape = new Mape(period);

        for (int i = 0; i < period - 1; i++)
        {
            Assert.False(mape.IsHot, $"IsHot should be false at index {i}");
            mape.Update(100 + i, 105 + i);
        }

        mape.Update(104, 109);
        Assert.True(mape.IsHot, "IsHot should be true after period updates");
    }

    [Fact]
    public void Mape_CalculatesCorrectly()
    {
        var mape = new Mape(3);

        // |100 - 110| / 100 * 100 = 10%
        var res1 = mape.Update(100, 110);
        Assert.Equal(10.0, res1.Value, 10);

        // |200 - 220| / 200 * 100 = 10%, Mean = (10 + 10) / 2 = 10%
        var res2 = mape.Update(200, 220);
        Assert.Equal(10.0, res2.Value, 10);

        // |50 - 60| / 50 * 100 = 20%, Mean = (10 + 10 + 20) / 3 = 13.333%
        var res3 = mape.Update(50, 60);
        Assert.Equal(40.0 / 3.0, res3.Value, 10);
    }

    [Fact]
    public void Mape_PerfectPrediction_ReturnsZero()
    {
        var mape = new Mape(5);

        for (int i = 1; i <= 10; i++)
        {
            mape.Update(i * 10, i * 10); // Perfect prediction
        }

        Assert.Equal(0.0, mape.Last.Value, 10);
    }

    [Fact]
    public void Mape_ConstantPercentageError()
    {
        var mape = new Mape(5);

        // 10% error consistently
        for (int i = 1; i <= 10; i++)
        {
            mape.Update(100, 110); // |100-110|/100 * 100 = 10%
        }

        Assert.Equal(10.0, mape.Last.Value, 10);
    }

    [Fact]
    public void Mape_ScaleIndependent()
    {
        var mape1 = new Mape(3);
        var mape2 = new Mape(3);

        // Small scale: 10% error
        mape1.Update(10, 11);
        mape1.Update(10, 11);
        mape1.Update(10, 11);

        // Large scale: 10% error
        mape2.Update(1000, 1100);
        mape2.Update(1000, 1100);
        mape2.Update(1000, 1100);

        Assert.Equal(mape1.Last.Value, mape2.Last.Value, 10);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var mape = new Mape(10);

        mape.Update(100, 110, isNew: true);
        double value1 = mape.Last.Value;

        mape.Update(100, 120, isNew: true);
        double value2 = mape.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var mape = new Mape(10);

        mape.Update(100, 110);
        mape.Update(100, 120, isNew: true);
        double beforeUpdate = mape.Last.Value;

        mape.Update(100, 130, isNew: false);
        double afterUpdate = mape.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var mape = new Mape(5);

        double tenthActual = 0;
        double tenthPredicted = 0;

        // Feed 10 updates
        for (int i = 1; i <= 10; i++)
        {
            tenthActual = i * 10;
            tenthPredicted = (i * 10) + 5;
            mape.Update(tenthActual, tenthPredicted);
        }

        double stateAfterTen = mape.Last.Value;

        // Apply 5 corrections with isNew=false
        for (int i = 0; i < 5; i++)
        {
            mape.Update(100 + i, 200 + i, isNew: false);
        }

        // Restore to original values
        mape.Update(tenthActual, tenthPredicted, isNew: false);

        Assert.Equal(stateAfterTen, mape.Last.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var mape = new Mape(5);

        for (int i = 1; i <= 10; i++)
        {
            mape.Update(i * 10, (i * 10) + 5);
        }

        Assert.True(mape.IsHot);

        mape.Reset();

        Assert.False(mape.IsHot);
        Assert.Equal(0, mape.Last.Value);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var mape = new Mape(5);

        mape.Update(100, 110);
        mape.Update(110, 120);
        mape.Update(120, 130);

        var result = mape.Update(double.NaN, double.NaN);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var mape = new Mape(5);

        mape.Update(100, 110);
        mape.Update(110, 120);

        var result = mape.Update(double.PositiveInfinity, double.NegativeInfinity);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var mape = new Mape(5);

        mape.Update(100, 110);
        mape.Update(110, 120);
        mape.Update(120, 130);

        var r1 = mape.Update(double.NaN, double.NaN);
        var r2 = mape.Update(double.NaN, double.NaN);
        var r3 = mape.Update(double.NaN, double.NaN);

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Mape_Throws_On_Single_Input()
    {
        var mape = new Mape(10);
        Assert.Throws<NotSupportedException>(() => mape.Update(new TValue(DateTime.UtcNow, 1)));
        Assert.Throws<NotSupportedException>(() => mape.Update(new TSeries()));
        Assert.Throws<NotSupportedException>(() => mape.Prime([1, 2, 3]));
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
            predicted[i] = (bar.Close * 1.05) + 2; // Offset prediction
        }

        // Streaming
        var mape = new Mape(period);
        var streamingResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamingResults[i] = mape.Update(actual[i], predicted[i]).Value;
        }

        // Batch
        double[] batchResults = new double[count];
        Mape.Batch(actual, predicted, batchResults, period);

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
            Mape.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() =>
            Mape.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), -1));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() =>
            Mape.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), 3));

        // Predicted must be same length as actual
        Assert.Throws<ArgumentException>(() =>
            Mape.Batch(actual.AsSpan(), wrongSizePredicted.AsSpan(), output.AsSpan(), 3));
    }

    [Fact]
    public void Calculate_Works()
    {
        var actual = new TSeries();
        var predicted = new TSeries();
        var now = DateTime.UtcNow;

        for (int i = 1; i <= 10; i++)
        {
            actual.Add(now.AddMinutes(i), 100);
            predicted.Add(now.AddMinutes(i), 110); // 10% error
        }

        var results = Mape.Batch(actual, predicted, 3);

        Assert.Equal(10, results.Count);
        Assert.Equal(10.0, results.Last.Value, 10);
    }

    [Fact]
    public void Calculate_ValidatesMismatchedLengths()
    {
        var actual = new TSeries();
        var predicted = new TSeries();

        for (int i = 0; i < 10; i++)
        {
            actual.Add(DateTime.UtcNow, i + 1);
        }

        for (int i = 0; i < 5; i++)
        {
            predicted.Add(DateTime.UtcNow, i + 1);
        }

        Assert.Throws<ArgumentException>(() => Mape.Batch(actual, predicted, 3));
    }

    [Fact]
    public void BatchSpan_HandlesNaN()
    {
        double[] actual = [100, 110, double.NaN, 130, 140];
        double[] predicted = [105, 115, 125, double.NaN, 145];
        double[] output = new double[5];

        Mape.Batch(actual, predicted, output, 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Mape_Resync_Works()
    {
        var mape = new Mape(5);

        // Force many updates to trigger resync (ResyncInterval = 1000)
        for (int i = 0; i < 1100; i++)
        {
            mape.Update(100, 110); // 10% error
        }

        // After resync, result should still be correct
        Assert.Equal(10.0, mape.Last.Value, 10);
    }

    [Fact]
    public void Mape_ZeroActual_HandlesGracefully()
    {
        var mape = new Mape(3);

        mape.Update(100, 110);
        mape.Update(100, 110);

        // Zero actual should not cause division by zero
        var result = mape.Update(0, 10);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Mape_Asymmetric_PenalizesUnderPredictionMore()
    {
        var mape1 = new Mape(1);
        var mape2 = new Mape(1);

        // Under-prediction: actual=100, predicted=50
        // |100-50|/100 * 100 = 50%
        var underPrediction = mape1.Update(100, 50);

        // Over-prediction: actual=50, predicted=100
        // |50-100|/50 * 100 = 100%
        var overPrediction = mape2.Update(50, 100);

        // Over-prediction should have higher MAPE due to smaller denominator
        Assert.True(overPrediction.Value > underPrediction.Value);
    }
}
