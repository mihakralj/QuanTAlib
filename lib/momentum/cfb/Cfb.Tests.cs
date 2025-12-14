using System;
using System.Collections.Generic;
using Xunit;

namespace QuanTAlib;

public class CfbTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var cfb = new Cfb();
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var data = bars.Close;

        for (int i = 0; i < data.Count; i++)
        {
            cfb.Update(new TValue(data.Times[i], data.Values[i]));
        }

        Assert.True(cfb.Last.Value >= 1.0);
    }

    [Fact]
    public void PerfectTrend_IncreasesCfb()
    {
        // Use small lengths for easier testing
        int[] lengths = { 4, 8, 12 };
        var cfb = new Cfb(lengths);

        // Feed a perfect uptrend
        for (int i = 0; i < 50; i++)
        {
            cfb.Update(new TValue(DateTime.UtcNow, i));
        }

        Assert.Equal(8.0, cfb.Last.Value);
    }

    [Fact]
    public void FlatLine_ReturnsOne()
    {
        var cfb = new Cfb();
        for (int i = 0; i < 100; i++)
        {
            cfb.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        // NetMove is 0. TotalMove is 0.
        // Ratio = 0/0 -> NaN?
        // Code handles TotalMove < 1e-12 by skipping.
        // So no lengths qualify.
        // Decay logic kicks in.
        // Should decay to 1.0.

        Assert.Equal(1.0, cfb.Last.Value);
    }

    [Fact]
    public void ZigZag_ReturnsOne()
    {
        var cfb = new Cfb(new int[] { 4, 8 });
        // 100, 101, 100, 101...
        // NetMove(4) = Abs(100 - 100) = 0. Ratio = 0.
        // NetMove(8) = 0. Ratio = 0.
        
        for (int i = 0; i < 100; i++)
        {
            double price = 100 + (i % 2);
            cfb.Update(new TValue(DateTime.UtcNow, price));
        }

        Assert.Equal(1.0, cfb.Last.Value);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var cfb = new Cfb();
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var data = new List<TValue>();
        for (int i = 0; i < bars.Count; i++)
        {
            data.Add(new TValue(bars.Close.Times[i], bars.Close.Values[i]));
        }

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            cfb.Update(data[i]);
        }

        // Update with 100th point (isNew=true)
        var val1 = cfb.Update(data[99], true);

        // Update with modified 100th point (isNew=false)
        var modified = new TValue(data[99].Time, data[99].Value + 1.0);
        var val2 = cfb.Update(modified, false);

        // Create new instance and feed up to modified
        var cfb2 = new Cfb();
        for (int i = 0; i < 99; i++)
        {
            cfb2.Update(data[i]);
        }
        var val3 = cfb2.Update(modified, true);

        Assert.Equal(val3.Value, val2.Value);
    }

    [Fact]
    public void StaticCalculate_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var cfb = new Cfb();
        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(cfb.Update(new TValue(series.Times[i], series.Values[i])).Value);
        }

        var staticResults = Cfb.Calculate(series);

        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i]);
        }
    }

    [Fact]
    public void SpanCalculate_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] values = bars.Close.Values.ToArray();

        var cfb = new Cfb();
        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(cfb.Update(new TValue(bars.Close.Times[i], bars.Close.Values[i])).Value);
        }

        double[] spanResults = new double[bars.Count];
        Cfb.Calculate(values, spanResults);

        for (int i = 0; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], spanResults[i]);
        }
    }
}
