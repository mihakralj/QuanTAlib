using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Skender.Stock.Indicators;

namespace QuanTAlib.Tests;

public class BetaValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public BetaValidationTests()
    {
        _data = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _data.Dispose();
        }
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
        double targetBeta = 1.5;
        
        // Use GBM for noise generation (sigma=0.2 gives ~0.0006 per step noise which matches original random noise level)
        var noiseGbm = new GBM(startPrice: 100, mu: 0, sigma: 0.2, seed: 777);

        assetQuotes.Add(new TBar(marketQuotes[0].Time, assetPrice, assetPrice, assetPrice, assetPrice, 1000));

        for (int i = 1; i < marketQuotes.Count; i++)
        {
            double marketReturn = (marketQuotes[i].Value - marketQuotes[i-1].Value) / marketQuotes[i-1].Value;
            
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
            if (sk != 0)
            {
                Assert.Equal(sk, ql, ValidationHelper.DefaultTolerance);
            }
        }
    }
}
