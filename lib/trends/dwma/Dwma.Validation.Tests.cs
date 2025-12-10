using System;
using Xunit;

namespace QuanTAlib;

public class DwmaValidationTests
{
    [Fact]
    public void Validate_Against_DoubleWma()
    {
        // DWMA should be exactly WMA(WMA(source, period), period)
        
        int period = 10;
        int count = 1000;
        var source = new TSeries();
        var rnd = new Random(42);
        
        for (int i = 0; i < count; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), rnd.NextDouble() * 100));
        }
        
        var dwma = new Dwma(period);
        var wma1 = new Wma(period);
        var wma2 = new Wma(period);
        
        for (int i = 0; i < count; i++)
        {
            var val = source[i];
            
            // Calculate DWMA
            var dwmaVal = dwma.Update(val);
            
            // Calculate WMA(WMA) manually
            var wma1Val = wma1.Update(val);
            var wma2Val = wma2.Update(wma1Val);
            
            Assert.Equal(wma2Val.Value, dwmaVal.Value, 10);
        }
    }
}
