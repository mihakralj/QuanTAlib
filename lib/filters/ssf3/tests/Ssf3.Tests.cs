
namespace QuanTAlib.Tests;

public class Ssf3Tests
{
    private readonly GBM _gbm;

    public Ssf3Tests()
    {
        _gbm = new GBM();
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ssf3(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ssf3(-1));
        var ssf = new Ssf3(1); // period=1 is valid
        Assert.NotNull(ssf);
    }

    [Fact]
    public void Calculate_ThrowsWhenDestinationTooSmall()
    {
        var source = new double[10];
        var destination = new double[5];
        Assert.Throws<ArgumentOutOfRangeException>(() => Ssf3.Batch(source, destination, 5, double.NaN));
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var ssf = new Ssf3(10);
        Assert.False(ssf.IsHot);
        ssf.Update(new TValue(DateTime.UtcNow, 100));
        Assert.False(ssf.IsHot);
        ssf.Update(new TValue(DateTime.UtcNow, 101));
        Assert.False(ssf.IsHot);
        ssf.Update(new TValue(DateTime.UtcNow, 102));
        Assert.False(ssf.IsHot);
        ssf.Update(new TValue(DateTime.UtcNow, 103));
        Assert.True(ssf.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var ssf = new Ssf3(10);
        for (int i = 0; i < 5; i++)
        {
            ssf.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(ssf.IsHot);

        ssf.Reset();
        Assert.False(ssf.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var ssf = new Ssf3(10);
        ssf.Update(new TValue(DateTime.UtcNow, 100));
        var result = ssf.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.Equal(100, result.Value);
    }

    [Fact]
    public void Initial_NaN_Input_ReturnsNaN()
    {
        var ssf = new Ssf3(10);
        var result = ssf.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsNaN(result.Value));
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int period = 10;
        var bars = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = new Ssf3(period).Update(series);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Ssf3.Batch(spanInput, spanOutput, period, double.NaN);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Ssf3(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Ssf3(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert
        Assert.Equal(expected, spanResult, 1e-9);
        Assert.Equal(expected, streamingResult, 1e-9);
        Assert.Equal(expected, eventingResult, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        int period = 10;
        var ssf = new Ssf3(period);

        // Feed 10 values
        for (int i = 0; i < 10; i++)
        {
            ssf.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        double expected = ssf.Last.Value;

        // Feed 5 updates with isNew=false
        for (int i = 0; i < 5; i++)
        {
            ssf.Update(new TValue(DateTime.UtcNow, 200 + i), isNew: false);
        }

        // Feed original 10th value again with isNew=false
        var result = ssf.Update(new TValue(DateTime.UtcNow, 109), isNew: false);

        Assert.Equal(expected, result.Value, 1e-9);
    }

    [Fact]
    public void ConstantInput_ConvergesToConstant()
    {
        var ssf = new Ssf3(20);

        // Feed constant value
        for (int i = 0; i < 200; i++)
        {
            ssf.Update(new TValue(DateTime.UtcNow, 100));
        }

        Assert.Equal(100, ssf.Last.Value, 1e-3);
    }
}
