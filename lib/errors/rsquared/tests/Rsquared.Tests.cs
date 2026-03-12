namespace QuanTAlib.Tests;

public class RsquaredTests
{
    private readonly GBM _gbm;
    private const int Period = 10;

    public RsquaredTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Rsquared(0));
        Assert.Throws<ArgumentException>(() => new Rsquared(-1));

        var r2 = new Rsquared(10);
        Assert.NotNull(r2);
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var r2 = new Rsquared(Period);
        var time = DateTime.UtcNow;

        var result = r2.Update(new TValue(time, 100), new TValue(time, 95));

        Assert.True(result.Value <= 1.0);
        Assert.Equal(result.Value, r2.Last.Value);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var r2 = new Rsquared(Period);

        Assert.Equal(0, r2.Last.Value);
        Assert.False(r2.IsHot);
        Assert.Contains("R²", r2.Name, StringComparison.Ordinal);

        r2.Update(new TValue(DateTime.UtcNow, 100), new TValue(DateTime.UtcNow, 95));
        Assert.NotEqual(0, r2.Last.Value);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var r2 = new Rsquared(Period);
        var time = DateTime.UtcNow;

        r2.Update(new TValue(time, 100), new TValue(time, 95), isNew: true);
        double value1 = r2.Last.Value;

        r2.Update(new TValue(time.AddSeconds(1), 102), new TValue(time.AddSeconds(1), 98), isNew: true);
        double value2 = r2.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var r2 = new Rsquared(Period);
        var time = DateTime.UtcNow;

        r2.Update(new TValue(time, 100), new TValue(time, 95));
        r2.Update(new TValue(time.AddSeconds(1), 105), new TValue(time.AddSeconds(1), 100), isNew: true);
        double beforeUpdate = r2.Last.Value;

        r2.Update(new TValue(time.AddSeconds(1), 110), new TValue(time.AddSeconds(1), 100), isNew: false);
        double afterUpdate = r2.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var r2 = new Rsquared(Period);

        r2.Update(new TValue(DateTime.UtcNow, 100), new TValue(DateTime.UtcNow, 95));
        r2.Update(new TValue(DateTime.UtcNow, 105), new TValue(DateTime.UtcNow, 100));

        r2.Reset();

        Assert.Equal(0, r2.Last.Value);
        Assert.False(r2.IsHot);

        r2.Update(new TValue(DateTime.UtcNow, 50), new TValue(DateTime.UtcNow, 48));
        Assert.NotEqual(0, r2.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var r2 = new Rsquared(5);

        Assert.False(r2.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            r2.Update(new TValue(DateTime.UtcNow, 100 + i), new TValue(DateTime.UtcNow, 100));
            Assert.False(r2.IsHot);
        }

        r2.Update(new TValue(DateTime.UtcNow, 106), new TValue(DateTime.UtcNow, 101));
        Assert.True(r2.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var r2 = new Rsquared(Period);

        r2.Update(new TValue(DateTime.UtcNow, 100), new TValue(DateTime.UtcNow, 95));
        r2.Update(new TValue(DateTime.UtcNow, 105), new TValue(DateTime.UtcNow, 100));

        var resultAfterNaN = r2.Update(new TValue(DateTime.UtcNow, double.NaN), new TValue(DateTime.UtcNow, 102));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var r2 = new Rsquared(Period);

        r2.Update(new TValue(DateTime.UtcNow, 100), new TValue(DateTime.UtcNow, 95));
        r2.Update(new TValue(DateTime.UtcNow, 105), new TValue(DateTime.UtcNow, 100));

        var resultAfterPosInf = r2.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity), new TValue(DateTime.UtcNow, 102));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        var resultAfterNegInf = r2.Update(new TValue(DateTime.UtcNow, 108), new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void PerfectPrediction_ReturnsOne()
    {
        var r2 = new Rsquared(Period);
        var time = DateTime.UtcNow;

        // Different actual values but perfect predictions
        for (int i = 0; i < 20; i++)
        {
            double val = 100 + (i * 2);
            r2.Update(new TValue(time.AddSeconds(i), val), new TValue(time.AddSeconds(i), val));
        }

        Assert.Equal(1.0, r2.Last.Value, 1e-10);
    }

    [Fact]
    public void R2EqualsOneMinusRse()
    {
        // R² = 1 - RSE relationship
        var r2 = new Rsquared(10);
        var rse = new Rse(10);
        var time = DateTime.UtcNow;

        // Generate data with some error
        for (int i = 0; i < 20; i++)
        {
            double actual = 100 + (i * 2);
            double predicted = actual + (((i % 3) - 1) * 2);
            r2.Update(new TValue(time.AddSeconds(i), actual), new TValue(time.AddSeconds(i), predicted));
            rse.Update(new TValue(time.AddSeconds(i), actual), new TValue(time.AddSeconds(i), predicted));
        }

        double r2Value = r2.Last.Value;
        double rseValue = rse.Last.Value;

        // R² = 1 - RSE
        Assert.Equal(r2Value, 1.0 - rseValue, 1e-10);
    }

    [Fact]
    public void MeanPredictor_ReturnsApproximatelyZero()
    {
        // When prediction = mean of actuals, R² ≈ 0
        var r2 = new Rsquared(5);
        var time = DateTime.UtcNow;

        double[] values = { 100, 104, 96, 108, 92, 110, 90, 105, 95, 100 };

        // Use running mean as predictor
        double runningSum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            runningSum += values[i];
            double mean = runningSum / (i + 1);
            r2.Update(new TValue(time.AddSeconds(i), values[i]), new TValue(time.AddSeconds(i), mean));
        }

        // R² should be close to 0 when predicting the mean
        Assert.True(r2.Last.Value > -0.5 && r2.Last.Value < 0.5,
            $"Expected R² ≈ 0, got {r2.Last.Value}");
    }

    [Fact]
    public void GoodPredictions_HighR2()
    {
        var r2 = new Rsquared(10);
        var time = DateTime.UtcNow;

        // Linear trend with small random noise in predictions
        for (int i = 0; i < 20; i++)
        {
            double actual = 100 + (i * 2);
            double predicted = actual + (i % 2 == 0 ? 0.5 : -0.5); // Small systematic error
            r2.Update(new TValue(time.AddSeconds(i), actual), new TValue(time.AddSeconds(i), predicted));
        }

        // Good predictions should have high R²
        Assert.True(r2.Last.Value > 0.9, $"Expected R² > 0.9 for good predictions, got {r2.Last.Value}");
    }

    [Fact]
    public void NegativeR2_WorseThanMean()
    {
        var r2 = new Rsquared(10);
        var time = DateTime.UtcNow;

        // Predictions that are anti-correlated with actuals
        for (int i = 0; i < 20; i++)
        {
            double actual = 100 + (i % 2 == 0 ? 10 : -10);
            double predicted = 100 + (i % 2 == 0 ? -10 : 10); // Opposite direction
            r2.Update(new TValue(time.AddSeconds(i), actual), new TValue(time.AddSeconds(i), predicted));
        }

        // Anti-correlated predictions should have negative R²
        Assert.True(r2.Last.Value < 0, $"Expected R² < 0 for anti-correlated predictions, got {r2.Last.Value}");
    }

    [Fact]
    public void FlatLine_ReturnsOne()
    {
        var r2 = new Rsquared(Period);

        // Flat actual values means TSS = 0
        // Should return 1.0 (default when TSS is zero)
        for (int i = 0; i < 20; i++)
        {
            r2.Update(new TValue(DateTime.UtcNow, 100), new TValue(DateTime.UtcNow, 95));
        }

        // When all actual values are the same, TSS = 0, returns 1.0
        Assert.Equal(1.0, r2.Last.Value, 1e-10);
    }

    [Fact]
    public void R2_RangeUpperBoundIsOne()
    {
        var r2 = new Rsquared(Period);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 100; i++)
        {
            double actual = 100 + (Math.Sin(i * 0.1) * 20);
            double predicted = actual + ((i % 5) - 2); // Small systematic error
            r2.Update(new TValue(time.AddSeconds(i), actual), new TValue(time.AddSeconds(i), predicted));

            // R² should never exceed 1
            Assert.True(r2.Last.Value <= 1.0 + 1e-10,
                $"R² = {r2.Last.Value} exceeded 1.0 at iteration {i}");
        }
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var r2Iterative = new Rsquared(Period);
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var actual = bars.Close;
        var predicted = new TSeries();
        foreach (var item in actual)
        {
            predicted.Add(item.Time, item.Value * 0.98);
        }

        var iterativeResults = new List<double>();
        for (int i = 0; i < actual.Count; i++)
        {
            iterativeResults.Add(r2Iterative.Update(actual[i], predicted[i]).Value);
        }

        var batchResults = Rsquared.Batch(actual, predicted, Period);

        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i], batchResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void SpanBatch_ValidatesInput()
    {
        double[] actual = [1, 2, 3, 4, 5];
        double[] predicted = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentException>(() =>
            Rsquared.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() =>
            Rsquared.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), -1));
        Assert.Throws<ArgumentException>(() =>
            Rsquared.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var actualSeries = bars.Close;
        var predictedSeries = new TSeries();
        foreach (var item in actualSeries)
        {
            predictedSeries.Add(item.Time, item.Value * 0.98);
        }

        double[] actualArr = actualSeries.Values.ToArray();
        double[] predictedArr = predictedSeries.Values.ToArray();
        double[] output = new double[100];

        var tseriesResult = Rsquared.Batch(actualSeries, predictedSeries, Period);
        Rsquared.Batch(actualArr.AsSpan(), predictedArr.AsSpan(), output.AsSpan(), Period);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var actualSeries = bars.Close;
        var predictedSeries = new TSeries();
        foreach (var item in actualSeries)
        {
            predictedSeries.Add(item.Time, item.Value * 0.98);
        }

        // 1. Batch Mode (static method)
        var batchSeries = Rsquared.Batch(actualSeries, predictedSeries, Period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        double[] actualArr = actualSeries.Values.ToArray();
        double[] predictedArr = predictedSeries.Values.ToArray();
        double[] spanOutput = new double[actualArr.Length];
        Rsquared.Batch(actualArr.AsSpan(), predictedArr.AsSpan(), spanOutput.AsSpan(), Period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Rsquared(Period);
        for (int i = 0; i < actualSeries.Count; i++)
        {
            streamingInd.Update(actualSeries[i], predictedSeries[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
    }

    [Fact]
    public void DoubleOverload_Works()
    {
        var r2 = new Rsquared(Period);

        var result = r2.Update(100.0, 95.0);

        Assert.True(result.Value <= 1.0);
        Assert.Equal(result.Value, r2.Last.Value);
    }

    [Fact]
    public void SingleInputUpdate_Throws()
    {
        var r2 = new Rsquared(Period);

        Assert.Throws<NotSupportedException>(() =>
            r2.Update(new TValue(DateTime.UtcNow, 100)));
    }

    [Fact]
    public void SingleInputTSeriesUpdate_Throws()
    {
        var r2 = new Rsquared(Period);
        var series = new TSeries();
        series.Add(DateTime.UtcNow, 100);

        Assert.Throws<NotSupportedException>(() => r2.Update(series));
    }
}
