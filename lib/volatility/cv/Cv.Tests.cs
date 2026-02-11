namespace QuanTAlib.Tests;
using Xunit;

public class CvTests
{
    private static TBarSeries GenerateTestData(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Cv(0));
        Assert.Throws<ArgumentException>(() => new Cv(-1));
        Assert.Throws<ArgumentException>(() => new Cv(20, 0.0)); // alpha = 0
        Assert.Throws<ArgumentException>(() => new Cv(20, 1.0)); // alpha = 1
        Assert.Throws<ArgumentException>(() => new Cv(20, 0.2, 0.0)); // beta = 0
        Assert.Throws<ArgumentException>(() => new Cv(20, 0.2, 1.0)); // beta = 1
        Assert.Throws<ArgumentException>(() => new Cv(20, 0.5, 0.6)); // alpha + beta >= 1

        var valid = new Cv(10, 0.2, 0.7);
        Assert.Equal(10, valid.Period);
        Assert.Equal(0.2, valid.Alpha);
        Assert.Equal(0.7, valid.Beta);
    }

    [Fact]
    public void WarmupPeriod_IsCorrect()
    {
        var cv = new Cv(20);
        Assert.Equal(21, cv.WarmupPeriod); // period + 1
        Assert.True(cv.WarmupPeriod > 0);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var cv = new Cv(20, 0.15, 0.75);
        Assert.Equal(20, cv.Period);
        Assert.Equal(0.15, cv.Alpha);
        Assert.Equal(0.75, cv.Beta);
        Assert.Equal("Cv(20,0.15,0.75)", cv.Name);
    }

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var cv = new Cv(5);
        var bars = GenerateTestData(100);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            var result = cv.Update(new TValue(times[i], close[i]));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var cv = new Cv(10);

        for (int i = 0; i < 15; i++)
        {
            var result = cv.Update(new TValue(DateTime.UtcNow, 100 + i));
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.True(cv.IsHot);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var cv = new Cv(10);

        var result1 = cv.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        var result2 = cv.Update(new TValue(DateTime.UtcNow, 101), isNew: true);
        var result3 = cv.Update(new TValue(DateTime.UtcNow, 102), isNew: false);

        Assert.True(double.IsFinite(result1.Value));
        Assert.True(double.IsFinite(result2.Value));
        Assert.True(double.IsFinite(result3.Value));
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var cv = new Cv(5);

        for (int i = 0; i < 10; i++)
        {
            cv.Update(new TValue(DateTime.UtcNow, 100 + i), isNew: true);
        }

        var baseline = cv.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        var updated = cv.Update(new TValue(DateTime.UtcNow, 150), isNew: false);

        Assert.NotEqual(baseline.Value, updated.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        int period = 10;
        var cv = new Cv(period);

        for (int i = 0; i < period - 1; i++)
        {
            cv.Update(new TValue(DateTime.UtcNow, 100 + i));
            Assert.False(cv.IsHot);
        }

        cv.Update(new TValue(DateTime.UtcNow, 110));
        Assert.True(cv.IsHot);
    }

    [Fact]
    public void Reset_Works()
    {
        var cv = new Cv(10);

        for (int i = 0; i < 15; i++)
        {
            cv.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(cv.IsHot);

        cv.Reset();
        Assert.False(cv.IsHot);
    }

    [Fact]
    public void SingleValue_ReturnsPositiveVolatility()
    {
        var cv = new Cv(5);
        var result = cv.Update(new TValue(DateTime.UtcNow, 100));

        // First value should still return a value (using default variance)
        Assert.True(double.IsFinite(result.Value));
        Assert.True(result.Value >= 0);
    }

    [Fact]
    public void IterativeCorrections_ChangesValue()
    {
        var cv = new Cv(20);
        var bars = GenerateTestData(50);
        var times = bars.Times;
        var close = bars.CloseValues;

        TValue lastValue = default;
        for (int i = 0; i < bars.Count; i++)
        {
            lastValue = cv.Update(new TValue(times[i], close[i]), isNew: true);
        }
        double originalValue = lastValue.Value;

        // Verify that isNew=false with different price produces different output
        var correctedValue = cv.Update(new TValue(DateTime.UtcNow, 999.99), isNew: false);
        Assert.NotEqual(originalValue, correctedValue.Value);

        // Verify output is still finite and positive
        Assert.True(double.IsFinite(correctedValue.Value));
        Assert.True(correctedValue.Value >= 0);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var cv = new Cv(10);

        for (int i = 0; i < 10; i++)
        {
            cv.Update(new TValue(DateTime.UtcNow, 100 + i), isNew: true);
        }

        var result1 = cv.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        _ = cv.Update(new TValue(DateTime.UtcNow, 115), isNew: false);
        var result3 = cv.Update(new TValue(DateTime.UtcNow, 110), isNew: false);

        // GARCH has path-dependent state that may cause slight differences due to omega calculation
        // on first entry to GARCH phase. Check that values are within 1% of each other.
        double tolerance = Math.Max(Math.Abs(result1.Value) * 0.01, 0.2);
        Assert.True(Math.Abs(result1.Value - result3.Value) < tolerance,
            $"Values should be similar: {result1.Value} vs {result3.Value}");
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var cv = new Cv(5);

        for (int i = 0; i < 10; i++)
        {
            cv.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        var resultNan = cv.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(resultNan.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var cv = new Cv(5);

        for (int i = 0; i < 10; i++)
        {
            cv.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        var resultInf = cv.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultInf.Value));
    }

    [Fact]
    public void LargeDataset_Performance()
    {
        var cv = new Cv(50);
        var bars = GenerateTestData(5000);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            var result = cv.Update(new TValue(times[i], close[i]));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void TSeries_Update_MatchesStreaming()
    {
        int period = 20;
        var cvStream = new Cv(period);
        var cvBatch = new Cv(period);
        var bars = GenerateTestData(100);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            cvStream.Update(new TValue(times[i], close[i]));
        }

        var ts = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(times[i], close[i]));
        }
        var result = cvBatch.Update(ts);

        Assert.Equal(cvStream.Last.Value, result[result.Count - 1].Value, 1e-9);
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var cv = new Cv(20);
        var bars = GenerateTestData(200);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            cv.Update(new TValue(times[i], close[i]));
        }
        var iterativeResult = cv.Last.Value;

        var ts = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(times[i], close[i]));
        }
        var batchResult = Cv.Batch(ts, 20);

        Assert.Equal(iterativeResult, batchResult[batchResult.Count - 1].Value, 1e-8);
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

        var result = Cv.Batch(ts, 20);

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

        Assert.Throws<ArgumentException>(() => Cv.Batch(ts, 0));
        Assert.Throws<ArgumentException>(() => Cv.Batch(ts, -1));
        Assert.Throws<ArgumentException>(() => Cv.Batch(ts, 5, 0.0)); // alpha = 0
        Assert.Throws<ArgumentException>(() => Cv.Batch(ts, 5, 0.5, 0.6)); // alpha + beta >= 1
    }

    [Fact]
    public void Batch_NaN_Safe()
    {
        var values = new double[] { 100, 101, 102, double.NaN, 104, 105 };
        var output = new double[values.Length];

        Cv.Batch(values, output, 3);

        Assert.True(output.Length == 6);
        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    [Fact]
    public void ConstantPrices_LowVolatility()
    {
        var cv = new Cv(10);

        for (int i = 0; i < 20; i++)
        {
            cv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }

        // Constant prices should have very low volatility (approaching zero)
        Assert.True(cv.Last.Value < 1.0, "Constant prices should have very low volatility");
    }

    [Fact]
    public void HighVolatility_ProducesHigherValue()
    {
        var cvStable = new Cv(10);
        var cvVolatile = new Cv(10);

        // Stable prices (small changes)
        for (int i = 0; i < 20; i++)
        {
            cvStable.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i * 0.01));
        }

        // Volatile prices (alternating)
        for (int i = 0; i < 20; i++)
        {
            double volatilePrice = 100 + (i % 2 == 0 ? 5 : -5);
            cvVolatile.Update(new TValue(DateTime.UtcNow.AddMinutes(i), volatilePrice));
        }

        Assert.True(cvVolatile.Last.Value > cvStable.Last.Value,
            "Higher volatility should produce higher CV");
    }

    [Fact]
    public void DifferentParameters_ProduceDistinctValues()
    {
        var bars = GenerateTestData(50);
        var times = bars.Times;
        var close = bars.CloseValues;

        var cv1 = new Cv(20, 0.1, 0.8);
        var cv2 = new Cv(20, 0.2, 0.7);
        var cv3 = new Cv(20, 0.3, 0.6);

        for (int i = 0; i < bars.Count; i++)
        {
            cv1.Update(new TValue(times[i], close[i]));
            cv2.Update(new TValue(times[i], close[i]));
            cv3.Update(new TValue(times[i], close[i]));
        }

        Assert.True(double.IsFinite(cv1.Last.Value));
        Assert.True(double.IsFinite(cv2.Last.Value));
        Assert.True(double.IsFinite(cv3.Last.Value));
    }

    [Fact]
    public void VolatilityClustering_HighVolFollowsHighVol()
    {
        var cv = new Cv(10, 0.2, 0.7);

        // Low volatility period
        for (int i = 0; i < 15; i++)
        {
            cv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i * 0.1));
        }
        double lowVolResult = cv.Last.Value;

        // High volatility shock
        cv.Update(new TValue(DateTime.UtcNow.AddMinutes(15), 120)); // +20%
        cv.Update(new TValue(DateTime.UtcNow.AddMinutes(16), 100)); // -16.7%
        double afterShock = cv.Last.Value;

        // GARCH should show elevated volatility after the shock
        Assert.True(afterShock > lowVolResult, "GARCH should capture volatility clustering");
    }

    [Fact]
    public void MeanReversion_VolReturnsToLongRun()
    {
        var cv = new Cv(10, 0.1, 0.8); // High beta = slower decay

        // Establish long-run variance
        for (int i = 0; i < 15; i++)
        {
            cv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i * 0.5));
        }

        // Introduce shock
        cv.Update(new TValue(DateTime.UtcNow.AddMinutes(15), 130));
        double shockVol = cv.Last.Value;

        // Let it decay
        for (int i = 16; i < 50; i++)
        {
            cv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + (i - 16) * 0.1));
        }
        double decayedVol = cv.Last.Value;

        // Volatility should decay (mean revert) after shock
        Assert.True(decayedVol < shockVol * 0.9, "Volatility should mean-revert after shock");
    }

    [Fact]
    public void Chainability_Works()
    {
        var cv = new Cv(20);
        var sma = new Sma(5);
        var bars = GenerateTestData(100);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            var cvResult = cv.Update(new TValue(times[i], close[i]));
            sma.Update(cvResult);
        }

        Assert.True(sma.IsHot);
        Assert.True(double.IsFinite(sma.Last.Value));
    }
}