using System;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;
using TALib;
using Xunit;
using QuanTAlib.Tests;

namespace QuanTAlib;

public sealed class AdxValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public AdxValidationTests()
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
        var adx = new Adx(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = adx.Update(_data.Bars[i]);
            results.Add(res.Value);
        }

        var skenderResults = _data.SkenderQuotes.GetAdx(14).ToList();

        ValidationHelper.VerifyData(results, skenderResults, x => x.Adx);
    }

    [Fact]
    public void MatchesTalib()
    {
        var adx = new Adx(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = adx.Update(_data.Bars[i]);
            results.Add(res.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _data.Bars.Close.Select(x => x.Value).ToArray();
        double[] outReal = new double[_data.Bars.Count];

        var retCode = TALib.Functions.Adx(hData, lData, cData, 0..^0, outReal, out var outRange, 14);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.AdxLookback(14);
        ValidationHelper.VerifyData(results, outReal, outRange, lookback);
    }

}
