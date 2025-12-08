using System;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;
using Tulip;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class HmaValidationTests
{
    private readonly TBarSeries _bars;
    private readonly TSeries _data;
    private readonly List<Quote> _skenderQuotes;
    private readonly ITestOutputHelper _output;

    public HmaValidationTests(ITestOutputHelper output)
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
        int[] periods = { 9, 14, 20, 50 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib HMA (batch TSeries)
            var hma = new global::QuanTAlib.Hma(period);
            var qResult = hma.Update(_data);

            // Calculate Skender HMA
            var sResult = _skenderQuotes.GetHma(period).ToList();

            // Compare last 100 records
            VerifyData_Skender(qResult, sResult);
        }
        _output.WriteLine("HMA Batch(TSeries) validated successfully against Skender");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 9, 14, 20, 50 };

        // Prepare data for Tulip (double[])
        double[] tData = _data.Select(x => x.Value).ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib HMA (batch TSeries)
            var hma = new global::QuanTAlib.Hma(period);
            var qResult = hma.Update(_data);

            // Calculate Tulip HMA
            var hmaIndicator = Tulip.Indicators.hma;
            double[][] inputs = { tData };
            double[] options = { period };

            // HMA lookback is period + sqrt(period) - 1 roughly
            // We'll calculate the output size based on the input size and expected lookback
            // Tulip usually returns (input_len - lookback) elements
            // But we can just let it fill what it can if we provide a large enough buffer?
            // No, Tulip.NET wrapper usually expects exact size or it might crash/misbehave.
            // Let's try to be precise.
            // WMA(n) lookback = n-1
            // HMA = WMA(sqrt(n), 2*WMA(n/2) - WMA(n))
            // Path 1: WMA(n) -> valid at n-1
            // Path 2: WMA(n/2) -> valid at n/2-1
            // Combined: valid at max(n-1, n/2-1) = n-1
            // Then WMA(sqrt(n)) on that -> adds sqrt(n)-1 lag
            // Total lookback = (n-1) + (sqrt(n)-1) = n + sqrt(n) - 2

            int sqrtPeriod = (int)Math.Sqrt(period);
            int lookback = period + sqrtPeriod - 2;

            double[][] outputs = { new double[tData.Length - lookback] };

            hmaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            VerifyData_Tulip(qResult, tResult, lookback);
        }
        _output.WriteLine("HMA Batch(TSeries) validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[] periods = { 9, 14, 20, 50 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib HMA (streaming)
            var hma = new global::QuanTAlib.Hma(period);
            var qResults = new List<double>();
            foreach (var item in _data)
            {
                qResults.Add(hma.Update(item).Value);
            }

            // Calculate Skender HMA
            var sResult = _skenderQuotes.GetHma(period).ToList();

            // Compare last 100 records
            VerifyData_Skender_Streaming(qResults, sResult);
        }
        _output.WriteLine("HMA Streaming validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Span()
    {
        int[] periods = { 9, 14, 20, 50 };

        // Prepare data for Span API
        double[] sourceData = _data.Select(x => x.Value).ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib HMA (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Hma.Calculate(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate Skender HMA
            var sResult = _skenderQuotes.GetHma(period).ToList();

            // Compare last 100 records
            VerifyData_Skender_Span(qOutput, sResult);
        }
        _output.WriteLine("HMA Span validated successfully against Skender");
    }

    private static void VerifyData_Skender(TSeries qSeries, List<HmaResult> sSeries)
    {
        Assert.Equal(qSeries.Count, sSeries.Count);

        int count = qSeries.Count;
        int skip = count - 100;

        for (int i = skip; i < count; i++)
        {
            double qValue = qSeries[i].Value;
            double? sValue = sSeries[i].Hma;

            if (!sValue.HasValue) continue;

            Assert.Equal(sValue.Value, qValue, 1e-6);
        }
    }

    private static void VerifyData_Skender_Streaming(List<double> qResults, List<HmaResult> sSeries)
    {
        Assert.Equal(qResults.Count, sSeries.Count);

        int count = qResults.Count;
        int skip = count - 100;

        for (int i = skip; i < count; i++)
        {
            double qValue = qResults[i];
            double? sValue = sSeries[i].Hma;

            if (!sValue.HasValue) continue;

            Assert.Equal(sValue.Value, qValue, 1e-6);
        }
    }

    private static void VerifyData_Skender_Span(double[] qOutput, List<HmaResult> sSeries)
    {
        Assert.Equal(qOutput.Length, sSeries.Count);

        int count = qOutput.Length;
        int skip = count - 100;

        for (int i = skip; i < count; i++)
        {
            double qValue = qOutput[i];
            double? sValue = sSeries[i].Hma;

            if (!sValue.HasValue) continue;

            Assert.Equal(sValue.Value, qValue, 1e-6);
        }
    }

    private static void VerifyData_Tulip(TSeries qSeries, double[] tOutput, int lookback)
    {
        int count = qSeries.Count;
        int skip = count - 100;

        for (int i = skip; i < count; i++)
        {
            double qValue = qSeries[i].Value;

            if (i < lookback) continue;

            int tIndex = i - lookback;
            if (tIndex >= tOutput.Length) continue;

            double tValue = tOutput[tIndex];

            Assert.Equal(tValue, qValue, 1e-6);
        }
    }
}
