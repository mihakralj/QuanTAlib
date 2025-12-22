using System;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;
using TALib;
using Tulip;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Enums;
using Xunit;
using QuanTAlib.Tests;

namespace QuanTAlib;

public sealed class AroonOscValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public AroonOscValidationTests()
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
        var aroon = new AroonOsc(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = aroon.Update(_data.Bars[i]);
            results.Add(res.Value);
        }

        var skenderResults = _data.SkenderQuotes.GetAroon(14).ToList();

        // Verify Oscillator
        ValidationHelper.VerifyData(results, skenderResults, x => x.Oscillator);
    }

    [Fact]
    public void MatchesTalib()
    {
        var aroon = new AroonOsc(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = aroon.Update(_data.Bars[i]);
            results.Add(res.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] outAroonOsc = new double[_data.Bars.Count];

        // TA-Lib AroonOsc
        var retCodeOsc = TALib.Functions.AroonOsc(hData, lData, 0..^0, outAroonOsc, out var outRangeOsc, 14);
        Assert.Equal(Core.RetCode.Success, retCodeOsc);

        int lookback = TALib.Functions.AroonLookback(14);
        
        // Verify Oscillator
        ValidationHelper.VerifyData(results, outAroonOsc, outRangeOsc, lookback);
    }

    [Fact]
    public void MatchesTulip()
    {
        var aroon = new AroonOsc(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = aroon.Update(_data.Bars[i]);
            results.Add(res.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[][] inputs = { hData, lData };
        double[] options = { 14 };

        // Tulip AroonOsc
        var aroonOscInd = Tulip.Indicators.aroonosc;
        double[][] outputsOsc = { new double[hData.Length - 14] };
        aroonOscInd.Run(inputs, options, outputsOsc);
        double[] tulipOsc = outputsOsc[0];

        // Verify Oscillator
        ValidationHelper.VerifyData(results, tulipOsc, lookback: 14);
    }

    [Fact(Skip = "Ooples implementation deviates from standard even with adjustment")]
    public void MatchesOoples()
    {
        // Note: OoplesFinance implementation of Aroon Oscillator differs by exactly 1 period (100/Period)
        // from Skender, TA-Lib, Tulip, and QuanTAlib.
        // We adjust Ooples results by adding 100/Period to match the standard implementation.

        var aroon = new AroonOsc(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = aroon.Update(_data.Bars[i]);
            results.Add(res.Value);
        }

        var ooplesData = _data.SkenderQuotes.Select(q => new TickerData
        {
            Date = q.Date,
            Open = (double)q.Open,
            High = (double)q.High,
            Low = (double)q.Low,
            Close = (double)q.Close,
            Volume = (double)q.Volume
        }).ToList();

        var stockData = new StockData(ooplesData);

        // Ooples only provides CalculateAroonOscillator
        var aroonOscResults = stockData.CalculateAroonOscillator(14);
        var ooplesOsc = aroonOscResults.OutputValues["Aroon"]
            .Select(x => x + (100.0 / 14.0))
            .ToArray();

        // Verify Oscillator
        ValidationHelper.VerifyData(results, ooplesOsc, lookback: 14, tolerance: ValidationHelper.OoplesTolerance);
    }
}
