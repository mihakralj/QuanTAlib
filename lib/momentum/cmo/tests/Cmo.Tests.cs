using Xunit;

namespace QuanTAlib.Tests;

public class CmoTests
{
    private const double Epsilon = 1e-10;

    // ═══════════════════════════════════════════════════════════════════════════
    // Ctor Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Ctor_WithValidPeriod_CreatesIndicator()
    {
        var cmo = new Cmo(14);
        Assert.Equal("Cmo(14)", cmo.Name);
        Assert.Equal(15, cmo.WarmupPeriod);
    }

    [Fact]
    public void Ctor_WithDefaultPeriod_UsesDefault()
    {
        var cmo = new Cmo();
        Assert.Equal("Cmo(14)", cmo.Name);
    }

    [Fact]
    public void Ctor_WithPeriodOne_IsValid()
    {
        var cmo = new Cmo(1);
        Assert.Equal("Cmo(1)", cmo.Name);
    }

    [Fact]
    public void Ctor_WithZeroPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Cmo(0));
    }

    [Fact]
    public void Ctor_WithNegativePeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Cmo(-1));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Warmup/IsHot Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var cmo = new Cmo(5);

        for (int i = 0; i < 4; i++)
        {
            cmo.Update(new TValue(DateTime.Now.Ticks + i, 100 + i));
        }

        Assert.False(cmo.IsHot);
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var cmo = new Cmo(5);

        for (int i = 0; i < 6; i++)
        {
            cmo.Update(new TValue(DateTime.Now.Ticks + i, 100 + i));
        }

        Assert.True(cmo.IsHot);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Calculation Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_WithSingleValue_ReturnsZero()
    {
        var cmo = new Cmo(5);

        var result = cmo.Update(new TValue(DateTime.Now.Ticks, 100.0));

        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void Update_AllUpMoves_ReturnsPositive100()
    {
        var cmo = new Cmo(5);

        // All upward moves should give CMO = 100
        cmo.Update(new TValue(DateTime.Now.Ticks, 100));
        cmo.Update(new TValue(DateTime.Now.Ticks + 1, 101));
        cmo.Update(new TValue(DateTime.Now.Ticks + 2, 102));
        cmo.Update(new TValue(DateTime.Now.Ticks + 3, 103));
        cmo.Update(new TValue(DateTime.Now.Ticks + 4, 104));
        var result = cmo.Update(new TValue(DateTime.Now.Ticks + 5, 105));

        Assert.Equal(100.0, result.Value, Epsilon);
    }

    [Fact]
    public void Update_AllDownMoves_ReturnsNegative100()
    {
        var cmo = new Cmo(5);

        // All downward moves should give CMO = -100
        cmo.Update(new TValue(DateTime.Now.Ticks, 105));
        cmo.Update(new TValue(DateTime.Now.Ticks + 1, 104));
        cmo.Update(new TValue(DateTime.Now.Ticks + 2, 103));
        cmo.Update(new TValue(DateTime.Now.Ticks + 3, 102));
        cmo.Update(new TValue(DateTime.Now.Ticks + 4, 101));
        var result = cmo.Update(new TValue(DateTime.Now.Ticks + 5, 100));

        Assert.Equal(-100.0, result.Value, Epsilon);
    }

    [Fact]
    public void Update_EqualUpAndDown_ReturnsZero()
    {
        var cmo = new Cmo(4);

        // Pattern: up 2, down 2, up 2, down 2 = equal
        cmo.Update(new TValue(DateTime.Now.Ticks, 100));
        cmo.Update(new TValue(DateTime.Now.Ticks + 1, 102)); // up 2
        cmo.Update(new TValue(DateTime.Now.Ticks + 2, 100)); // down 2
        cmo.Update(new TValue(DateTime.Now.Ticks + 3, 102)); // up 2
        var result = cmo.Update(new TValue(DateTime.Now.Ticks + 4, 100)); // down 2

        // sumUp = 4, sumDown = 4, CMO = 0
        Assert.Equal(0.0, result.Value, Epsilon);
    }

    [Fact]
    public void Update_NoChange_ReturnsZero()
    {
        var cmo = new Cmo(5);

        // No change in prices
        for (int i = 0; i < 10; i++)
        {
            cmo.Update(new TValue(DateTime.Now.Ticks + i, 100));
        }

        Assert.Equal(0, cmo.Last.Value);
    }

    [Fact]
    public void Update_MixedMoves_CalculatesCorrectCmo()
    {
        var cmo = new Cmo(5);

        // Construct specific pattern
        cmo.Update(new TValue(DateTime.Now.Ticks, 100));
        cmo.Update(new TValue(DateTime.Now.Ticks + 1, 105)); // up 5
        cmo.Update(new TValue(DateTime.Now.Ticks + 2, 102)); // down 3
        cmo.Update(new TValue(DateTime.Now.Ticks + 3, 107)); // up 5
        cmo.Update(new TValue(DateTime.Now.Ticks + 4, 104)); // down 3
        var result = cmo.Update(new TValue(DateTime.Now.Ticks + 5, 106)); // up 2

        // sumUp = 5+5+2 = 12, sumDown = 3+3 = 6
        // CMO = 100 * (12-6)/(12+6) = 100 * 6/18 = 33.333...
        Assert.Equal(100.0 * 6.0 / 18.0, result.Value, Epsilon);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Sliding Window Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_SlidingWindow_DropsOldValues()
    {
        var cmo = new Cmo(3);

        cmo.Update(new TValue(DateTime.Now.Ticks, 100));      // no change (first value)
        cmo.Update(new TValue(DateTime.Now.Ticks + 1, 110));  // up 10
        cmo.Update(new TValue(DateTime.Now.Ticks + 2, 105));  // down 5
        cmo.Update(new TValue(DateTime.Now.Ticks + 3, 108));  // up 3, window now has: up 10, down 5, up 3

        // Window contains changes from last 3 bars: up 10, down 5, up 3
        // sumUp = 10 + 3 = 13, sumDown = 5
        // CMO = 100 * (13-5)/(13+5) = 100 * 8/18 = 44.444...
        var result = cmo.Last;
        Assert.Equal(100.0 * 8.0 / 18.0, result.Value, Epsilon);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Bar Update Tests (isNew = false)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_WithIsNewFalse_UpdatesCurrentBar()
    {
        var cmo = new Cmo(5);

        // Initial values: alternating to mix up/down
        cmo.Update(new TValue(DateTime.Now.Ticks, 100));
        cmo.Update(new TValue(DateTime.Now.Ticks + 1, 105));  // up 5
        cmo.Update(new TValue(DateTime.Now.Ticks + 2, 102));  // down 3
        cmo.Update(new TValue(DateTime.Now.Ticks + 3, 106));  // up 4
        cmo.Update(new TValue(DateTime.Now.Ticks + 4, 104));  // down 2
        cmo.Update(new TValue(DateTime.Now.Ticks + 5, 108));  // up 4, isNew

        var beforeUpdate = cmo.Last;

        // Update current bar with different value (isNew = false)
        cmo.Update(new TValue(DateTime.Now.Ticks + 5, 100), isNew: false); // now down 4 instead of up 4
        var afterUpdate = cmo.Last;

        // Value should change since we updated the last bar
        Assert.NotEqual(beforeUpdate.Value, afterUpdate.Value);
    }

    [Fact]
    public void Update_MultipleIsNewFalse_DoesNotAdvanceWindow()
    {
        var cmo = new Cmo(5);

        // Initial values
        for (int i = 0; i < 5; i++)
        {
            cmo.Update(new TValue(DateTime.Now.Ticks + i, 100.0));
        }

        // Multiple updates with isNew = false
        cmo.Update(new TValue(DateTime.Now.Ticks + 5, 110), isNew: false);
        cmo.Update(new TValue(DateTime.Now.Ticks + 5, 115), isNew: false);
        cmo.Update(new TValue(DateTime.Now.Ticks + 5, 120), isNew: false);

        // Final state should reflect the last update only
        // Since all previous values were 100, the only "up" is the current bar update
        Assert.True(cmo.Last.Value > 0); // Should be positive since we went from 100 to 120
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Reset Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Reset_ClearsState()
    {
        var cmo = new Cmo(5);

        for (int i = 0; i < 10; i++)
        {
            cmo.Update(new TValue(DateTime.Now.Ticks + i, 100 + i));
        }

        cmo.Reset();

        Assert.Equal(0, cmo.Last.Time);
        Assert.False(cmo.IsHot);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var cmo = new Cmo(5);

        for (int i = 0; i < 10; i++)
        {
            cmo.Update(new TValue(DateTime.Now.Ticks + i, 100 + i));
        }

        cmo.Reset();

        // Should work after reset
        var result = cmo.Update(new TValue(DateTime.Now.Ticks, 50));
        Assert.Equal(0, result.Value); // First value, no change yet
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Batch Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Batch_ProducesCorrectResults()
    {
        var prices = new TSeries();
        for (int i = 0; i < 20; i++)
        {
            prices.Add(new TValue(DateTime.Now.Ticks + i, 100 + (Math.Sin(i) * 10)));
        }

        var results = Cmo.Batch(prices, 5);

        Assert.Equal(prices.Count, results.Count);
    }

    [Fact]
    public void Batch_MatchesStreamingResults()
    {
        var prices = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            prices.Add(new TValue(DateTime.Now.Ticks + i, 100 + (Math.Sin(i * 0.5) * 20)));
        }

        var batchResults = Cmo.Batch(prices, 14);

        var cmo = new Cmo(14);
        var streamingResults = new TSeries();
        foreach (var price in prices)
        {
            streamingResults.Add(cmo.Update(price));
        }

        Assert.Equal(batchResults.Count, streamingResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(batchResults[i].Value, streamingResults[i].Value, 1e-9);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Edge Case Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_WithNaN_DoesNotCrash()
    {
        var cmo = new Cmo(5);

        for (int i = 0; i < 5; i++)
        {
            cmo.Update(new TValue(DateTime.Now.Ticks + i, 100));
        }

        // NaN input should not throw
        var result = cmo.Update(new TValue(DateTime.Now.Ticks + 5, double.NaN));

        // The result is a valid TValue (struct is never null)
        Assert.True(result.Time > 0);
    }

    [Fact]
    public void Update_WithLargeValues_CalculatesCorrectly()
    {
        var cmo = new Cmo(5);

        double largeBase = 1e15;
        for (int i = 0; i < 6; i++)
        {
            cmo.Update(new TValue(DateTime.Now.Ticks + i, largeBase + i));
        }

        // All up moves, should be 100
        Assert.Equal(100.0, cmo.Last.Value, 1e-6);
    }

    [Fact]
    public void Update_WithVerySmallChanges_CalculatesCorrectly()
    {
        var cmo = new Cmo(5);

        double baseVal = 100.0;
        double smallChange = 1e-10;
        for (int i = 0; i < 6; i++)
        {
            cmo.Update(new TValue(DateTime.Now.Ticks + i, baseVal + (i * smallChange)));
        }

        // All tiny up moves, should still be 100
        Assert.Equal(100.0, cmo.Last.Value, 1e-6);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Publisher Pattern Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Pub_PublishesOnUpdate()
    {
        var cmo = new Cmo(5);
        TValue? received = null;

        cmo.Pub += (object? sender, in TValueEventArgs args) =>
        {
            received = args.Value;
        };

        for (int i = 0; i < 6; i++)
        {
            cmo.Update(new TValue(DateTime.Now.Ticks + i, 100 + i));
        }

        Assert.NotNull(received);
        Assert.Equal(cmo.Last.Value, received!.Value.Value);
    }

    [Fact]
    public void Ctor_WithSourcePublisher_SubscribesCorrectly()
    {
        var source = new Sma(5);
        var cmo = new Cmo(source, 5);

        for (int i = 0; i < 10; i++)
        {
            source.Update(new TValue(DateTime.Now.Ticks + i, 100 + i));
        }

        Assert.True(cmo.IsHot);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Prime Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Prime_WarmupIndicator()
    {
        var cmo = new Cmo(5);
        Span<double> data = [100, 101, 102, 103, 104, 105];

        cmo.Prime(data);

        Assert.True(cmo.IsHot);
        Assert.Equal(100.0, cmo.Last.Value, Epsilon); // All up moves
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Calculate Static Method Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Calculate_MatchesInstanceMethod()
    {
        double[] source = [100, 105, 102, 107, 104, 106, 103, 108, 101, 109];
        double[] output = new double[source.Length];

        Cmo.Batch(source, output, 5);

        var cmo = new Cmo(5);
        for (int i = 0; i < source.Length; i++)
        {
            var result = cmo.Update(new TValue(DateTime.Now.Ticks + i, source[i]));
            Assert.Equal(output[i], result.Value, 1e-9);
        }
    }

    [Fact]
    public void Calculate_WithMismatchedLengths_ThrowsArgumentException()
    {
        double[] source = new double[10];
        double[] output = new double[5];

        Assert.Throws<ArgumentException>(() => Cmo.Batch(source, output, 5));
    }

    [Fact]
    public void Calculate_WithInvalidPeriod_ThrowsArgumentException()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        Assert.Throws<ArgumentException>(() => Cmo.Batch(source, output, 0));
    }
}
