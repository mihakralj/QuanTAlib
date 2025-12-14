using System;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class DmxValidationTests
{
    private readonly ITestOutputHelper _output;

    public DmxValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Validate_Consistency_UpdateVsSeries()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        
        var dmx = new Dmx(14);
        var streamResult = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            streamResult.Add(dmx.Update(bars[i]));
        }

        var dmx2 = new Dmx(14);
        var seriesResult = dmx2.Update(bars);

        Assert.Equal(streamResult.Count, seriesResult.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamResult[i].Value, seriesResult[i].Value, 1e-9);
        }
        _output.WriteLine("DMX Update vs Series validated successfully");
    }

    [Fact]
    public void Validate_Range()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        
        var dmx = new Dmx(14);
        for (int i = 0; i < bars.Count; i++)
        {
            var val = dmx.Update(bars[i]).Value;
            Assert.True(val >= -100.0 && val <= 100.0, $"DMX value {val} out of range [-100, 100]");
        }
        _output.WriteLine("DMX range validated successfully");
    }

    [Fact]
    public void Validate_Trend_Direction()
    {
        // Create a synthetic uptrend
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;
        double price = 100;
        for (int i = 0; i < 100; i++)
        {
            bars.Add(time, price, price + 2, price - 1, price + 1, 1000);
            time = time.AddMinutes(1);
            price += 1.0; // Steady uptrend
        }

        var dmx = new Dmx(14);
        var result = dmx.Update(bars);

        // Check the last few values, they should be positive
        for (int i = 80; i < 100; i++)
        {
            Assert.True(result[i].Value > 0, $"DMX should be positive in uptrend at index {i}, got {result[i].Value}");
        }

        // Create a synthetic downtrend
        bars = new TBarSeries();
        time = DateTime.UtcNow;
        price = 200;
        for (int i = 0; i < 100; i++)
        {
            bars.Add(time, price, price + 1, price - 2, price - 1, 1000);
            time = time.AddMinutes(1);
            price -= 1.0; // Steady downtrend
        }

        dmx = new Dmx(14);
        result = dmx.Update(bars);

        // Check the last few values, they should be negative
        for (int i = 80; i < 100; i++)
        {
            Assert.True(result[i].Value < 0, $"DMX should be negative in downtrend at index {i}, got {result[i].Value}");
        }
        
        _output.WriteLine("DMX trend direction validated successfully");
    }
}
