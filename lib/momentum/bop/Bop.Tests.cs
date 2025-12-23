using Xunit;
using System;

namespace QuanTAlib.Tests;

public class BopTests
{
    [Fact]
    public void BasicCalculation()
    {
        var bop = new Bop();
        var bar = new TBar(DateTime.UtcNow, 10, 20, 5, 15, 100);
        // Open=10, High=20, Low=5, Close=15
        // Range = 20 - 5 = 15
        // Diff = 15 - 10 = 5
        // BOP = 5 / 15 = 0.3333...

        var result = bop.Update(bar);
        Assert.Equal(1.0 / 3.0, result.Value, 6);
    }

    [Fact]
    public void HighEqualsLow()
    {
        var bop = new Bop();
        var bar = new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100);
        // Range = 0
        // BOP should be 0

        var result = bop.Update(bar);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void BuyersDominate()
    {
        var bop = new Bop();
        var bar = new TBar(DateTime.UtcNow, 10, 20, 10, 20, 100);
        // Open=10, High=20, Low=10, Close=20
        // Range = 10
        // Diff = 10
        // BOP = 1

        var result = bop.Update(bar);
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public void SellersDominate()
    {
        var bop = new Bop();
        var bar = new TBar(DateTime.UtcNow, 20, 20, 10, 10, 100);
        // Open=20, High=20, Low=10, Close=10
        // Range = 10
        // Diff = -10
        // BOP = -1

        var result = bop.Update(bar);
        Assert.Equal(-1, result.Value);
    }

    [Fact]
    public void BatchMatchesStreaming()
    {
        var bop = new Bop();
        var bars = new TBarSeries();
        bars.Add(new TBar(DateTime.UtcNow, 10, 20, 5, 15, 100));
        bars.Add(new TBar(DateTime.UtcNow.AddMinutes(1), 15, 25, 10, 20, 100));

        var batchResult = Bop.Update(bars);
        
        bop.Reset();
        var streamResult1 = bop.Update(bars[0]);
        var streamResult2 = bop.Update(bars[1]);

        Assert.Equal(batchResult[0].Value, streamResult1.Value);
        Assert.Equal(batchResult[1].Value, streamResult2.Value);
    }
    
    [Fact]
    public void SpanMatchesBatch()
    {
        var bars = new TBarSeries();
        bars.Add(new TBar(DateTime.UtcNow, 10, 20, 5, 15, 100));
        bars.Add(new TBar(DateTime.UtcNow.AddMinutes(1), 15, 25, 10, 20, 100));
        
        var batchResult = Bop.Batch(bars);
        
        var output = new double[bars.Count];
        Bop.Calculate(bars.Open.Values, bars.High.Values, bars.Low.Values, bars.Close.Values, output);
        
        Assert.Equal(batchResult[0].Value, output[0]);
        Assert.Equal(batchResult[1].Value, output[1]);
    }
}
