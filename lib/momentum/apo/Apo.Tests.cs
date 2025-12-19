using Xunit;
using System;

namespace QuanTAlib.Tests;

public class ApoTests
{
    private readonly GBM _gbm;

    public ApoTests()
    {
        _gbm = new GBM();
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Apo(fastPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Apo(slowPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Apo(fastPeriod: 26, slowPeriod: 12)); // Fast >= Slow
    }

    [Fact]
    public void Update_ReturnsValidValue()
    {
        var apo = new Apo(12, 26);
        var result = apo.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(0, result.Value); // First value: EMA(100) - EMA(100) = 0
    }

    [Fact]
    public void IsHot_BecomesTrue()
    {
        var apo = new Apo(12, 26);
        for (int i = 0; i < 100; i++)
        {
            apo.Update(new TValue(DateTime.UtcNow, 100));
        }
        Assert.True(apo.IsHot);
    }

    [Fact]
    public void Batch_Matches_Streaming()
    {
        var source = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var tSeries = new TSeries(source.Close.Count);
        for (int i = 0; i < source.Close.Count; i++)
        {
            tSeries.Add(source.Close[i]);
        }

        var apoBatch = Apo.Batch(tSeries, 12, 26);

        var apoStream = new Apo(12, 26);
        var streamResults = new List<double>();
        for (int i = 0; i < tSeries.Count; i++)
        {
            streamResults.Add(apoStream.Update(tSeries[i]).Value);
        }

        Assert.Equal(apoBatch.Count, streamResults.Count);
        for (int i = 0; i < apoBatch.Count; i++)
        {
            Assert.Equal(apoBatch[i].Value, streamResults[i], precision: 9);
        }
    }
}
