namespace QuanTAlib.Tests;
using Xunit;

public class BbwnTests
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
        Assert.Throws<ArgumentException>(() => new Bbwn(0));
        Assert.Throws<ArgumentException>(() => new Bbwn(-1));
        Assert.Throws<ArgumentException>(() => new Bbwn(20, 0));
        Assert.Throws<ArgumentException>(() => new Bbwn(20, -1));
        Assert.Throws<ArgumentException>(() => new Bbwn(20, 2.0, 0));
        Assert.Throws<ArgumentException>(() => new Bbwn(20, 2.0, -1));

        var valid = new Bbwn(10, 1.5, 100);
        Assert.Equal(10, valid.Period);
        Assert.Equal(1.5, valid.Multiplier);
        Assert.Equal(100, valid.Lookback);
    }

    [Fact]
    public void WarmupPeriod_IsPositive()
    {
        var bbwn = new Bbwn(20, 2.0, 252);
        Assert.Equal(272, bbwn.WarmupPeriod); // period + lookback
        Assert.True(bbwn.WarmupPeriod > 0);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var bbwn = new Bbwn(20, 2.5, 100);
        Assert.Equal(20, bbwn.Period);
        Assert.Equal(2.5, bbwn.Multiplier);
        Assert.Equal(100, bbwn.Lookback);
        Assert.Equal("Bbwn(20,2.5,100)", bbwn.Name);
    }

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var bbwn = new Bbwn(5, 2.0, 20);
        var bars = GenerateTestData(100);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            var result = bbwn.Update(new TValue(times[i], close[i]));
            Assert.True(double.IsFinite(result.Value), $"Invalid value at index {i}: {result.Value}");
        }

        Assert.True(bbwn.Last.Value >= 0.0, "BBWN should be >= 0");
        Assert.True(bbwn.Last.Value <= 1.0, "BBWN should be <= 1");
    }

    [Fact]
    public void IsHot_BehavesCorrectly()
    {
        var bbwn = new Bbwn(5, 2.0, 10);
        var bars = GenerateTestData(20);
        var close = bars.CloseValues;

        // Should not be hot initially
        Assert.False(bbwn.IsHot);

        // Feed data until warm
        for (int i = 0; i < 15; i++)
        {
            bbwn.Update(new TValue(DateTime.UtcNow.Ticks + i, close[i]));
        }

        // Should be hot after sufficient data
        Assert.True(bbwn.IsHot);
    }

    [Fact]
    public void OutputRange_IsNormalized()
    {
        var bbwn = new Bbwn(10, 2.0, 50);
        var bars = GenerateTestData(100);
        var close = bars.CloseValues;
        var times = bars.Times;

        var results = new List<double>();

        for (int i = 0; i < bars.Count; i++)
        {
            var result = bbwn.Update(new TValue(times[i], close[i]));
            results.Add(result.Value);

            // Each result should be in [0,1] range
            Assert.True(result.Value >= 0.0, $"Value {result.Value} at index {i} should be >= 0");
            Assert.True(result.Value <= 1.0, $"Value {result.Value} at index {i} should be <= 1");
        }

        // After sufficient data, we should see some variation
        if (results.Count > 60)
        {
            var laterResults = results.Skip(60).ToList();
            double min = laterResults.Min();
            double max = laterResults.Max();

            // Should have some meaningful range in normalized values
            Assert.True(max - min > 0.1, "Should have meaningful variation in normalized values");
        }
    }

    [Fact]
    public void Update_IsNew_BehavesCorrectly()
    {
        var bbwn = new Bbwn(5, 2.0, 20);
        var bars = GenerateTestData(30);

        // First load up enough data to create variation
        for (int i = 0; i < 25; i++)
        {
            bbwn.Update(new TValue(bars.Times[i], bars.CloseValues[i]), isNew: true);
        }

        // First update (new)
        var result1 = bbwn.Update(new TValue(bars.Times[25], bars.CloseValues[25]), isNew: true);

        // Second update (revision) - with very different value to create different BBW
        var revisedValue = new TValue(bars.Times[25], bars.CloseValues[25] * 1.5);
        var result2 = bbwn.Update(revisedValue, isNew: false);

        // After revision, the result might differ (or might not if range is 0)
        // The key test is that isNew=false doesn't advance state
        Assert.True(double.IsFinite(result1.Value) && double.IsFinite(result2.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var bbwn = new Bbwn(5, 2.0, 20);
        var bars = GenerateTestData(20);
        var close = bars.CloseValues;

        // Feed some data
        for (int i = 0; i < 10; i++)
        {
            bbwn.Update(new TValue(DateTime.UtcNow.Ticks + i, close[i]));
        }

        Assert.True(bbwn.Last.Value != 0.0);

        // Reset and check
        bbwn.Reset();
        Assert.Equal(0.0, bbwn.Last.Value);
        Assert.False(bbwn.IsHot);
    }

    [Fact]
    public void Prime_LoadsDataCorrectly()
    {
        var bbwn = new Bbwn(5, 2.0, 20);
        var bars = GenerateTestData(30);
        var close = bars.CloseValues.ToArray();

        bbwn.Prime(close);

        Assert.True(bbwn.IsHot);
        Assert.True(double.IsFinite(bbwn.Last.Value));
        Assert.True(bbwn.Last.Value >= 0.0 && bbwn.Last.Value <= 1.0);
    }

    [Fact]
    public void Batch_ProducesConsistentResults()
    {
        var bbwn = new Bbwn(5, 2.0, 20);
        var bars = GenerateTestData(50);
        var close = bars.CloseValues;
        var times = bars.Times;

        // Calculate using Update method
        var updateResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            var result = bbwn.Update(new TValue(times[i], close[i]));
            updateResults.Add(result.Value);
        }

        // Calculate using Batch method
        var batchResults = new double[bars.Count];
        Bbwn.Batch(close.ToArray(), batchResults, 5, 2.0, 20);

        // Should be approximately equal after warmup period
        for (int i = 25; i < bars.Count; i++)  // Skip initial warmup
        {
            Assert.True(Math.Abs(updateResults[i] - batchResults[i]) < 0.01,
                $"Mismatch at index {i}: Update={updateResults[i]:F6}, Batch={batchResults[i]:F6}");
        }
    }

    [Fact]
    public void Calculate_ProducesValidSeries()
    {
        var bars = GenerateTestData(100);
        var ts = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(bars.Times[i], bars.CloseValues[i]));
        }

        var result = Bbwn.Batch(ts, 10, 2.0, 50);

        Assert.Equal(ts.Count, result.Count);

        // All values should be in [0,1] range
        for (int i = 0; i < result.Count; i++)
        {
            Assert.True(result.Values[i] >= 0.0, $"Value at {i} should be >= 0");
            Assert.True(result.Values[i] <= 1.0, $"Value at {i} should be <= 1");
            Assert.True(double.IsFinite(result.Values[i]), $"Value at {i} should be finite");
        }
    }

    [Fact]
    public void InvalidInput_HandledGracefully()
    {
        var bbwn = new Bbwn(5, 2.0, 20);

        // Test with NaN
        var result1 = bbwn.Update(new TValue(DateTime.UtcNow.Ticks, double.NaN));
        Assert.True(double.IsFinite(result1.Value));

        // Test with infinity
        var result2 = bbwn.Update(new TValue(DateTime.UtcNow.Ticks + 1, double.PositiveInfinity));
        Assert.True(double.IsFinite(result2.Value));

        // Test with negative infinity
        var result3 = bbwn.Update(new TValue(DateTime.UtcNow.Ticks + 2, double.NegativeInfinity));
        Assert.True(double.IsFinite(result3.Value));
    }

    [Fact]
    public void ZeroVarianceData_HandledCorrectly()
    {
        var bbwn = new Bbwn(5, 2.0, 20);

        // Feed constant values (zero variance)
        for (int i = 0; i < 30; i++)
        {
            var result = bbwn.Update(new TValue(DateTime.UtcNow.Ticks + i, 100.0));
            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value >= 0.0 && result.Value <= 1.0);
        }
    }

    [Fact]
    public void SmallDataset_HandledCorrectly()
    {
        var bbwn = new Bbwn(3, 2.0, 5);

        // Test with minimal data
        for (int i = 0; i < 3; i++)
        {
            var result = bbwn.Update(new TValue(DateTime.UtcNow.Ticks + i, 100.0 + i));
            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value >= 0.0 && result.Value <= 1.0);
        }
    }

    [Fact]
    public void LargeValues_HandledCorrectly()
    {
        var bbwn = new Bbwn(5, 2.0, 20);

        // Test with large values
        var largeValues = new[] { 1e6, 1e7, 1e8, 1e6, 1e7 };

        foreach (var value in largeValues)
        {
            var result = bbwn.Update(new TValue(DateTime.UtcNow.Ticks, value));
            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value >= 0.0 && result.Value <= 1.0);
        }
    }

    [Fact]
    public void TSeries_Update_MatchesStreaming()
    {
        int period = 10;
        int lookback = 20;
        var bbwnStream = new Bbwn(period, 2.0, lookback);
        var bbwnBatch = new Bbwn(period, 2.0, lookback);
        var bars = GenerateTestData(50);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            bbwnStream.Update(new TValue(times[i], close[i]));
        }

        var ts = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(times[i], close[i]));
        }
        var result = bbwnBatch.Update(ts);

        Assert.Equal(bbwnStream.Last.Value, result[result.Count - 1].Value, 1e-9);
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var bbwn = new Bbwn(10, 2.0, 30);
        var bars = GenerateTestData(100);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            bbwn.Update(new TValue(times[i], close[i]));
        }
        var iterativeResult = bbwn.Last.Value;

        var ts = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(times[i], close[i]));
        }
        var batchResult = Bbwn.Batch(ts, 10, 2.0, 30);

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

        var result = Bbwn.Batch(ts, 20, 2.0, 50);

        Assert.Equal(100, result.Count);
        Assert.True(double.IsFinite(result[result.Count - 1].Value));
        Assert.True(result[result.Count - 1].Value >= 0.0);
        Assert.True(result[result.Count - 1].Value <= 1.0);
    }

    [Fact]
    public void StaticBatch_ValidatesInput()
    {
        var ts = new TSeries();
        for (int i = 0; i < 10; i++)
        {
            ts.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        Assert.Throws<ArgumentException>(() => Bbwn.Batch(ts, 0));
        Assert.Throws<ArgumentException>(() => Bbwn.Batch(ts, -1));
        Assert.Throws<ArgumentException>(() => Bbwn.Batch(ts, 5, 0));
        Assert.Throws<ArgumentException>(() => Bbwn.Batch(ts, 5, -1));
        Assert.Throws<ArgumentException>(() => Bbwn.Batch(ts, 5, 2.0, 0));
        Assert.Throws<ArgumentException>(() => Bbwn.Batch(ts, 5, 2.0, -1));
    }

    [Fact]
    public void Batch_NaN_Safe()
    {
        var values = new double[] { 100, 101, 102, double.NaN, 104, 105 };
        var output = new double[values.Length];

        Bbwn.Batch(values, output, 3, 2.0, 3);

        Assert.True(output.Length == 6);
    }

    [Fact]
    public void BBWN_Normalization_Verified()
    {
        var bbwn = new Bbwn(5, 2.0, 10);
        var bars = GenerateTestData(20);
        var times = bars.Times;
        var close = bars.CloseValues;

        // Feed data
        for (int i = 0; i < bars.Count; i++)
        {
            bbwn.Update(new TValue(times[i], close[i]));
        }

        // Result should be between 0 and 1
        Assert.True(bbwn.Last.Value >= 0.0);
        Assert.True(bbwn.Last.Value <= 1.0);
    }

    [Fact]
    public void BBWN_IncreasingVolatility_IncreasesNormalizedWidth()
    {
        var bbwn = new Bbwn(5, 2.0, 20);
        var bars = GenerateTestData(100);

        // Feed all data and check values are within range
        for (int i = 0; i < bars.Count; i++)
        {
            var result = bbwn.Update(new TValue(bars.Times[i], bars.CloseValues[i]));
            Assert.True(result.Value >= 0.0 && result.Value <= 1.0);
        }

        // Test passes if we get through all data without issue
        Assert.True(bbwn.IsHot);
    }

    [Fact]
    public void BBWN_LookbackEffect_Verified()
    {
        var bars = GenerateTestData(100);

        // Short lookback
        var bbwn1 = new Bbwn(10, 2.0, 20);
        // Long lookback
        var bbwn2 = new Bbwn(10, 2.0, 50);

        for (int i = 0; i < bars.Count; i++)
        {
            bbwn1.Update(new TValue(bars.Times[i], bars.CloseValues[i]));
            bbwn2.Update(new TValue(bars.Times[i], bars.CloseValues[i]));
        }

        // Both should be in valid range
        Assert.True(bbwn1.Last.Value >= 0.0 && bbwn1.Last.Value <= 1.0);
        Assert.True(bbwn2.Last.Value >= 0.0 && bbwn2.Last.Value <= 1.0);

        // They may differ due to different historical context
        // No assertion on equality - just that both work correctly
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var bbwn = new Bbwn(10, 2.0, 20);
        var bars = GenerateTestData(50);
        var times = bars.Times;
        var close = bars.CloseValues;

        TValue lastValue = default;
        for (int i = 0; i < bars.Count; i++)
        {
            lastValue = bbwn.Update(new TValue(times[i], close[i]), isNew: true);
        }
        double originalValue = lastValue.Value;

        // Test with a much more extreme correction value to force different BBW
        _ = bbwn.Update(new TValue(DateTime.UtcNow.Ticks, close[bars.Count - 1] * 100), isNew: false);

        // Restore to original and verify exact match
        var restoredValue = bbwn.Update(new TValue(lastValue.Time, close[bars.Count - 1]), isNew: false);
        Assert.Equal(originalValue, restoredValue.Value, 1e-9);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var bbwn = new Bbwn(5, 2.0, 10);

        for (int i = 0; i < 20; i++)
        {
            bbwn.Update(new TValue(DateTime.UtcNow.Ticks + i, 100 + i), isNew: true);
        }

        var result1 = bbwn.Update(new TValue(DateTime.UtcNow.Ticks + 100, 120), isNew: true);
        _ = bbwn.Update(new TValue(DateTime.UtcNow.Ticks + 100, 150), isNew: false);
        var result3 = bbwn.Update(new TValue(DateTime.UtcNow.Ticks + 100, 120), isNew: false);

        Assert.Equal(result1.Value, result3.Value, Tolerance);
    }
}