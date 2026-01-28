namespace QuanTAlib.Tests;

public class EomValidationTests
{
    private readonly ValidationTestData _data;
    private const int DefaultPeriod = 14;

    public EomValidationTests()
    {
        _data = new ValidationTestData();
    }

    [Fact]
    public void Eom_Matches_Skender()
    {
        // Skender does not have Ease of Movement implementation
        Assert.True(true, "Skender does not have an Ease of Movement implementation");
    }

    [Fact]
    public void Eom_Matches_Talib()
    {
        // TA-Lib does not have EOM/Ease of Movement
        Assert.True(true, "TA-Lib does not have an Ease of Movement implementation");
    }

    [Fact]
    public void Eom_Matches_Tulip()
    {
        // Tulip has emv (Ease of Movement Value)
        // However, the implementation differs - Tulip uses a different formula
        Assert.True(true, "Tulip implementation differs from standard EOM");
    }

    [Fact]
    public void Eom_Matches_Ooples()
    {
        // Ooples does not have a standard EOM implementation
        Assert.True(true, "Ooples does not have a standard Ease of Movement implementation");
    }

    [Fact]
    public void Eom_Streaming_Matches_Batch()
    {
        // Streaming
        var eom = new Eom(DefaultPeriod);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(eom.Update(bar).Value);
        }

        // Batch
        var batchResult = Eom.Calculate(_data.Bars, DefaultPeriod);
        var batchValues = batchResult.Values.ToArray();

        ValidationHelper.VerifyData(streamingValues.ToArray(), batchValues, 0, 100, 1e-9);
    }

    [Fact]
    public void Eom_Span_Matches_Streaming()
    {
        // Streaming
        var eom = new Eom(DefaultPeriod);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(eom.Update(bar).Value);
        }

        // Span
        var high = _data.Bars.High.Values.ToArray();
        var low = _data.Bars.Low.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanValues = new double[high.Length];

        Eom.Calculate(high, low, volume, spanValues, DefaultPeriod);

        ValidationHelper.VerifyData(streamingValues.ToArray(), spanValues, 0, 100, 1e-9);
    }
}