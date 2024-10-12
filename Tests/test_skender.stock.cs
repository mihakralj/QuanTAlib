using Xunit;
using Skender.Stock.Indicators;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

#pragma warning disable S1944, S2053, S2222, S2259, S2583, S2589, S3329, S3655, S3900, S3949, S3966, S4158, S4347, S5773, S6781

namespace QuanTAlib.Tests;

public class SkenderTests
{
    private readonly TBarSeries bars;
    private readonly GbmFeed feed;
    private readonly RandomNumberGenerator rng;
    private readonly double range;
    private int period;
    private readonly int iterations = 3; // Initialized directly at declaration
    private readonly IEnumerable<Quote> quotes;

    public SkenderTests()
    {
        rng = RandomNumberGenerator.Create();
        feed = new(sigma: 0.5, mu: 0.0);
        bars = new(feed);
        range = 1e-9;
        feed.Add(10000);
        quotes = bars.Select(q => new Quote
        {
            Date = q.Time,
            Open = (decimal)q.Open,
            High = (decimal)q.High,
            Low = (decimal)q.Low,
            Close = (decimal)q.Close,
            Volume = (decimal)q.Volume
        });
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
            period = GetRandomNumber(5, 55);
            Sma ma = new(period);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            var SK = quotes.GetSma(lookbackPeriods: period).Select(i => i.Sma.Null2NaN()!);
            Assert.Equal(QL.Length, SK.Count());
            for (int i = QL.Length - 1; i > period; i--)
            {
                Assert.InRange(SK.ElementAt(i) - QL[i].Value, -range, range);
            }
        }
    }

    [Fact]
    public void SMAEMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            period = GetRandomNumber(5, 55);
            Ema ma = new(period, useSma: true);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            var SK = quotes.GetEma(lookbackPeriods: period).Select(i => i.Ema.Null2NaN()!);
            Assert.Equal(QL.Length, SK.Count());
            for (int i = QL.Length - 1; i > period; i--)
            {
                Assert.InRange(SK.ElementAt(i) - QL[i].Value, -range, range);
            }
        }
    }

    [Fact]
    public void EMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            period = GetRandomNumber(5, 55);
            Ema ma = new(period, useSma: false);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            var SK = quotes.GetEma(lookbackPeriods: period).Select(i => i.Ema.Null2NaN()!);
            Assert.Equal(QL.Length, SK.Count());
            for (int i = QL.Length - 1; i > QL.Length - 500; i--)
            {
                Assert.InRange(SK.ElementAt(i) - QL[i].Value, -range, range);
            }
        }
    }

    [Fact]
    public void DEMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            period = GetRandomNumber(5, 55);
            Dema ma = new(period);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            var SK = quotes.GetDema(lookbackPeriods: period).Select(i => i.Dema.Null2NaN()!);
            Assert.Equal(QL.Length, SK.Count());
            for (int i = QL.Length - 1; i > QL.Length - 500; i--)
            {
                Assert.InRange(SK.ElementAt(i) - QL[i].Value, -range, range);
            }
        }
    }

    [Fact]
    public void TEMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            period = GetRandomNumber(5, 55);
            Tema ma = new(period);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            var SK = quotes.GetTema(lookbackPeriods: period).Select(i => i.Tema.Null2NaN()!);
            Assert.Equal(QL.Length, SK.Count());
            for (int i = QL.Length - 1; i > QL.Length - 500; i--)
            {
                Assert.InRange(SK.ElementAt(i) - QL[i].Value, -range, range);
            }
        }
    }

    [Fact]
    public void SMAConvolution()
    {
        for (int run = 0; run < iterations; run++)
        {
            period = GetRandomNumber(5, 55);
            double[] kernel = Enumerable.Repeat(1.0, period).ToArray();
            Convolution ma = new(kernel);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            var SK = quotes.GetSma(lookbackPeriods: period).Select(i => i.Sma.Null2NaN()!);
            Assert.Equal(QL.Length, SK.Count());
            for (int i = QL.Length - 1; i > period; i--)
            {
                Assert.InRange(SK.ElementAt(i) - QL[i].Value, -range, range);
            }
        }
    }

    [Fact]
    public void WMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            period = GetRandomNumber(5, 55);
            Wma ma = new(period);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            var SK = quotes.GetWma(lookbackPeriods: period).Select(i => i.Wma.Null2NaN()!);
            Assert.Equal(QL.Length, SK.Count());
            for (int i = QL.Length - 1; i > period + 2; i--)
            {
                Assert.InRange(SK.ElementAt(i) - QL[i].Value, -range, range);
            }
        }
    }

    [Fact]
    public void HMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            period = GetRandomNumber(5, 55);
            Hma ma = new(period);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            var SK = quotes.GetHma(lookbackPeriods: period).Select(i => i.Hma.Null2NaN()!);
            Assert.Equal(QL.Length, SK.Count());
            for (int i = QL.Length - 1; i > period + 5; i--)
            {
                Assert.InRange(SK.ElementAt(i) - QL[i].Value, -range, range);
            }
        }
    }

    [Fact]
    public void EPMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            period = GetRandomNumber(5, 55);
            Epma ma = new(period);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            var SK = quotes.GetEpma(lookbackPeriods: period).Select(i => i.Epma.Null2NaN()!);
            Assert.Equal(QL.Length, SK.Count());
            for (int i = QL.Length - 1; i > period + 5; i--)
            {
                Assert.InRange(SK.ElementAt(i) - QL[i].Value, -range, range);
            }
        }
    }

    [Fact]
    public void ALMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            period = GetRandomNumber(5, 55);
            Alma ma = new(period, offset: 0.85, sigma: 6);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            var SK = quotes.GetAlma(lookbackPeriods: period).Select(i => i.Alma.Null2NaN()!);
            Assert.Equal(QL.Length, SK.Count());
            for (int i = QL.Length - 1; i > period; i--)
            {
                Assert.InRange(SK.ElementAt(i) - QL[i].Value, -range, range);
            }
        }
    }

    [Fact]
    public void T3()
    {
        for (int run = 0; run < iterations; run++)
        {
            period = GetRandomNumber(5, 55);
            T3 ma = new(period, vfactor: 0.7, useSma: false);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            var SK = quotes.GetT3(lookbackPeriods: period, volumeFactor: 0.7).Select(i => i.T3.Null2NaN()!);
            Assert.Equal(QL.Length, SK.Count());
            for (int i = QL.Length - 1; i > period; i--)
            {
                Assert.InRange(SK.ElementAt(i) - QL[i].Value, -range, range);
            }
        }
    }

    [Fact]
    public void SMMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            period = GetRandomNumber(5, 55);
            Smma ma = new(period);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            var SK = quotes.GetSmma(lookbackPeriods: period).Select(i => i.Smma.Null2NaN()!);
            Assert.Equal(QL.Length, SK.Count());
            for (int i = QL.Length - 1; i > period; i--)
            {
                Assert.InRange(SK.ElementAt(i) - QL[i].Value, -range, range);
            }
        }
    }

    [Fact]
    public void KAMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            period = GetRandomNumber(5, 55);
            Kama ma = new(period);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            var SK = quotes.GetKama(erPeriods: period).Select(i => i.Kama.Null2NaN()!);
            Assert.Equal(QL.Length, SK.Count());
            for (int i = QL.Length - 1; i > period; i--)
            {
                Assert.InRange(SK.ElementAt(i) - QL[i].Value, -range, range);
            }
        }
    }

    [Fact]
    public void MAMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            Mama ma = new(fastLimit: 0.5, slowLimit: 0.05);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            var SK = quotes.Select(q => (q.Date, (double)q.Close))
                .GetMama(fastLimit: 0.5, slowLimit: 0.05)
                .Select(i => i.Mama.Null2NaN()!);
            Assert.Equal(QL.Length, SK.Count());
            for (int i = QL.Length - 1; i > 100; i--)
            {
                Assert.InRange(SK.ElementAt(i) - QL[i].Value, -range, range);
            }
        }
    }

    [Fact]
    public void MGDI()
    {
        for (int run = 0; run < iterations; run++)
        {
            period = GetRandomNumber(5, 55);
            Mgdi ma = new(period: period);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }
            var SK = quotes.Select(q => (q.Date, (double)q.Close))
                .GetDynamic(lookbackPeriods: period)
                .Select(i => i.Dynamic.Null2NaN()!);
            Assert.Equal(QL.Length, SK.Count());
            for (int i = QL.Length - 1; i > period + 5; i--)
            {
                Assert.InRange(SK.ElementAt(i) - QL[i].Value, -range, range);
            }
        }
    }

    [Fact]
    public void ATR()
    {
        for (int run = 0; run < iterations; run++)
        {
            period = GetRandomNumber(5, 55);
            Atr ma = new(period: period);
            TSeries QL = new();
            foreach (TBar item in bars) { QL.Add(ma.Calc(item)); }

            var atrValues = quotes.GetAtr(lookbackPeriods: period).Select(i => i.Atr.Null2NaN()!);
            const int AdditionalPeriods = 500;

            for (int i = QL.Length - 1; i > period + AdditionalPeriods; i--)
            {
                Assert.InRange(atrValues.ElementAt(i) - QL[i].Value, -range, range);
            }
        }
    }
}
