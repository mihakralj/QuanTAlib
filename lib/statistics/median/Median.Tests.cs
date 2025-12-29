using Xunit;

namespace QuanTAlib;

public class MedianTests
{
    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Median(0));
        Assert.Throws<ArgumentException>(() => new Median(-1));
    }

    [Fact]
    public void Properties_Accessible()
    {
        var median = new Median(5);
        Assert.Equal(0, median.Last.Value);
        Assert.False(median.IsHot);
        Assert.Contains("Median", median.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var median = new Median(5);
        Assert.False(median.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            median.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(median.IsHot);
        }

        median.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(median.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var median = new Median(5);
        for (int i = 0; i < 10; i++)
        {
            median.Update(new TValue(DateTime.UtcNow, i * 10));
        }
        Assert.True(median.IsHot);

        median.Reset();
        Assert.False(median.IsHot);
        Assert.Equal(0, median.Last.Value);

        // After reset, should accept new values
        var result = median.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50, result.Value);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var median = new Median(3);
        median.Update(new TValue(DateTime.UtcNow, 10));
        median.Update(new TValue(DateTime.UtcNow, 20));

        var result = median.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var median = new Median(3);
        median.Update(new TValue(DateTime.UtcNow, 10));
        median.Update(new TValue(DateTime.UtcNow, 20));

        var resultPosInf = median.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultPosInf.Value));

        var resultNegInf = median.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultNegInf.Value));
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var median = new Median(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            median.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = median.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            median.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalResult = median.Update(tenthInput, isNew: false);

        // State should match the original state after 10 values
        Assert.Equal(stateAfterTen, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Median_OddPeriod_ReturnsMiddleValue()
    {
        // Arrange
        var median = new Median(3);

        // Act
        median.Update(new TValue(DateTime.MinValue, 10));
        median.Update(new TValue(DateTime.MinValue, 30));
        var result = median.Update(new TValue(DateTime.MinValue, 20));

        // Assert
        // Window: [10, 30, 20] -> Sorted: [10, 20, 30] -> Median: 20
        Assert.Equal(20, result.Value);
    }

    [Fact]
    public void Median_EvenPeriod_ReturnsAverageOfMiddleValues()
    {
        // Arrange
        var median = new Median(4);

        // Act
        median.Update(new TValue(DateTime.MinValue, 10));
        median.Update(new TValue(DateTime.MinValue, 40));
        median.Update(new TValue(DateTime.MinValue, 20));
        var result = median.Update(new TValue(DateTime.MinValue, 30));

        // Assert
        // Window: [10, 40, 20, 30] -> Sorted: [10, 20, 30, 40] -> Median: (20 + 30) / 2 = 25
        Assert.Equal(25, result.Value);
    }

    [Fact]
    public void Median_UpdatesWithIsNewFalse_Correctly()
    {
        // Arrange
        var median = new Median(3);

        // Act
        median.Update(new TValue(DateTime.MinValue, 10));
        median.Update(new TValue(DateTime.MinValue, 20));

        // Update with 30 (isNew=true)
        var r1 = median.Update(new TValue(DateTime.MinValue, 30));
        // Window: [10, 20, 30] -> Median 20
        Assert.Equal(20, r1.Value);

        // Update with 40 (isNew=false) -> Replaces 30 with 40
        var r2 = median.Update(new TValue(DateTime.MinValue, 40), isNew: false);
        // Window: [10, 20, 40] -> Median 20
        Assert.Equal(20, r2.Value);

        // Update with 5 (isNew=false) -> Replaces 40 with 5
        var r3 = median.Update(new TValue(DateTime.MinValue, 5), isNew: false);
        // Window: [10, 20, 5] -> Sorted [5, 10, 20] -> Median 10
        Assert.Equal(10, r3.Value);
    }

    [Fact]
    public void Median_Batch_Matches_Streaming()
    {
        // Arrange
        int period = 5;
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        // Act
        var medianBatch = Median.Batch(source, period);
        var medianStream = new Median(period);
        var streamResults = new List<double>();

        foreach (var val in source)
        {
            streamResults.Add(medianStream.Update(val).Value);
        }

        // Assert
        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(medianBatch.Values[i], streamResults[i], 1e-9);
        }
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        int period = 5;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Median.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Median.Batch(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Median(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Median(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }

    [Fact]
    public void SpanBatch_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        // Period must be > 0
        Assert.Throws<ArgumentException>(() =>
            Median.Batch(source.AsSpan(), output.AsSpan(), 0));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() =>
            Median.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Median_StaticBatch_Matches_ClassBatch()
    {
        // Arrange
        int period = 5;
        double[] data = new double[20];
        for(int i=0; i<data.Length; i++) data[i] = i;

        // Act
        double[] output = new double[data.Length];
        Median.Batch(data, output, period);

        var series = new TSeries();
        for(int i=0; i<data.Length; i++) series.Add(new TValue(DateTime.MinValue, data[i]));
        var batchSeries = Median.Batch(series, period);

        // Assert
        for (int i = 0; i < data.Length; i++)
        {
            Assert.Equal(batchSeries.Values[i], output[i], 1e-9);
        }
    }
}
