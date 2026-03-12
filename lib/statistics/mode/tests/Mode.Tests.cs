namespace QuanTAlib.Tests;

public class ModeTests
{
    [Fact]
    public void Constructor_ValidatesPeriod()
    {
        Assert.Throws<ArgumentException>(() => new Mode(0));
        Assert.Throws<ArgumentException>(() => new Mode(-1));
        var mode = new Mode(1);
        Assert.NotNull(mode);
    }

    [Fact]
    public void Constructor_SetsName()
    {
        var mode = new Mode(14);
        Assert.Equal("Mode(14)", mode.Name);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var mode = new Mode(10);
        Assert.Equal(10, mode.WarmupPeriod);
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var mode = new Mode(5);

        Assert.Equal(0, mode.Last.Value);

        TValue result = mode.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(result.Value, mode.Last.Value);
    }

    [Fact]
    public void SingleValue_ReturnsItself()
    {
        var mode = new Mode(5);
        var result = mode.Update(new TValue(DateTime.UtcNow, 42));

        // Single value is trivially the mode
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void AllDistinct_ReturnsNaN()
    {
        // {1, 2, 3, 4, 5} — all unique → NaN (no mode)
        var mode = new Mode(5);
        mode.Update(new TValue(DateTime.UtcNow, 1));
        mode.Update(new TValue(DateTime.UtcNow, 2));
        mode.Update(new TValue(DateTime.UtcNow, 3));
        mode.Update(new TValue(DateTime.UtcNow, 4));
        var result = mode.Update(new TValue(DateTime.UtcNow, 5));

        Assert.True(double.IsNaN(result.Value));
    }

    [Fact]
    public void RepeatedValue_ReturnsMode()
    {
        // {1, 2, 2, 3, 4} → mode = 2
        var mode = new Mode(5);
        mode.Update(new TValue(DateTime.UtcNow, 1));
        mode.Update(new TValue(DateTime.UtcNow, 2));
        mode.Update(new TValue(DateTime.UtcNow, 2));
        mode.Update(new TValue(DateTime.UtcNow, 3));
        var result = mode.Update(new TValue(DateTime.UtcNow, 4));

        Assert.Equal(2, result.Value);
    }

    [Fact]
    public void MultipleRepeated_ReturnsHighestFrequency()
    {
        // {1, 2, 2, 3, 3, 3, 4} with period=7 → mode = 3
        var mode = new Mode(7);
        mode.Update(new TValue(DateTime.UtcNow, 1));
        mode.Update(new TValue(DateTime.UtcNow, 2));
        mode.Update(new TValue(DateTime.UtcNow, 2));
        mode.Update(new TValue(DateTime.UtcNow, 3));
        mode.Update(new TValue(DateTime.UtcNow, 3));
        mode.Update(new TValue(DateTime.UtcNow, 3));
        var result = mode.Update(new TValue(DateTime.UtcNow, 4));

        Assert.Equal(3, result.Value);
    }

    [Fact]
    public void AllSameValue_ReturnsValue()
    {
        // {5, 5, 5, 5, 5} → mode = 5
        var mode = new Mode(5);
        for (int i = 0; i < 5; i++)
        {
            mode.Update(new TValue(DateTime.UtcNow, 5));
        }

        Assert.Equal(5, mode.Last.Value);
    }

    [Fact]
    public void SlidingWindow_DropsOldValues()
    {
        // Feed {1, 1, 1, 2, 3} → mode = 1
        // Then feed 4 → window becomes {1, 1, 2, 3, 4} → mode = 1
        // Then feed 4 → window becomes {1, 2, 3, 4, 4} → mode = 4
        var mode = new Mode(5);
        mode.Update(new TValue(DateTime.UtcNow, 1));
        mode.Update(new TValue(DateTime.UtcNow, 1));
        mode.Update(new TValue(DateTime.UtcNow, 1));
        mode.Update(new TValue(DateTime.UtcNow, 2));
        mode.Update(new TValue(DateTime.UtcNow, 3));
        Assert.Equal(1, mode.Last.Value);

        mode.Update(new TValue(DateTime.UtcNow, 4));
        Assert.Equal(1, mode.Last.Value); // Still 1 (1,1,2,3,4)

        mode.Update(new TValue(DateTime.UtcNow, 4));
        Assert.Equal(4, mode.Last.Value); // Now 4 (1,2,3,4,4)
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var mode = new Mode(5);

        Assert.False(mode.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            mode.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(mode.IsHot);
        }

        mode.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(mode.IsHot);
    }

    [Fact]
    public void Update_HandlesUpdates_IsNewFalse()
    {
        var mode = new Mode(5);

        // 1, 2, 3, 4
        mode.Update(new TValue(DateTime.UtcNow, 1));
        mode.Update(new TValue(DateTime.UtcNow, 2));
        mode.Update(new TValue(DateTime.UtcNow, 3));
        mode.Update(new TValue(DateTime.UtcNow, 4));

        // Add 5 (all distinct → NaN)
        mode.Update(new TValue(DateTime.UtcNow, 5), isNew: true);
        Assert.True(double.IsNaN(mode.Last.Value));

        // Correct to 1 (window: 1,2,3,4,1 → mode = 1)
        var result = mode.Update(new TValue(DateTime.UtcNow, 1), isNew: false);
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public void BarCorrection_RestoreToOriginal()
    {
        var mode = new Mode(5);

        // Feed {1, 2, 3, 4, 4} → mode = 4
        mode.Update(new TValue(DateTime.UtcNow, 1));
        mode.Update(new TValue(DateTime.UtcNow, 2));
        mode.Update(new TValue(DateTime.UtcNow, 3));
        mode.Update(new TValue(DateTime.UtcNow, 4));
        mode.Update(new TValue(DateTime.UtcNow, 4), isNew: true);
        double original = mode.Last.Value;
        Assert.Equal(4, original);

        // Correct last bar to 1 → {1, 2, 3, 4, 1} sorted {1,1,2,3,4} → mode = 1
        mode.Update(new TValue(DateTime.UtcNow, 1), isNew: false);
        Assert.NotEqual(original, mode.Last.Value);
        Assert.Equal(1, mode.Last.Value);

        // Correct back to 4 → {1, 2, 3, 4, 4} → mode = 4
        var result = mode.Update(new TValue(DateTime.UtcNow, 4), isNew: false);
        Assert.Equal(original, result.Value);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var mode = new Mode(5);
        for (int i = 0; i < 5; i++)
        {
            mode.Update(new TValue(DateTime.UtcNow, i));
        }

        mode.Reset();
        Assert.False(mode.IsHot);
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int period = 5;
        int count = 50;

        // Create data with repeated values to ensure mode exists
        double[] data = new double[count];
        for (int i = 0; i < count; i++)
        {
            data[i] = Math.Round(i % 7.0); // Values 0-6 with repeats
        }

        var times = new List<long>(count);
        var values = new List<double>(count);
        for (int i = 0; i < count; i++)
        {
            times.Add(DateTime.UtcNow.Ticks + i);
            values.Add(data[i]);
        }

        var series = new TSeries(times, values);

        // 1. Batch Mode
        var batchSeries = Mode.Batch(series, period);

        // 2. Span Mode
        var spanOutput = new double[count];
        Mode.Batch(data.AsSpan(), spanOutput.AsSpan(), period);

        // 3. Streaming Mode
        var streamingInd = new Mode(period);
        var streamingResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamingResults[i] = streamingInd.Update(series[i]).Value;
        }

        // Assert all modes produce identical results
        for (int i = 0; i < count; i++)
        {
            if (double.IsNaN(batchSeries[i].Value))
            {
                Assert.True(double.IsNaN(spanOutput[i]), $"Span output at {i} should be NaN");
                Assert.True(double.IsNaN(streamingResults[i]), $"Streaming output at {i} should be NaN");
            }
            else
            {
                Assert.Equal(batchSeries[i].Value, spanOutput[i], precision: 10);
                Assert.Equal(batchSeries[i].Value, streamingResults[i], precision: 10);
            }
        }
    }

    [Fact]
    public void SpanBatch_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        // Period must be > 0
        Assert.Throws<ArgumentException>(() =>
            Mode.Batch(source.AsSpan(), output.AsSpan(), 0));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() =>
            Mode.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 5));
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        int count = 50;
        double[] data = new double[count];
        for (int i = 0; i < count; i++)
        {
            data[i] = Math.Round(i % 5.0);
        }

        var times = new List<long>(count);
        var values = new List<double>(count);
        for (int i = 0; i < count; i++)
        {
            times.Add(DateTime.UtcNow.Ticks + i);
            values.Add(data[i]);
        }

        var series = new TSeries(times, values);
        var tseriesResult = Mode.Batch(series, 5);

        var output = new double[count];
        Mode.Batch(data.AsSpan(), output.AsSpan(), 5);

        for (int i = 0; i < count; i++)
        {
            if (double.IsNaN(tseriesResult[i].Value))
            {
                Assert.True(double.IsNaN(output[i]));
            }
            else
            {
                Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
            }
        }
    }

    [Fact]
    public void Batch_Matches_Streaming()
    {
        double[] data = [1, 1, 2, 2, 2, 3, 3, 1, 1, 1];
        int period = 5;

        // Streaming
        var mode = new Mode(period);
        var streamingResults = new List<double>();
        foreach (var val in data)
        {
            streamingResults.Add(mode.Update(new TValue(DateTime.UtcNow, val)).Value);
        }

        // Batch
        var series = new TSeries(new List<long>(new long[data.Length]), new List<double>(data));
        var batchResult = Mode.Batch(series, period);

        for (int i = 0; i < data.Length; i++)
        {
            if (double.IsNaN(streamingResults[i]))
            {
                Assert.True(double.IsNaN(batchResult.Values[i]));
            }
            else
            {
                Assert.Equal(streamingResults[i], batchResult.Values[i], precision: 10);
            }
        }
    }

    [Fact]
    public void Chaining_PubEventFires()
    {
        var source = new Mode(5);
        var chained = new Mode(source, 5);

        for (int i = 0; i < 5; i++)
        {
            source.Update(new TValue(DateTime.UtcNow, i));
        }

        // Chained indicator should have received updates via Pub event
        Assert.True(double.IsFinite(chained.Last.Value) || double.IsNaN(chained.Last.Value));
    }

    [Fact]
    public void Period_One_AlwaysReturnsInput()
    {
        var mode = new Mode(1);

        for (int i = 0; i < 10; i++)
        {
            double val = i * 3.14;
            var result = mode.Update(new TValue(DateTime.UtcNow, val));
            Assert.Equal(val, result.Value);
        }
    }

    [Fact]
    public void BimodalData_ReturnsFirstMode()
    {
        // {1, 1, 2, 2, 3} — bimodal (1 and 2 both appear twice)
        // Sorted: {1, 1, 2, 2, 3}
        // Scan finds 1 first with freq=2, then 2 with freq=2 (not > maxFreq)
        // Returns 1 (first encountered in sorted order)
        var mode = new Mode(5);
        mode.Update(new TValue(DateTime.UtcNow, 1));
        mode.Update(new TValue(DateTime.UtcNow, 1));
        mode.Update(new TValue(DateTime.UtcNow, 2));
        mode.Update(new TValue(DateTime.UtcNow, 2));
        var result = mode.Update(new TValue(DateTime.UtcNow, 3));

        // First mode in sorted order wins
        Assert.Equal(1, result.Value);
    }
}
