using System;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;
using TALib;
using Tulip;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class TrimaValidationTests
{
    private readonly TBarSeries _bars;
    private readonly TSeries _data;
    private readonly List<Quote> _skenderQuotes;
    private readonly ITestOutputHelper _output;

    public TrimaValidationTests(ITestOutputHelper output)
    {
        _output = output;

        // 1. Generate 5000 records using GBM feed
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2);
        _bars = gbm.Fetch(5000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

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
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib TRIMA (batch TSeries)
            var trima = new global::QuanTAlib.Trima(period);
            var qResult = trima.Update(_data);

            // Calculate Skender Composite TRIMA: SMA(SMA(x, p1), p2)
            int p1 = period / 2 + 1;
            int p2 = (period + 1) / 2;

            var sma1Results = _skenderQuotes.GetSma(p1).ToList();
            
            // Map SMA1 results to Quotes for the second pass
            // Note: We use 0 for null values during warmup, which might affect early values
            // but should stabilize for the verification window (last 100 records)
            var quotes2 = sma1Results.Select(r => new Quote 
            { 
                Date = r.Date, 
                Close = (decimal)(r.Sma ?? 0) 
            }).ToList();

            var sResult = quotes2.GetSma(p2).ToList();

            // Compare last 100 records
            VerifyData_Skender(qResult, sResult);
        }
        _output.WriteLine("TRIMA Batch(TSeries) validated successfully against Skender Composite SMA");
    }

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for TA-Lib (double[])
        double[] tData = _data.Select(x => x.Value).ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib TRIMA (batch TSeries)
            var trima = new global::QuanTAlib.Trima(period);
            var qResult = trima.Update(_data);

            // Calculate TA-Lib TRIMA
            var retCode = TALib.Functions.Trima<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.TrimaLookback(period);

            // Compare last 100 records
            VerifyData_Talib(qResult, output, outRange, lookback);
        }
        _output.WriteLine("TRIMA Batch(TSeries) validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for Tulip (double[])
        double[] tData = _data.Select(x => x.Value).ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib TRIMA (batch TSeries)
            var trima = new global::QuanTAlib.Trima(period);
            var qResult = trima.Update(_data);

            // Calculate Tulip TRIMA
            var trimaIndicator = Tulip.Indicators.trima;
            double[][] inputs = { tData };
            double[] options = { period };
            // Tulip TRIMA lookback might be different, let's calculate or infer
            // Usually it's period-1 for simple averages, but TRIMA is double smoothed.
            // We'll rely on the output length to align.
            // Tulip.Indicators.trima.Run expects outputs to be sized correctly.
            // We can try to run it with a large buffer and see what happens, 
            // or calculate the expected lookback.
            // For TRIMA(n), lookback is roughly n-1.
            int lookback = period - 1; 
            double[][] outputs = { new double[tData.Length - lookback] };

            trimaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            VerifyData_Tulip(qResult, tResult, lookback);
        }
        _output.WriteLine("TRIMA Batch(TSeries) validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Talib_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data
        double[] sourceData = _data.Select(x => x.Value).ToArray();
        double[] talibOutput = new double[sourceData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib TRIMA (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Trima.Calculate(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate TA-Lib TRIMA
            var retCode = TALib.Functions.Trima<double>(sourceData, 0..^0, talibOutput, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.TrimaLookback(period);

            // Compare last 100 records
            VerifyData_Talib_Span(qOutput, talibOutput, outRange, lookback);
        }
        _output.WriteLine("TRIMA Span validated successfully against TA-Lib");
    }

    // ==================== Verification Helpers ====================

    private static void VerifyData_Skender(TSeries qSeries, List<SmaResult> sSeries)
    {
        Assert.Equal(qSeries.Count, sSeries.Count);

        int count = qSeries.Count;
        int skip = count - 100;

        for (int i = skip; i < count; i++)
        {
            double qValue = qSeries[i].Value;
            double? sValue = sSeries[i].Sma;

            if (!sValue.HasValue) continue;

            Assert.Equal(sValue.Value, qValue, 1e-6);
        }
    }

    private static void VerifyData_Talib(TSeries qSeries, double[] tOutput, Range outRange, int lookback)
    {
        int count = qSeries.Count;
        int skip = count - 100;
        int validCount = outRange.End.Value - outRange.Start.Value;

        for (int i = skip; i < count; i++)
        {
            double qValue = qSeries[i].Value;

            if (i < lookback) continue;

            int tIndex = i - lookback;
            if (tIndex >= validCount) continue;

            double tValue = tOutput[tIndex];

            Assert.Equal(tValue, qValue, 1e-6);
        }
    }

    private static void VerifyData_Talib_Span(double[] qOutput, double[] tOutput, Range outRange, int lookback)
    {
        int count = qOutput.Length;
        int skip = count - 100;
        int validCount = outRange.End.Value - outRange.Start.Value;

        for (int i = skip; i < count; i++)
        {
            double qValue = qOutput[i];

            if (i < lookback) continue;

            int tIndex = i - lookback;
            if (tIndex >= validCount) continue;

            double tValue = tOutput[tIndex];

            Assert.Equal(tValue, qValue, 1e-6);
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
