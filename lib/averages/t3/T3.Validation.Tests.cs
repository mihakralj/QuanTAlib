using System;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;
using TALib;
using Tulip;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class T3ValidationTests
{
    private readonly TBarSeries _bars;
    private readonly TSeries _data;
    private readonly List<Quote> _skenderQuotes;
    private readonly ITestOutputHelper _output;

    public T3ValidationTests(ITestOutputHelper output)
    {
        _output = output;

        // 1. Generate 2000 records using GBM feed
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2);
        _bars = gbm.Fetch(2000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

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
        int[] periods = { 5, 10, 20 };
        double vFactor = 0.7;

        foreach (var period in periods)
        {
            // Calculate QuanTAlib T3
            var t3 = new global::QuanTAlib.T3(period, vFactor);
            var qResult = t3.Update(_data);

            // Calculate Skender T3
            var sResult = _skenderQuotes.GetT3(period, vFactor).ToList();

            // Compare last 100 records
            VerifyData_Skender(qResult, sResult);
        }
        _output.WriteLine("T3 Batch(TSeries) validated successfully against Skender");
    }

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[] periods = { 5, 10, 20 };
        double vFactor = 0.7;

        // Prepare data for TA-Lib
        double[] tData = _data.Select(x => x.Value).ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib T3
            var t3 = new global::QuanTAlib.T3(period, vFactor);
            var qResult = t3.Update(_data);

            // Calculate TA-Lib T3
            var retCode = TALib.Functions.T3<double>(tData, 0..^0, output, out var outRange, period, vFactor);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.T3Lookback(period);

            // Compare last 100 records
            VerifyData_Talib(qResult, output, outRange, lookback);
        }
        _output.WriteLine("T3 Batch(TSeries) validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[] periods = { 5, 10, 20 };
        double vFactor = 0.7;

        // Prepare data for TA-Lib
        double[] tData = _data.Select(x => x.Value).ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib T3 (streaming)
            var t3 = new global::QuanTAlib.T3(period, vFactor);
            var qResults = new List<double>();
            foreach (var item in _data)
            {
                qResults.Add(t3.Update(item).Value);
            }

            // Calculate TA-Lib T3
            var retCode = TALib.Functions.T3<double>(tData, 0..^0, output, out var outRange, period, vFactor);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.T3Lookback(period);

            // Compare last 100 records
            VerifyData_Talib_Streaming(qResults, output, outRange, lookback);
        }
        _output.WriteLine("T3 Streaming validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Span()
    {
        int[] periods = { 5, 10, 20 };
        double vFactor = 0.7;

        // Prepare data
        double[] sourceData = _data.Select(x => x.Value).ToArray();
        double[] talibOutput = new double[sourceData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib T3 (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.T3.Calculate(sourceData.AsSpan(), qOutput.AsSpan(), period, vFactor);

            // Calculate TA-Lib T3
            var retCode = TALib.Functions.T3<double>(sourceData, 0..^0, talibOutput, out var outRange, period, vFactor);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.T3Lookback(period);

            // Compare last 100 records
            VerifyData_Talib_Span(qOutput, talibOutput, outRange, lookback);
        }
        _output.WriteLine("T3 Span validated successfully against TA-Lib");
    }

    private static void VerifyData_Skender(TSeries qSeries, List<T3Result> sSeries)
    {
        Assert.Equal(qSeries.Count, sSeries.Count);

        int count = qSeries.Count;
        int skip = count - 100;

        for (int i = skip; i < count; i++)
        {
            double qValue = qSeries[i].Value;
            double? sValue = sSeries[i].T3;

            if (!sValue.HasValue) continue;

            Assert.Equal(sValue.Value, qValue, 1e-4);
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

            Assert.Equal(tValue, qValue, 1e-4);
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

            Assert.Equal(tValue, qValue, 1e-4);
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

            Assert.Equal(tValue, qValue, 1e-4);
        }
    }
}
