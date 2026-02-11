using Xunit;

namespace QuanTAlib.Tests;

public class JerkTests
{
    [Fact]
    public void Properties_Accessible()
    {
        var jerk = new Jerk();
        Assert.Equal(0, jerk.Last.Value);
        Assert.False(jerk.IsHot);
        Assert.Contains("Jerk", jerk.Name, StringComparison.Ordinal);
        Assert.Equal(4, jerk.WarmupPeriod);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var jerk = new Jerk();
        jerk.Update(new TValue(DateTime.UtcNow, 10));
        jerk.Update(new TValue(DateTime.UtcNow, 20));
        jerk.Update(new TValue(DateTime.UtcNow, 30));
        jerk.Update(new TValue(DateTime.UtcNow, 40));

        double valueBefore = jerk.Last.Value;

        // Update with isNew=false should change the result
        jerk.Update(new TValue(DateTime.UtcNow, 100), isNew: false);
        double valueAfter = jerk.Last.Value;

        Assert.NotEqual(valueBefore, valueAfter);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var jerk = new Jerk();
        jerk.Update(new TValue(DateTime.UtcNow, 10));
        jerk.Update(new TValue(DateTime.UtcNow, 20));
        jerk.Update(new TValue(DateTime.UtcNow, 30));
        jerk.Update(new TValue(DateTime.UtcNow, 40));

        var result = jerk.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var jerk = new Jerk();
        jerk.Update(new TValue(DateTime.UtcNow, 10));
        jerk.Update(new TValue(DateTime.UtcNow, 20));
        jerk.Update(new TValue(DateTime.UtcNow, 30));
        jerk.Update(new TValue(DateTime.UtcNow, 40));

        var resultPosInf = jerk.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultPosInf.Value));

        var resultNegInf = jerk.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultNegInf.Value));
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var jerk = new Jerk();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            jerk.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = jerk.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            jerk.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalResult = jerk.Update(tenthInput, isNew: false);

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
            Jerk.Batch(source.AsSpan(), wrongSizeOutput.AsSpan()));
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
        Jerk.Batch(tValues, batchOutput);
        double expected = batchOutput[^1];

        // 2. Streaming Mode
        var streamingInd = new Jerk();
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 3. TSeries Batch Mode
        var batchSeriesResult = Jerk.Batch(series);
        double tseriesResult = batchSeriesResult.Last.Value;

        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, tseriesResult, precision: 9);
    }

    [Fact]
    public void Calculation_KnownValues()
    {
        // jerk[i] = source[i] - 3*source[i-1] + 3*source[i-2] - source[i-3]
        // Data: 10, 20, 35, 40, 42, 50
        // jerk[0] = 0 (insufficient history)
        // jerk[1] = 0 (insufficient history)
        // jerk[2] = 0 (insufficient history)
        // jerk[3] = 40 - 3*35 + 3*20 - 10 = 40 - 105 + 60 - 10 = -15
        // jerk[4] = 42 - 3*40 + 3*35 - 20 = 42 - 120 + 105 - 20 = 7
        // jerk[5] = 50 - 3*42 + 3*40 - 35 = 50 - 126 + 120 - 35 = 9

        double[] data = [10, 20, 35, 40, 42, 50];
        double[] expected = [0, 0, 0, -15, 7, 9];

        var jerk = new Jerk();
        for (int i = 0; i < data.Length; i++)
        {
            var result = jerk.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var jerk = new Jerk();

        Assert.False(jerk.IsHot);
        jerk.Update(new TValue(DateTime.UtcNow, 10));
        Assert.False(jerk.IsHot);
        jerk.Update(new TValue(DateTime.UtcNow, 20));
        Assert.False(jerk.IsHot);
        jerk.Update(new TValue(DateTime.UtcNow, 30));
        Assert.False(jerk.IsHot);
        jerk.Update(new TValue(DateTime.UtcNow, 40));
        Assert.True(jerk.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var jerk = new Jerk();
        for (int i = 0; i < 10; i++)
        {
            jerk.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.True(jerk.IsHot);

        jerk.Reset();
        Assert.False(jerk.IsHot);
        Assert.Equal(0, jerk.Last.Value);
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
        var jerk = new Jerk();
        var iterativeResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            jerk.Update(new TValue(DateTime.UtcNow, data[i]));
            iterativeResults[i] = jerk.Last.Value;
        }

        // Batch
        var batchResults = new double[count];
        Jerk.Batch(data, batchResults);

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
        var jerk = new Jerk();
        var iterativeResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            jerk.Update(data[i]);
            iterativeResults[i] = jerk.Last.Value;
        }

        // TSeries Batch
        var jerkBatch = new Jerk();
        var batchSeries = jerkBatch.Update(data);

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
        var jerk = new Jerk(source);

        source.Add(new TValue(DateTime.UtcNow, 10));
        source.Add(new TValue(DateTime.UtcNow, 20));
        source.Add(new TValue(DateTime.UtcNow, 35));
        source.Add(new TValue(DateTime.UtcNow, 40));

        Assert.True(jerk.IsHot);
        // jerk = 40 - 3*35 + 3*20 - 10 = 40 - 105 + 60 - 10 = -15
        Assert.Equal(-15, jerk.Last.Value);
    }

    [Fact]
    public void DerivativeChain_MatchesDirectCalculation()
    {
        // Jerk should equal Accel of Slope
        // Also: Jerk[i] = Accel[i] - Accel[i-1]
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 456);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Direct Jerk calculation
        var jerk = new Jerk();
        var jerkResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            jerk.Update(series[i]);
            jerkResults[i] = jerk.Last.Value;
        }

        // Chain: Slope -> Accel (should match Jerk after accounting for warmup)
        var slope = new Slope();
        var accel = new Accel();
        var chainResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            var slopeVal = slope.Update(series[i]);
            var accelOfSlope = accel.Update(slopeVal);
            chainResults[i] = accelOfSlope.Value;
        }

        // Compare from index 3 onwards (when both have sufficient warmup)
        for (int i = 3; i < series.Count; i++)
        {
            Assert.Equal(jerkResults[i], chainResults[i], precision: 9);
        }
    }
}
