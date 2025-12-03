using System;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;
using TALib;
using Tulip;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class WmaValidationTests
{
    private readonly TBarSeries _bars;
    private readonly TSeries _data;
    private readonly List<Quote> _skenderQuotes;
    private readonly ITestOutputHelper _output;

    public WmaValidationTests(ITestOutputHelper output)
    {
        _output = output;

        // 1. Generate 1000 records using GBM feed
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2);
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
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib WMA (batch TSeries)
            var wma = new global::QuanTAlib.Wma(period);
            var qResult = wma.Update(_data);

            // Calculate Skender WMA
            var sResult = _skenderQuotes.GetWma(period).ToList();

            // Compare last 100 records
            VerifyData_Skender(qResult, sResult);
        }
        _output.WriteLine("WMA Batch(TSeries) validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib WMA (streaming)
            var wma = new global::QuanTAlib.Wma(period);
            var qResults = new List<double>();
            foreach (var item in _data)
            {
                qResults.Add(wma.Update(item).Value);
            }

            // Calculate Skender WMA
            var sResult = _skenderQuotes.GetWma(period).ToList();

            // Compare last 100 records
            VerifyData_Skender_Streaming(qResults, sResult);
        }
        _output.WriteLine("WMA Streaming validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for Span API
        double[] sourceData = _data.Select(x => x.Value).ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib WMA (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Wma.Calculate(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate Skender WMA
            var sResult = _skenderQuotes.GetWma(period).ToList();

            // Compare last 100 records
            VerifyData_Skender_Span(qOutput, sResult);
        }
        _output.WriteLine("WMA Span validated successfully against Skender");
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
            // Calculate QuanTAlib WMA (batch TSeries)
            var wma = new global::QuanTAlib.Wma(period);
            var qResult = wma.Update(_data);

            // Calculate TA-Lib WMA
            var retCode = TALib.Functions.Wma<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.WmaLookback(period);

            // Compare last 100 records
            VerifyData_Talib(qResult, output, outRange, lookback);
        }
        _output.WriteLine("WMA Batch(TSeries) validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for TA-Lib (double[])
        double[] tData = _data.Select(x => x.Value).ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib WMA (streaming)
            var wma = new global::QuanTAlib.Wma(period);
            var qResults = new List<double>();
            foreach (var item in _data)
            {
                qResults.Add(wma.Update(item).Value);
            }

            // Calculate TA-Lib WMA
            var retCode = TALib.Functions.Wma<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.WmaLookback(period);

            // Compare last 100 records
            VerifyData_Talib_Streaming(qResults, output, outRange, lookback);
        }
        _output.WriteLine("WMA Streaming validated successfully against TA-Lib");
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
            // Calculate QuanTAlib WMA (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Wma.Calculate(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate TA-Lib WMA
            var retCode = TALib.Functions.Wma<double>(sourceData, 0..^0, talibOutput, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.WmaLookback(period);

            // Compare last 100 records
            VerifyData_Talib_Span(qOutput, talibOutput, outRange, lookback);
        }
        _output.WriteLine("WMA Span validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for Tulip (double[])
        double[] tData = _data.Select(x => x.Value).ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib WMA (batch TSeries)
            var wma = new global::QuanTAlib.Wma(period);
            var qResult = wma.Update(_data);

            // Calculate Tulip WMA
            var wmaIndicator = Tulip.Indicators.wma;
            double[][] inputs = { tData };
            double[] options = { period };
            int lookback = period - 1;
            double[][] outputs = { new double[tData.Length - lookback] };

            wmaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            VerifyData_Tulip(qResult, tResult, lookback);
        }
        _output.WriteLine("WMA Batch(TSeries) validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Tulip_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for Tulip (double[])
        double[] tData = _data.Select(x => x.Value).ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib WMA (streaming)
            var wma = new global::QuanTAlib.Wma(period);
            var qResults = new List<double>();
            foreach (var item in _data)
            {
                qResults.Add(wma.Update(item).Value);
            }

            // Calculate Tulip WMA
            var wmaIndicator = Tulip.Indicators.wma;
            double[][] inputs = { tData };
            double[] options = { period };
            int lookback = period - 1;
            double[][] outputs = { new double[tData.Length - lookback] };

            wmaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            VerifyData_Tulip_Streaming(qResults, tResult, lookback);
        }
        _output.WriteLine("WMA Streaming validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Tulip_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data
        double[] sourceData = _data.Select(x => x.Value).ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib WMA (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Wma.Calculate(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate Tulip WMA
            var wmaIndicator = Tulip.Indicators.wma;
            double[][] inputs = { sourceData };
            double[] options = { period };
            int lookback = period - 1;
            double[][] outputs = { new double[sourceData.Length - lookback] };

            wmaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            VerifyData_Tulip_Span(qOutput, tResult, lookback);
        }
        _output.WriteLine("WMA Span validated successfully against Tulip");
    }

    // ==================== Verification Helpers ====================

    private static void VerifyData_Skender(TSeries qSeries, List<WmaResult> sSeries)
    {
        Assert.Equal(qSeries.Count, sSeries.Count);

        int count = qSeries.Count;
        int skip = count - 100;

        for (int i = skip; i < count; i++)
        {
            double qValue = qSeries[i].Value;
            double? sValue = sSeries[i].Wma;

            if (!sValue.HasValue) continue;

            Assert.Equal(sValue.Value, qValue, 1e-6);
        }
    }

    private static void VerifyData_Skender_Streaming(List<double> qResults, List<WmaResult> sSeries)
    {
        Assert.Equal(qResults.Count, sSeries.Count);

        int count = qResults.Count;
        int skip = count - 100;

        for (int i = skip; i < count; i++)
        {
            double qValue = qResults[i];
            double? sValue = sSeries[i].Wma;

            if (!sValue.HasValue) continue;

            Assert.Equal(sValue.Value, qValue, 1e-6);
        }
    }

    private static void VerifyData_Skender_Span(double[] qOutput, List<WmaResult> sSeries)
    {
        Assert.Equal(qOutput.Length, sSeries.Count);

        int count = qOutput.Length;
        int skip = count - 100;

        for (int i = skip; i < count; i++)
        {
            double qValue = qOutput[i];
            double? sValue = sSeries[i].Wma;

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

    private static void VerifyData_Talib_Streaming(List<double> qResults, double[] tOutput, Range outRange, int lookback)
    {
        int count = qResults.Count;
        int skip = count - 100;
        int validCount = outRange.End.Value - outRange.Start.Value;

        for (int i = skip; i < count; i++)
        {
            double qValue = qResults[i];

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

    private static void VerifyData_Tulip_Streaming(List<double> qResults, double[] tOutput, int lookback)
    {
        int count = qResults.Count;
        int skip = count - 100;

        for (int i = skip; i < count; i++)
        {
            double qValue = qResults[i];

            if (i < lookback) continue;

            int tIndex = i - lookback;
            if (tIndex >= tOutput.Length) continue;

            double tValue = tOutput[tIndex];

            Assert.Equal(tValue, qValue, 1e-6);
        }
    }

    private static void VerifyData_Tulip_Span(double[] qOutput, double[] tOutput, int lookback)
    {
        int count = qOutput.Length;
        int skip = count - 100;

        for (int i = skip; i < count; i++)
        {
            double qValue = qOutput[i];

            if (i < lookback) continue;

            int tIndex = i - lookback;
            if (tIndex >= tOutput.Length) continue;

            double tValue = tOutput[tIndex];

            Assert.Equal(tValue, qValue, 1e-6);
        }
    }
}
