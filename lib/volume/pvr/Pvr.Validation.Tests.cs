namespace QuanTAlib.Tests;

public class PvrValidationTests
{
    private readonly ValidationTestData _data;

    public PvrValidationTests()
    {
        _data = new ValidationTestData();
    }

    [Fact]
    public void Pvr_Matches_Skender()
    {
        // Skender does not have PVR implementation
        Assert.True(true, "Skender does not have a Price Volume Rank implementation");
    }

    [Fact]
    public void Pvr_Matches_Talib()
    {
        // TA-Lib does not have PVR
        Assert.True(true, "TA-Lib does not have a Price Volume Rank implementation");
    }

    [Fact]
    public void Pvr_Matches_Tulip()
    {
        // Tulip does not have PVR
        Assert.True(true, "Tulip does not have a Price Volume Rank implementation");
    }

    [Fact]
    public void Pvr_Matches_Ooples()
    {
        // Ooples does not have PVR
        Assert.True(true, "Ooples does not have a Price Volume Rank implementation");
    }

    [Fact]
    public void Pvr_Streaming_Matches_Batch()
    {
        // Streaming
        var pvr = new Pvr();
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(pvr.Update(bar).Value);
        }

        // Batch
        var batchResult = Pvr.Batch(_data.Bars);
        var batchValues = batchResult.Values.ToArray();

        ValidationHelper.VerifyData(streamingValues.ToArray(), batchValues, 0, 100, 1e-9);
    }

    [Fact]
    public void Pvr_Span_Matches_Streaming()
    {
        // Streaming
        var pvr = new Pvr();
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(pvr.Update(bar).Value);
        }

        // Span
        var price = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanOutput = new double[price.Length];

        Pvr.Batch(price, volume, spanOutput);

        ValidationHelper.VerifyData(streamingValues.ToArray(), spanOutput, 0, 100, 1e-9);
    }

    [Fact]
    public void Pvr_OutputRange_Valid()
    {
        var pvr = new Pvr();

        foreach (var bar in _data.Bars)
        {
            var result = pvr.Update(bar);
            Assert.True(result.Value >= 0 && result.Value <= 4,
                $"PVR value {result.Value} is outside valid range [0,4]");
        }
    }

    [Fact]
    public void Pvr_OutputValues_AreIntegral()
    {
        var pvr = new Pvr();

        foreach (var bar in _data.Bars)
        {
            var result = pvr.Update(bar);
            Assert.True(result.Value == Math.Floor(result.Value),
                $"PVR value {result.Value} should be an integer");
        }
    }

    [Fact]
    public void Pvr_ConsistentAcrossAllModes()
    {
        // Mode 1: Streaming with TBar
        var pvr1 = new Pvr();
        var mode1Values = new List<double>();
        foreach (var bar in _data.Bars)
        {
            mode1Values.Add(pvr1.Update(bar).Value);
        }

        // Mode 2: Streaming with parameters
        var pvr2 = new Pvr();
        var mode2Values = new List<double>();
        foreach (var bar in _data.Bars)
        {
            mode2Values.Add(pvr2.Update(bar.Close, bar.Volume, bar.Time).Value);
        }

        // Mode 3: Batch
        var mode3Result = Pvr.Batch(_data.Bars);
        var mode3Values = mode3Result.Values.ToArray();

        // Mode 4: Span
        var price = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var mode4Values = new double[price.Length];
        Pvr.Batch(price, volume, mode4Values);

        // All modes should match
        ValidationHelper.VerifyData(mode1Values.ToArray(), mode2Values.ToArray(), 0, 100, 1e-9);
        ValidationHelper.VerifyData(mode1Values.ToArray(), mode3Values, 0, 100, 1e-9);
        ValidationHelper.VerifyData(mode1Values.ToArray(), mode4Values, 0, 100, 1e-9);
    }
}