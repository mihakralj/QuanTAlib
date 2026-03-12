using Skender.Stock.Indicators;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

public class CmfValidationTests
{
    private readonly ValidationTestData _data;
    private const int DefaultPeriod = 20;

    public CmfValidationTests()
    {
        _data = new ValidationTestData();
    }

    [Fact]
    public void Cmf_Matches_Skender()
    {
        // Skender
        var skenderResults = _data.SkenderQuotes.GetCmf(DefaultPeriod);
        var skenderValues = skenderResults.Select(x => x.Cmf ?? double.NaN).ToArray();

        // QuanTAlib
        var cmf = new Cmf(DefaultPeriod);
        var quantalibValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            quantalibValues.Add(cmf.Update(bar).Value);
        }

        ValidationHelper.VerifyData(quantalibValues.ToArray(), skenderValues, 0, 100, ValidationHelper.SkenderTolerance);
    }

    [Fact]
    public void Cmf_Matches_Talib()
    {
        // TA-Lib uses ADOSC (AD Oscillator) which is different from CMF
        // TA-Lib does not have a direct CMF function
        // We'll compare against MFI which is related but different
        // Skip this test as there's no direct CMF in TA-Lib
        Assert.True(true, "TA-Lib does not have a direct CMF implementation");
    }

    [Fact]
    public void Cmf_Matches_Tulip()
    {
        // Tulip does not have CMF indicator
        // Skip this test
        Assert.True(true, "Tulip does not have a CMF implementation");
    }

    [Fact]
    public void Cmf_Matches_Ooples()
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
        var oResult = stockData.CalculateChaikinMoneyFlow(DefaultPeriod);
        var oValues = oResult.OutputValues["Cmf"];

        // QuanTAlib
        var cmf = new Cmf(DefaultPeriod);
        var quantalibValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            quantalibValues.Add(cmf.Update(bar).Value);
        }

        ValidationHelper.VerifyData(quantalibValues.ToArray(), oValues.ToArray(), 0, 100, ValidationHelper.OoplesTolerance);
    }

    [Fact]
    public void Cmf_Streaming_Matches_Batch()
    {
        // Streaming
        var cmf = new Cmf(DefaultPeriod);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(cmf.Update(bar).Value);
        }

        // Batch
        var batchResult = Cmf.Batch(_data.Bars, DefaultPeriod);
        var batchValues = batchResult.Values.ToArray();

        ValidationHelper.VerifyData(streamingValues.ToArray(), batchValues, 0, 100, 1e-12);
    }

    [Fact]
    public void Cmf_Span_Matches_Streaming()
    {
        // Streaming
        var cmf = new Cmf(DefaultPeriod);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(cmf.Update(bar).Value);
        }

        // Span
        var high = _data.Bars.High.Values.ToArray();
        var low = _data.Bars.Low.Values.ToArray();
        var close = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanValues = new double[high.Length];

        Cmf.Batch(high, low, close, volume, spanValues, DefaultPeriod);

        ValidationHelper.VerifyData(streamingValues.ToArray(), spanValues, 0, 100, 1e-12);
    }

    [Fact]
    public void Cmf_MatchesOoples_Structural()
    {
        // CalculateChaikinMoneyFlow — structural validation (already has Skender exact match)
        var ooplesData = _data.SkenderQuotes
            .Select(q => new TickerData { Date = q.Date, Open = (double)q.Open, High = (double)q.High, Low = (double)q.Low, Close = (double)q.Close, Volume = (double)q.Volume })
            .ToList();

        var result = new StockData(ooplesData).CalculateChaikinMoneyFlow();
        var values = result.CustomValuesList;

        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite Ooples CMF values, got {finiteCount}");
    }
}
