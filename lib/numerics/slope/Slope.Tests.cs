namespace QuanTAlib.Tests;

public class SlopeTests
{
    [Fact]
    public void Properties_Accessible()
    {
        var slope = new Slope();
        Assert.Equal(0, slope.Last.Value);
        Assert.False(slope.IsHot);
        Assert.Contains("Slope", slope.Name, StringComparison.Ordinal);
        Assert.Equal(2, slope.WarmupPeriod);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var slope = new Slope();
        slope.Update(new TValue(DateTime.UtcNow, 10));
        slope.Update(new TValue(DateTime.UtcNow, 20));

        double valueBefore = slope.Last.Value;

        // Update with isNew=false should change the result
        slope.Update(new TValue(DateTime.UtcNow, 100), isNew: false);
        double valueAfter = slope.Last.Value;

        Assert.NotEqual(valueBefore, valueAfter);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var slope = new Slope();
        slope.Update(new TValue(DateTime.UtcNow, 10));
        slope.Update(new TValue(DateTime.UtcNow, 20));

        var result = slope.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var slope = new Slope();
        slope.Update(new TValue(DateTime.UtcNow, 10));
        slope.Update(new TValue(DateTime.UtcNow, 20));

        var resultPosInf = slope.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultPosInf.Value));

        var resultNegInf = slope.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultNegInf.Value));
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var slope = new Slope();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            slope.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = slope.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            slope.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalResult = slope.Update(tenthInput, isNew: false);

        // State should match the original state after 10 values
        Assert.Equal(stateAfterTen, finalResult.Value, 1e-9);
    }

    [Fact]
    public void SpanBatch_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] wrongSizeOutput = new double[3];

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() =>
            Slope.Calculate(source.AsSpan(), wrongSizeOutput.AsSpan()));
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode (static span)
        var tValues = series.Values.ToArray();
        var batchOutput = new double[tValues.Length];
        Slope.Calculate(tValues, batchOutput);
        double expected = batchOutput[^1];

        // 2. Streaming Mode
        var streamingInd = new Slope();
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 3. TSeries Batch Mode
        var batchSeriesResult = Slope.Calculate(series);
        double tseriesResult = batchSeriesResult.Last.Value;

        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, tseriesResult, precision: 9);
    }

    [Fact]
    public void Calculation_KnownValues()
    {
        // slope[i] = source[i] - source[i-1]
        // Data: 10, 20, 25, 30, 28
        // Slopes: 0, 10, 5, 5, -2

        double[] data = [10, 20, 25, 30, 28];
        double[] expected = [0, 10, 5, 5, -2];

        var slope = new Slope();
        for (int i = 0; i < data.Length; i++)
        {
            var result = slope.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var slope = new Slope();

        Assert.False(slope.IsHot);
        slope.Update(new TValue(DateTime.UtcNow, 10));
        Assert.False(slope.IsHot);
        slope.Update(new TValue(DateTime.UtcNow, 20));
        Assert.True(slope.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var slope = new Slope();
        for (int i = 0; i < 10; i++)
        {
            slope.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.True(slope.IsHot);

        slope.Reset();
        Assert.False(slope.IsHot);
        Assert.Equal(0, slope.Last.Value);
    }

    [Fact]
    public void Batch_Matches_Iterative()
    {
        int count = 1000;
        var data = new double[count];
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        for (int i = 0; i < count; i++)
        {
            data[i] = gbm.Next().Close;
        }

        // Iterative
        var slope = new Slope();
        var iterativeResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            slope.Update(new TValue(DateTime.UtcNow, data[i]));
            iterativeResults[i] = slope.Last.Value;
        }

        // Batch
        var batchResults = new double[count];
        Slope.Calculate(data, batchResults);

        // Compare
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(iterativeResults[i], batchResults[i], precision: 9);
        }
    }

    [Fact]
    public void Update_TSeries_Matches_Iterative()
    {
        int count = 1000;
        var data = new TSeries();
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            data.Add(new TValue(bar.Time, bar.Close));
        }

        // Iterative
        var slope = new Slope();
        var iterativeResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            slope.Update(data[i]);
            iterativeResults[i] = slope.Last.Value;
        }

        // TSeries Batch
        var slopeBatch = new Slope();
        var batchSeries = slopeBatch.Update(data);

        // Compare
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(iterativeResults[i], batchSeries[i].Value, precision: 9);
        }
    }

    [Fact]
    public void EventSubscription_Works()
    {
        var source = new TSeries();
        var slope = new Slope(source);

        source.Add(new TValue(DateTime.UtcNow, 10));
        source.Add(new TValue(DateTime.UtcNow, 20));

        Assert.True(slope.IsHot);
        Assert.Equal(10, slope.Last.Value);
    }
}
