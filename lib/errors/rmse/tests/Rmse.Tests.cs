namespace QuanTAlib.Tests;

public class RmseTests
{
    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Rmse(0));
        Assert.Throws<ArgumentException>(() => new Rmse(-1));

        var rmse = new Rmse(10);
        Assert.NotNull(rmse);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var rmse = new Rmse(10);

        Assert.Equal(0, rmse.Last.Value);
        Assert.False(rmse.IsHot);
        Assert.Contains("Rmse", rmse.Name, StringComparison.Ordinal);

        rmse.Update(100, 105);
        Assert.NotEqual(0, rmse.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        const int period = 5;
        var rmse = new Rmse(period);

        for (int i = 0; i < period - 1; i++)
        {
            Assert.False(rmse.IsHot);
            rmse.Update(i * 10, i * 10 + 5);
        }

        rmse.Update((period - 1) * 10, (period - 1) * 10 + 5);
        Assert.True(rmse.IsHot);
    }

    [Fact]
    public void Rmse_CalculatesCorrectly()
    {
        var rmse = new Rmse(3);

        // (10 - 15)² = 25, RMSE = √25 = 5
        var res1 = rmse.Update(10, 15);
        Assert.Equal(5.0, res1.Value, 10);

        // (20 - 30)² = 100, MSE = (25 + 100) / 2 = 62.5, RMSE = √62.5
        var res2 = rmse.Update(20, 30);
        Assert.Equal(Math.Sqrt(62.5), res2.Value, 10);

        // (30 - 25)² = 25, MSE = (25 + 100 + 25) / 3 = 50, RMSE = √50
        var res3 = rmse.Update(30, 25);
        Assert.Equal(Math.Sqrt(50.0), res3.Value, 10);
    }

    [Fact]
    public void Rmse_IsSqrtOfMse()
    {
        var rmse = new Rmse(5);
        var mse = new Mse(5);

        for (int i = 0; i < 20; i++)
        {
            rmse.Update(i * 10, i * 10 + 7);
            mse.Update(i * 10, i * 10 + 7);
        }

        Assert.Equal(Math.Sqrt(mse.Last.Value), rmse.Last.Value, 10);
    }

    [Fact]
    public void Rmse_PerfectPrediction_ReturnsZero()
    {
        var rmse = new Rmse(5);

        for (int i = 0; i < 10; i++)
        {
            rmse.Update(i * 10, i * 10);
        }

        Assert.Equal(0.0, rmse.Last.Value, 10);
    }

    [Fact]
    public void Rmse_ConstantError_ReturnsSameAsError()
    {
        var rmse = new Rmse(5);

        for (int i = 0; i < 10; i++)
        {
            rmse.Update(100, 110); // Constant error of 10
        }

        // MSE = 100, RMSE = √100 = 10 (same as error because error is constant)
        Assert.Equal(10.0, rmse.Last.Value, 10);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var rmse = new Rmse(10);

        rmse.Update(100, 110);
        rmse.Update(100, 120, isNew: true);
        double beforeUpdate = rmse.Last.Value;

        rmse.Update(100, 130, isNew: false);
        double afterUpdate = rmse.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var rmse = new Rmse(5);

        double tenthActual = 0;
        double tenthPredicted = 0;

        for (int i = 0; i < 10; i++)
        {
            tenthActual = i * 10;
            tenthPredicted = i * 10 + 5;
            rmse.Update(tenthActual, tenthPredicted);
        }

        double stateAfterTen = rmse.Last.Value;

        for (int i = 0; i < 5; i++)
        {
            rmse.Update(100 + i, 200 + i, isNew: false);
        }

        rmse.Update(tenthActual, tenthPredicted, isNew: false);

        Assert.Equal(stateAfterTen, rmse.Last.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var rmse = new Rmse(5);

        for (int i = 0; i < 10; i++)
        {
            rmse.Update(i * 10, i * 10 + 5);
        }

        Assert.True(rmse.IsHot);

        rmse.Reset();

        Assert.False(rmse.IsHot);
        Assert.Equal(0, rmse.Last.Value);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var rmse = new Rmse(5);

        rmse.Update(100, 110);
        rmse.Update(110, 120);

        var result = rmse.Update(double.NaN, double.NaN);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Rmse_Throws_On_Single_Input()
    {
        var rmse = new Rmse(10);
        Assert.Throws<NotSupportedException>(() => rmse.Update(new TValue(DateTime.UtcNow, 1)));
        Assert.Throws<NotSupportedException>(() => rmse.Update(new TSeries()));
        Assert.Throws<NotSupportedException>(() => rmse.Prime([1, 2, 3]));
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

        var rmse = new Rmse(period);
        var streamingResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamingResults[i] = rmse.Update(actual[i], predicted[i]).Value;
        }

        double[] batchResults = new double[count];
        Rmse.Batch(actual, predicted, batchResults, period);

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
            Rmse.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() =>
            Rmse.Batch(actual.AsSpan(), predicted.AsSpan(), new double[3].AsSpan(), 3));
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

        var results = Rmse.Batch(actual, predicted, 3);

        Assert.Equal(10, results.Count);
        // All errors are 5, MSE = 25, RMSE = 5
        Assert.Equal(5.0, results.Last.Value, 10);
    }
}
