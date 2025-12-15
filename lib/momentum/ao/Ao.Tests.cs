using Xunit;

namespace QuanTAlib.Tests;

public class AoTests
{
    [Fact]
    public void Constructor_ValidatesParameters()
    {
        Assert.Throws<ArgumentException>(() => new Ao(0, 34));
        Assert.Throws<ArgumentException>(() => new Ao(5, 0));
        Assert.Throws<ArgumentException>(() => new Ao(34, 5)); // Fast >= Slow
    }

    [Fact]
    public void IsHot_BecomesTrueAfterSlowPeriod()
    {
        var ao = new Ao(2, 5);
        
        // Add 4 values
        for (int i = 0; i < 4; i++)
        {
            ao.Update(new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100));
            Assert.False(ao.IsHot);
        }

        // Add 5th value
        ao.Update(new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100));
        Assert.True(ao.IsHot);
    }

    [Fact]
    public void Calculation_Correctness()
    {
        // AO = SMA(Median, 5) - SMA(Median, 34)
        // Let's use smaller periods for testing: 2 and 4
        var ao = new Ao(2, 4);
        
        // Median prices: 10, 20, 30, 40, 50
        // SMA2: -, 15, 25, 35, 45
        // SMA4: -, -, -, 25, 35
        // AO:   -, -, -, 10, 10
        
        var data = new[] { 10.0, 20.0, 30.0, 40.0, 50.0 };
        // Sma returns average of available data.
        // SMA2(10) = 10
        // SMA2(10, 20) = 15
        // SMA2(20, 30) = 25
        // SMA2(30, 40) = 35
        // SMA2(40, 50) = 45
        
        // SMA4(10) = 10
        // SMA4(10, 20) = 15
        // SMA4(10, 20, 30) = 20
        // SMA4(10, 20, 30, 40) = 25
        // SMA4(20, 30, 40, 50) = 35
        
        // AO:
        // 1: 10 - 10 = 0
        // 2: 15 - 15 = 0
        // 3: 25 - 20 = 5
        // 4: 35 - 25 = 10
        // 5: 45 - 35 = 10

        for (int i = 0; i < data.Length; i++)
        {
            var bar = new TBar(DateTime.UtcNow, data[i], data[i], data[i], data[i], 100);
            var result = ao.Update(bar);
            
            if (i == 2) Assert.Equal(5.0, result.Value);
            if (i >= 3) Assert.Equal(10.0, result.Value);
        }
    }

    [Fact]
    public void Update_WithIsNewFalse_UpdatesLastValue()
    {
        var ao = new Ao(2, 4);
        
        // 1. Add 10
        ao.Update(new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100));
        // SMA2=10, SMA4=10, AO=0
        
        // 2. Add 20
        ao.Update(new TBar(DateTime.UtcNow, 20, 20, 20, 20, 100));
        // SMA2=15, SMA4=15, AO=0
        
        // 3. Update last with 30 (instead of 20)
        var result = ao.Update(new TBar(DateTime.UtcNow, 30, 30, 30, 30, 100), isNew: false);
        
        // SMA2(10, 30) = 20
        // SMA4(10, 30) = 20
        // AO = 0
        Assert.Equal(0.0, result.Value);
        
        // 4. Add 40
        result = ao.Update(new TBar(DateTime.UtcNow, 40, 40, 40, 40, 100));
        // SMA2(30, 40) = 35
        // SMA4(10, 30, 40) = 26.666...
        // AO = 35 - 26.666... = 8.333...
        
        Assert.True(result.Value > 0);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var ao = new Ao(2, 4);
        ao.Update(new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100));
        ao.Update(new TBar(DateTime.UtcNow, 20, 20, 20, 20, 100));
        
        ao.Reset();
        
        Assert.False(ao.IsHot);
        Assert.Equal(0, ao.Last.Value);
        
        // Should behave like new
        ao.Update(new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100));
        Assert.Equal(0, ao.Last.Value);
    }
}
