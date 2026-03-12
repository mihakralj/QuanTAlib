// Va: Mathematical property validation tests
// Volume Accumulation is a cumulative indicator. No standard external library equivalents
// with matching implementation. Validation uses mathematical property testing.

namespace QuanTAlib.Tests;

using Xunit;

public class VaValidationTests
{
    private const int TestDataLength = 500;

    [Fact]
    public void Va_Output_IsFiniteForGbmData()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var va = new Va();

        for (int i = 0; i < bars.Count; i++)
        {
            var result = va.Update(bars[i], isNew: true);
            Assert.True(double.IsFinite(result.Value),
                $"Va output must be finite at bar {i}, got {result.Value}");
        }
    }

    [Fact]
    public void Va_CloseAboveMidpoint_PositiveAccumulation()
    {
        var va = new Va();

        // Close is above midpoint: (H+L)/2 = 100, Close = 102
        var bar = new TBar(DateTime.UtcNow, 101, 101, 99, 102, 1000);
        var result = va.Update(bar, isNew: true);

        // VA_period = volume * (close - midpoint) = 1000 * (102 - 100) = 2000
        Assert.True(result.Value > 0,
            $"VA should be positive when close > midpoint, got {result.Value}");
    }

    [Fact]
    public void Va_CloseBelowMidpoint_NegativeAccumulation()
    {
        var va = new Va();

        // Close is below midpoint: (H+L)/2 = 100, Close = 98
        var bar = new TBar(DateTime.UtcNow, 101, 101, 99, 98, 1000);
        var result = va.Update(bar, isNew: true);

        // VA_period = volume * (close - midpoint) = 1000 * (98 - 100) = -2000
        Assert.True(result.Value < 0,
            $"VA should be negative when close < midpoint, got {result.Value}");
    }

    [Fact]
    public void Va_CloseAtMidpoint_ZeroAccumulation()
    {
        var va = new Va();

        // Close is exactly at midpoint
        var bar = new TBar(DateTime.UtcNow, 101, 101, 99, 100, 1000);
        var result = va.Update(bar, isNew: true);

        Assert.Equal(0.0, result.Value, precision: 10);
    }

    [Fact]
    public void Va_ZeroVolume_ZeroAccumulation()
    {
        var va = new Va();

        // Even with close above midpoint, zero volume = zero VA contribution
        var bar = new TBar(DateTime.UtcNow, 101, 101, 99, 102, 0);
        var result = va.Update(bar, isNew: true);

        Assert.Equal(0.0, result.Value, precision: 10);
    }

    [Fact]
    public void Va_IsCumulative_AccumulatesOverBars()
    {
        var va = new Va();

        // Bar 1: close above midpoint
        var bar1 = new TBar(DateTime.UtcNow, 101, 101, 99, 102, 1000);
        var r1 = va.Update(bar1, isNew: true);
        double expectedVa1 = 1000 * (102 - 100.0); // 2000

        // Bar 2: close below midpoint
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 101, 101, 99, 98, 500);
        var r2 = va.Update(bar2, isNew: true);
        double expectedVa2 = expectedVa1 + (500 * (98 - 100.0)); // 2000 + (-1000) = 1000

        Assert.Equal(expectedVa1, r1.Value, precision: 10);
        Assert.Equal(expectedVa2, r2.Value, precision: 10);
    }

    [Fact]
    public void Va_KnownCalculation_MatchesManual()
    {
        var va = new Va();

        // Manually verified calculation
        // Bar: O=100, H=105, L=95, C=103, V=2000
        // Midpoint = (105 + 95) / 2 = 100
        // VA_period = 2000 * (103 - 100) = 6000
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 103, 2000);
        var result = va.Update(bar, isNew: true);

        Assert.Equal(6000.0, result.Value, precision: 10);
    }

    [Fact]
    public void Va_BatchAndStreaming_ProduceSameResults()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Batch
        var batchResults = Va.Batch(bars);

        // Streaming
        var streamVa = new Va();
        var streamResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            var result = streamVa.Update(bars[i], isNew: true);
            streamResults[i] = result.Value;
        }

        Assert.Equal(batchResults.Count, bars.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(batchResults.Values[i], streamResults[i], precision: 8);
        }
    }

    [Fact]
    public void Va_SpanAndStreaming_ProduceSameResults()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var spanOutput = new double[bars.Count];

        Va.Batch(
            bars.High.Values, bars.Low.Values,
            bars.Close.Values, bars.Volume.Values,
            spanOutput);

        // Streaming
        var streamVa = new Va();
        for (int i = 0; i < bars.Count; i++)
        {
            var result = streamVa.Update(bars[i], isNew: true);
            Assert.Equal(spanOutput[i], result.Value, precision: 8);
        }
    }

    [Fact]
    public void Va_BarCorrection_IsNewFalse_RestoresState()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var va = new Va();

        for (int i = 0; i < 30; i++)
        {
            va.Update(bars[i], isNew: true);
        }

        va.Update(bars[30], isNew: true);
        double afterNew = va.Last.Value;

        va.Update(bars[30], isNew: false);
        double afterCorrection = va.Last.Value;

        Assert.Equal(afterNew, afterCorrection, precision: 10);
    }
}
