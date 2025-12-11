using Skender.Stock.Indicators;

namespace QuanTAlib.Tests;

public class RmaValidationTests
{
    [Fact]
    public void Rma_Matches_Skender_Smma()
    {
        // Arrange
        int period = 14;
        int length = 1000;
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(length, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        
        // QuanTAlib RMA
        var rma = new Rma(period);
        var quantalibResults = new TSeries();
        foreach (var bar in bars)
        {
            quantalibResults.Add(rma.Update(new TValue(bar.Time, bar.Close)));
        }

        // Skender SMMA
        var quotes = bars.Select(b => new Quote
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = (decimal)b.Open,
            High = (decimal)b.High,
            Low = (decimal)b.Low,
            Close = (decimal)b.Close,
            Volume = (decimal)b.Volume
        }).ToList();

        var skenderResults = quotes.GetSmma(period).ToList();

        // Assert
        Assert.Equal(quantalibResults.Count, skenderResults.Count);

        // Skip warmup period for comparison
        // Skender uses SMA initialization, QuanTAlib uses zero-lag compensator
        // They should converge after some periods
        int skip = period * 20;

        for (int i = skip; i < length; i++)
        {
            double qValue = quantalibResults[i].Value;
            double? sValue = skenderResults[i].Smma;

            if (sValue.HasValue)
            {
                Assert.Equal(sValue.Value, qValue, 1e-6);
            }
        }
    }
}
