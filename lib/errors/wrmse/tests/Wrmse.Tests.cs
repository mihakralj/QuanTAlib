namespace QuanTAlib.Tests;

public class WrmseTests
{
    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Wrmse(0));
        Assert.Throws<ArgumentException>(() => new Wrmse(-1));

        var wrmse = new Wrmse(10);
        Assert.NotNull(wrmse);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var wrmse = new Wrmse(10);

        Assert.Equal(0, wrmse.Last.Value);
        Assert.False(wrmse.IsHot);
        Assert.Contains("Wrmse", wrmse.Name, StringComparison.Ordinal);

        wrmse.Update(100, 105);
        Assert.NotEqual(0, wrmse.Last.Time);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        const int period = 5;
        var wrmse = new Wrmse(period);

        for (int i = 0; i < period - 1; i++)
        {
            Assert.False(wrmse.IsHot);
            wrmse.Update(i * 10, i * 10 + 5);
        }

        wrmse.Update((period - 1) * 10, (period - 1) * 10 + 5);
        Assert.True(wrmse.IsHot);
    }

    [Fact]
    public void Wrmse_WithUniformWeights_EqualsRmse()
    {
        var wrmse = new Wrmse(5);
        var rmse = new Rmse(5);

        for (int i = 0; i < 20; i++)
        {
            wrmse.Update(i * 10, i * 10 + 7);
            rmse.Update(i * 10, i * 10 + 7);
        }

        // With default weight of 1.0, WRMSE should equal RMSE
        Assert.Equal(rmse.Last.Value, wrmse.Last.Value, 10);
    }

    [Fact]
    public void Wrmse_CalculatesCorrectlyWithWeights()
    {
        var wrmse = new Wrmse(3);

        // (10 - 15)² = 25, weight = 1.0
        // Weighted error = 1.0 * 25 = 25, sum weights = 1.0
        // WRMSE = √(25/1) = 5
        var res1 = wrmse.Update(10, 15, 1.0);
        Assert.Equal(5.0, res1.Value, 10);

        // (20 - 30)² = 100, weight = 2.0
        // Weighted errors = 25 + 200 = 225, sum weights = 1 + 2 = 3
        // WRMSE = √(225/3) = √75
        var res2 = wrmse.Update(20, 30, 2.0);
        Assert.Equal(Math.Sqrt(75.0), res2.Value, 10);

        // (30 - 25)² = 25, weight = 3.0
        // Weighted errors = 25 + 200 + 75 = 300, sum weights = 1 + 2 + 3 = 6
        // WRMSE = √(300/6) = √50
        var res3 = wrmse.Update(30, 25, 3.0);
        Assert.Equal(Math.Sqrt(50.0), res3.Value, 10);
    }

    [Fact]
    public void Wrmse_HigherWeightsHaveMoreInfluence()
    {
        var wrmse1 = new Wrmse(2);
        var wrmse2 = new Wrmse(2);

        // First scenario: low weight on large error
        wrmse1.Update(10, 10, 10.0);  // error=0, weight=10
        wrmse1.Update(10, 20, 1.0);   // error=100, weight=1

        // Second scenario: high weight on large error
        wrmse2.Update(10, 10, 1.0);   // error=0, weight=1
        wrmse2.Update(10, 20, 10.0);  // error=100, weight=10

        // wrmse2 should be higher because the large error has more weight
        Assert.True(wrmse2.Last.Value > wrmse1.Last.Value);
    }

    [Fact]
    public void Wrmse_PerfectPrediction_ReturnsZero()
    {
        var wrmse = new Wrmse(5);

        for (int i = 0; i < 10; i++)
        {
            wrmse.Update(i * 10, i * 10, i + 1.0);
        }

        Assert.Equal(0.0, wrmse.Last.Value, 10);
    }

    [Fact]
    public void Wrmse_ConstantError_ConstantWeight()
    {
        var wrmse = new Wrmse(5);

        for (int i = 0; i < 10; i++)
        {
            wrmse.Update(100, 110, 2.0); // Constant error of 10, weight of 2
        }

        // Weighted error = 2 * 100 = 200, sum weights = 2
        // WRMSE = √(200/2) = √100 = 10
        Assert.Equal(10.0, wrmse.Last.Value, 10);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var wrmse = new Wrmse(10);

        wrmse.Update(100, 110, 1.0);
        wrmse.Update(100, 120, 1.0, isNew: true);
        double beforeUpdate = wrmse.Last.Value;

        wrmse.Update(100, 130, 1.0, isNew: false);
        double afterUpdate = wrmse.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var wrmse = new Wrmse(5);

        double tenthActual = 0;
        double tenthPredicted = 0;
        double tenthWeight = 0;

        for (int i = 0; i < 10; i++)
        {
            tenthActual = i * 10;
            tenthPredicted = i * 10 + 5;
            tenthWeight = i + 1.0;
            wrmse.Update(tenthActual, tenthPredicted, tenthWeight);
        }

        double stateAfterTen = wrmse.Last.Value;

        for (int i = 0; i < 5; i++)
        {
            wrmse.Update(100 + i, 200 + i, 5.0, isNew: false);
        }

        wrmse.Update(tenthActual, tenthPredicted, tenthWeight, isNew: false);

        Assert.Equal(stateAfterTen, wrmse.Last.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var wrmse = new Wrmse(5);

        for (int i = 0; i < 10; i++)
        {
            wrmse.Update(i * 10, i * 10 + 5, i + 1.0);
        }

        Assert.True(wrmse.IsHot);

        wrmse.Reset();

        Assert.False(wrmse.IsHot);
        Assert.Equal(0, wrmse.Last.Value);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var wrmse = new Wrmse(5);

        wrmse.Update(100, 110, 1.0);
        wrmse.Update(110, 120, 2.0);

        var result = wrmse.Update(double.NaN, double.NaN, double.NaN);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void NegativeWeight_UsesLastValidWeight()
    {
        var wrmse = new Wrmse(5);

        wrmse.Update(100, 110, 2.0);
        var beforeResult = wrmse.Last.Value;

        wrmse.Update(100, 110, -1.0); // Negative weight should use last valid (2.0)

        // Both should compute same result since same weight is used
        Assert.Equal(beforeResult, wrmse.Last.Value, 10);
    }

    [Fact]
    public void Wrmse_Throws_On_Single_Input()
    {
        var wrmse = new Wrmse(10);
        Assert.Throws<NotSupportedException>(() => wrmse.Update(new TValue(DateTime.UtcNow, 1)));
        Assert.Throws<NotSupportedException>(() => wrmse.Update(new TSeries()));
        Assert.Throws<NotSupportedException>(() => wrmse.Prime([1, 2, 3]));
    }

    [Fact]
    public void BatchSpan_UniformWeights_MatchesStreaming()
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

        var wrmse = new Wrmse(period);
        var streamingResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamingResults[i] = wrmse.Update(actual[i], predicted[i]).Value;
        }

        double[] batchResults = new double[count];
        Wrmse.Batch(actual, predicted, batchResults, period);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i], 9);
        }
    }

    [Fact]
    public void BatchSpan_WithWeights_MatchesStreaming()
    {
        int period = 5;
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 456);

        double[] actual = new double[count];
        double[] predicted = new double[count];
        double[] weights = new double[count];
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            actual[i] = bar.Close;
            predicted[i] = bar.Close * 1.05 + 2;
            weights[i] = (i % 5) + 1.0; // Varying weights 1-5
        }

        var wrmse = new Wrmse(period);
        var streamingResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamingResults[i] = wrmse.Update(actual[i], predicted[i], weights[i]).Value;
        }

        double[] batchResults = new double[count];
        Wrmse.Batch(actual, predicted, weights, batchResults, period);

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
        double[] weights = [1, 1, 1, 1, 1];
        double[] output = new double[5];

        Assert.Throws<ArgumentException>(() =>
            Wrmse.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() =>
            Wrmse.Batch(actual.AsSpan(), predicted.AsSpan(), new double[3].AsSpan(), 3));
        Assert.Throws<ArgumentException>(() =>
            Wrmse.Batch(actual.AsSpan(), predicted.AsSpan(), weights.AsSpan(), new double[3].AsSpan(), 3));
    }

    [Fact]
    public void Calculate_Works_UniformWeights()
    {
        var actual = new TSeries();
        var predicted = new TSeries();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            actual.Add(now.AddMinutes(i), i * 10);
            predicted.Add(now.AddMinutes(i), i * 10 + 5);
        }

        var results = Wrmse.Batch(actual, predicted, 3);

        Assert.Equal(10, results.Count);
        // All errors are 5, MSE = 25, RMSE = 5
        Assert.Equal(5.0, results.Last.Value, 10);
    }

    [Fact]
    public void Calculate_Works_CustomWeights()
    {
        var actual = new TSeries();
        var predicted = new TSeries();
        var weights = new TSeries();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            actual.Add(now.AddMinutes(i), 100.0);
            predicted.Add(now.AddMinutes(i), 110.0); // Error = 10, Squared = 100
            weights.Add(now.AddMinutes(i), 2.0); // Weight = 2
        }

        var results = Wrmse.Batch(actual, predicted, weights, 3);

        Assert.Equal(10, results.Count);
        // Weighted error = 2 * 100 = 200 per point, sum weights = 6 (period=3)
        // WRMSE = √(600/6) = √100 = 10
        Assert.Equal(10.0, results.Last.Value, 10);
    }

    [Fact]
    public void Calculate_ThrowsOnMismatchedLengths()
    {
        var actual = new TSeries();
        var predicted = new TSeries();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            actual.Add(now.AddMinutes(i), i * 10);
            if (i < 5)
            {
                predicted.Add(now.AddMinutes(i), i * 10 + 5);
            }
        }

        Assert.Throws<ArgumentException>(() => Wrmse.Batch(actual, predicted, 3));
    }

    [Fact]
    public void Calculate_ThrowsOnMismatchedWeightsLength()
    {
        var actual = new TSeries();
        var predicted = new TSeries();
        var weights = new TSeries();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            actual.Add(now.AddMinutes(i), i * 10);
            predicted.Add(now.AddMinutes(i), i * 10 + 5);
            if (i < 5)
            {
                weights.Add(now.AddMinutes(i), 1.0);
            }
        }

        Assert.Throws<ArgumentException>(() => Wrmse.Batch(actual, predicted, weights, 3));
    }

    [Fact]
    public void UniformWeightsBatch_MatchesRmseBatch()
    {
        int period = 5;
        int count = 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 789);

        double[] actual = new double[count];
        double[] predicted = new double[count];
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            actual[i] = bar.Close;
            predicted[i] = bar.Close * 1.03;
        }

        double[] wrmseResults = new double[count];
        double[] rmseResults = new double[count];

        Wrmse.Batch(actual, predicted, wrmseResults, period);
        Rmse.Batch(actual, predicted, rmseResults, period);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(rmseResults[i], wrmseResults[i], 9);
        }
    }
}
