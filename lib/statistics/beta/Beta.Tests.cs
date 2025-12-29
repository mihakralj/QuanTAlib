using System;
using Xunit;

namespace QuanTAlib.Tests;

public class BetaTests
{
    [Fact]
    public void Constructor_ValidatesPeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Beta(0));
    }

    [Fact]
    public void Update_ThrowsOnSingleInput()
    {
        var beta = new Beta(10);
        Assert.Throws<NotSupportedException>(() => beta.Update(new TValue(DateTime.UtcNow, 100)));
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
    public void Reset_ClearsState()
    {
        var beta = new Beta(5);
        for (int i = 0; i < 10; i++)
        {
            beta.Update(100 + i, 100 + i);
        }
        Assert.True(beta.IsHot);

        beta.Reset();
        Assert.False(beta.IsHot);

        // Re-initialize
        beta.Update(100, 100);
        Assert.False(beta.IsHot);
    }
}
