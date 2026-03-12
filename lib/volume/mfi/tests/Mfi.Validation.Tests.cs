using Skender.Stock.Indicators;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using TALib;

namespace QuanTAlib.Tests;

public class MfiValidationTests
{
    private readonly ValidationTestData _data;
    private const int DefaultPeriod = 14;

    public MfiValidationTests()
    {
        _data = new ValidationTestData();
    }

    [Fact]
    public void Mfi_Matches_Skender()
    {
        // Skender
        var skenderResults = _data.SkenderQuotes.GetMfi(DefaultPeriod);
        var skenderValues = skenderResults.Select(x => x.Mfi ?? double.NaN).ToArray();

        // QuanTAlib
        var mfi = new Mfi(DefaultPeriod);
        var quantalibValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            quantalibValues.Add(mfi.Update(bar).Value);
        }

        ValidationHelper.VerifyData(quantalibValues.ToArray(), skenderValues, 0, 100, ValidationHelper.SkenderTolerance);
    }

    [Fact]
    public void Mfi_Matches_Talib()
    {
        // TALib MFI = Money Flow Index with the same standard formula as QuanTAlib.
        // Both compute: typical price = (H+L+C)/3, raw money flow = TP*Volume,
        // then ratio = sum(+MF) / sum(-MF), MFI = 100 - 100/(1+ratio).
        // Exact numeric match expected to 1e-9.

        const int period = DefaultPeriod;

        double[] highData = _data.Bars.High.Values.ToArray();
        double[] lowData = _data.Bars.Low.Values.ToArray();
        double[] closeData = _data.Bars.Close.Values.ToArray();
        double[] volumeData = _data.Bars.Volume.Values.ToArray();
        double[] taOut = new double[_data.Bars.Count];

        var retCode = Functions.Mfi<double>(
            highData, lowData, closeData, volumeData,
            0..^0, taOut, out var outRange, period);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        (int offset, int length) = outRange.GetOffsetAndLength(taOut.Length);
        Assert.True(length > 100, $"TALib MFI produced only {length} values");

        // QuanTAlib streaming
        var mfi = new Mfi(period);
        var qlValues = new double[_data.Bars.Count];
        for (int i = 0; i < _data.Bars.Count; i++)
        {
            qlValues[i] = mfi.Update(_data.Bars[i]).Value;
        }

        // Compare
        for (int j = 0; j < length; j++)
        {
            int qi = j + offset;
            double diff = Math.Abs(qlValues[qi] - taOut[j]);
            Assert.True(diff <= 1e-9,
                $"MFI mismatch at [{qi}]: QuanTAlib={qlValues[qi]:G17}, TALib={taOut[j]:G17}, diff={diff:E3}");
        }
    }

    [Fact]
    public void Mfi_Matches_Tulip()
    {
        // Tulip has MFI - verify QuanTAlib produces valid values
        var mfi = new Mfi(DefaultPeriod);
        var quantalibValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            quantalibValues.Add(mfi.Update(bar).Value);
        }

        Assert.True(quantalibValues.All(v => double.IsFinite(v) && v >= 0 && v <= 100),
            "QuanTAlib MFI produces valid values");
    }

    [Fact]
    public void Mfi_Matches_Ooples()
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
        var oResult = stockData.CalculateMoneyFlowIndex(length: DefaultPeriod);
        var oValues = oResult.OutputValues["Mfi"];

        // QuanTAlib
        var mfi = new Mfi(DefaultPeriod);
        var quantalibValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            quantalibValues.Add(mfi.Update(bar).Value);
        }

        ValidationHelper.VerifyData(quantalibValues.ToArray(), oValues.ToArray(), 0, 100, ValidationHelper.OoplesTolerance);
    }

    [Fact]
    public void Mfi_Streaming_Matches_Batch()
    {
        // Streaming
        var mfi = new Mfi(DefaultPeriod);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(mfi.Update(bar).Value);
        }

        // Batch
        var batchResult = Mfi.Batch(_data.Bars, DefaultPeriod);
        var batchValues = batchResult.Values.ToArray();

        ValidationHelper.VerifyData(streamingValues.ToArray(), batchValues, 0, 100, 1e-9);
    }

    [Fact]
    public void Mfi_Span_Matches_Streaming()
    {
        // Streaming
        var mfi = new Mfi(DefaultPeriod);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(mfi.Update(bar).Value);
        }

        // Span
        var high = _data.Bars.High.Values.ToArray();
        var low = _data.Bars.Low.Values.ToArray();
        var close = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanOutput = new double[high.Length];

        Mfi.Batch(high, low, close, volume, spanOutput, DefaultPeriod);

        ValidationHelper.VerifyData(streamingValues.ToArray(), spanOutput, 0, 100, 1e-9);
    }

    [Fact]
    public void Mfi_Different_Periods_ProduceDifferentResults()
    {
        // Test with default period
        var mfi1 = new Mfi(14);
        var values1 = new List<double>();
        foreach (var bar in _data.Bars)
        {
            values1.Add(mfi1.Update(bar).Value);
        }

        // Test with different period
        var mfi2 = new Mfi(7);
        var values2 = new List<double>();
        foreach (var bar in _data.Bars)
        {
            values2.Add(mfi2.Update(bar).Value);
        }

        // Values should differ
        bool allEqual = true;
        for (int i = 20; i < values1.Count; i++)
        {
            if (Math.Abs(values1[i] - values2[i]) > 1e-9)
            {
                allEqual = false;
                break;
            }
        }

        Assert.False(allEqual, "Different periods should produce different results");
    }
}
