using System;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;
using TALib;
using Xunit;
using QuanTAlib.Tests;

namespace QuanTAlib;

public sealed class AroonValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public AroonValidationTests()
    {
        _data = new ValidationTestData();
    }

    public void Dispose()
    {
        _data.Dispose();
    }

    [Fact]
    public void MatchesSkender()
    {
        var aroon = new Aroon(14);
        var results = new List<double>();
        var upResults = new List<double>();
        var downResults = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = aroon.Update(_data.Bars[i]);
            results.Add(res.Value);
            upResults.Add(aroon.Up.Value);
            downResults.Add(aroon.Down.Value);
        }

        var skenderResults = _data.SkenderQuotes.GetAroon(14).ToList();

        // Verify Oscillator
        ValidationHelper.VerifyData(results, skenderResults, x => x.Oscillator);
        
        // Verify Up
        ValidationHelper.VerifyData(upResults, skenderResults, x => x.AroonUp);
        
        // Verify Down
        ValidationHelper.VerifyData(downResults, skenderResults, x => x.AroonDown);
    }

    [Fact]
    public void MatchesTalib()
    {
        var aroon = new Aroon(14);
        var results = new List<double>();
        var upResults = new List<double>();
        var downResults = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = aroon.Update(_data.Bars[i]);
            results.Add(res.Value);
            upResults.Add(aroon.Up.Value);
            downResults.Add(aroon.Down.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] outAroonUp = new double[_data.Bars.Count];
        double[] outAroonDown = new double[_data.Bars.Count];
        double[] outAroonOsc = new double[_data.Bars.Count];

        // TA-Lib Aroon (Up/Down)
        var retCode = TALib.Functions.Aroon(hData, lData, 0..^0, outAroonDown, outAroonUp, out var outRange, 14);
        Assert.Equal(Core.RetCode.Success, retCode);

        // TA-Lib AroonOsc
        var retCodeOsc = TALib.Functions.AroonOsc(hData, lData, 0..^0, outAroonOsc, out var outRangeOsc, 14);
        Assert.Equal(Core.RetCode.Success, retCodeOsc);

        int lookback = TALib.Functions.AroonLookback(14);
        
        // Verify Up
        ValidationHelper.VerifyData(upResults, outAroonUp, outRange, lookback);
        
        // Verify Down
        ValidationHelper.VerifyData(downResults, outAroonDown, outRange, lookback);
        
        // Verify Oscillator
        ValidationHelper.VerifyData(results, outAroonOsc, outRangeOsc, lookback);
    }
}
