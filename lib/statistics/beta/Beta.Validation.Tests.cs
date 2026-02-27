using Skender.Stock.Indicators;
using TALib;

namespace QuanTAlib.Tests;

public sealed class BetaValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public BetaValidationTests()
    {
        _data = new ValidationTestData();
    }

    public void Dispose()
    {
        _data.Dispose();
    }

    [Fact]
    public void Validate_Against_Skender()
    {
        // Generate Market Data (use existing Data)
        var marketQuotes = _data.Data;

        // Generate Asset Data correlated to Market
        // Asset Returns = 1.5 * Market Returns + Noise
        var assetQuotes = new List<TBar>();
        double assetPrice = 100;
        const double targetBeta = 1.5;

        // Use GBM for noise generation (sigma=0.2 gives ~0.0006 per step noise which matches original random noise level)
        var noiseGbm = new GBM(startPrice: 100, mu: 0, sigma: 0.2, seed: 777);

        assetQuotes.Add(new TBar(marketQuotes[0].Time, assetPrice, assetPrice, assetPrice, assetPrice, 1000));

        for (int i = 1; i < marketQuotes.Count; i++)
        {
            double marketReturn = (marketQuotes[i].Value - marketQuotes[i - 1].Value) / marketQuotes[i - 1].Value;

            // Get noise from GBM return
            var noiseBar = noiseGbm.Next();
            double noise = (noiseBar.Close - noiseBar.Open) / noiseBar.Open;

            double assetReturn = targetBeta * marketReturn + noise;

            assetPrice *= (1 + assetReturn);
            assetQuotes.Add(new TBar(marketQuotes[i].Time, assetPrice, assetPrice, assetPrice, assetPrice, 1000));
        }

        // Skender
        // Skender expects IEnumerable<Quote>
        var skenderMarket = marketQuotes.Select(x => new Quote { Date = x.AsDateTime, Close = (decimal)x.Value }).ToList();
        var skenderAsset = assetQuotes.Select(x => new Quote { Date = x.AsDateTime, Close = (decimal)x.Close }).ToList();

        int period = 20;
        var skenderBeta = skenderAsset.GetBeta(skenderMarket, period).ToList();

        // QuanTAlib
        var beta = new Beta(period);
        var qlBeta = new List<double>();

        for (int i = 0; i < marketQuotes.Count; i++)
        {
            var result = beta.Update(assetQuotes[i].Close, marketQuotes[i].Value);
            qlBeta.Add(result.Value);
        }

        // Compare
        // Skip warmup period. Skender Beta needs period returns, so period+1 prices?
        // Skender results align with input quotes.
        // First valid value should be at index 'period'.

        // We verify the last 100 values
        int count = qlBeta.Count;
        int skip = period + 5; // Safety margin

        for (int i = skip; i < count; i++)
        {
            double sk = (skenderBeta[i].Beta ?? 0);
            double ql = qlBeta[i];

            // Skender might return null/0 for warmup.
            if (Math.Abs(sk) > 1e-10)
            {
                Assert.Equal(sk, ql, ValidationHelper.DefaultTolerance);
            }
        }
    }

    [Fact]
    public void Validate_Against_Talib()
    {
        // TALib Beta takes two price series (e.g. stock vs market returns via price series).
        // TALib.Functions.Beta(stockPrices, marketPrices, range, output, outRange, period)
        // Internally computes beta from price returns within each rolling window.
        //
        // Note: TALib Beta uses a different return calculation (price[i]/price[i-1] - 1)
        // and a different beta formula (covariance/variance from returns) than Skender.
        // QuanTAlib Beta matches Skender (covariance of returns / variance of market returns).
        // Direct numeric equality with TALib is not expected; we verify structural properties.

        var marketQuotes = _data.Data;

        // Build correlated asset prices
        var noiseGbm = new GBM(startPrice: 100, mu: 0, sigma: 0.2, seed: 999);
        double assetPrice = 100;
        const double targetBeta = 1.2;

        var assetPrices = new double[marketQuotes.Count];
        var marketPrices = new double[marketQuotes.Count];
        assetPrices[0] = assetPrice;
        marketPrices[0] = marketQuotes[0].Value;

        for (int i = 1; i < marketQuotes.Count; i++)
        {
            double mktReturn = (marketQuotes[i].Value - marketQuotes[i - 1].Value) / marketQuotes[i - 1].Value;
            var noiseBar = noiseGbm.Next();
            double noise = (noiseBar.Close - noiseBar.Open) / noiseBar.Open;
            double astReturn = targetBeta * mktReturn + noise * 0.1;
            assetPrice *= (1 + astReturn);
            assetPrices[i] = assetPrice;
            marketPrices[i] = marketQuotes[i].Value;
        }

        const int period = 20;

        // TALib Beta
        double[] taOut = new double[marketPrices.Length];
        var retCode = Functions.Beta<double>(
            assetPrices.AsSpan(), marketPrices.AsSpan(),
            0..^0, taOut, out var outRange, period);
        Assert.Equal(Core.RetCode.Success, retCode);

        (int offset, int length) = outRange.GetOffsetAndLength(taOut.Length);

        // Verify TALib produces finite values
        Assert.True(length > 0, "TALib Beta produced no output");
        for (int j = 0; j < length; j++)
        {
            Assert.True(double.IsFinite(taOut[j]),
                $"TALib Beta[{j}] = {taOut[j]} is not finite");
        }

        // QuanTAlib Beta
        var beta = new Beta(period);
        var qlBetaArr = new double[marketQuotes.Count];
        for (int i = 0; i < marketQuotes.Count; i++)
        {
            qlBetaArr[i] = beta.Update(assetPrices[i], marketPrices[i]).Value;
        }

        // Both should produce finite values after warmup
        for (int i = period + 5; i < marketQuotes.Count; i++)
        {
            Assert.True(double.IsFinite(qlBetaArr[i]), $"QuanTAlib Beta[{i}] is not finite");
        }

        // Sign agreement: positively correlated asset → >60% positive betas from both
        int taPositive = 0;
        int qlPositive = 0;
        for (int j = 0; j < length; j++)
        {
            int qi = j + offset;
            if (taOut[j] > 0) { taPositive++; }
            if (qlBetaArr[qi] > 0) { qlPositive++; }
        }
        Assert.True(taPositive > length * 0.6, $"TALib Beta positive rate {taPositive}/{length} < 60%");
        Assert.True(qlPositive > length * 0.6, $"QuanTAlib Beta positive rate {qlPositive}/{length} < 60%");
    }
}
