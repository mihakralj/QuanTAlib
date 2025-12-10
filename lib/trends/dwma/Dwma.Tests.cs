using System;
using Xunit;

namespace QuanTAlib;

public class DwmaTests
{
    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Dwma(0));
        Assert.Throws<ArgumentException>(() => new Dwma(-1));
    }

    [Fact]
    public void Update_ValidInput_CalculatesCorrectly()
    {
        // DWMA(3) of [1, 2, 3, 4, 5]
        // WMA(3) of [1, 2, 3, 4, 5]
        // 1: 1
        // 2: (1*1 + 2*2) / 3 = 5/3 = 1.666...
        // 3: (1*1 + 2*2 + 3*3) / 6 = 14/6 = 2.333...
        // 4: (1*2 + 2*3 + 3*4) / 6 = 20/6 = 3.333...
        // 5: (1*3 + 2*4 + 3*5) / 6 = 26/6 = 4.333...
        
        // WMA(3) results: [1, 1.666, 2.333, 3.333, 4.333]
        
        // DWMA(3) = WMA(3) of [1, 1.666, 2.333, 3.333, 4.333]
        // 1: 1
        // 2: (1*1 + 2*1.666) / 3 = 4.333/3 = 1.444...
        // 3: (1*1 + 2*1.666 + 3*2.333) / 6 = (1 + 3.333 + 7) / 6 = 11.333/6 = 1.888...
        
        var dwma = new Dwma(3);
        
        var v1 = dwma.Update(new TValue(DateTime.UtcNow, 1)).Value;
        var v2 = dwma.Update(new TValue(DateTime.UtcNow, 2)).Value;
        var v3 = dwma.Update(new TValue(DateTime.UtcNow, 3)).Value;
        
        Assert.Equal(1.0, v1, 6);
        Assert.Equal(1.444444, v2, 5);
        Assert.Equal(1.888888, v3, 5);
    }

    [Fact]
    public void Update_IsNewFalse_CorrectsValue()
    {
        var dwma = new Dwma(3);
        
        dwma.Update(new TValue(DateTime.UtcNow, 1));
        dwma.Update(new TValue(DateTime.UtcNow, 2));
        
        // Update with 3, then correct to 4
        var v3 = dwma.Update(new TValue(DateTime.UtcNow, 3), isNew: true).Value;
        var v3_corrected = dwma.Update(new TValue(DateTime.UtcNow, 4), isNew: false).Value;
        
        // Manual calc for sequence [1, 2, 4]
        // WMA(3):
        // 1: 1
        // 2: 1.666
        // 4: (1*1 + 2*2 + 3*4) / 6 = 17/6 = 2.8333
        
        // DWMA(3) of [1, 1.666, 2.8333]
        // 3: (1*1 + 2*1.666 + 3*2.8333) / 6 = (1 + 3.333 + 8.5) / 6 = 12.833/6 = 2.1388
        
        Assert.Equal(1.888888, v3, 5); // From previous test
        Assert.Equal(2.138888, v3_corrected, 5);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var dwma = new Dwma(3);
        dwma.Update(new TValue(DateTime.UtcNow, 1));
        dwma.Update(new TValue(DateTime.UtcNow, 2));
        
        dwma.Reset();
        
        Assert.False(dwma.IsHot);
        var v1 = dwma.Update(new TValue(DateTime.UtcNow, 1)).Value;
        Assert.Equal(1.0, v1);
    }

    [Fact]
    public void StaticCalculate_MatchesInstance()
    {
        int period = 10;
        int count = 100;
        var source = new TSeries();
        var dwma = new Dwma(period);
        
        for (int i = 0; i < count; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), i));
            dwma.Update(source.Last);
        }
        
        var staticResult = Dwma.Calculate(source, period);
        
        Assert.Equal(source.Count, staticResult.Count);
        Assert.Equal(dwma.Last.Value, staticResult.Last.Value, 8);
    }
}
