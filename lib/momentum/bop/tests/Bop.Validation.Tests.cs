using Skender.Stock.Indicators;
using TALib;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

public sealed class BopValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public BopValidationTests()
    {
        _data = new ValidationTestData();
    }

    public void Dispose()
    {
        _data.Dispose();
    }

    [Fact]
    public void Validate_Against_Skender()
    {
        var skenderResult = _data.SkenderQuotes.GetBop().ToList();
        var quanTAlibResult = Bop.Batch(_data.Bars);

        ValidationHelper.VerifyData(quanTAlibResult, skenderResult, (x) => x.Bop, skip: 0, tolerance: ValidationHelper.SkenderTolerance);
    }

    [Fact]
    public void Validate_Against_TALib()
    {
        var open = _data.Bars.Open.Values.ToArray();
        var high = _data.Bars.High.Values.ToArray();
        var low = _data.Bars.Low.Values.ToArray();
        var close = _data.Bars.Close.Values.ToArray();

        var talibResult = new double[_data.Bars.Count];
        var retCode = TALib.Functions.Bop(open, high, low, close, 0..^0, talibResult, out var outRange);

        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        var quanTAlibResult = Bop.Batch(_data.Bars);

        ValidationHelper.VerifyData(quanTAlibResult, talibResult, outRange, lookback: 0, skip: 0, tolerance: ValidationHelper.TalibTolerance);
    }

    [Fact]
    public void Validate_Against_Tulip()
    {
        var open = _data.Bars.Open.Values.ToArray();
        var high = _data.Bars.High.Values.ToArray();
        var low = _data.Bars.Low.Values.ToArray();
        var close = _data.Bars.Close.Values.ToArray();

        double[][] inputs = { open, high, low, close };
        double[] options = Array.Empty<double>(); // No options for BOP

        var bopInd = Tulip.Indicators.bop;
        double[][] outputs = { new double[open.Length - bopInd.Start(options)] };
        bopInd.Run(inputs, options, outputs);
        double[] tulipResult = outputs[0];

        var quanTAlibResult = Bop.Batch(_data.Bars);

        ValidationHelper.VerifyData(quanTAlibResult, tulipResult, lookback: 0, skip: 0, tolerance: ValidationHelper.TulipTolerance);
    }

    [Fact]
    public void Validate_Against_Ooples()
    {
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
        var ooplesResult = stockData.CalculateBalanceOfPower().OutputValues["Bop"].ToArray();

        var quanTAlibResult = Bop.Batch(_data.Bars);

        ValidationHelper.VerifyData(quanTAlibResult, ooplesResult, lookback: 0, skip: 0, tolerance: ValidationHelper.OoplesTolerance);
    }
}
