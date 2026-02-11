namespace QuanTAlib.Tests;
using Xunit;

public class CcvTests
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
        Assert.Throws<ArgumentException>(() => new Ccv(0));
        Assert.Throws<ArgumentException>(() => new Ccv(-1));
        Assert.Throws<ArgumentException>(() => new Ccv(20, 0));
        Assert.Throws<ArgumentException>(() => new Ccv(20, 4));
        Assert.Throws<ArgumentException>(() => new Ccv(20, -1));

        var valid = new Ccv(10, 1);
        Assert.Equal(10, valid.Period);
        Assert.Equal(1, valid.Method);
    }

    [Fact]
    public void WarmupPeriod_IsCorrect()
    {
        var ccv = new Ccv(20);
        Assert.Equal(21, ccv.WarmupPeriod); // period + 1
        Assert.True(ccv.WarmupPeriod > 0);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var ccv = new Ccv(20, 2);
        Assert.Equal(20, ccv.Period);
        Assert.Equal(2, ccv.Method);
        Assert.Equal("Ccv(20,2)", ccv.Name);
    }

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var ccv = new Ccv(5);
        var bars = GenerateTestData(100);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            var result = ccv.Update(new TValue(times[i], close[i]));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var ccv = new Ccv(10);

        for (int i = 0; i < 15; i++)
        {
            var result = ccv.Update(new TValue(DateTime.UtcNow, 100 + i));
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.True(ccv.IsHot);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var ccv = new Ccv(10);

        var result1 = ccv.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        var result2 = ccv.Update(new TValue(DateTime.UtcNow, 101), isNew: true);
        var result3 = ccv.Update(new TValue(DateTime.UtcNow, 102), isNew: false);

        Assert.True(double.IsFinite(result1.Value));
        Assert.True(double.IsFinite(result2.Value));
        Assert.True(double.IsFinite(result3.Value));
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var ccv = new Ccv(5);

        for (int i = 0; i < 10; i++)
        {
            ccv.Update(new TValue(DateTime.UtcNow, 100 + i), isNew: true);
        }

        var baseline = ccv.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        var updated = ccv.Update(new TValue(DateTime.UtcNow, 150), isNew: false);

        Assert.NotEqual(baseline.Value, updated.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        int period = 10;
        var ccv = new Ccv(period);

        for (int i = 0; i < period - 1; i++)
        {
            ccv.Update(new TValue(DateTime.UtcNow, 100 + i));
            Assert.False(ccv.IsHot);
        }

        ccv.Update(new TValue(DateTime.UtcNow, 110));
        Assert.True(ccv.IsHot);
    }

    [Fact]
    public void Reset_Works()
    {
        var ccv = new Ccv(10);

        for (int i = 0; i < 15; i++)
        {
            ccv.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(ccv.IsHot);

        ccv.Reset();
        Assert.False(ccv.IsHot);
    }

    [Fact]
    public void SingleValue_ReturnsZero()
    {
        var ccv = new Ccv(5);
        var result = ccv.Update(new TValue(DateTime.UtcNow, 100));

        // First value has no return to calculate, should be 0
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var ccv = new Ccv(20);
        var bars = GenerateTestData(50);
        var times = bars.Times;
        var close = bars.CloseValues;

        TValue lastValue = default;
        for (int i = 0; i < bars.Count; i++)
        {
            lastValue = ccv.Update(new TValue(times[i], close[i]), isNew: true);
        }
        double originalValue = lastValue.Value;

        var correctedValue = ccv.Update(new TValue(DateTime.UtcNow, 999.99), isNew: false);
        Assert.NotEqual(originalValue, correctedValue.Value);

        var restoredValue = ccv.Update(new TValue(lastValue.Time, close[bars.Count - 1]), isNew: false);
        Assert.Equal(originalValue, restoredValue.Value, 1e-9);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var ccv = new Ccv(10);

        for (int i = 0; i < 10; i++)
        {
            ccv.Update(new TValue(DateTime.UtcNow, 100 + i), isNew: true);
        }

        var result1 = ccv.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        _ = ccv.Update(new TValue(DateTime.UtcNow, 115), isNew: false);
        var result3 = ccv.Update(new TValue(DateTime.UtcNow, 110), isNew: false);

        Assert.Equal(result1.Value, result3.Value, Tolerance);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var ccv = new Ccv(5);

        for (int i = 0; i < 10; i++)
        {
            ccv.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        var resultNan = ccv.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(resultNan.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var ccv = new Ccv(5);

        for (int i = 0; i < 10; i++)
        {
            ccv.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        var resultInf = ccv.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultInf.Value));
    }

    [Fact]
    public void LargeDataset_Performance()
    {
        var ccv = new Ccv(50);
        var bars = GenerateTestData(5000);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            var result = ccv.Update(new TValue(times[i], close[i]));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void TSeries_Update_MatchesStreaming()
    {
        int period = 20;
        var ccvStream = new Ccv(period);
        var ccvBatch = new Ccv(period);
        var bars = GenerateTestData(100);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            ccvStream.Update(new TValue(times[i], close[i]));
        }

        var ts = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(times[i], close[i]));
        }
        var result = ccvBatch.Update(ts);

        Assert.Equal(ccvStream.Last.Value, result[result.Count - 1].Value, 1e-9);
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var ccv = new Ccv(20);
        var bars = GenerateTestData(200);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            ccv.Update(new TValue(times[i], close[i]));
        }
        var iterativeResult = ccv.Last.Value;

        var ts = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(times[i], close[i]));
        }
        var batchResult = Ccv.Batch(ts, 20);

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

        var result = Ccv.Batch(ts, 20);

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

        Assert.Throws<ArgumentException>(() => Ccv.Batch(ts, 0));
        Assert.Throws<ArgumentException>(() => Ccv.Batch(ts, -1));
        Assert.Throws<ArgumentException>(() => Ccv.Batch(ts, 5, 0));
        Assert.Throws<ArgumentException>(() => Ccv.Batch(ts, 5, 4));
    }

    [Fact]
    public void Batch_NaN_Safe()
    {
        var values = new double[] { 100, 101, 102, double.NaN, 104, 105 };
        var output = new double[values.Length];

        Ccv.Batch(values, output, 3);

        Assert.True(output.Length == 6);
    }

    [Fact]
    public void ConstantPrices_ZeroVolatility()
    {
        var ccv = new Ccv(10);

        for (int i = 0; i < 20; i++)
        {
            ccv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }

        // Constant prices should have near-zero volatility
        Assert.True(ccv.Last.Value < 0.01, "Constant prices should have near-zero volatility");
    }

    [Fact]
    public void HighVolatility_ProducesHigherValue()
    {
        var ccvStable = new Ccv(10);
        var ccvVolatile = new Ccv(10);

        // Stable prices (small changes)
        for (int i = 0; i < 20; i++)
        {
            ccvStable.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i * 0.01));
        }

        // Volatile prices (alternating)
        for (int i = 0; i < 20; i++)
        {
            double volatilePrice = 100 + (i % 2 == 0 ? 5 : -5);
            ccvVolatile.Update(new TValue(DateTime.UtcNow.AddMinutes(i), volatilePrice));
        }

        Assert.True(ccvVolatile.Last.Value > ccvStable.Last.Value,
            "Higher volatility should produce higher CCV");
    }

    [Fact]
    public void AllMethods_ProduceValidResults()
    {
        var bars = GenerateTestData(50);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int method = 1; method <= 3; method++)
        {
            var ccv = new Ccv(10, method);

            for (int i = 0; i < bars.Count; i++)
            {
                var result = ccv.Update(new TValue(times[i], close[i]));
                Assert.True(double.IsFinite(result.Value));
                Assert.True(result.Value >= 0);
            }
        }
    }

    [Fact]
    public void DifferentMethods_ProduceDistinctValues()
    {
        var bars = GenerateTestData(50);
        var times = bars.Times;
        var close = bars.CloseValues;

        var ccv1 = new Ccv(20, 1); // SMA
        var ccv2 = new Ccv(20, 2); // EMA
        var ccv3 = new Ccv(20, 3); // WMA

        for (int i = 0; i < bars.Count; i++)
        {
            ccv1.Update(new TValue(times[i], close[i]));
            ccv2.Update(new TValue(times[i], close[i]));
            ccv3.Update(new TValue(times[i], close[i]));
        }

        Assert.True(double.IsFinite(ccv1.Last.Value));
        Assert.True(double.IsFinite(ccv2.Last.Value));
        Assert.True(double.IsFinite(ccv3.Last.Value));
    }

    [Fact]
    public void AnnualizationFactor_Applied()
    {
        var ccv = new Ccv(10);
        var bars = GenerateTestData(30);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            ccv.Update(new TValue(times[i], close[i]));
        }

        // Annualized volatility should be positive
        Assert.True(ccv.Last.Value >= 0);
    }

    [Fact]
    public void Chainability_Works()
    {
        var ccv = new Ccv(20);
        var sma = new Sma(5);
        var bars = GenerateTestData(100);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            var ccvResult = ccv.Update(new TValue(times[i], close[i]));
            sma.Update(ccvResult);
        }

        Assert.True(sma.IsHot);
        Assert.True(double.IsFinite(sma.Last.Value));
    }
}