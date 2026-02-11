namespace QuanTAlib.Tests;

public class AccelTests
{
    [Fact]
    public void Properties_Accessible()
    {
        var accel = new Accel();
        Assert.Equal(0, accel.Last.Value);
        Assert.False(accel.IsHot);
        Assert.Contains("Accel", accel.Name, StringComparison.Ordinal);
        Assert.Equal(3, accel.WarmupPeriod);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var accel = new Accel();
        accel.Update(new TValue(DateTime.UtcNow, 10));
        accel.Update(new TValue(DateTime.UtcNow, 20));
        accel.Update(new TValue(DateTime.UtcNow, 30));

        double valueBefore = accel.Last.Value;

        // Update with isNew=false should change the result
        accel.Update(new TValue(DateTime.UtcNow, 100), isNew: false);
        double valueAfter = accel.Last.Value;

        Assert.NotEqual(valueBefore, valueAfter);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var accel = new Accel();
        accel.Update(new TValue(DateTime.UtcNow, 10));
        accel.Update(new TValue(DateTime.UtcNow, 20));
        accel.Update(new TValue(DateTime.UtcNow, 30));

        var result = accel.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var accel = new Accel();
        accel.Update(new TValue(DateTime.UtcNow, 10));
        accel.Update(new TValue(DateTime.UtcNow, 20));
        accel.Update(new TValue(DateTime.UtcNow, 30));

        var resultPosInf = accel.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultPosInf.Value));

        var resultNegInf = accel.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultNegInf.Value));
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var accel = new Accel();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            accel.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = accel.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            accel.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalResult = accel.Update(tenthInput, isNew: false);

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
            Accel.Batch(source.AsSpan(), wrongSizeOutput.AsSpan()));
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
        Accel.Batch(tValues, batchOutput);
        double expected = batchOutput[^1];

        // 2. Streaming Mode
        var streamingInd = new Accel();
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 3. TSeries Batch Mode
        var batchSeriesResult = Accel.Batch(series);
        double tseriesResult = batchSeriesResult.Last.Value;

        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, tseriesResult, precision: 9);
    }

    [Fact]
    public void Calculation_KnownValues()
    {
        // accel[i] = source[i] - 2*source[i-1] + source[i-2]
        // Data: 10, 20, 35, 40, 42
        // slope[1] = 20-10 = 10
        // slope[2] = 35-20 = 15
        // slope[3] = 40-35 = 5
        // slope[4] = 42-40 = 2
        // accel[0] = 0 (insufficient history)
        // accel[1] = 0 (insufficient history)
        // accel[2] = 35 - 2*20 + 10 = 35 - 40 + 10 = 5
        // accel[3] = 40 - 2*35 + 20 = 40 - 70 + 20 = -10
        // accel[4] = 42 - 2*40 + 35 = 42 - 80 + 35 = -3

        double[] data = [10, 20, 35, 40, 42];
        double[] expected = [0, 0, 5, -10, -3];

        var accel = new Accel();
        for (int i = 0; i < data.Length; i++)
        {
            var result = accel.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var accel = new Accel();

        Assert.False(accel.IsHot);
        accel.Update(new TValue(DateTime.UtcNow, 10));
        Assert.False(accel.IsHot);
        accel.Update(new TValue(DateTime.UtcNow, 20));
        Assert.False(accel.IsHot);
        accel.Update(new TValue(DateTime.UtcNow, 30));
        Assert.True(accel.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var accel = new Accel();
        for (int i = 0; i < 10; i++)
        {
            accel.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.True(accel.IsHot);

        accel.Reset();
        Assert.False(accel.IsHot);
        Assert.Equal(0, accel.Last.Value);
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
        var accel = new Accel();
        var iterativeResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            accel.Update(new TValue(DateTime.UtcNow, data[i]));
            iterativeResults[i] = accel.Last.Value;
        }

        // Batch
        var batchResults = new double[count];
        Accel.Batch(data, batchResults);

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
        var accel = new Accel();
        var iterativeResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            accel.Update(data[i]);
            iterativeResults[i] = accel.Last.Value;
        }

        // TSeries Batch
        var accelBatch = new Accel();
        var batchSeries = accelBatch.Update(data);

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
        var accel = new Accel(source);

        source.Add(new TValue(DateTime.UtcNow, 10));
        source.Add(new TValue(DateTime.UtcNow, 20));
        source.Add(new TValue(DateTime.UtcNow, 35));

        Assert.True(accel.IsHot);
        Assert.Equal(5, accel.Last.Value); // 35 - 2*20 + 10 = 5
    }
}
