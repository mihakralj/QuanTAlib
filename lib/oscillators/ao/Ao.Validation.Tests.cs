using Skender.Stock.Indicators;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using QuanTAlib.Tests;

namespace QuanTAlib;

public sealed class AoValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public AoValidationTests()
    {
        _data = new ValidationTestData();
    }

    public void Dispose()
    {
        _data.Dispose();
    }

    [Fact]
    public void MatchesSkender()
    {
        var ao = new Ao(5, 34);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = ao.Update(_data.Bars[i]);
            results.Add(res.Value);
        }

        var skenderResults = _data.SkenderQuotes.GetAwesome(5, 34).ToList();

        Assert.Equal(_data.Bars.Count, skenderResults.Count);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            // Skender returns null for warmup
            if (skenderResults[i].Oscillator == null)
            {
                continue;
            }

            Assert.Equal((double)skenderResults[i].Oscillator!, results[i], ValidationHelper.SkenderTolerance);
        }
    }

    [Fact]
    public void MatchesTulip()
    {
        var ao = new Ao(5, 34);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = ao.Update(_data.Bars[i]);
            results.Add(res.Value);
        }

        var high = _data.Bars.High.Select(x => x.Value).ToArray();
        var low = _data.Bars.Low.Select(x => x.Value).ToArray();

        var tulipIndicator = Tulip.Indicators.ao;
        double[][] inputs = { high, low };
        double[] options = Array.Empty<double>();

        const int lookback = 33;
        double[][] outputs = [new double[_data.Bars.Count - lookback]];

        tulipIndicator.Run(inputs, options, outputs);
        var tulipResults = outputs[0];

        for (int i = 0; i < tulipResults.Length; i++)
        {
            Assert.Equal(tulipResults[i], results[i + lookback], ValidationHelper.TulipTolerance);
        }
    }

    [Fact]
    public void MatchesOoples()
    {
        var ao = new Ao(5, 34);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = ao.Update(_data.Bars[i]);
            results.Add(res.Value);
        }

        var ooplesData = _data.SkenderQuotes.Select(q => new TickerData
        {
            Date = q.Date,
            Open = (double)q.Open,
            High = (double)q.High,
            Low = (double)q.Low,
            Close = (double)q.Close,
            Volume = (double)q.Volume
        }).ToList();

        var stockData = new StockData(ooplesData);
        var oResult = stockData.CalculateAwesomeOscillator(fastLength: 5, slowLength: 34);
        var oValues = oResult.OutputValues["Ao"];

        Assert.Equal(_data.Bars.Count, oValues.Count);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            // Ooples might return 0 for warmup
            if (i < 33) continue; // Skip warmup

            Assert.Equal(oValues[i], results[i], ValidationHelper.OoplesTolerance);
        }
    }
}
