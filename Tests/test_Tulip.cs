using Xunit;
using Tulip;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

#pragma warning disable S1944, S2053, S2222, S2259, S2583, S2589, S3329, S3655, S3900, S3949, S3966, S4158, S4347, S5773, S6781

namespace QuanTAlib;

public class TulipTests
{
    private readonly GbmFeed feed;
    private readonly RandomNumberGenerator rng;
    private readonly double range;
    private readonly int iterations;
    private readonly double[] data;
    private readonly double[] outdata;
    private readonly int skip;

    public TulipTests()
    {
        rng = RandomNumberGenerator.Create();
        feed = new(sigma: 0.5, mu: 0.0);
        range = 1e-9;
        feed.Add(10000);
        iterations = 3;
        skip = 500;
        data = feed.Close.v.ToArray();
        outdata = new double[data.Count()];
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

            double[][] arrin = [data];
            double[][] arrout = [outdata];
            Tulip.Indicators.sma.Run(inputs: arrin, options: [period], outputs: arrout);
            Assert.Equal(QL.Length, arrout[0].Length);
            for (int i = QL.Length - 1; i > skip; i--)
            {
                double QL_item = QL[i].Value;
                double TU = i < period - 1 ? double.NaN : arrout[0][i - period + 1];
                Assert.InRange(TU - QL_item, -range, range);
            }
        }
    }

    [Fact]
    public void EMA()
    {
        for (int run = 0; run < iterations; run++)
        {
            int period = GetRandomNumber(5, 35);
            Ema ma = new(period, useSma: false);
            TSeries QL = new();
            foreach (TBar item in feed)
            { QL.Add(ma.Calc(new TValue(item.Time, item.Close))); }

            double[][] arrin = [data];
            double[][] arrout = [outdata];
            Tulip.Indicators.ema.Run(inputs: arrin, options: [period], outputs: arrout);

            Assert.Equal(QL.Length, arrout[0].Length);
            for (int i = QL.Length - 1; i > skip * 2; i--)  //Initial Tulip Ema value is (wrongly) set to the first input value - therefore large skip
            {
                double QL_item = QL[i].Value;
                double TU = arrout[0][i];
                Assert.True(Math.Abs(TU - QL_item) <= range, $"Assertion failed at index {i} for period {period}: TU = {TU}, QL_item = {QL_item}, delta = {TU - QL_item}");
            }
        }
    }
}