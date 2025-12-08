using System;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class KamaValidationTests
{
    private readonly TBarSeries _bars;
    private readonly TSeries _data;
    private readonly List<Quote> _skenderQuotes;
    private readonly ITestOutputHelper _output;

    public KamaValidationTests(ITestOutputHelper output)
    {
        _output = output;

        // 1. Generate 1000 records using GBM feed
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        _bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // 2. Extract Close TSeries
        _data = _bars.Close;

        // 3. Prepare data for Skender (List<Quote>)
        _skenderQuotes = new List<Quote>();
        for (int i = 0; i < _bars.Count; i++)
        {
            _skenderQuotes.Add(new Quote
            {
                Date = new DateTime(_bars.Open.Times[i], DateTimeKind.Utc),
                Open = (decimal)_bars.Open[i].Value,
                High = (decimal)_bars.High[i].Value,
                Low = (decimal)_bars.Low[i].Value,
                Close = (decimal)_bars.Close[i].Value,
                Volume = (decimal)_bars.Volume[i].Value
            });
        }
    }

    [Fact]
    public void Validate_Skender_Batch()
    {
        int[] periods = { 10, 14, 20 };
        int fastPeriod = 2;
        int slowPeriod = 30;

        foreach (var period in periods)
        {
            // Calculate QuanTAlib KAMA (batch TSeries)
            var kama = new global::QuanTAlib.Kama(period, fastPeriod, slowPeriod);
            var qResult = kama.Update(_data);

            // Calculate Skender KAMA
            var sResult = _skenderQuotes.GetKama(period, fastPeriod, slowPeriod).ToList();

            // Compare last 100 records
            VerifyData_Skender(qResult, sResult);
        }
        _output.WriteLine("KAMA Batch(TSeries) validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[] periods = { 10, 14, 20 };
        int fastPeriod = 2;
        int slowPeriod = 30;

        foreach (var period in periods)
        {
            // Calculate QuanTAlib KAMA (streaming)
            var kama = new global::QuanTAlib.Kama(period, fastPeriod, slowPeriod);
            var qResults = new List<double>();
            foreach (var item in _data)
            {
                qResults.Add(kama.Update(item).Value);
            }

            // Calculate Skender KAMA
            var sResult = _skenderQuotes.GetKama(period, fastPeriod, slowPeriod).ToList();

            // Compare last 100 records
            VerifyData_Skender_Streaming(qResults, sResult);
        }
        _output.WriteLine("KAMA Streaming validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Span()
    {
        int[] periods = { 10, 14, 20 };
        int fastPeriod = 2;
        int slowPeriod = 30;

        // Prepare data for Span API
        double[] sourceData = _data.Select(x => x.Value).ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib KAMA (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Kama.Calculate(sourceData.AsSpan(), qOutput.AsSpan(), period, fastPeriod, slowPeriod);

            // Calculate Skender KAMA
            var sResult = _skenderQuotes.GetKama(period, fastPeriod, slowPeriod).ToList();

            // Compare last 100 records
            VerifyData_Skender_Span(qOutput, sResult);
        }
        _output.WriteLine("KAMA Span validated successfully against Skender");
    }

    private static void VerifyData_Skender(TSeries qSeries, List<KamaResult> sSeries)
    {
        Assert.Equal(qSeries.Count, sSeries.Count);

        int count = qSeries.Count;
        int skip = count - 100;

        for (int i = skip; i < count; i++)
        {
            double qValue = qSeries[i].Value;
            double? sValue = (double?)sSeries[i].Kama;

            if (!sValue.HasValue) continue;

            Assert.Equal(sValue.Value, qValue, 1e-6);
        }
    }

    private static void VerifyData_Skender_Streaming(List<double> qResults, List<KamaResult> sSeries)
    {
        Assert.Equal(qResults.Count, sSeries.Count);

        int count = qResults.Count;
        int skip = count - 100;

        for (int i = skip; i < count; i++)
        {
            double qValue = qResults[i];
            double? sValue = (double?)sSeries[i].Kama;

            if (!sValue.HasValue) continue;

            Assert.Equal(sValue.Value, qValue, 1e-6);
        }
    }

    private static void VerifyData_Skender_Span(double[] qOutput, List<KamaResult> sSeries)
    {
        Assert.Equal(qOutput.Length, sSeries.Count);

        int count = qOutput.Length;
        int skip = count - 100;

        for (int i = skip; i < count; i++)
        {
            double qValue = qOutput[i];
            double? sValue = (double?)sSeries[i].Kama;

            if (!sValue.HasValue) continue;

            Assert.Equal(sValue.Value, qValue, 1e-6);
        }
    }
}
