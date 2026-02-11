namespace QuanTAlib.Tests;

public class WadValidationTests
{
    private readonly ValidationTestData _data;

    public WadValidationTests()
    {
        _data = new ValidationTestData();
    }

    [Fact]
    public void Wad_BatchMatchesStreaming()
    {
        // Batch calculation
        var batchResult = Wad.Batch(_data.Bars);

        // Streaming calculation
        var wad = new Wad();
        var streamingResult = wad.Update(_data.Bars);

        // Compare all values
        Assert.Equal(batchResult.Count, streamingResult.Count);
        for (int i = 0; i < batchResult.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResult[i].Value, precision: 10);
        }
    }

    [Fact]
    public void Wad_SpanMatchesStreaming()
    {
        var high = _data.Bars.High.Values.ToArray();
        var low = _data.Bars.Low.Values.ToArray();
        var close = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanOutput = new double[high.Length];

        // Span calculation
        Wad.Batch(high, low, close, volume, spanOutput);

        // Streaming calculation
        var wad = new Wad();
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(wad.Update(bar).Value);
        }

        // Compare all values
        Assert.Equal(spanOutput.Length, streamingValues.Count);
        for (int i = 0; i < spanOutput.Length; i++)
        {
            Assert.Equal(spanOutput[i], streamingValues[i], precision: 10);
        }
    }
}