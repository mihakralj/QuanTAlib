using Xunit;

namespace QuanTAlib.Tests;

public sealed class LoessTests
{
    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Loess(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Loess(0));

        var loess = new Loess(5);
        Assert.NotNull(loess);
        Assert.Equal(5, loess.Period);
    }

    [Fact]
    public void Constructor_AdjustsEvenPeriod()
    {
        // Should adjust 6 to 7 (Round Up to next odd number)
        var loess = new Loess(6);
        Assert.Equal(7, loess.Period);
        Assert.Contains("Loess(7)", loess.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var loess = new Loess(5);
        var result = loess.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(100.0, result.Value); // First value fallback
        Assert.Equal(result.Value, loess.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var loess = new Loess(3);

        loess.Update(new TValue(DateTime.UtcNow, 1));
        Assert.False(loess.IsHot);

        loess.Update(new TValue(DateTime.UtcNow, 2));
        Assert.False(loess.IsHot);

        loess.Update(new TValue(DateTime.UtcNow, 3));
        Assert.True(loess.IsHot);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var loess = new Loess(5);

        // Feed 4 values
        for (int i = 0; i < 4; i++)
        {
            loess.Update(new TValue(DateTime.UtcNow, i), isNew: true);
        }

        // 5th value
        loess.Update(new TValue(DateTime.UtcNow, 10), isNew: true);
        double val1 = loess.Last.Value;

        // 6th value
        loess.Update(new TValue(DateTime.UtcNow, 20), isNew: true);
        double val2 = loess.Last.Value;

        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var loess = new Loess(3);

        // 1, 2
        loess.Update(new TValue(DateTime.UtcNow, 1), isNew: true);
        loess.Update(new TValue(DateTime.UtcNow, 2), isNew: true);

        // New bar: 3
        loess.Update(new TValue(DateTime.UtcNow, 3), isNew: true);
        double val1 = loess.Last.Value;

        // Update current bar: 3 -> 4
        loess.Update(new TValue(DateTime.UtcNow, 4), isNew: false);
        double val2 = loess.Last.Value;

        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        var data = series.Values.ToArray();

        // 1. TSeries Batch
        var loessBatch = new Loess(period);
        var resBatch = loessBatch.Update(series);

        // 2. Span Batch
        var resSpan = new double[data.Length];
        Loess.Calculate(data.AsSpan(), resSpan.AsSpan(), period);

        // 3. Streaming
        var loessStream = new Loess(period);
        var resStream = new List<double>();
        foreach (var item in series)
        {
            resStream.Add(loessStream.Update(item).Value);
        }

        for (int i = 0; i < data.Length; i++)
        {
            Assert.Equal(resBatch[i].Value, resSpan[i], 1e-9);
            Assert.Equal(resBatch[i].Value, resStream[i], 1e-9);
        }
    }

    [Fact]
    public void Handles_NaN()
    {
        // Loess implementation handles NaN robustly by using last finite value
        var loess = new Loess(3);

        loess.Update(new TValue(DateTime.UtcNow, 1));
        loess.Update(new TValue(DateTime.UtcNow, 2));
        var res = loess.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.False(double.IsNaN(res.Value));
        Assert.True(double.IsFinite(res.Value));
    }
}
