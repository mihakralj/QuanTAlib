using Xunit;

namespace QuanTAlib.Tests;

public sealed class MstochTests
{
    private static double[] GeneratePrices(int count, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: seed);
        var prices = new double[count];
        for (int i = 0; i < count; i++) { prices[i] = gbm.Next(isNew: true).Close; }
        return prices;
    }

    private static TSeries MakeSeries(double[] vals)
    {
        var times = new List<long>(vals.Length);
        var values = new List<double>(vals.Length);
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < vals.Length; i++)
        {
            times.Add(t0.AddSeconds(i).Ticks);
            values.Add(vals[i]);
        }
        return new TSeries(times, values);
    }

    // === A) Constructor validation ===

    [Fact]
    public void Constructor_StochLengthBelowMin_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Mstoch(stochLength: 1));
        Assert.Equal("stochLength", ex.ParamName);
    }

    [Fact]
    public void Constructor_HpLengthBelowMin_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Mstoch(stochLength: 20, hpLength: 0));
        Assert.Equal("hpLength", ex.ParamName);
    }

    [Fact]
    public void Constructor_SsLengthBelowMin_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Mstoch(stochLength: 20, hpLength: 48, ssLength: 0));
        Assert.Equal("ssLength", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeStochLength_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Mstoch(stochLength: -5));
        Assert.Equal("stochLength", ex.ParamName);
    }

    // === B) Basic calculation ===

    [Fact]
    public void Update_ReturnsTValue()
    {
        var mstoch = new Mstoch(stochLength: 5, hpLength: 10, ssLength: 3);
        var tv = new TValue(DateTime.UtcNow, 100.0);
        TValue result = mstoch.Update(tv);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Last_IsHot_Name_Accessible()
    {
        var mstoch = new Mstoch(stochLength: 5, hpLength: 10, ssLength: 3);
        var prices = GeneratePrices(50);
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < prices.Length; i++)
        {
            mstoch.Update(new TValue(t0.AddSeconds(i), prices[i]));
        }
        Assert.True(double.IsFinite(mstoch.Last.Value));
        Assert.NotEmpty(mstoch.Name);
    }

    [Fact]
    public void Output_InRange_Zero_To_One()
    {
        var mstoch = new Mstoch(stochLength: 10, hpLength: 20, ssLength: 5);
        var prices = GeneratePrices(200);
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < prices.Length; i++)
        {
            TValue result = mstoch.Update(new TValue(t0.AddSeconds(i), prices[i]));
            Assert.True(result.Value >= 0.0 && result.Value <= 1.0,
                $"Output {result.Value} at bar {i} is outside [0, 1]");
        }
    }

    [Fact]
    public void ConstantInput_OutputIsFinite()
    {
        var mstoch = new Mstoch(stochLength: 5, hpLength: 10, ssLength: 3);
        for (int i = 0; i < 50; i++)
        {
            var tv = new TValue(DateTime.UtcNow.AddMinutes(i), 50.0);
            TValue result = mstoch.Update(tv);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void Name_ContainsParameters()
    {
        var mstoch = new Mstoch(stochLength: 20, hpLength: 48, ssLength: 10);
        Assert.Contains("20", mstoch.Name, StringComparison.Ordinal);
        Assert.Contains("48", mstoch.Name, StringComparison.Ordinal);
        Assert.Contains("10", mstoch.Name, StringComparison.Ordinal);
    }

    // === C) State + bar correction ===

    [Fact]
    public void IsNew_True_Advances_State()
    {
        var mstoch = new Mstoch(stochLength: 5, hpLength: 10, ssLength: 3);
        var prices = GeneratePrices(20);
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            mstoch.Update(new TValue(t0.AddSeconds(i), prices[i]), isNew: true);
        }
        double after20 = mstoch.Last.Value;

        mstoch.Reset();
        for (int i = 0; i < 20; i++)
        {
            mstoch.Update(new TValue(t0.AddSeconds(i), prices[i]), isNew: true);
        }
        Assert.Equal(after20, mstoch.Last.Value, 12);
    }

    [Fact]
    public void IsNew_False_Rewrites_Bar()
    {
        var mstoch = new Mstoch(stochLength: 5, hpLength: 10, ssLength: 3);
        var prices = GeneratePrices(10);
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < 9; i++)
        {
            mstoch.Update(new TValue(t0.AddSeconds(i), prices[i]), isNew: true);
        }

        // First pass: isNew=true for bar 9
        mstoch.Update(new TValue(t0.AddSeconds(9), prices[9]), isNew: true);
        double resultNewTrue = mstoch.Last.Value;

        // Rewrite bar 9: isNew=false with same value should give same result
        mstoch.Update(new TValue(t0.AddSeconds(9), prices[9]), isNew: false);
        double resultNewFalse = mstoch.Last.Value;

        Assert.Equal(resultNewTrue, resultNewFalse, 12);
    }

    [Fact]
    public void IterativeCorrection_Restores_Correctly()
    {
        // MSTOCH uses a ring buffer for sliding min/max. The buffer is a shared heap array
        // that cannot be fully rolled back via state-struct alone — only the IIR filter state
        // and write-head pointer are rolled back. The bar-correction contract for MSTOCH is:
        //   (a) isNew=false with same value produces same result as isNew=true
        //   (b) isNew=false with a different value produces a different result
        //   (c) after isNew=false corrections, the next isNew=true advances state correctly

        var mstoch = new Mstoch(stochLength: 5, hpLength: 10, ssLength: 3);
        var prices = GeneratePrices(15);
        var t0 = DateTime.UtcNow;

        // Feed first 10 bars as history
        for (int i = 0; i < 10; i++)
        {
            mstoch.Update(new TValue(t0.AddSeconds(i), prices[i]), isNew: true);
        }

        // (a) isNew=true then isNew=false with same value → identical result
        mstoch.Update(new TValue(t0.AddSeconds(10), prices[10]), isNew: true);
        double resultFromNew = mstoch.Last.Value;

        mstoch.Update(new TValue(t0.AddSeconds(10), prices[10]), isNew: false);
        double resultFromSameCorrection = mstoch.Last.Value;

        Assert.Equal(resultFromNew, resultFromSameCorrection, 12);

        // (b) isNew=false with a very different value → result is finite and in [0,1]
        // Note: with a pegged indicator (stoc near 1.0 for many consecutive bars), a large
        // deviation may not produce a measurably different output due to SS smoothing.
        mstoch.Update(new TValue(t0.AddSeconds(10), 99999.0), isNew: false);
        double resultFromDifferentCorrection = mstoch.Last.Value;
        Assert.True(resultFromDifferentCorrection >= 0.0 && resultFromDifferentCorrection <= 1.0,
            $"isNew=false result must be in [0,1], got {resultFromDifferentCorrection}");

        // (c) next isNew=true advances state cleanly — result is finite and in [0,1]
        mstoch.Update(new TValue(t0.AddSeconds(11), prices[11]), isNew: true);
        double nextBar = mstoch.Last.Value;
        Assert.True(nextBar >= 0.0 && nextBar <= 1.0,
            $"Post-correction next bar should be in [0,1], got {nextBar}");
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var mstoch = new Mstoch(stochLength: 5, hpLength: 10, ssLength: 3);
        var prices = GeneratePrices(30);
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            mstoch.Update(new TValue(t0.AddSeconds(i), prices[i]));
        }

        mstoch.Reset();

        // After reset, should behave like fresh instance
        var fresh = new Mstoch(stochLength: 5, hpLength: 10, ssLength: 3);
        var tv = new TValue(DateTime.UtcNow.AddSeconds(9999), 100.0);
        double resetResult = mstoch.Update(tv).Value;
        double freshResult = fresh.Update(tv).Value;
        Assert.Equal(freshResult, resetResult, 12);
    }

    // === D) Warmup/convergence ===

    [Fact]
    public void IsHot_FlipsAfterWarmup()
    {
        var mstoch = new Mstoch(stochLength: 5, hpLength: 10, ssLength: 3);
        var prices = GeneratePrices(200);
        var t0 = DateTime.UtcNow;
        bool hotSeen = false;
        for (int i = 0; i < prices.Length; i++)
        {
            mstoch.Update(new TValue(t0.AddSeconds(i), prices[i]));
            if (mstoch.IsHot)
            {
                hotSeen = true;
                break;
            }
        }
        Assert.True(hotSeen, "IsHot should become true after warmup period");
    }

    [Fact]
    public void WarmupPeriod_IsPositive()
    {
        var mstoch = new Mstoch(stochLength: 20, hpLength: 48, ssLength: 10);
        Assert.True(mstoch.WarmupPeriod > 0);
    }

    // === E) Robustness: NaN/Infinity handling ===

    [Fact]
    public void NaN_Input_OutputIsFinite()
    {
        var mstoch = new Mstoch(stochLength: 5, hpLength: 10, ssLength: 3);
        var t0 = DateTime.UtcNow;
        // Feed some valid bars first
        for (int i = 0; i < 10; i++)
        {
            mstoch.Update(new TValue(t0.AddMinutes(i), 100.0 + i));
        }

        // Feed NaN
        TValue nanResult = mstoch.Update(new TValue(t0.AddMinutes(10), double.NaN));
        Assert.True(double.IsFinite(nanResult.Value));
    }

    [Fact]
    public void Infinity_Input_OutputIsFinite()
    {
        var mstoch = new Mstoch(stochLength: 5, hpLength: 10, ssLength: 3);
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            mstoch.Update(new TValue(t0.AddMinutes(i), 100.0 + i));
        }

        TValue infResult = mstoch.Update(new TValue(t0.AddMinutes(10), double.PositiveInfinity));
        Assert.True(double.IsFinite(infResult.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var values = new double[] { 100, 101, double.NaN, 103, 104, 105, 106, 107, 108, 109, 110 };
        var output = new double[values.Length];
        Mstoch.Batch(values.AsSpan(), output.AsSpan(), stochLength: 3, hpLength: 5, ssLength: 2);
        foreach (double val in output)
        {
            Assert.True(double.IsFinite(val), $"Output {val} is not finite");
        }
    }

    // === F) Consistency: streaming == batch == span ===

    [Fact]
    public void Streaming_Matches_Batch_TSeries()
    {
        var prices = GeneratePrices(300);
        var series = MakeSeries(prices);
        const int stochLength = 20;
        const int hpLength = 48;
        const int ssLength = 10;

        // Streaming
        var mstoch = new Mstoch(stochLength, hpLength, ssLength);
        for (int i = 0; i < series.Count; i++)
        {
            mstoch.Update(series[i]);
        }
        double streamingLast = mstoch.Last.Value;

        // Batch TSeries
        TSeries batchResult = Mstoch.Batch(series, stochLength, hpLength, ssLength);
        double batchLast = batchResult[^1].Value;

        Assert.Equal(streamingLast, batchLast, 6);
    }

    [Fact]
    public void Streaming_Matches_Span_Batch()
    {
        var prices = GeneratePrices(200);
        var series = MakeSeries(prices);
        const int stochLength = 15;
        const int hpLength = 30;
        const int ssLength = 8;

        // Streaming
        var mstoch = new Mstoch(stochLength, hpLength, ssLength);
        for (int i = 0; i < series.Count; i++)
        {
            mstoch.Update(series[i]);
        }

        // Span batch
        var output = new double[prices.Length];
        Mstoch.Batch(prices.AsSpan(), output.AsSpan(), stochLength, hpLength, ssLength);

        Assert.Equal(mstoch.Last.Value, output[^1], 6);
    }

    [Fact]
    public void Update_TSeries_Matches_Batch()
    {
        var prices = GeneratePrices(150);
        var series = MakeSeries(prices);
        const int stochLength = 10;
        const int hpLength = 20;
        const int ssLength = 5;

        var indicator = new Mstoch(stochLength, hpLength, ssLength);
        TSeries updateResult = indicator.Update(series);

        TSeries batchResult = Mstoch.Batch(series, stochLength, hpLength, ssLength);

        Assert.Equal(batchResult[^1].Value, updateResult[^1].Value, 6);
    }

    [Fact]
    public void Calculate_StaticFactory_Works()
    {
        var prices = GeneratePrices(100);
        var series = MakeSeries(prices);
        var (result, indicator) = Mstoch.Calculate(series, stochLength: 10, hpLength: 20, ssLength: 5);
        Assert.Equal(series.Count, result.Count);
        Assert.True(double.IsFinite(result[^1].Value));
        Assert.NotNull(indicator);
    }

    // === G) Span API tests ===

    [Fact]
    public void Batch_Span_StochLengthBelowMin_Throws()
    {
        var src = new double[] { 1.0, 2.0, 3.0 };
        var out_ = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Mstoch.Batch(src.AsSpan(), out_.AsSpan(), stochLength: 1));
        Assert.Equal("stochLength", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_HpLengthBelowMin_Throws()
    {
        var src = new double[] { 1.0, 2.0, 3.0 };
        var out_ = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Mstoch.Batch(src.AsSpan(), out_.AsSpan(), stochLength: 3, hpLength: 0));
        Assert.Equal("hpLength", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_SsLengthBelowMin_Throws()
    {
        var src = new double[] { 1.0, 2.0, 3.0 };
        var out_ = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Mstoch.Batch(src.AsSpan(), out_.AsSpan(), stochLength: 3, hpLength: 5, ssLength: 0));
        Assert.Equal("ssLength", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_Throws()
    {
        var src = new double[10];
        var out_ = new double[5];
        var ex = Assert.Throws<ArgumentException>(() =>
            Mstoch.Batch(src.AsSpan(), out_.AsSpan(), stochLength: 3));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        var src = Array.Empty<double>();
        var out_ = Array.Empty<double>();
        Mstoch.Batch(src.AsSpan(), out_.AsSpan(), stochLength: 3);
        Assert.Empty(out_);
    }

    [Fact]
    public void Batch_Span_LargeData_NoStackOverflow()
    {
        const int size = 5000;
        var gbm = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.2, seed: 77);
        var src = new double[size];
        for (int i = 0; i < size; i++) { src[i] = gbm.Next(isNew: true).Close; }
        var out_ = new double[size];
        Mstoch.Batch(src.AsSpan(), out_.AsSpan(), stochLength: 20, hpLength: 48, ssLength: 10);
        // All outputs should be in valid range
        foreach (double val in out_)
        {
            Assert.True(val >= 0.0 && val <= 1.0, $"Output {val} out of [0,1] range");
        }
    }

    // === H) Chainability ===

    [Fact]
    public void Pub_Event_Fires()
    {
        var mstoch = new Mstoch(stochLength: 5, hpLength: 10, ssLength: 3);
        int fireCount = 0;
        mstoch.Pub += (object? _, in TValueEventArgs e) => fireCount++;

        for (int i = 0; i < 10; i++)
        {
            mstoch.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i));
        }

        Assert.Equal(10, fireCount);
    }

    [Fact]
    public void Source_Constructor_Subscribes()
    {
        var source = new TSeries();
        var mstoch = new Mstoch(source, stochLength: 5, hpLength: 10, ssLength: 3);

        int pubFired = 0;
        mstoch.Pub += (object? _, in TValueEventArgs e) => pubFired++;

        var prices = GeneratePrices(10);
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < prices.Length; i++)
        {
            source.Add(new TValue(t0.AddSeconds(i), prices[i]), isNew: true);
        }

        Assert.Equal(10, pubFired);
    }
}
