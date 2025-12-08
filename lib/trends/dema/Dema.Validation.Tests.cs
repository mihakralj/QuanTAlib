using System;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;
using TALib;
using Tulip;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class DemaValidationTests
{
    private readonly TBarSeries _bars;
    private readonly TSeries _data;
    private readonly List<Quote> _skenderQuotes;
    private readonly ITestOutputHelper _output;

    public DemaValidationTests(ITestOutputHelper output)
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
            // Calculate QuanTAlib DEMA (batch TSeries)
            var dema = new global::QuanTAlib.Dema(period);
            var qResult = dema.Update(_data);

            // Calculate Skender DEMA
            var sResult = _skenderQuotes.GetDema(period).ToList();

            // Compare last 100 records
            VerifyData_Skender(qResult, sResult);
        }
        _output.WriteLine("DEMA Batch(TSeries) validated successfully against Skender.Stock.Indicators");
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
            // Calculate QuanTAlib DEMA (batch TSeries)
            var dema = new global::QuanTAlib.Dema(period);
            var qResult = dema.Update(_data);

            // Calculate TA-Lib DEMA
            var retCode = TALib.Functions.Dema<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.DemaLookback(period);

            // Compare last 100 records
            VerifyData_Talib(qResult, output, outRange, lookback);
        }
        _output.WriteLine("DEMA Batch(TSeries) validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for Tulip (double[])
        double[] tData = _data.Select(x => x.Value).ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib DEMA (batch TSeries)
            var dema = new global::QuanTAlib.Dema(period);
            var qResult = dema.Update(_data);

            // Calculate Tulip DEMA
            var demaIndicator = Tulip.Indicators.dema;
            double[][] inputs = { tData };
            double[] options = { period };
            
            // Tulip DEMA lookback is usually period-1 for EMA, but DEMA is 2*EMA - EMA(EMA)
            // Let's rely on the output length to align.
            // Tulip DEMA lookback is same as EMA lookback? No, it involves double smoothing.
            // Actually, Tulip's DEMA implementation might have a specific lookback.
            // We'll calculate it based on output length.
            
            // Tulip.Indicators.dema.Run expects outputs to be sized correctly.
            // We'll use a large buffer and resize if needed, or just calculate lookback.
            // For DEMA(n), lookback is roughly n-1 (same as EMA).
            // Wait, DEMA uses EMA(EMA), so it might be 2*(n-1)?
            // Let's try with n-1 first, if it fails we adjust.
            // Actually, TA-Lib DEMA lookback is 2*(period-1).
            // Let's assume Tulip is similar.
            int lookback = 2 * (period - 1); 
            double[][] outputs = { new double[tData.Length - lookback] };

            demaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            VerifyData_Tulip(qResult, tResult, lookback);
        }
        _output.WriteLine("DEMA Batch(TSeries) validated successfully against Tulip");
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
            // Calculate QuanTAlib DEMA (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Dema.Calculate(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate TA-Lib DEMA
            var retCode = TALib.Functions.Dema<double>(sourceData, 0..^0, talibOutput, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.DemaLookback(period);

            // Compare last 100 records
            VerifyData_Talib_Span(qOutput, talibOutput, outRange, lookback);
        }
        _output.WriteLine("DEMA Span validated successfully against TA-Lib");
    }

    // ==================== Verification Helpers ====================

    private static void VerifyData_Skender(TSeries qSeries, List<DemaResult> sSeries)
    {
        Assert.Equal(qSeries.Count, sSeries.Count);

        int count = qSeries.Count;
        int skip = count - 100;

        for (int i = skip; i < count; i++)
        {
            double qValue = qSeries[i].Value;
            double? sValue = sSeries[i].Dema;

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
