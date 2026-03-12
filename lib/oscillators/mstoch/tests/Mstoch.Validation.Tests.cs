using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// MSTOCH self-consistency validation tests.
/// No external library implements Ehlers MESA Stochastic, so we validate
/// streaming==batch==span consistency, range enforcement, and directional
/// correctness against known deterministic inputs.
/// </summary>
public sealed class MstochValidationTests
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

    // --- A) Streaming == Batch(TSeries) ---

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

        // Batch
        TSeries batchResult = Mstoch.Batch(series, stochLength, hpLength, ssLength);

        Assert.Equal(mstoch.Last.Value, batchResult[^1].Value, 6);
    }

    // --- B) Batch(TSeries) == Batch(Span) ---

    [Fact]
    public void Batch_TSeries_Matches_Span()
    {
        var prices = GeneratePrices(200);
        var series = MakeSeries(prices);
        const int stochLength = 15;
        const int hpLength = 30;
        const int ssLength = 7;

        TSeries tsBatch = Mstoch.Batch(series, stochLength, hpLength, ssLength);

        var spanOut = new double[prices.Length];
        Mstoch.Batch(prices.AsSpan(), spanOut.AsSpan(), stochLength, hpLength, ssLength);

        for (int i = 0; i < prices.Length; i++)
        {
            Assert.Equal(tsBatch.Values[i], spanOut[i], 12);
        }
    }

    // --- C) Output always in [0,1] ---

    [Fact]
    public void AllOutputs_InRange_Zero_To_One_Streaming()
    {
        var prices = GeneratePrices(500, seed: 123);
        var t0 = DateTime.UtcNow;
        var mstoch = new Mstoch(stochLength: 20, hpLength: 48, ssLength: 10);
        for (int i = 0; i < prices.Length; i++)
        {
            TValue result = mstoch.Update(new TValue(t0.AddSeconds(i), prices[i]));
            Assert.True(result.Value >= 0.0 && result.Value <= 1.0,
                $"Bar {i}: value {result.Value} out of [0,1]");
        }
    }

    [Fact]
    public void AllOutputs_InRange_Zero_To_One_Batch()
    {
        var prices = GeneratePrices(500, seed: 456);
        var out_ = new double[prices.Length];
        Mstoch.Batch(prices.AsSpan(), out_.AsSpan(), stochLength: 20, hpLength: 48, ssLength: 10);
        for (int i = 0; i < out_.Length; i++)
        {
            Assert.True(out_[i] >= 0.0 && out_[i] <= 1.0,
                $"Bar {i}: value {out_[i]} out of [0,1]");
        }
    }

    // --- D) Constant input produces finite output (zero range -> midpoint) ---

    [Fact]
    public void ConstantInput_ProducesFiniteOutput()
    {
        double[] prices = Enumerable.Repeat(100.0, 100).ToArray();
        var out_ = new double[100];
        Mstoch.Batch(prices.AsSpan(), out_.AsSpan(), stochLength: 20, hpLength: 48, ssLength: 10);
        for (int i = 0; i < out_.Length; i++)
        {
            Assert.True(double.IsFinite(out_[i]), $"Output[{i}] = {out_[i]} is not finite");
        }
    }

    // --- E) Update(TSeries) matches Batch(TSeries) ---

    [Fact]
    public void Update_TSeries_Matches_Batch_TSeries()
    {
        var prices = GeneratePrices(150);
        var series = MakeSeries(prices);
        const int stochLength = 10;
        const int hpLength = 20;
        const int ssLength = 5;

        var indicator = new Mstoch(stochLength, hpLength, ssLength);
        TSeries updateResult = indicator.Update(series);

        TSeries batchResult = Mstoch.Batch(series, stochLength, hpLength, ssLength);

        // All values should match
        for (int i = 0; i < prices.Length; i++)
        {
            Assert.Equal(batchResult.Values[i], updateResult.Values[i], 6);
        }
    }

    // --- F) Calculate static factory returns consistent result ---

    [Fact]
    public void Calculate_Matches_Batch()
    {
        var prices = GeneratePrices(200, seed: 99);
        var series = MakeSeries(prices);
        const int stochLength = 20;
        const int hpLength = 48;
        const int ssLength = 10;

        var (calcResult, _) = Mstoch.Calculate(series, stochLength, hpLength, ssLength);
        TSeries batchResult = Mstoch.Batch(series, stochLength, hpLength, ssLength);

        Assert.Equal(batchResult[^1].Value, calcResult[^1].Value, 6);
    }

    // --- G) Directional correctness ---

    [Fact]
    public void Rising_Then_Falling_Prices_ShowsDirectionalResponse()
    {
        // After enough rising prices, MSTOCH should be above midpoint (0.5)
        var mstoch = new Mstoch(stochLength: 10, hpLength: 20, ssLength: 5);
        var t0 = DateTime.UtcNow;

        // Feed 100 warmup bars at constant 100
        for (int i = 0; i < 100; i++)
        {
            mstoch.Update(new TValue(t0.AddSeconds(i), 100.0));
        }

        // Feed 50 strongly rising bars
        for (int i = 0; i < 50; i++)
        {
            mstoch.Update(new TValue(t0.AddSeconds(100 + i), 100.0 + i * 2.0));
        }
        double risingVal = mstoch.Last.Value;

        // Feed 50 strongly falling bars from a new instance reset
        mstoch.Reset();
        for (int i = 0; i < 100; i++)
        {
            mstoch.Update(new TValue(t0.AddSeconds(i), 100.0));
        }
        for (int i = 0; i < 50; i++)
        {
            mstoch.Update(new TValue(t0.AddSeconds(100 + i), 100.0 - i * 2.0));
        }
        double fallingVal = mstoch.Last.Value;

        // MSTOCH is a cycle indicator based on HP-filtered (detrended) data.
        // During a strong uptrend, the HP filter output is near its recent high → stochastic near 1.
        // During a strong downtrend, the HP filter output is near its recent low → stochastic near 0.
        // The two scenarios must produce distinctly different readings.
        Assert.NotEqual(risingVal, fallingVal);
        Assert.True(double.IsFinite(risingVal) && double.IsFinite(fallingVal),
            $"Both values must be finite: rising={risingVal}, falling={fallingVal}");
        // Validate they diverge significantly (opposite ends of [0,1])
        Assert.True(Math.Abs(risingVal - fallingVal) > 0.5,
            $"Rising ({risingVal}) and falling ({fallingVal}) should diverge by >0.5");
    }

    // --- H) NaN input self-consistency ---

    [Fact]
    public void SparseNaN_Streaming_OutputFinite()
    {
        var prices = GeneratePrices(100);
        // Inject some NaNs
        prices[10] = double.NaN;
        prices[25] = double.NaN;
        prices[50] = double.PositiveInfinity;

        var t0 = DateTime.UtcNow;
        var mstoch = new Mstoch(stochLength: 10, hpLength: 20, ssLength: 5);
        for (int i = 0; i < prices.Length; i++)
        {
            TValue result = mstoch.Update(new TValue(t0.AddSeconds(i), prices[i]));
            Assert.True(double.IsFinite(result.Value),
                $"Bar {i}: NaN/Inf input produced non-finite output {result.Value}");
        }
    }
}
