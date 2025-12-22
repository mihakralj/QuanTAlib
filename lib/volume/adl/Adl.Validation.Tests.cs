using Xunit;
using QuanTAlib;
using Skender.Stock.Indicators;
using TALib;
using Tulip;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

public class AdlValidationTests
{
    private readonly ValidationTestData _data;

    public AdlValidationTests()
    {
        _data = new ValidationTestData();
    }

    [Fact]
    public void Adl_Matches_Skender()
    {
        // Skender
        var skenderResults = _data.SkenderQuotes.GetAdl();
        var skenderValues = skenderResults.Select(x => x.Adl).ToArray();

        // QuanTAlib
        var adl = new Adl();
        var quantalibValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            quantalibValues.Add(adl.Update(bar).Value);
        }

        ValidationHelper.VerifyData(quantalibValues.ToArray(), skenderValues, 0, 100, ValidationHelper.SkenderTolerance);
    }

    [Fact]
    public void Adl_Matches_Talib()
    {
        // TA-Lib
        var high = _data.Bars.High.Values.ToArray();
        var low = _data.Bars.Low.Values.ToArray();
        var close = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var talibValues = new double[high.Length];
        
        var retCode = TALib.Functions.Ad(high, low, close, volume, 0..^0, talibValues, out var outRange);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        // QuanTAlib
        var adl = new Adl();
        var quantalibValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            quantalibValues.Add(adl.Update(bar).Value);
        }

        ValidationHelper.VerifyData(quantalibValues.ToArray(), talibValues, outRange, 0, 100, ValidationHelper.TalibTolerance);
    }

    [Fact]
    public void Adl_Matches_Tulip()
    {
        // Tulip
        var high = _data.Bars.High.Values.ToArray();
        var low = _data.Bars.Low.Values.ToArray();
        var close = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        
        var tulipIndicator = Tulip.Indicators.ad;
        double[][] inputs = { high, low, close, volume };
        double[] options = Array.Empty<double>();
        double[][] outputs = { new double[high.Length] };

        tulipIndicator.Run(inputs, options, outputs);
        var tulipValues = outputs[0];

        // QuanTAlib
        var adl = new Adl();
        var quantalibValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            quantalibValues.Add(adl.Update(bar).Value);
        }

        ValidationHelper.VerifyData(quantalibValues.ToArray(), tulipValues, 0, 100, ValidationHelper.TulipTolerance);
    }

    [Fact]
    public void Adl_Matches_Ooples()
    {
        // Ooples
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
        var oResult = stockData.CalculateAccumulationDistributionLine();
        var oValues = oResult.OutputValues["Adl"];

        // QuanTAlib
        var adl = new Adl();
        var quantalibValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            quantalibValues.Add(adl.Update(bar).Value);
        }

        ValidationHelper.VerifyData(quantalibValues.ToArray(), oValues.ToArray(), 0, 100, ValidationHelper.OoplesTolerance);
    }
}
