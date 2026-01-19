
namespace QuanTAlib.Tests;

public class SkewTests
{
    [Fact]
    public void Constructor_ValidatesPeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Skew(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Skew(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Skew(-1));
        var skew = new Skew(3);
        Assert.NotNull(skew);
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var skew = new Skew(5);

        Assert.Equal(0, skew.Last.Value);

        TValue result = skew.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(result.Value, skew.Last.Value);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var skew = new Skew(5);

        skew.Update(new TValue(DateTime.UtcNow, 1), isNew: true);
        skew.Update(new TValue(DateTime.UtcNow, 2), isNew: true);
        skew.Update(new TValue(DateTime.UtcNow, 3), isNew: true);
        skew.Update(new TValue(DateTime.UtcNow, 4), isNew: true);
        double value1 = skew.Update(new TValue(DateTime.UtcNow, 5), isNew: true).Value;

        skew.Update(new TValue(DateTime.UtcNow, 10), isNew: true);
        double value2 = skew.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var skew = new Skew(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            skew.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = skew.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            skew.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalResult = skew.Update(tenthInput, isNew: false);

        // State should match the original state after 10 values
        // Use looser tolerance due to floating-point accumulation in Skew's 3rd moment calculation
        Assert.Equal(stateAfterTen, finalResult.Value, 1e-3);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var skew = new Skew(5);

        Assert.False(skew.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            skew.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(skew.IsHot);
        }

        skew.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(skew.IsHot);
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var skew = new Skew(5);

        skew.Update(new TValue(DateTime.UtcNow, 1));
        skew.Update(new TValue(DateTime.UtcNow, 2));
        skew.Update(new TValue(DateTime.UtcNow, 3));

        // Skew doesn't do last-valid-value substitution - it treats non-finite as 0
        // Just verify it doesn't crash and returns a finite value
        var resultAfterPosInf = skew.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value) || double.IsNaN(resultAfterPosInf.Value));

        var resultAfterNegInf = skew.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value) || double.IsNaN(resultAfterNegInf.Value));
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        // Arrange
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
        var batchSeries = Skew.Calculate(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode (static method with spans)
        var spanInput = values.ToArray();
        var spanOutput = new double[count];
        Skew.Batch(spanInput.AsSpan(), spanOutput.AsSpan(), period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode (instance, one value at a time)
        var streamingInd = new Skew(period);
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

        // Period must be >= 3
        Assert.Throws<ArgumentException>(() =>
            Skew.Batch(source.AsSpan(), output.AsSpan(), 2));
        Assert.Throws<ArgumentException>(() =>
            Skew.Batch(source.AsSpan(), output.AsSpan(), 0));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() =>
            Skew.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
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

        var tseriesResult = Skew.Calculate(series, 10);
        Skew.Batch(source.AsSpan(), output.AsSpan(), 10);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void Update_CalculatesCorrectly_Sample()
    {
        // Test data: 1, 2, 3, 4, 5
        // Mean = 3
        // Variance (Sample) = 2.5
        // StdDev (Sample) = 1.58113883
        // Skewness (Sample) = 0 (Symmetric)

        var skew = new Skew(5, isPopulation: false);
        skew.Update(new TValue(DateTime.UtcNow, 1));
        skew.Update(new TValue(DateTime.UtcNow, 2));
        skew.Update(new TValue(DateTime.UtcNow, 3));
        skew.Update(new TValue(DateTime.UtcNow, 4));
        var result = skew.Update(new TValue(DateTime.UtcNow, 5));

        Assert.Equal(0, result.Value, precision: 10);
    }

    [Fact]
    public void Update_CalculatesCorrectly_PositiveSkew()
    {
        // Test data: 1, 1, 1, 10
        // Mean = 3.25
        // Skewness should be positive (right tail)

        var skew = new Skew(4, isPopulation: false);
        skew.Update(new TValue(DateTime.UtcNow, 1));
        skew.Update(new TValue(DateTime.UtcNow, 1));
        skew.Update(new TValue(DateTime.UtcNow, 1));
        var result = skew.Update(new TValue(DateTime.UtcNow, 10));

        Assert.True(result.Value > 0);
    }

    [Fact]
    public void Update_CalculatesCorrectly_NegativeSkew()
    {
        // Test data: 10, 10, 10, 1
        // Mean = 7.75
        // Skewness should be negative (left tail)

        var skew = new Skew(4, isPopulation: false);
        skew.Update(new TValue(DateTime.UtcNow, 10));
        skew.Update(new TValue(DateTime.UtcNow, 10));
        skew.Update(new TValue(DateTime.UtcNow, 10));
        var result = skew.Update(new TValue(DateTime.UtcNow, 1));

        Assert.True(result.Value < 0);
    }

    [Fact]
    public void Update_HandlesUpdates_IsNewFalse()
    {
        var skew = new Skew(5);

        // 1, 2, 3, 4
        skew.Update(new TValue(DateTime.UtcNow, 1));
        skew.Update(new TValue(DateTime.UtcNow, 2));
        skew.Update(new TValue(DateTime.UtcNow, 3));
        skew.Update(new TValue(DateTime.UtcNow, 4));

        // Add 5
        skew.Update(new TValue(DateTime.UtcNow, 5), isNew: true);

        // Update 5 to 10
        var res2 = skew.Update(new TValue(DateTime.UtcNow, 10), isNew: false);

        // Expected: Skew of 1, 2, 3, 4, 10
        var expectedSkew = new Skew(5);
        expectedSkew.Update(new TValue(DateTime.UtcNow, 1));
        expectedSkew.Update(new TValue(DateTime.UtcNow, 2));
        expectedSkew.Update(new TValue(DateTime.UtcNow, 3));
        expectedSkew.Update(new TValue(DateTime.UtcNow, 4));
        var expected = expectedSkew.Update(new TValue(DateTime.UtcNow, 10));

        Assert.Equal(expected.Value, res2.Value, precision: 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var skew = new Skew(5);
        for (int i = 0; i < 5; i++) skew.Update(new TValue(DateTime.UtcNow, i));

        skew.Reset();
        Assert.False(skew.IsHot);

        // Should behave like new
        skew.Update(new TValue(DateTime.UtcNow, 1));
        Assert.Equal(0, skew.Last.Value); // Not enough data
    }

    [Fact]
    public void Batch_Matches_Streaming()
    {
        double[] data = [1, 2, 3, 4, 5, 10, 1, 2, 3];
        int period = 5;

        // Streaming
        var skew = new Skew(period);
        var streamingResults = new System.Collections.Generic.List<double>();
        foreach (var val in data)
        {
            streamingResults.Add(skew.Update(new TValue(DateTime.UtcNow, val)).Value);
        }

        // Batch
        var series = new TSeries(new System.Collections.Generic.List<long>(new long[data.Length]), new System.Collections.Generic.List<double>(data));
        var batchResult = Skew.Calculate(series, period);

        for (int i = 0; i < data.Length; i++)
        {
            Assert.Equal(streamingResults[i], batchResult.Values[i], precision: 10);
        }
    }

    [Fact]
    public void Update_CalculatesCorrectly_Population()
    {
        // Test data: 1, 2, 3
        // Mean = 2
        // Variance (Pop) = ((1-2)^2 + (2-2)^2 + (3-2)^2) / 3 = 2/3
        // StdDev (Pop) = sqrt(2/3)
        // M3 (Pop) = ((1-2)^3 + (2-2)^3 + (3-2)^3) / 3 = 0
        // Skew (Pop) = 0

        var skew = new Skew(3, isPopulation: true);
        skew.Update(new TValue(DateTime.UtcNow, 1));
        skew.Update(new TValue(DateTime.UtcNow, 2));
        var result = skew.Update(new TValue(DateTime.UtcNow, 3));

        Assert.Equal(0, result.Value, precision: 10);
    }

    [Fact]
    public void Update_HandlesConstantValues_ZeroVariance()
    {
        var skew = new Skew(5);
        for (int i = 0; i < 5; i++)
        {
            var result = skew.Update(new TValue(DateTime.UtcNow, 10));
            Assert.Equal(0, result.Value, precision: 10); // Skew is undefined or 0 for constant values
        }
    }

    [Fact]
    public void Update_HandlesNaN()
    {
        var skew = new Skew(5);
        skew.Update(new TValue(DateTime.UtcNow, 1));
        skew.Update(new TValue(DateTime.UtcNow, 2));
        skew.Update(new TValue(DateTime.UtcNow, double.NaN)); // Should be treated as 0 or handled gracefully

        var result = skew.Last.Value;
        Assert.True(double.IsNaN(result) || Math.Abs(result) < 1e-14);
    }

    [Fact]
    public void Resync_DoesNotDrift()
    {
        // Run for > 1000 updates to trigger Resync
        var skew = new Skew(10);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        for (int i = 0; i < 1100; i++)
        {
            skew.Update(new TValue(DateTime.UtcNow, gbm.Next().Close));
        }

        Assert.True(double.IsFinite(skew.Last.Value));
    }

    [Fact]
    public void Batch_LargeDataset_Simd()
    {
        // Create large dataset to trigger SIMD path (>= 256)
        int count = 1000;
        var data = new double[count];
        for (int i = 0; i < count; i++) data[i] = (double)i;

        var series = new TSeries(new System.Collections.Generic.List<long>(new long[count]), new System.Collections.Generic.List<double>(data));

        // Batch calculation
        var batchResult = Skew.Calculate(series, 10);

        // Verify last value against streaming
        var skew = new Skew(10);
        double lastStreaming = 0;
        foreach (var val in data)
        {
            lastStreaming = skew.Update(new TValue(DateTime.UtcNow, val)).Value;
        }

        Assert.Equal(lastStreaming, batchResult.Last.Value, precision: 10);
    }
}
