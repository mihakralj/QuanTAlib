using System;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;
using TALib;
using Tulip;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class TemaValidationTests
{
    private readonly TBarSeries _bars;
    private readonly TSeries _data;
    private readonly List<Quote> _skenderQuotes;
    private readonly ITestOutputHelper _output;

    public TemaValidationTests(ITestOutputHelper output)
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
            // Calculate QuanTAlib TEMA (batch TSeries)
            var tema = new global::QuanTAlib.Tema(period);
            var qResult = tema.Update(_data);

            // Calculate Skender TEMA
            var sResult = _skenderQuotes.GetTema(period).ToList();

            // Compare last 100 records
            VerifyData_Skender(qResult, sResult);
        }
        _output.WriteLine("TEMA Batch(TSeries) validated successfully against Skender.Stock.Indicators");
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
            // Calculate QuanTAlib TEMA (batch TSeries)
            var tema = new global::QuanTAlib.Tema(period);
            var qResult = tema.Update(_data);

            // Calculate TA-Lib TEMA
            var retCode = TALib.Functions.Tema<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.TemaLookback(period);

            // Compare last 100 records
            VerifyData_Talib(qResult, output, outRange, lookback);
        }
        _output.WriteLine("TEMA Batch(TSeries) validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for Tulip (double[])
        double[] tData = _data.Select(x => x.Value).ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib TEMA (batch TSeries)
            var tema = new global::QuanTAlib.Tema(period);
            var qResult = tema.Update(_data);

            // Calculate Tulip TEMA
            var temaIndicator = Tulip.Indicators.tema;
            double[][] inputs = { tData };
            double[] options = { period };
            
            // Tulip TEMA lookback is 3*(period-1)
            int lookback = 3 * (period - 1); 
            double[][] outputs = { new double[tData.Length - lookback] };

            temaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            VerifyData_Tulip(qResult, tResult, lookback);
        }
        _output.WriteLine("TEMA Batch(TSeries) validated successfully against Tulip");
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
            // Calculate QuanTAlib TEMA (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Tema.Calculate(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate TA-Lib TEMA
            var retCode = TALib.Functions.Tema<double>(sourceData, 0..^0, talibOutput, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.TemaLookback(period);

            // Compare last 100 records
            VerifyData_Talib_Span(qOutput, talibOutput, outRange, lookback);
        }
        _output.WriteLine("TEMA Span validated successfully against TA-Lib");
    }

    // ==================== Verification Helpers ====================

    private static void VerifyData_Skender(TSeries qSeries, List<TemaResult> sSeries)
    {
        Assert.Equal(qSeries.Count, sSeries.Count);

        int count = qSeries.Count;
        int skip = count - 100;

        for (int i = skip; i < count; i++)
        {
            double qValue = qSeries[i].Value;
            double? sValue = sSeries[i].Tema;

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

            Assert.Equal(tValue, qValue, 1e-5);
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

            Assert.Equal(tValue, qValue, 1e-5);
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

            Assert.Equal(tValue, qValue, 1e-5);
        }
    }
}
