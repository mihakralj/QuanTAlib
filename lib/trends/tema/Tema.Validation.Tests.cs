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
    // Note: OoplesFinance TEMA implementation diverges significantly from Skender, TA-Lib, and Tulip
    // for larger periods, likely due to different initialization or smoothing logic.
    // Therefore, we do not validate against Ooples for TEMA.

    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    public TemaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    [Fact]
    public void Validate_Skender_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib TEMA (batch TSeries)
            var tema = new global::QuanTAlib.Tema(period);
            var qResult = tema.Update(_testData.Data);

            // Calculate Skender TEMA
            var sResult = _testData.SkenderQuotes.GetTema(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, x => x.Tema);
        }
        _output.WriteLine("TEMA Batch(TSeries) validated successfully against Skender.Stock.Indicators");
    }

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for TA-Lib (double[])
        double[] output = new double[_testData.RawData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib TEMA (batch TSeries)
            var tema = new global::QuanTAlib.Tema(period);
            var qResult = tema.Update(_testData.Data);

            // Calculate TA-Lib TEMA
            var retCode = TALib.Functions.Tema<double>(_testData.RawData.Span, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.TemaLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, output, outRange, lookback);
        }
        _output.WriteLine("TEMA Batch(TSeries) validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib TEMA (batch TSeries)
            var tema = new global::QuanTAlib.Tema(period);
            var qResult = tema.Update(_testData.Data);

            // Calculate Tulip TEMA
            var temaIndicator = Tulip.Indicators.tema;
            double[][] inputs = { _testData.RawData.ToArray() };
            double[] options = { period };
            
            // Tulip TEMA lookback is 3*(period-1)
            int lookback = 3 * (period - 1); 
            double[][] outputs = { new double[_testData.RawData.Length - lookback] };

            temaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, tResult, lookback);
        }
        _output.WriteLine("TEMA Batch(TSeries) validated successfully against Tulip");
    }


    [Fact]
    public void Validate_Talib_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data
        double[] talibOutput = new double[_testData.RawData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib TEMA (Span API)
            double[] qOutput = new double[_testData.RawData.Length];
            global::QuanTAlib.Tema.Batch(_testData.RawData.Span, qOutput.AsSpan(), period);

            // Calculate TA-Lib TEMA
            var retCode = TALib.Functions.Tema<double>(_testData.RawData.Span, 0..^0, talibOutput, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.TemaLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, talibOutput, outRange, lookback);
        }
        _output.WriteLine("TEMA Span validated successfully against TA-Lib");
    }
}
