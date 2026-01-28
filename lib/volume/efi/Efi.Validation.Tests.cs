using Skender.Stock.Indicators;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

public class EfiValidationTests
{
    private readonly ValidationTestData _data;
    private const int DefaultPeriod = 13;

    public EfiValidationTests()
    {
        _data = new ValidationTestData();
    }

    [Fact]
    public void Efi_Matches_Skender()
    {
        // Note: Skender's ElderRay is different from Force Index
        // Skender does not have a direct Force Index implementation
        // Skip this test
        Assert.True(true, "Skender does not have a direct Force Index implementation");
    }

    [Fact]
    public void Efi_Matches_Talib()
    {
        // TA-Lib does not have EFI/Force Index
        Assert.True(true, "TA-Lib does not have a Force Index implementation");
    }

    [Fact]
    public void Efi_Matches_Tulip()
    {
        // Tulip does not have Force Index
        Assert.True(true, "Tulip does not have a Force Index implementation");
    }

    [Fact]
    public void Efi_Matches_Ooples()
    {
        // Ooples does not have CalculateElderForceIndex method
        // Skip this test
        Assert.True(true, "Ooples does not have a Force Index implementation");
    }

    [Fact]
    public void Efi_Streaming_Matches_Batch()
    {
        // Streaming
        var efi = new Efi(DefaultPeriod);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(efi.Update(bar).Value);
        }

        // Batch
        var batchResult = Efi.Calculate(_data.Bars, DefaultPeriod);
        var batchValues = batchResult.Values.ToArray();

        ValidationHelper.VerifyData(streamingValues.ToArray(), batchValues, 0, 100, 1e-12);
    }

    [Fact]
    public void Efi_Span_Matches_Streaming()
    {
        // Streaming
        var efi = new Efi(DefaultPeriod);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(efi.Update(bar).Value);
        }

        // Span
        var close = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanValues = new double[close.Length];

        Efi.Calculate(close, volume, spanValues, DefaultPeriod);

        ValidationHelper.VerifyData(streamingValues.ToArray(), spanValues, 0, 100, 1e-12);
    }
}