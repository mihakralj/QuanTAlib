using Xunit;
using QuanTAlib.Tests;

namespace QuanTAlib;

public class AdoscTests
{
    private readonly GBM _gbm;
    private readonly TBarSeries _bars;

    public AdoscTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        _bars = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Adosc(fastPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Adosc(slowPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Adosc(fastPeriod: 10, slowPeriod: 5));
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var adosc = new Adosc(3, 10);
        var result = adosc.Update(_bars[0]);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Properties_Accessible()
    {
        var adosc = new Adosc(3, 10);
        Assert.Equal("Adosc(3,10)", adosc.Name);
        Assert.False(adosc.IsHot);
        Assert.Equal(10, adosc.WarmupPeriod);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var adosc = new Adosc(3, 10);
        adosc.Update(_bars[0], isNew: true);
        adosc.Update(_bars[1], isNew: true);
        Assert.NotEqual(adosc.Last.Time, _bars[0].Time);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var adosc = new Adosc(3, 10);
        adosc.Update(_bars[0], isNew: true);
        var firstResult = adosc.Last.Value;

        var modifiedBar = new TBar(_bars[0].Time, _bars[0].Open, _bars[0].High, _bars[0].Low, _bars[0].Close * 1.1, _bars[0].Volume);
        adosc.Update(modifiedBar, isNew: false);

        Assert.NotEqual(firstResult, adosc.Last.Value);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var adosc = new Adosc(3, 10);
        adosc.Update(_bars[0]);
        adosc.Reset();
        Assert.False(adosc.IsHot);
        Assert.Equal(0, adosc.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var adosc = new Adosc(3, 10);
        for (int i = 0; i < 20; i++)
        {
            adosc.Update(_bars[i]);
        }
        Assert.True(adosc.IsHot);
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        var adosc = new Adosc(3, 10);
        var batchResult = Adosc.Batch(_bars, 3, 10);

        var streamResult = new List<double>();
        foreach (var bar in _bars)
        {
            streamResult.Add(adosc.Update(bar).Value);
        }

        var spanOutput = new double[_bars.Count];
        Adosc.Calculate(_bars.High.Values, _bars.Low.Values, _bars.Close.Values, _bars.Volume.Values, spanOutput, 3, 10);

        for (int i = 0; i < _bars.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamResult[i], 1e-9);
            Assert.Equal(batchResult[i].Value, spanOutput[i], 1e-6);
        }
    }
}
