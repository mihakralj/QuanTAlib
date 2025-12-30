namespace QuanTAlib.Tests;

public class MaseTests
{
    private readonly GBM _gbm;
    private const int Period = 10;

    public MaseTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Mase(0));
        Assert.Throws<ArgumentException>(() => new Mase(-1));

        var mase = new Mase(10);
        Assert.NotNull(mase);
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var mase = new Mase(Period);
        var time = DateTime.UtcNow;

        var result = mase.Update(new TValue(time, 100), new TValue(time, 95));

        Assert.True(result.Value >= 0);
        Assert.Equal(result.Value, mase.Last.Value);
    }

    [Fact]
    public void FirstValue_ReturnsAbsoluteError()
    {
        var mase = new Mase(Period);
        var time = DateTime.UtcNow;

        var result = mase.Update(new TValue(time, 100), new TValue(time, 95));

        // First value has no scale (no previous value), so returns MAE / 1.0 = MAE = |100-95| = 5
        Assert.Equal(5.0, result.Value, 1e-10);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var mase = new Mase(Period);

        Assert.Equal(0, mase.Last.Value);
        Assert.False(mase.IsHot);
        Assert.Contains("Mase", mase.Name, StringComparison.Ordinal);

        mase.Update(new TValue(DateTime.UtcNow, 100), new TValue(DateTime.UtcNow, 95));
        Assert.NotEqual(0, mase.Last.Value);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var mase = new Mase(Period);
        var time = DateTime.UtcNow;

        mase.Update(new TValue(time, 100), new TValue(time, 95), isNew: true);
        double value1 = mase.Last.Value;

        mase.Update(new TValue(time.AddSeconds(1), 102), new TValue(time.AddSeconds(1), 98), isNew: true);
        double value2 = mase.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var mase = new Mase(Period);
        var time = DateTime.UtcNow;

        mase.Update(new TValue(time, 100), new TValue(time, 95));
        mase.Update(new TValue(time.AddSeconds(1), 105), new TValue(time.AddSeconds(1), 100), isNew: true);
        double beforeUpdate = mase.Last.Value;

        mase.Update(new TValue(time.AddSeconds(1), 110), new TValue(time.AddSeconds(1), 100), isNew: false);
        double afterUpdate = mase.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var mase = new Mase(Period);

        mase.Update(new TValue(DateTime.UtcNow, 100), new TValue(DateTime.UtcNow, 95));
        mase.Update(new TValue(DateTime.UtcNow, 105), new TValue(DateTime.UtcNow, 100));

        mase.Reset();

        Assert.Equal(0, mase.Last.Value);
        Assert.False(mase.IsHot);

        mase.Update(new TValue(DateTime.UtcNow, 50), new TValue(DateTime.UtcNow, 48));
        Assert.NotEqual(0, mase.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var mase = new Mase(5);

        Assert.False(mase.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            mase.Update(new TValue(DateTime.UtcNow, 100 + i), new TValue(DateTime.UtcNow, 100));
            Assert.False(mase.IsHot);
        }

        mase.Update(new TValue(DateTime.UtcNow, 106), new TValue(DateTime.UtcNow, 101));
        Assert.True(mase.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var mase = new Mase(Period);

        mase.Update(new TValue(DateTime.UtcNow, 100), new TValue(DateTime.UtcNow, 95));
        mase.Update(new TValue(DateTime.UtcNow, 105), new TValue(DateTime.UtcNow, 100));

        var resultAfterNaN = mase.Update(new TValue(DateTime.UtcNow, double.NaN), new TValue(DateTime.UtcNow, 102));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.True(resultAfterNaN.Value >= 0);
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var mase = new Mase(Period);

        mase.Update(new TValue(DateTime.UtcNow, 100), new TValue(DateTime.UtcNow, 95));
        mase.Update(new TValue(DateTime.UtcNow, 105), new TValue(DateTime.UtcNow, 100));

        var resultAfterPosInf = mase.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity), new TValue(DateTime.UtcNow, 102));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        var resultAfterNegInf = mase.Update(new TValue(DateTime.UtcNow, 108), new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void PerfectPrediction_ReturnsZero()
    {
        var mase = new Mase(Period);
        var time = DateTime.UtcNow;

        // All perfect predictions
        for (int i = 0; i < 20; i++)
        {
            double val = 100 + i;
            mase.Update(new TValue(time.AddSeconds(i), val), new TValue(time.AddSeconds(i), val));
        }

        Assert.Equal(0.0, mase.Last.Value, 1e-10);
    }

    [Fact]
    public void NaiveForecast_ReturnsApproximatelyOne()
    {
        // When prediction = previous actual (naive forecast), MASE ≈ 1
        var mase = new Mase(10);
        var time = DateTime.UtcNow;

        double[] values = { 100, 102, 98, 105, 103, 108, 106, 110, 107, 112, 109, 115, 112 };

        double prevValue = double.NaN;
        for (int i = 0; i < values.Length; i++)
        {
            double predicted = double.IsFinite(prevValue) ? prevValue : values[i];
            mase.Update(new TValue(time.AddSeconds(i), values[i]), new TValue(time.AddSeconds(i), predicted));
            prevValue = values[i];
        }

        // MASE should be close to 1 when using naive forecast
        Assert.True(Math.Abs(mase.Last.Value - 1.0) < 0.5, $"Expected MASE ≈ 1, got {mase.Last.Value}");
    }

    [Fact]
    public void BetterThanNaive_ReturnsLessThanOne()
    {
        // When prediction is closer to actual than naive forecast, MASE < 1
        var mase = new Mase(10);
        var time = DateTime.UtcNow;

        // Generate data where prediction is always perfect
        for (int i = 0; i < 20; i++)
        {
            double actual = 100 + i * 2;
            double perfect = actual; // Perfect prediction
            mase.Update(new TValue(time.AddSeconds(i), actual), new TValue(time.AddSeconds(i), perfect));
        }

        // With perfect predictions, MASE should be 0
        Assert.Equal(0.0, mase.Last.Value, 1e-10);
    }

    [Fact]
    public void FlatLine_ReturnsCorrectValue()
    {
        var mase = new Mase(Period);

        // Flat actual, prediction off by 5 -> MAE = 5, Scale = 0, returns MAE = 5
        for (int i = 0; i < 20; i++)
        {
            mase.Update(new TValue(DateTime.UtcNow, 100), new TValue(DateTime.UtcNow, 95));
        }

        // Flat line has scale ≈ 0, so result should be MAE (5)
        Assert.Equal(5.0, mase.Last.Value, 1e-10);
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var maseIterative = new Mase(Period);
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
            iterativeResults.Add(maseIterative.Update(actual[i], predicted[i]).Value);
        }

        var batchResults = Mase.Calculate(actual, predicted, Period);

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
            Mase.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() =>
            Mase.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), -1));
        Assert.Throws<ArgumentException>(() =>
            Mase.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), 3));
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

        var tseriesResult = Mase.Calculate(actualSeries, predictedSeries, Period);
        Mase.Batch(actualArr.AsSpan(), predictedArr.AsSpan(), output.AsSpan(), Period);

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
        var batchSeries = Mase.Calculate(actualSeries, predictedSeries, Period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        double[] actualArr = actualSeries.Values.ToArray();
        double[] predictedArr = predictedSeries.Values.ToArray();
        double[] spanOutput = new double[actualArr.Length];
        Mase.Batch(actualArr.AsSpan(), predictedArr.AsSpan(), spanOutput.AsSpan(), Period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Mase(Period);
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
        var mase = new Mase(Period);

        var result = mase.Update(100.0, 95.0);

        Assert.True(result.Value >= 0);
        Assert.Equal(result.Value, mase.Last.Value);
    }

    [Fact]
    public void SingleInputUpdate_Throws()
    {
        var mase = new Mase(Period);

        Assert.Throws<NotSupportedException>(() =>
            mase.Update(new TValue(DateTime.UtcNow, 100)));
    }

    [Fact]
    public void SingleInputTSeriesUpdate_Throws()
    {
        var mase = new Mase(Period);
        var series = new TSeries();
        series.Add(DateTime.UtcNow, 100);

        Assert.Throws<NotSupportedException>(() => mase.Update(series));
    }
}
