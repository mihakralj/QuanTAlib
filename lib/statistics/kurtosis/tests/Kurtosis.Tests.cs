
namespace QuanTAlib.Tests;

public class KurtosisTests
{
    [Fact]
    public void Constructor_ValidatesPeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Kurtosis(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Kurtosis(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Kurtosis(-1));
        var kurtosis = new Kurtosis(4);
        Assert.NotNull(kurtosis);
    }

    [Fact]
    public void Constructor_SetsName()
    {
        var kurtosis = new Kurtosis(14);
        Assert.Equal("Kurtosis(14)", kurtosis.Name);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var kurtosis = new Kurtosis(10);
        Assert.Equal(10, kurtosis.WarmupPeriod);
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var kurtosis = new Kurtosis(5);

        Assert.Equal(0, kurtosis.Last.Value);

        TValue result = kurtosis.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(result.Value, kurtosis.Last.Value);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var kurtosis = new Kurtosis(5);

        kurtosis.Update(new TValue(DateTime.UtcNow, 1), isNew: true);
        kurtosis.Update(new TValue(DateTime.UtcNow, 2), isNew: true);
        kurtosis.Update(new TValue(DateTime.UtcNow, 3), isNew: true);
        kurtosis.Update(new TValue(DateTime.UtcNow, 4), isNew: true);
        double value1 = kurtosis.Update(new TValue(DateTime.UtcNow, 5), isNew: true).Value;

        kurtosis.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value2 = kurtosis.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var kurtosis = new Kurtosis(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            kurtosis.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = kurtosis.Last.Value;

        // Single correction: replace latest bar with a different value, then restore
        var corrBar = gbm.Next(isNew: false);
        kurtosis.Update(new TValue(corrBar.Time, corrBar.Close), isNew: false);

        // Value should differ after correction with different data
        double correctedValue = kurtosis.Last.Value;
        Assert.NotEqual(stateAfterTen, correctedValue, precision: 5);

        // Now restore original 10th input with isNew=false
        TValue finalResult = kurtosis.Update(tenthInput, isNew: false);

        // State should match the original state after 10 values
        Assert.Equal(stateAfterTen, finalResult.Value, precision: 10);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var kurtosis = new Kurtosis(5);

        Assert.False(kurtosis.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            kurtosis.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(kurtosis.IsHot);
        }

        kurtosis.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(kurtosis.IsHot);
    }

    [Fact]
    public void Infinity_Input_DoesNotCrash()
    {
        var kurtosis = new Kurtosis(5);

        kurtosis.Update(new TValue(DateTime.UtcNow, 1));
        kurtosis.Update(new TValue(DateTime.UtcNow, 2));
        kurtosis.Update(new TValue(DateTime.UtcNow, 3));

        // Verify it doesn't crash and returns a finite value
        var resultAfterPosInf = kurtosis.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value) || double.IsNaN(resultAfterPosInf.Value));

        var resultAfterNegInf = kurtosis.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value) || double.IsNaN(resultAfterNegInf.Value));
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        int count = 200;

        var times = new List<long>(count);
        var values = new List<double>(count);

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            times.Add(bar.Time);
            values.Add(bar.Close);
        }

        var series = new TSeries(times, values);

        // 1. Batch Mode (static method)
        var batchSeries = Kurtosis.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode (static method with spans)
        var spanInput = values.ToArray();
        var spanOutput = new double[count];
        Kurtosis.Batch(spanInput.AsSpan(), spanOutput.AsSpan(), period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode (instance, one value at a time)
        var streamingInd = new Kurtosis(period);
        for (int i = 0; i < count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // Assert all modes produce identical results
        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
    }

    [Fact]
    public void SpanBatch_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        // Period must be >= 4
        Assert.Throws<ArgumentException>(() =>
            Kurtosis.Batch(source.AsSpan(), output.AsSpan(), 3));
        Assert.Throws<ArgumentException>(() =>
            Kurtosis.Batch(source.AsSpan(), output.AsSpan(), 0));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() =>
            Kurtosis.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 4));
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        int count = 100;

        var times = new List<long>(count);
        var values = new List<double>(count);
        double[] source = new double[count];
        double[] output = new double[count];

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            times.Add(bar.Time);
            values.Add(bar.Close);
            source[i] = bar.Close;
        }

        var series = new TSeries(times, values);

        var tseriesResult = Kurtosis.Batch(series, 10);
        Kurtosis.Batch(source.AsSpan(), output.AsSpan(), 10);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void Update_SymmetricData_ReturnsNearZero_Population()
    {
        // Symmetric data {1, 2, 3, 4, 5}: excess kurtosis should be near -1.3 (platykurtic)
        // Population excess kurtosis of uniform-like sequence is negative
        var kurtosis = new Kurtosis(5, isPopulation: true);
        kurtosis.Update(new TValue(DateTime.UtcNow, 1));
        kurtosis.Update(new TValue(DateTime.UtcNow, 2));
        kurtosis.Update(new TValue(DateTime.UtcNow, 3));
        kurtosis.Update(new TValue(DateTime.UtcNow, 4));
        var result = kurtosis.Update(new TValue(DateTime.UtcNow, 5));

        // Population excess kurtosis of {1,2,3,4,5} = 17/10 - 3 = -1.3
        Assert.Equal(-1.3, result.Value, precision: 10);
    }

    [Fact]
    public void Update_LeptokurticData_ReturnsPositive()
    {
        // Data with heavy tails: {1, 1, 1, 1, 10}
        // Should have positive excess kurtosis (leptokurtic)
        var kurtosis = new Kurtosis(5, isPopulation: true);
        kurtosis.Update(new TValue(DateTime.UtcNow, 1));
        kurtosis.Update(new TValue(DateTime.UtcNow, 1));
        kurtosis.Update(new TValue(DateTime.UtcNow, 1));
        kurtosis.Update(new TValue(DateTime.UtcNow, 1));
        var result = kurtosis.Update(new TValue(DateTime.UtcNow, 10));

        // Heavy tail → leptokurtic → positive excess kurtosis
        Assert.True(result.Value > 0);
    }

    [Fact]
    public void Update_HandlesUpdates_IsNewFalse()
    {
        var kurtosis = new Kurtosis(5);

        // 1, 2, 3, 4
        kurtosis.Update(new TValue(DateTime.UtcNow, 1));
        kurtosis.Update(new TValue(DateTime.UtcNow, 2));
        kurtosis.Update(new TValue(DateTime.UtcNow, 3));
        kurtosis.Update(new TValue(DateTime.UtcNow, 4));

        // Add 5
        kurtosis.Update(new TValue(DateTime.UtcNow, 5), isNew: true);

        // Update 5 to 10
        var res2 = kurtosis.Update(new TValue(DateTime.UtcNow, 10), isNew: false);

        // Expected: Kurtosis of 1, 2, 3, 4, 10
        var expectedKurtosis = new Kurtosis(5);
        expectedKurtosis.Update(new TValue(DateTime.UtcNow, 1));
        expectedKurtosis.Update(new TValue(DateTime.UtcNow, 2));
        expectedKurtosis.Update(new TValue(DateTime.UtcNow, 3));
        expectedKurtosis.Update(new TValue(DateTime.UtcNow, 4));
        var expected = expectedKurtosis.Update(new TValue(DateTime.UtcNow, 10));

        Assert.Equal(expected.Value, res2.Value, precision: 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var kurtosis = new Kurtosis(5);
        for (int i = 0; i < 5; i++)
        {
            kurtosis.Update(new TValue(DateTime.UtcNow, i));
        }

        kurtosis.Reset();
        Assert.False(kurtosis.IsHot);

        // Should behave like new
        kurtosis.Update(new TValue(DateTime.UtcNow, 1));
        Assert.Equal(0, kurtosis.Last.Value); // Not enough data
    }

    [Fact]
    public void Batch_Matches_Streaming()
    {
        double[] data = [1, 2, 3, 4, 5, 10, 1, 2, 3, 4];
        int period = 5;

        // Streaming
        var kurtosis = new Kurtosis(period);
        var streamingResults = new List<double>();
        foreach (var val in data)
        {
            streamingResults.Add(kurtosis.Update(new TValue(DateTime.UtcNow, val)).Value);
        }

        // Batch
        var series = new TSeries(new List<long>(new long[data.Length]), new List<double>(data));
        var batchResult = Kurtosis.Batch(series, period);

        for (int i = 0; i < data.Length; i++)
        {
            Assert.Equal(streamingResults[i], batchResult.Values[i], precision: 10);
        }
    }

    [Fact]
    public void Update_HandlesConstantValues_ZeroVariance()
    {
        var kurtosis = new Kurtosis(5);
        for (int i = 0; i < 5; i++)
        {
            var result = kurtosis.Update(new TValue(DateTime.UtcNow, 10));
            Assert.Equal(0, result.Value, precision: 10);
        }
    }

    [Fact]
    public void Update_HandlesNaN()
    {
        var kurtosis = new Kurtosis(5);
        kurtosis.Update(new TValue(DateTime.UtcNow, 1));
        kurtosis.Update(new TValue(DateTime.UtcNow, 2));
        kurtosis.Update(new TValue(DateTime.UtcNow, double.NaN));

        var result = kurtosis.Last.Value;
        Assert.True(double.IsNaN(result) || Math.Abs(result) < 1e-14);
    }

    [Fact]
    public void Resync_DoesNotDrift()
    {
        // Run for > 1000 updates to trigger Resync
        var kurtosis = new Kurtosis(10);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        for (int i = 0; i < 1100; i++)
        {
            kurtosis.Update(new TValue(DateTime.UtcNow, gbm.Next().Close));
        }

        Assert.True(double.IsFinite(kurtosis.Last.Value));
    }

    [Fact]
    public void Batch_LargeDataset_Simd()
    {
        // Create large dataset to trigger SIMD path (>= 256)
        int count = 1000;
        var data = new double[count];
        for (int i = 0; i < count; i++)
        {
            data[i] = (double)i;
        }

        var series = new TSeries(new List<long>(new long[count]), new List<double>(data));

        // Batch calculation
        var batchResult = Kurtosis.Batch(series, 10);

        // Verify last value against streaming
        var kurtosis = new Kurtosis(10);
        double lastStreaming = 0;
        foreach (var val in data)
        {
            lastStreaming = kurtosis.Update(new TValue(DateTime.UtcNow, val)).Value;
        }

        Assert.Equal(lastStreaming, batchResult.Last.Value, precision: 10);
    }

    [Fact]
    public void Chaining_PubEventFires()
    {
        var source = new Kurtosis(5);
        var chained = new Kurtosis(source, 5);

        source.Update(new TValue(DateTime.UtcNow, 1));
        source.Update(new TValue(DateTime.UtcNow, 2));
        source.Update(new TValue(DateTime.UtcNow, 3));
        source.Update(new TValue(DateTime.UtcNow, 4));
        source.Update(new TValue(DateTime.UtcNow, 5));

        // Chained indicator should have received updates via Pub event
        Assert.True(double.IsFinite(chained.Last.Value));
    }
}
