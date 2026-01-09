
namespace QuanTAlib.Tests;

public class StdDevTests
{
    [Fact]
    public void Constructor_ValidatesPeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StdDev(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StdDev(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StdDev(-1));
    }

    [Fact]
    public void Properties_Accessible()
    {
        var stddev = new StdDev(5);
        Assert.Equal(0, stddev.Last.Value);
        Assert.False(stddev.IsHot);
        Assert.Contains("StdDev", stddev.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var stddev = new StdDev(3);
        stddev.Update(new TValue(DateTime.UtcNow, 10));
        stddev.Update(new TValue(DateTime.UtcNow, 20));
        stddev.Update(new TValue(DateTime.UtcNow, 30));

        double valueBefore = stddev.Last.Value;

        // Update with isNew=false should change the result
        stddev.Update(new TValue(DateTime.UtcNow, 100), isNew: false);
        double valueAfter = stddev.Last.Value;

        Assert.NotEqual(valueBefore, valueAfter);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var stddev = new StdDev(5);
        stddev.Update(new TValue(DateTime.UtcNow, 10));
        stddev.Update(new TValue(DateTime.UtcNow, 20));

        var result = stddev.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var stddev = new StdDev(5);
        stddev.Update(new TValue(DateTime.UtcNow, 10));
        stddev.Update(new TValue(DateTime.UtcNow, 20));

        var resultPosInf = stddev.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultPosInf.Value));

        var resultNegInf = stddev.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultNegInf.Value));
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var stddev = new StdDev(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            stddev.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = stddev.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            stddev.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalResult = stddev.Update(tenthInput, isNew: false);

        // State should match the original state after 10 values
        // Note: FMA optimization in RingBuffer provides better precision, so we use a slightly relaxed tolerance
        Assert.Equal(stateAfterTen, finalResult.Value, 1e-9);
    }

    [Fact]
    public void SpanBatch_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        // Period must be > 1
        Assert.Throws<ArgumentException>(() =>
            StdDev.Batch(source.AsSpan(), output.AsSpan(), 1));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() =>
            StdDev.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode (static span)
        var tValues = series.Values.ToArray();
        var batchOutput = new double[tValues.Length];
        StdDev.Batch(tValues, batchOutput, period);
        double expected = batchOutput[^1];

        // 2. Streaming Mode
        var streamingInd = new StdDev(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 3. TSeries Batch Mode
        var batchSeriesResult = StdDev.Calculate(series, period);
        double tseriesResult = batchSeriesResult.Last.Value;

        Assert.Equal(expected, streamingResult, precision: 6);
        Assert.Equal(expected, tseriesResult, precision: 6);
    }

    [Fact]
    public void Calculation_KnownValues()
    {
        // Data: 2, 4, 4, 4, 5, 5, 7, 9
        // Mean: 5
        // Deviations: -3, -1, -1, -1, 0, 0, 2, 4
        // Sq Devs: 9, 1, 1, 1, 0, 0, 4, 16
        // Sum Sq Devs: 32
        // Population Variance (N=8): 32 / 8 = 4
        // Population StdDev: Sqrt(4) = 2
        // Sample Variance (N-1=7): 32 / 7 = 4.571428...
        // Sample StdDev: Sqrt(4.571428...) = 2.1380899...

        double[] data = [2, 4, 4, 4, 5, 5, 7, 9];

        // Test Population StdDev
        var popStd = new StdDev(8, isPopulation: true);
        foreach (var val in data)
        {
            popStd.Update(new TValue(DateTime.UtcNow, val));
        }
        Assert.Equal(2.0, popStd.Last.Value, precision: 6);

        // Test Sample StdDev
        var sampStd = new StdDev(8, isPopulation: false);
        foreach (var val in data)
        {
            sampStd.Update(new TValue(DateTime.UtcNow, val));
        }
        Assert.Equal(Math.Sqrt(32.0 / 7.0), sampStd.Last.Value, precision: 6);
    }

    [Fact]
    public void IsHot_BecomesTrueAfterPeriod()
    {
        const int period = 5;
        var stdDev = new StdDev(period);

        for (int i = 0; i < period; i++)
        {
            Assert.False(stdDev.IsHot);
            stdDev.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.True(stdDev.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var stdDev = new StdDev(5);
        for (int i = 0; i < 10; i++)
        {
            stdDev.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.True(stdDev.IsHot);

        stdDev.Reset();
        Assert.False(stdDev.IsHot);
        Assert.Equal(0, stdDev.Last.Value);
    }

    [Fact]
    public void Batch_Matches_Iterative()
    {
        int period = 10;
        int count = 1000;
        var data = new double[count];
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        for (int i = 0; i < count; i++)
        {
            data[i] = gbm.Next().Close;
        }

        // Iterative
        var stdDev = new StdDev(period);
        var iterativeResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            stdDev.Update(new TValue(DateTime.UtcNow, data[i]));
            iterativeResults[i] = stdDev.Last.Value;
        }

        // Batch
        var batchResults = new double[count];
        StdDev.Batch(data, batchResults, period);

        // Compare
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(iterativeResults[i], batchResults[i], precision: 6);
        }
    }

    [Fact]
    public void Update_TSeries_Matches_Iterative()
    {
        int period = 10;
        int count = 1000;
        var data = new TSeries();
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            data.Add(new TValue(bar.Time, bar.Close));
        }

        // Iterative
        var stdDev = new StdDev(period);
        var iterativeResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            stdDev.Update(data[i]);
            iterativeResults[i] = stdDev.Last.Value;
        }

        // TSeries Batch
        var stdDevBatch = new StdDev(period);
        var batchSeries = stdDevBatch.Update(data);

        // Compare
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(iterativeResults[i], batchSeries[i].Value, precision: 6);
        }
    }
}
