using System;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;

namespace QuanTAlib.Tests;

public sealed class ValidationTestData : IDisposable
{
    public TBarSeries Bars { get; }
    public TSeries Data { get; }
    public IReadOnlyList<Quote> SkenderQuotes { get; }
    public ReadOnlyMemory<double> RawData { get; }

    public ValidationTestData(int count = 5000, double startPrice = 1000.0, double mu = 0.05, double sigma = 2.0, int seed = 123)
    {
        var gbm = new GBM(startPrice, mu, sigma, seed: seed);
        Bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        Data = Bars.Close;
        RawData = Data.Select(x => x.Value).ToArray();

        var quotes = new List<Quote>();
        for (int i = 0; i < Bars.Count; i++)
        {
            quotes.Add(new Quote
            {
                Date = new DateTime(Bars.Open.Times[i], DateTimeKind.Utc),
                Open = (decimal)Bars.Open[i].Value,
                High = (decimal)Bars.High[i].Value,
                Low = (decimal)Bars.Low[i].Value,
                Close = (decimal)Bars.Close[i].Value,
                Volume = (decimal)Bars.Volume[i].Value
            });
        }
        SkenderQuotes = quotes;
    }

    public void Dispose()
    {
        // No resources to dispose
    }
}
