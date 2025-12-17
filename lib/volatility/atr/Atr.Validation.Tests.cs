using System;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;
using TALib;
using Xunit;
using QuanTAlib.Tests;

namespace QuanTAlib;

public sealed class AtrValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public AtrValidationTests()
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
        var atr = new Atr(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = atr.Update(_data.Bars[i]);
            results.Add(res.Value);
        }

        var skenderResults = _data.SkenderQuotes.GetAtr(14).ToList();

        // ATR involves smoothing, so early values might differ slightly depending on initialization.
        // Skender uses Wilder's initialization method.
        ValidationHelper.VerifyData(results, skenderResults, x => x.Atr);
    }

    [Fact]
    public void MatchesTalib()
    {
        var atr = new Atr(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = atr.Update(_data.Bars[i]);
            results.Add(res.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _data.Bars.Close.Select(x => x.Value).ToArray();
        double[] outReal = new double[_data.Bars.Count];

        var retCode = TALib.Functions.Atr(hData, lData, cData, 0..^0, outReal, out var outRange, 14);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.AtrLookback(14);
        ValidationHelper.VerifyData(results, outReal, outRange, lookback);
    }
}
