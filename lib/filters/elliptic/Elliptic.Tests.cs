using Xunit;

namespace QuanTAlib;

public class EllipticTests
{
    private readonly GBM _gbm;

    public EllipticTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Elliptic(1));
        var filter = new Elliptic(10);
        Assert.NotNull(filter);
        Assert.Equal("Elliptic(10)", filter.Name);
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        int period = 20;
        var data = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // 1. Batch
        var batchResult = new Elliptic(period).Update(series);

        // 2. Streaming
        var streaming = new Elliptic(period);
        var streamingResults = new List<double>();
        foreach (var item in series)
        {
            streamingResults.Add(streaming.Update(item).Value);
        }

        // 3. Span
        double[] spanInput = series.Values.ToArray();
        double[] spanOutput = new double[spanInput.Length];
        Elliptic.Calculate(spanInput, spanOutput, period);

        // Assert
        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResults[i], 1e-9);
            Assert.Equal(batchResult[i].Value, spanOutput[i], 1e-9);
        }
    }

    [Fact]
    public void FlatLine_ReturnsSameValue()
    {
        var filter = new Elliptic(10);
        double val = 100.0;
        
        // Warmup
        for (int i = 0; i < 20; i++)
        {
            filter.Update(new TValue(DateTime.UtcNow, val));
        }

        // Check consistency
        var result = filter.Update(new TValue(DateTime.UtcNow, val));
        Assert.Equal(val, result.Value, 1e-6);
    }

    [Fact]
    public void Handle_NaN_Input()
    {
        var filter = new Elliptic(5);
        filter.Update(new TValue(DateTime.UtcNow, 100));
        var result = filter.Update(new TValue(DateTime.UtcNow, double.NaN));
        
        Assert.Equal(100.0, result.Value, 0.1); // Should stay close to last valid
    }

    [Fact]
    public void Chainability_Works()
    {
        var source = new TSeries();
        var filter = new Elliptic(source, 10);
        bool eventFired = false;
        filter.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.True(eventFired);
        Assert.NotEqual(0, filter.Last.Value);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var filter = new Elliptic(10);
        filter.Update(new TValue(DateTime.UtcNow, 100));
        filter.Reset();
        
        Assert.False(filter.IsHot);
        Assert.Equal(0, filter.Last.Value);
    }
}