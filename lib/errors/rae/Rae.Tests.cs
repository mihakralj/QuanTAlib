namespace QuanTAlib.Tests;

public class RaeTests
{
    private readonly GBM _gbm;
    private const int Period = 10;

    public RaeTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Rae(0));
        Assert.Throws<ArgumentException>(() => new Rae(-1));

        var rae = new Rae(10);
        Assert.NotNull(rae);
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var rae = new Rae(Period);
        var time = DateTime.UtcNow;

        var result = rae.Update(new TValue(time, 100), new TValue(time, 95));

        Assert.True(result.Value >= 0);
        Assert.Equal(result.Value, rae.Last.Value);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var rae = new Rae(Period);

        Assert.Equal(0, rae.Last.Value);
        Assert.False(rae.IsHot);
        Assert.Contains("Rae", rae.Name, StringComparison.Ordinal);

        rae.Update(new TValue(DateTime.UtcNow, 100), new TValue(DateTime.UtcNow, 95));
        Assert.NotEqual(0, rae.Last.Value);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var rae = new Rae(Period);
        var time = DateTime.UtcNow;

        rae.Update(new TValue(time, 100), new TValue(time, 95), isNew: true);
        double value1 = rae.Last.Value;

        rae.Update(new TValue(time.AddSeconds(1), 102), new TValue(time.AddSeconds(1), 98), isNew: true);
        double value2 = rae.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var rae = new Rae(Period);
        var time = DateTime.UtcNow;

        rae.Update(new TValue(time, 100), new TValue(time, 95));
        rae.Update(new TValue(time.AddSeconds(1), 105), new TValue(time.AddSeconds(1), 100), isNew: true);
        double beforeUpdate = rae.Last.Value;

        rae.Update(new TValue(time.AddSeconds(1), 110), new TValue(time.AddSeconds(1), 100), isNew: false);
        double afterUpdate = rae.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var rae = new Rae(Period);

        rae.Update(new TValue(DateTime.UtcNow, 100), new TValue(DateTime.UtcNow, 95));
        rae.Update(new TValue(DateTime.UtcNow, 105), new TValue(DateTime.UtcNow, 100));

        rae.Reset();

        Assert.Equal(0, rae.Last.Value);
        Assert.False(rae.IsHot);

        rae.Update(new TValue(DateTime.UtcNow, 50), new TValue(DateTime.UtcNow, 48));
        Assert.NotEqual(0, rae.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var rae = new Rae(5);

        Assert.False(rae.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            rae.Update(new TValue(DateTime.UtcNow, 100 + i), new TValue(DateTime.UtcNow, 100));
            Assert.False(rae.IsHot);
        }

        rae.Update(new TValue(DateTime.UtcNow, 106), new TValue(DateTime.UtcNow, 101));
        Assert.True(rae.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var rae = new Rae(Period);

        rae.Update(new TValue(DateTime.UtcNow, 100), new TValue(DateTime.UtcNow, 95));
        rae.Update(new TValue(DateTime.UtcNow, 105), new TValue(DateTime.UtcNow, 100));

        var resultAfterNaN = rae.Update(new TValue(DateTime.UtcNow, double.NaN), new TValue(DateTime.UtcNow, 102));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.True(resultAfterNaN.Value >= 0);
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var rae = new Rae(Period);

        rae.Update(new TValue(DateTime.UtcNow, 100), new TValue(DateTime.UtcNow, 95));
        rae.Update(new TValue(DateTime.UtcNow, 105), new TValue(DateTime.UtcNow, 100));

        var resultAfterPosInf = rae.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity), new TValue(DateTime.UtcNow, 102));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        var resultAfterNegInf = rae.Update(new TValue(DateTime.UtcNow, 108), new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void PerfectPrediction_ReturnsZero()
    {
        var rae = new Rae(Period);
        var time = DateTime.UtcNow;

        // Different actual values but perfect predictions
        for (int i = 0; i < 20; i++)
        {
            double val = 100 + i * 2;
            rae.Update(new TValue(time.AddSeconds(i), val), new TValue(time.AddSeconds(i), val));
        }

        Assert.Equal(0.0, rae.Last.Value, 1e-10);
    }

    [Fact]
    public void MeanPredictor_ReturnsApproximatelyOne()
    {
        // When prediction = mean of actuals, RAE ≈ 1
        var rae = new Rae(5);
        var time = DateTime.UtcNow;

        // First pass to establish mean, then predict with mean
        double[] values = { 100, 104, 96, 108, 92, 110, 90, 105, 95, 100 };

        // Use running mean as predictor
        double runningSum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            runningSum += values[i];
            double mean = runningSum / (i + 1);
            rae.Update(new TValue(time.AddSeconds(i), values[i]), new TValue(time.AddSeconds(i), mean));
        }

        // RAE should be close to 1 when predicting the mean
        Assert.True(rae.Last.Value > 0.5 && rae.Last.Value < 1.5,
            $"Expected RAE ≈ 1, got {rae.Last.Value}");
    }

    [Fact]
    public void BetterThanMean_ReturnsLessThanOne()
    {
        var rae = new Rae(10);
        var time = DateTime.UtcNow;

        // Perfect predictions should give RAE = 0 (better than mean)
        for (int i = 0; i < 20; i++)
        {
            double actual = 100 + i;
            rae.Update(new TValue(time.AddSeconds(i), actual), new TValue(time.AddSeconds(i), actual));
        }

        Assert.True(rae.Last.Value < 1.0, $"Expected RAE < 1, got {rae.Last.Value}");
    }

    [Fact]
    public void FlatLine_ReturnsPredictorError()
    {
        var rae = new Rae(Period);

        // Flat actual values means baseline = 0 (all values equal mean)
        // Should return 1.0 (default when baseline is zero)
        for (int i = 0; i < 20; i++)
        {
            rae.Update(new TValue(DateTime.UtcNow, 100), new TValue(DateTime.UtcNow, 95));
        }

        // When all actual values are the same, baseline error is 0, returns 1.0
        Assert.Equal(1.0, rae.Last.Value, 1e-10);
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var raeIterative = new Rae(Period);
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
            iterativeResults.Add(raeIterative.Update(actual[i], predicted[i]).Value);
        }

        var batchResults = Rae.Calculate(actual, predicted, Period);

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
            Rae.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() =>
            Rae.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), -1));
        Assert.Throws<ArgumentException>(() =>
            Rae.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), 3));
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

        var tseriesResult = Rae.Calculate(actualSeries, predictedSeries, Period);
        Rae.Batch(actualArr.AsSpan(), predictedArr.AsSpan(), output.AsSpan(), Period);

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
        var batchSeries = Rae.Calculate(actualSeries, predictedSeries, Period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        double[] actualArr = actualSeries.Values.ToArray();
        double[] predictedArr = predictedSeries.Values.ToArray();
        double[] spanOutput = new double[actualArr.Length];
        Rae.Batch(actualArr.AsSpan(), predictedArr.AsSpan(), spanOutput.AsSpan(), Period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Rae(Period);
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
        var rae = new Rae(Period);

        var result = rae.Update(100.0, 95.0);

        Assert.True(result.Value >= 0);
        Assert.Equal(result.Value, rae.Last.Value);
    }

    [Fact]
    public void SingleInputUpdate_Throws()
    {
        var rae = new Rae(Period);

        Assert.Throws<NotSupportedException>(() =>
            rae.Update(new TValue(DateTime.UtcNow, 100)));
    }

    [Fact]
    public void SingleInputTSeriesUpdate_Throws()
    {
        var rae = new Rae(Period);
        var series = new TSeries();
        series.Add(DateTime.UtcNow, 100);

        Assert.Throws<NotSupportedException>(() => rae.Update(series));
    }
}
