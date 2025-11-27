using System;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;
using TALib;
using Tulip;
using Xunit;
using Xunit.Abstractions;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class EmaValidationTests : IDisposable
{
    private readonly TBarSeries _bars;
    private readonly TSeries _data;
    private readonly List<Quote> _skenderQuotes;
    private readonly Random _rnd = new(42);
    private readonly ITestOutputHelper _output;

    public EmaValidationTests(ITestOutputHelper output)
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
                Date = new DateTime(_bars.Open.Times[i]),
                Open = (decimal)_bars.Open[i].Value,
                High = (decimal)_bars.High[i].Value,
                Low = (decimal)_bars.Low[i].Value,
                Close = (decimal)_bars.Close[i].Value,
                Volume = (decimal)_bars.Volume[i].Value
            });
        }
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Validate_Skender()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib EMA
            var ema = new global::QuanTAlib.Ema(period);
            var qResult = ema.Update(_data);

            // Calculate Skender EMA
            var sResult = _skenderQuotes.GetEma(period).ToList();

            // Compare last 100 records
            VerifyData(qResult, sResult, period);
        }
        _output.WriteLine("EMA validated successfully against Skender");
    }

    [Fact]
    public void Validate_Talib()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for TA-Lib (double[])
        double[] tData = _data.Select(x => x.Value).ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib EMA
            var ema = new global::QuanTAlib.Ema(period);
            var qResult = ema.Update(_data);

            // Calculate TA-Lib EMA
            var retCode = TALib.Functions.Ema(tData, 0..^0, output, out var outRange, period);
            
            // Check success
            Assert.Equal(Core.RetCode.Success, retCode);

            // TA-Lib skips the lookback period, so output[0] corresponds to input[lookback]
            int lookback = TALib.Functions.EmaLookback(period);
            
            // Compare last 100 records
            VerifyData_Talib(qResult, output, outRange, lookback, period);
        }
        _output.WriteLine("EMA validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Tulip()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for Tulip (double[])
        double[] tData = _data.Select(x => x.Value).ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib EMA
            var ema = new global::QuanTAlib.Ema(period);
            var qResult = ema.Update(_data);

            // Calculate Tulip EMA
            var emaIndicator = Tulip.Indicators.ema;
            double[][] inputs = { tData };
            double[] options = { (double)period };
            double[][] outputs = { new double[tData.Length] };
            
            emaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            VerifyData(qResult, tResult.ToList(), period);
        }
        _output.WriteLine("EMA validated successfully against Tulip");
    }

    private void VerifyData(TSeries qSeries, List<double> tSeries, int period)
    {
        // Ensure we have enough data
        Assert.Equal(qSeries.Count, tSeries.Count);
        
        int count = qSeries.Count;
        int skip = count - 100; // Last 100 records

        for (int i = skip; i < count; i++)
        {
            double qValue = qSeries[i].Value;
            double tValue = tSeries[i];
            if (tValue == 0) continue;

            Assert.Equal(tValue, qValue, 1e-6);
        }
    }

    private void VerifyData(TSeries qSeries, List<EmaResult> sSeries, int period)
    {
        // Ensure we have enough data
        Assert.Equal(qSeries.Count, sSeries.Count);
        
        int count = qSeries.Count;
        int skip = count - 100; // Last 100 records

        for (int i = skip; i < count; i++)
        {
            double qValue = qSeries[i].Value;
            double? sValue = sSeries[i].Ema;

            // Skip if Skender returns null (warmup period)
            if (!sValue.HasValue) continue;

            // Assert equality with tolerance
            Assert.Equal(sValue.Value, qValue, 1e-6);
        }
    }

    private void VerifyData_Talib(TSeries qSeries, double[] tOutput, Range outRange, int lookback, int period)
    {
        int count = qSeries.Count;
        int skip = count - 100; // Last 100 records

        // outRange.End.Value is the number of elements written to tOutput
        int validCount = outRange.End.Value - outRange.Start.Value;
        
        for (int i = skip; i < count; i++)
        {
            double qValue = qSeries[i].Value;
            
            // Calculate index in tOutput
            // If i < lookback, we don't have a value from TA-Lib
            if (i < lookback) continue;
            
            int tIndex = i - lookback;
            
            // Check if tIndex is within valid range
            if (tIndex >= validCount) continue;

            double tValue = tOutput[tIndex];

            // Assert equality with tolerance
            Assert.Equal(tValue, qValue, 1e-6);
        }
    }
}
