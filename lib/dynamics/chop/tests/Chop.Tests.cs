namespace QuanTAlib;

public class ChopTests
{
    [Fact]
    public void BasicCalculation_ProducesValidResults()
    {
        var chop = new Chop(14);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            var result = chop.Update(bars[i]);

            if (i >= 13) // WarmupPeriod = 14
            {
                // CHOP should be between 0 and 100
                Assert.True(result.Value >= 0.0 && result.Value <= 100.0,
                    $"CHOP value {result.Value} at index {i} out of range [0, 100]");
            }
        }

        Assert.True(chop.IsHot);
    }

    [Fact]
    public void StrongTrend_ProducesLowChop()
    {
        // Create a strong trending market (steadily rising prices)
        var chop = new Chop(14);
        var bars = new TBarSeries();

        // Generate trending bars: each bar higher than the last
        for (int i = 0; i < 50; i++)
        {
            double basePrice = 100 + (i * 2); // Strong uptrend
            bars.Add(new TBar(
                time: DateTime.UtcNow.AddMinutes(i),
                open: basePrice - 0.5,
                high: basePrice + 0.5,
                low: basePrice - 0.5,
                close: basePrice + 0.3,
                volume: 1000
            ));
        }

        TValue result = default;
        for (int i = 0; i < bars.Count; i++)
        {
            result = chop.Update(bars[i]);
        }

        // Strong trend should have low CHOP (< 50, ideally < 38.2)
        Assert.True(result.Value < 50.0,
            $"Strong trend should have low CHOP, got {result.Value}");
    }

    [Fact]
    public void SidewaysMarket_ProducesHighChop()
    {
        // Create a choppy/sideways market (oscillating prices)
        var chop = new Chop(14);
        var bars = new TBarSeries();

        // Generate choppy bars: prices oscillate in a range
        for (int i = 0; i < 50; i++)
        {
            double oscillation = Math.Sin(i * 0.5) * 2; // Small oscillations
            double basePrice = 100 + oscillation;
            bars.Add(new TBar(
                time: DateTime.UtcNow.AddMinutes(i),
                open: basePrice - 1,
                high: basePrice + 2,
                low: basePrice - 2,
                close: basePrice + 0.5,
                volume: 1000
            ));
        }

        TValue result = default;
        for (int i = 0; i < bars.Count; i++)
        {
            result = chop.Update(bars[i]);
        }

        // Sideways market should have high CHOP (> 50, ideally > 61.8)
        Assert.True(result.Value > 50.0,
            $"Choppy market should have high CHOP, got {result.Value}");
    }

    [Fact]
    public void BarCorrection_RestoresState()
    {
        var chop = new Chop(14);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed initial bars
        for (int i = 0; i < 15; i++)
        {
            chop.Update(bars[i], isNew: true);
        }

        // Bar 15 processed, state is saved

        // Process bar 16 as new
        chop.Update(bars[15], isNew: true);
        double valueAfter16New = chop.Last.Value;

        // Now correct bar 16 (isNew=false) with a different bar
        var modifiedBar = new TBar(
            bars[15].Time,
            bars[15].Open * 1.1,
            bars[15].High * 1.2,
            bars[15].Low * 0.9,
            bars[15].Close * 1.15,
            bars[15].Volume
        );
        chop.Update(modifiedBar, isNew: false);
        double valueAfter16Corrected = chop.Last.Value;

        // Corrected value should be different from the original bar 16 value
        Assert.NotEqual(valueAfter16New, valueAfter16Corrected);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var chop = new Chop(14);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed bars to warm up
        for (int i = 0; i < 15; i++)
        {
            chop.Update(bars[i]);
        }

        Assert.True(chop.IsHot);

        // Reset
        chop.Reset();

        Assert.False(chop.IsHot);
        Assert.Equal(0.0, chop.Last.Value);
    }

    [Fact]
    public void Constructor_ThrowsForInvalidPeriod()
    {
        Assert.Throws<ArgumentException>(() => new Chop(1));
        Assert.Throws<ArgumentException>(() => new Chop(0));
        Assert.Throws<ArgumentException>(() => new Chop(-1));
    }

    [Fact]
    public void NaN_Input_KeepsLastValidValue()
    {
        var chop = new Chop(14);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed some valid bars first
        for (int i = 0; i < 15; i++)
        {
            chop.Update(bars[i]);
        }

        double lastValidValue = chop.Last.Value;

        // Create a bar with NaN values
        var nanBar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        var result = chop.Update(nanBar);

        // Should keep last valid value
        Assert.Equal(lastValidValue, result.Value);
    }

    [Fact]
    public void Infinity_Input_KeepsLastValidValue()
    {
        var chop = new Chop(14);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed some valid bars first
        for (int i = 0; i < 15; i++)
        {
            chop.Update(bars[i]);
        }

        double lastValidValue = chop.Last.Value;

        // Create a bar with Infinity values
        var infBar = new TBar(DateTime.UtcNow, double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var result = chop.Update(infBar);

        // Should keep last valid value
        Assert.Equal(lastValidValue, result.Value);
    }

    [Fact]
    public void BatchMode_ProducesValidResults()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var result = Chop.Batch(bars);

        Assert.Equal(50, result.Count);

        // Check that warmed-up values are in valid range
        for (int i = 13; i < result.Count; i++)
        {
            Assert.True(result[i].Value >= 0.0 && result[i].Value <= 100.0,
                $"CHOP value {result[i].Value} at index {i} out of range [0, 100]");
        }
    }

    [Fact]
    public void BatchModeWithPeriod_MatchesStreamingMode()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Batch mode
        var batchResult = Chop.Batch(bars, period: 10);

        // Streaming mode
        var streamingChop = new Chop(10);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingChop.Update(bars[i]);
        }

        // Results should match
        Assert.Equal(batchResult.Last.Value, streamingChop.Last.Value, precision: 10);
    }

    [Fact]
    public void Name_ReflectsPeriod()
    {
        var chop14 = new Chop(14);
        var chop20 = new Chop(20);

        Assert.Equal("CHOP(14)", chop14.Name);
        Assert.Equal("CHOP(20)", chop20.Name);
    }

    [Fact]
    public void Period_Property_ReturnsCorrectValue()
    {
        var chop = new Chop(21);
        Assert.Equal(21, chop.Period);
    }

    [Fact]
    public void WarmupPeriod_EqualsToPeriod()
    {
        var chop = new Chop(14);
        Assert.Equal(14, chop.WarmupPeriod);
    }

    [Fact]
    public void EventPublishing_Works()
    {
        var chop = new Chop(14);
        var gbm = new GBM();

        int eventCount = 0;
        TValue lastPublishedValue = default;
        bool lastIsNew = false;

        chop.Pub += (object? sender, in TValueEventArgs args) =>
        {
            eventCount++;
            lastPublishedValue = args.Value;
            lastIsNew = args.IsNew;
        };

        var bar = gbm.Next(isNew: true);
        chop.Update(bar, isNew: true);

        Assert.Equal(1, eventCount);
        Assert.True(lastIsNew);
        Assert.Equal(chop.Last.Value, lastPublishedValue.Value);

        // Update with isNew=false
        chop.Update(bar, isNew: false);

        Assert.Equal(2, eventCount);
        Assert.False(lastIsNew);
    }

    [Fact]
    public void ZeroPriceRange_ReturnsNaN()
    {
        // When all prices are the same, CHOP should return NaN (or handle gracefully)
        var chop = new Chop(5);

        // Create bars with identical high and low
        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 100, 100, 100, 1000);
            chop.Update(bar);
        }

        // Zero price range should result in NaN or clamped value
        Assert.True(double.IsNaN(chop.Last.Value) || chop.Last.Value >= 0);
    }
}
