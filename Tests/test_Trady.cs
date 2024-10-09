using Xunit;
using Trady.Analysis.Indicator;
using Trady.Core;
using Trady.Core.Infrastructure;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace QuanTAlib;

[SuppressMessage("Security", "SCS0005:Weak random number generator.", Justification = "Acceptable for tests")]
public class TradyTests
{
    private readonly TBarSeries bars;
    private readonly GbmFeed feed;
    private readonly RandomNumberGenerator rng;
    private readonly double range;
    private readonly int iterations;
    private readonly int skip;
    private readonly IEnumerable<IOhlcv> Candles;

    public TradyTests()
    {
        rng = RandomNumberGenerator.Create();
        feed = new(sigma: 0.5, mu: 0.0);
        bars = new(feed);
        range = 1e-9;
        feed.Add(10000);
        iterations = 3;
        skip = 500;
        Candles = bars.Select(bar => new Candle(
            bar.Time,
            (decimal)bar.Open,
            (decimal)bar.High,
            (decimal)bar.Low,
            (decimal)bar.Close,
            (decimal)bar.Volume
        )).ToList();
    }

    private int GetRandomNumber(int minValue, int maxValue)
    {
        byte[] randomBytes = new byte[4];
        rng.GetBytes(randomBytes);
        int randomInt = BitConverter.ToInt32(randomBytes, 0);
        return Math.Abs(randomInt % (maxValue - minValue)) + minValue;
    }

    [Fact]
    public void SMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            int period = GetRandomNumber(5, 55);
            Sma ma = new(period);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }

            var Trady = new SimpleMovingAverage(Candles, period)
                 .Compute()
                 .Select(result => new
                 {
                     Date = result.DateTime,
                     Value = result.Tick.HasValue ? (double)result.Tick.Value : double.NaN
                 })
                 .ToList();

            Assert.Equal(QL.Length, Trady.Count);
            for (int i = QL.Length - 1; i > skip; i--)
            {
                double QL_item = QL[i].Value;
                double Tr_item = Trady[i].Value;
                Assert.InRange(Tr_item - QL_item, -range, range);
            }
        }
    }

    [Fact]
    public void EMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            int period = GetRandomNumber(5, 55);
            Ema ma = new(period);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }

            var Trady = new ExponentialMovingAverage(Candles, period)
                 .Compute()
                 .Select(result => new
                 {
                     Date = result.DateTime,
                     Value = result.Tick.HasValue ? (double)result.Tick.Value : double.NaN
                 })
                 .ToList();

            Assert.Equal(QL.Length, Trady.Count);
            for (int i = QL.Length - 1; i > skip * 2; i--)
            {
                double QL_item = QL[i].Value;
                double Tr_item = Trady[i].Value;
                Assert.InRange(Tr_item - QL_item, -range, range);
            }
        }
    }
}