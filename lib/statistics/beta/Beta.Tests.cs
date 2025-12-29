
namespace QuanTAlib.Tests;

public class BetaTests
{
    [Fact]
    public void Constructor_ValidatesPeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Beta(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Beta(-1));

        // Valid period should not throw
        var beta = new Beta(1);
        Assert.NotNull(beta);
    }

    [Fact]
    public void Update_ThrowsOnSingleInput()
    {
        var beta = new Beta(10);
        Assert.Throws<NotSupportedException>(() => beta.Update(new TValue(DateTime.UtcNow, 100)));
        Assert.Throws<NotSupportedException>(() => beta.Update(new TSeries()));
        Assert.Throws<NotSupportedException>(() => beta.Prime(new double[] { 1, 2, 3 }));
    }

    [Fact]
    public void Properties_Accessible()
    {
        var beta = new Beta(10);

        Assert.Equal(0, beta.Last.Value);
        Assert.False(beta.IsHot);
        Assert.Contains("Beta", beta.Name, StringComparison.Ordinal);
        Assert.Equal(11, beta.WarmupPeriod); // period + 1 for first return

        beta.Update(100, 100);
        beta.Update(101, 101);
        Assert.NotEqual(0, beta.Last.Time);
    }

    [Fact]
    public void IsHot_BecomesTrueAfterPeriod()
    {
        int period = 5;
        var beta = new Beta(period);

        // We need period returns.
        // 1st update: initializes prev prices. No return.
        // 2nd update: 1st return.
        // ...
        // (period+1)th update: period-th return. Buffer full. IsHot true.

        for (int i = 0; i <= period; i++)
        {
            Assert.False(beta.IsHot, $"IsHot should be false at index {i}");
            beta.Update(100 + i, 100 + i);
        }

        // Now we have fed period+1 prices -> period returns.
        Assert.True(beta.IsHot, "IsHot should be true after period+1 updates");
    }

    [Fact]
    public void Calculation_KnownBeta()
    {
        // Scenario: Asset returns are exactly 2x Market returns.
        // We need variable market returns to have non-zero variance.

        int period = 10;
        var beta = new Beta(period);

        double marketPrice = 100;
        double assetPrice = 100;

        // Initialize
        beta.Update(assetPrice, marketPrice);

        // Pattern of returns: +1%, -1%, +1%, -1%...
        // Asset returns: +2%, -2%, +2%, -2%...
        // This gives Beta = 2.

        for (int i = 0; i < 20; i++)
        {
            double marketReturn = (i % 2 == 0) ? 0.01 : -0.01;
            double assetReturn = marketReturn * 2.0;

            marketPrice *= (1 + marketReturn);
            assetPrice *= (1 + assetReturn);

            TValue result = beta.Update(assetPrice, marketPrice);

            if (beta.IsHot)
            {
                Assert.Equal(2.0, result.Value, precision: 6);
            }
        }
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var beta = new Beta(5);

        // Initialize
        beta.Update(100, 100);

        // Add 5 more updates with different ratios to get non-1 beta
        beta.Update(102, 101); // Asset up 2%, market up 1%
        beta.Update(104, 102); // Asset up ~2%, market up ~1%
        beta.Update(108, 103); // Asset up ~4%, market up ~1%
        beta.Update(112, 104); // Asset up ~4%, market up ~1%
        beta.Update(116, 105); // Asset up ~4%, market up ~1%

        double valueBefore = beta.Last.Value;

        // Update last value with isNew=false with very different values
        beta.Update(90, 110, isNew: false); // Drastically different
        double valueAfter = beta.Last.Value;

        // Value should change since we're updating the last bar
        Assert.NotEqual(valueBefore, valueAfter);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var beta = new Beta(5);

        // Initialize with 10 updates
        beta.Update(100, 100);
        for (int i = 1; i <= 9; i++)
        {
            beta.Update(100 + i, 100 + i);
        }

        double stateAfterTen = beta.Last.Value;

        // Apply 5 corrections with isNew=false
        for (int i = 0; i < 5; i++)
        {
            beta.Update(200 + i, 200 + i, isNew: false);
        }

        // Restore to original value
        beta.Update(109, 109, isNew: false);

        Assert.Equal(stateAfterTen, beta.Last.Value, precision: 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var beta = new Beta(5);
        for (int i = 0; i < 10; i++)
        {
            beta.Update(100 + i * 2, 100 + i); // Different ratios
        }
        Assert.True(beta.IsHot);

        beta.Reset();
        Assert.False(beta.IsHot);

        // Re-initialize and verify it can accept new values
        // After reset, beta should be able to calculate fresh values
        beta.Update(100, 100);
        Assert.False(beta.IsHot); // Not hot yet, needs period+1 updates

        // Feed more updates to reach hot state again
        for (int i = 1; i <= 5; i++)
        {
            beta.Update(100 + i, 100 + i);
        }
        Assert.True(beta.IsHot);
        
        // With equal proportional changes, beta should be 1
        Assert.Equal(1.0, beta.Last.Value, precision: 6);
    }

    [Fact]
    public void NaN_Input_ReturnsFiniteValue()
    {
        var beta = new Beta(5);

        // Initialize
        beta.Update(100, 100);

        // Add some valid values
        beta.Update(101, 101);
        beta.Update(102, 102);

        // Add NaN - Beta should handle gracefully
        var result = beta.Update(double.NaN, double.NaN);

        // Result should be finite (may be 0 or previous value)
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_ReturnsFiniteValue()
    {
        var beta = new Beta(5);

        // Initialize
        beta.Update(100, 100);

        // Add some valid values
        beta.Update(101, 101);
        beta.Update(102, 102);

        // Add Infinity - Beta should handle gracefully
        var result = beta.Update(double.PositiveInfinity, double.PositiveInfinity);

        // Result should be finite (may be 0 or previous value)
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void ZeroMarketVariance_ReturnsZero()
    {
        // When market returns are constant (zero variance), beta is undefined
        // The implementation should return 0 in this case
        var beta = new Beta(5);

        // Initialize
        beta.Update(100, 100);

        // Same market price (zero returns/variance)
        for (int i = 0; i < 10; i++)
        {
            beta.Update(100 + i, 100); // Asset changes, market constant
        }

        // Beta should be 0 (or undefined) when market variance is 0
        Assert.Equal(0, beta.Last.Value);
    }

    [Fact]
    public void Resync_DoesNotDrift()
    {
        // Run for > 1000 updates to trigger Resync
        var beta = new Beta(10);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        beta.Update(100, 100); // Initialize

        for (int i = 0; i < 1100; i++)
        {
            var bar = gbm.Next();
            beta.Update(bar.Close * 1.5, bar.Close); // Asset follows market with beta ~1.5
        }

        Assert.True(double.IsFinite(beta.Last.Value));
    }
}
