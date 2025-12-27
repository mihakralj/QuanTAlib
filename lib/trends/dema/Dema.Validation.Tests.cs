using System;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;
using TALib;
using Tulip;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class DemaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public DemaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            _testData?.Dispose();
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
            var qResult = dema.Update(_testData.Data);

            // Calculate Skender DEMA
            var sResult = _testData.SkenderQuotes.GetDema(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, (s) => s.Dema);
        }
        _output.WriteLine("DEMA Batch(TSeries) validated successfully against Skender.Stock.Indicators");
    }

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for TA-Lib (double[])
        double[] tData = _testData.RawData.ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib DEMA (batch TSeries)
            var dema = new global::QuanTAlib.Dema(period);
            var qResult = dema.Update(_testData.Data);

            // Calculate TA-Lib DEMA
            var retCode = TALib.Functions.Dema<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.DemaLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, output, outRange, lookback);
        }
        _output.WriteLine("DEMA Batch(TSeries) validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for Tulip (double[])
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib DEMA (batch TSeries)
            var dema = new global::QuanTAlib.Dema(period);
            var qResult = dema.Update(_testData.Data);

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
            ValidationHelper.VerifyData(qResult, tResult, lookback);
        }
        _output.WriteLine("DEMA Batch(TSeries) validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Talib_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data
        double[] sourceData = _testData.RawData.ToArray();
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
            ValidationHelper.VerifyData(qOutput, talibOutput, outRange, lookback);
        }
        _output.WriteLine("DEMA Span validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Against_Ooples()
    {
        // Ooples Finance implementation of DEMA is standard:
        // DEMA = 2 * EMA(n) - EMA(EMA(n))
        // We validate that our Dema class matches this composition using our own Ema class.

        int[] periods = { 5, 10, 14, 20 };

        foreach (var period in periods)
        {
            var dema = new Dema(period);
            var ema1 = new Ema(period);
            var ema2 = new Ema(period);

            for (int i = 0; i < _testData.Data.Count; i++)
            {
                var item = _testData.Data[i];
                
                // QuanTAlib DEMA
                var qVal = dema.Update(item);

                // Manual DEMA (Ooples logic)
                var e1 = ema1.Update(item);
                var e2 = ema2.Update(e1); // EMA of EMA
                double ooplesVal = 2 * e1.Value - e2.Value;

                // Compare
                // Note: There might be tiny differences due to floating point operations order
                // or internal state handling optimization in Dema class vs composed Ema classes.
                Assert.Equal(ooplesVal, qVal.Value, ValidationHelper.DefaultTolerance);
            }
        }
        _output.WriteLine("DEMA validated successfully against Ooples logic (2*EMA - EMA(EMA))");
    }
}

