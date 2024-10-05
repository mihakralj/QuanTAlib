using Xunit;
using TALib;
using QuanTAlib;

public class TAlibTests
{
    private readonly TBarSeries bars;
    private readonly GbmFeed feed;
    private readonly Random rnd;
    private readonly double range;
        private readonly int iterations;
    private readonly double[] data;
    private readonly double[] TALIB;


    public TAlibTests()
    {
        rnd = new((int)DateTime.Now.Ticks);
        feed = new(sigma: 0.5, mu: 0.0);
        bars = new(feed);
        range = 1e-9;
        feed.Add(10000);
        iterations = 3;
        data = feed.Close.v.ToArray();
        TALIB = new double[data.Count()];

    }

    [Fact]
    public void SMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            int period = rnd.Next(50) + 5;
            Sma ma = new(period);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            Core.Sma(data, 0, QL.Length - 1, TALIB, out int outBegIdx, out _, period);
            Assert.Equal(QL.Length, TALIB.Count());
            for (int i = QL.Length - 1; i > period; i--)
            {
                double TL = i < outBegIdx ? double.NaN : TALIB[i - outBegIdx];
                Assert.InRange(TALIB[i - outBegIdx] - QL[i].Value, -range, range);
            }
        }
    }

    [Fact]
    public void EMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            int period = rnd.Next(50) + 5;
            Ema ma = new(period, useSma: true);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            Core.Ema(data, 0, QL.Length - 1, TALIB, out int outBegIdx, out _, period);
            Assert.Equal(QL.Length, TALIB.Count());
            for (int i = QL.Length - 1; i > period; i--)
            {
                double TL = i < outBegIdx ? double.NaN : TALIB[i - outBegIdx];
                Assert.InRange(TALIB[i - outBegIdx] - QL[i].Value, -range, range);
            }
        }
    }

    [Fact]
    public void DEMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            int period = rnd.Next(50) + 5;
            Dema ma = new(period);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            Core.Dema(data, 0, QL.Length - 1, TALIB, out int outBegIdx, out _, period);
            Assert.Equal(QL.Length, TALIB.Length);
            for (int i = QL.Length - 1; i > period * 20; i--)
            {
                double TL = i < outBegIdx ? double.NaN : TALIB[i - outBegIdx];
                Assert.InRange(TALIB[i - outBegIdx] - QL[i].Value, -range, range);
            }
        }
    }

    [Fact]
    public void TEMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            int period = rnd.Next(50) + 5;
            Tema ma = new(period);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            Core.Tema(data, 0, QL.Length - 1, TALIB, out int outBegIdx, out _, period);
            Assert.Equal(QL.Length, TALIB.Length);
            for (int i = QL.Length - 1; i > period * 20; i--)
            {
                double TL = i < outBegIdx ? double.NaN : TALIB[i - outBegIdx];
                Assert.InRange(TALIB[i - outBegIdx] - QL[i].Value, -range, range);
            }
        }
    }

//TODO fix WMA
/*
    [Fact]
    public void WMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            period = rnd.Next(50) + 5;
            Wma ma = new(period);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            Core.Wma(data, 0, QL.Length - 1, TALIB, out int outBegIdx, out _, period);
            Assert.Equal(QL.Length, TALIB.Count());
            for (int i = QL.Length - 1; i > period*3; i--)
            {
                double TL = i < outBegIdx ? double.NaN : TALIB[i - outBegIdx];
                Assert.InRange(TALIB[i - outBegIdx] - QL[i].Value, -range, range);
            }
        }
    }
    */

    [Fact]
    public void T3()
    {
        for (int run = 0; run < iterations; run++)
        {
            int period = rnd.Next(50) + 5;
            T3 ma = new(period, vfactor: 0.7, useSma: false);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            Core.T3(data, 0, QL.Length - 1, TALIB, out int outBegIdx, out _, optInTimePeriod: period, optInVFactor: 0.7);
            Assert.Equal(QL.Length, TALIB.Length);
            for (int i = QL.Length - 1; i > period * 20; i--)
            {
                double TL = i < outBegIdx ? double.NaN : TALIB[i - outBegIdx];
                Assert.InRange(TALIB[i - outBegIdx] - QL[i].Value, -range, range);
            }
        }
    }

}