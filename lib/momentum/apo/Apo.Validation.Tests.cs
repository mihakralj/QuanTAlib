using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using QuanTAlib.Tests;
using Skender.Stock.Indicators;
using TALib;
using Tulip;

namespace QuanTAlib;

public class ApoValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;

    public ApoValidationTests()
    {
        _testData = new ValidationTestData(); // Default 5000 bars
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _testData.Dispose();
        }
    }

    [Fact]
    public void Validate_Against_TALib_Apo()
    {
        int fastPeriod = 12;
        int slowPeriod = 26;
        double[] input = _testData.Data.Values.ToArray();
        double[] output = new double[input.Length];

        // TA-Lib APO: double[] inReal, int optInFastPeriod, int optInSlowPeriod, int optInMAType
        // MAType 1 = EMA
        var retCode = TALib.Functions.Apo<double>(input, 0..^0, output, out var outRange, fastPeriod, slowPeriod, TALib.Core.MAType.Ema);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        // 1. Batch Mode
        var apo = new Apo(fastPeriod, slowPeriod);
        var result = apo.Update(_testData.Data);
        ValidationHelper.VerifyData(result, output, outRange, lookback: slowPeriod - 1);

        // 2. Streaming Mode
        var apoStream = new Apo(fastPeriod, slowPeriod);
        var streamResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            streamResults.Add(apoStream.Update(item).Value);
        }
        ValidationHelper.VerifyData(streamResults, output, outRange, lookback: slowPeriod - 1);

        // 3. Span Mode
        double[] spanOutput = new double[input.Length];
        Apo.Calculate(input.AsSpan(), spanOutput.AsSpan(), fastPeriod, slowPeriod);
        ValidationHelper.VerifyData(spanOutput, output, outRange, lookback: slowPeriod - 1);
    }

    [Fact]
    public void Validate_Against_Tulip_Apo()
    {
        // Tulip APO uses standard EMA initialization (first value), while QuanTAlib uses
        // compensated EMA initialization (zero-based). They converge after sufficient periods.
        // With 5000 bars, the tail (last 100) should match closely.
        int fastPeriod = 12;
        int slowPeriod = 26;
        double[] input = _testData.Data.Values.ToArray();

        var apoIndicator = Tulip.Indicators.apo;
        double[][] inputs = { input };
        double[] options = { fastPeriod, slowPeriod };
        double[][] outputs = { new double[input.Length - 1] }; // Tulip APO starts at 1

        apoIndicator.Run(inputs, options, outputs);
        double[] output = outputs[0];

        // 1. Batch Mode
        var apo = new Apo(fastPeriod, slowPeriod);
        var result = apo.Update(_testData.Data);
        ValidationHelper.VerifyData(result, output, lookback: 1);

        // 2. Streaming Mode
        var apoStream = new Apo(fastPeriod, slowPeriod);
        var streamResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            streamResults.Add(apoStream.Update(item).Value);
        }
        ValidationHelper.VerifyData(streamResults, output, lookback: 1);

        // 3. Span Mode
        double[] spanOutput = new double[input.Length];
        Apo.Calculate(input.AsSpan(), spanOutput.AsSpan(), fastPeriod, slowPeriod);
        ValidationHelper.VerifyData(spanOutput, output, lookback: 1);
    }
}
