namespace QuanTAlib.Tests;
using Xunit;

public class BbwTests
{
    private const double Tolerance = 1e-10;

    private static TBarSeries GenerateTestData(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Bbw(0));
        Assert.Throws<ArgumentException>(() => new Bbw(-1));
        Assert.Throws<ArgumentException>(() => new Bbw(20, 0));
        Assert.Throws<ArgumentException>(() => new Bbw(20, -1));

        var valid = new Bbw(10, 1.5);
        Assert.Equal(10, valid.Period);
        Assert.Equal(1.5, valid.Multiplier);
    }

    [Fact]
    public void WarmupPeriod_IsPositive()
    {
        var bbw = new Bbw(20, 2.0);
        Assert.Equal(20, bbw.WarmupPeriod);
        Assert.True(bbw.WarmupPeriod > 0);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var bbw = new Bbw(20, 2.5);
        Assert.Equal(20, bbw.Period);
        Assert.Equal(2.5, bbw.Multiplier);
        Assert.Equal("Bbw(20,2.5)", bbw.Name);
    }

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var bbw = new Bbw(5);
        var bars = GenerateTestData(100);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            var result = bbw.Update(new TValue(times[i], close[i]));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var bbw = new Bbw(10);

        for (int i = 0; i < 15; i++)
        {
            var result = bbw.Update(new TValue(DateTime.UtcNow, 100 + i));
            Assert.True(double.IsFinite(result.Value) || i < 1);
        }

        Assert.True(bbw.IsHot);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var bbw = new Bbw(10);

        var result1 = bbw.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        var result2 = bbw.Update(new TValue(DateTime.UtcNow, 101), isNew: true);
        var result3 = bbw.Update(new TValue(DateTime.UtcNow, 102), isNew: false);

        Assert.True(double.IsFinite(result1.Value));
        Assert.True(double.IsFinite(result2.Value));
        Assert.True(double.IsFinite(result3.Value));
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var bbw = new Bbw(5);

        for (int i = 0; i < 5; i++)
        {
            bbw.Update(new TValue(DateTime.UtcNow, 100 + i), isNew: true);
        }

        var baseline = bbw.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        var updated = bbw.Update(new TValue(DateTime.UtcNow, 150), isNew: false);

        Assert.NotEqual(baseline.Value, updated.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        int period = 10;
        var bbw = new Bbw(period);

        for (int i = 0; i < period - 1; i++)
        {
            bbw.Update(new TValue(DateTime.UtcNow, 100 + i));
            Assert.False(bbw.IsHot);
        }

        bbw.Update(new TValue(DateTime.UtcNow, 110));
        Assert.True(bbw.IsHot);
    }

    [Fact]
    public void Reset_Works()
    {
        var bbw = new Bbw(10);

        for (int i = 0; i < 15; i++)
        {
            bbw.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(bbw.IsHot);

        bbw.Reset();
        Assert.False(bbw.IsHot);
    }

    [Fact]
    public void SingleValue_ReturnsZero()
    {
        var bbw = new Bbw(5);
        var result = bbw.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Period1_Works()
    {
        var bbw = new Bbw(1, 2.0);
        var result = bbw.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(bbw.IsHot);
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var bbw = new Bbw(20);
        var bars = GenerateTestData(50);
        var times = bars.Times;
        var close = bars.CloseValues;

        TValue lastValue = default;
        for (int i = 0; i < bars.Count; i++)
        {
            lastValue = bbw.Update(new TValue(times[i], close[i]), isNew: true);
        }
        double originalValue = lastValue.Value;

        var correctedValue = bbw.Update(new TValue(DateTime.UtcNow, 999.99), isNew: false);
        Assert.NotEqual(originalValue, correctedValue.Value);

        var restoredValue = bbw.Update(new TValue(lastValue.Time, close[bars.Count - 1]), isNew: false);
        Assert.Equal(originalValue, restoredValue.Value, 1e-9);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var bbw = new Bbw(10);

        for (int i = 0; i < 10; i++)
        {
            bbw.Update(new TValue(DateTime.UtcNow, 100 + i), isNew: true);
        }

        var result1 = bbw.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        _ = bbw.Update(new TValue(DateTime.UtcNow, 115), isNew: false);
        var result3 = bbw.Update(new TValue(DateTime.UtcNow, 110), isNew: false);

        Assert.Equal(result1.Value, result3.Value, Tolerance);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var bbw = new Bbw(5);

        for (int i = 0; i < 5; i++)
        {
            bbw.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        var resultNan = bbw.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(resultNan.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var bbw = new Bbw(5);

        for (int i = 0; i < 5; i++)
        {
            bbw.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        var resultInf = bbw.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultInf.Value));
    }

    [Fact]
    public void LargeDataset_Performance()
    {
        var bbw = new Bbw(50);
        var bars = GenerateTestData(5000);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            var result = bbw.Update(new TValue(times[i], close[i]));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void TSeries_Update_MatchesStreaming()
    {
        int period = 20;
        var bbwStream = new Bbw(period);
        var bbwBatch = new Bbw(period);
        var bars = GenerateTestData(100);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            bbwStream.Update(new TValue(times[i], close[i]));
        }

        var ts = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(times[i], close[i]));
        }
        var result = bbwBatch.Update(ts);

        Assert.Equal(bbwStream.Last.Value, result[result.Count - 1].Value, 1e-9);
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var bbw = new Bbw(20);
        var bars = GenerateTestData(200);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            bbw.Update(new TValue(times[i], close[i]));
        }
        var iterativeResult = bbw.Last.Value;

        var ts = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(times[i], close[i]));
        }
        var batchResult = Bbw.Batch(ts, 20);

        Assert.Equal(iterativeResult, batchResult[batchResult.Count - 1].Value, 1e-8);
    }

    [Fact]
    public void Chainability_Works()
    {
        var bbw = new Bbw(20);
        var sma = new Sma(5);
        var bars = GenerateTestData(100);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            var bbwResult = bbw.Update(new TValue(times[i], close[i]));
            sma.Update(bbwResult);
        }

        var smaBatch = new Sma(5);
        var ts = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(times[i], close[i]));
        }
        var bbwBatch = Bbw.Batch(ts, 20);
        var smaResult = smaBatch.Update(bbwBatch);

        Assert.Equal(sma.Last.Value, smaResult[smaResult.Count - 1].Value, 1e-8);
    }

    [Fact]
    public void StaticBatch_Works()
    {
        var bars = GenerateTestData(100);
        var times = bars.Times;
        var close = bars.CloseValues;

        var ts = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(times[i], close[i]));
        }

        var result = Bbw.Batch(ts, 20, 2.0);

        Assert.Equal(100, result.Count);
        Assert.True(double.IsFinite(result[result.Count - 1].Value));
    }

    [Fact]
    public void StaticBatch_ValidatesInput()
    {
        var ts = new TSeries();
        for (int i = 0; i < 10; i++)
        {
            ts.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        Assert.Throws<ArgumentException>(() => Bbw.Batch(ts, 0));
        Assert.Throws<ArgumentException>(() => Bbw.Batch(ts, -1));
        Assert.Throws<ArgumentException>(() => Bbw.Batch(ts, 5, 0));
        Assert.Throws<ArgumentException>(() => Bbw.Batch(ts, 5, -1));
    }

    [Fact]
    public void Batch_NaN_Safe()
    {
        var values = new double[] { 100, 101, 102, double.NaN, 104, 105 };
        var output = new double[values.Length];

        Bbw.Batch(values, output, 3);

        Assert.True(output.Length == 6);
    }

    [Fact]
    public void BBW_Formula_Verified()
    {
        var bbw = new Bbw(5, 2.0);

        double[] values = { 100, 102, 98, 101, 99 };
        foreach (var v in values)
        {
            bbw.Update(new TValue(DateTime.UtcNow, v));
        }

        double mean = values.Average();
        double variance = values.Select(v => (v - mean) * (v - mean)).Average();
        double stddev = Math.Sqrt(variance);
        double expectedBbw = (2.0 * 2.0 * stddev) / mean;

        Assert.Equal(expectedBbw, bbw.Last.Value, 1e-10);
    }

    [Fact]
    public void BBW_IncreasingVolatility_IncreasesWidth()
    {
        var bbw = new Bbw(10);

        for (int i = 0; i < 10; i++)
        {
            bbw.Update(new TValue(DateTime.UtcNow, 100 + (i * 0.1)));
        }
        double lowVolatilityBbw = bbw.Last.Value;

        bbw.Reset();

        for (int i = 0; i < 10; i++)
        {
            bbw.Update(new TValue(DateTime.UtcNow, 100 + (i * 10)));
        }
        double highVolatilityBbw = bbw.Last.Value;

        Assert.True(highVolatilityBbw > lowVolatilityBbw);
    }

    [Fact]
    public void BBW_MultiplierEffect_Verified()
    {
        var bbw1 = new Bbw(10, 1.0);
        var bbw2 = new Bbw(10, 2.0);
        var bbw3 = new Bbw(10, 3.0);
        var bars = GenerateTestData(20);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            bbw1.Update(new TValue(times[i], close[i]));
            bbw2.Update(new TValue(times[i], close[i]));
            bbw3.Update(new TValue(times[i], close[i]));
        }

        Assert.Equal(bbw1.Last.Value * 2.0, bbw2.Last.Value, 1e-10);
        Assert.Equal(bbw1.Last.Value * 3.0, bbw3.Last.Value, 1e-10);
    }

    [Fact]
    public void AlternatingValues_ProducesExpectedWidth()
    {
        var bbw = new Bbw(2, 2.0);

        bbw.Update(new TValue(DateTime.UtcNow, 100));
        bbw.Update(new TValue(DateTime.UtcNow, 110));

        double expectedBbw = (2.0 * 2.0 * 5.0) / 105.0;
        Assert.Equal(expectedBbw, bbw.Last.Value, 1e-10);
    }
}
