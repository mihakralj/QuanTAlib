using Xunit;

namespace QuanTAlib.Tests;

public class HannTests
{
    private readonly GBM _gbm;

    public HannTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hann(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hann(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hann(-1));
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var hann = new Hann(10);
        var result = hann.Update(new TValue(DateTime.UtcNow, 100));
        Assert.NotEqual(0.0, result.Value);
        Assert.Equal(result.Value, hann.Last.Value);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var hann = new Hann(10);
        Assert.Equal(10, hann.Length);
        Assert.Contains("Hann", hann.Name, StringComparison.Ordinal);
        Assert.False(hann.IsHot);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        const int length = 5;
        var hann = new Hann(length);

        for (int i = 0; i < length; i++)
        {
            Assert.False(hann.IsHot);
            hann.Update(new TValue(DateTime.UtcNow, 100));
        }

        // After length updates calling IsHot property again should return true?
        // Wait, buffer size is length. If we add length items, it becomes full.
        // Update method updates IsHot state.

        Assert.True(hann.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue_Or_Ignores()
    {
        // Hann implementation dynamically normalizes weights.
        // If a value is NaN, it is skipped and weights are renormalized.
        var hann = new Hann(5);

        hann.Update(new TValue(DateTime.UtcNow, 100));
        var r1 = hann.Update(new TValue(DateTime.UtcNow, double.NaN));

        // At index 1 (second point), if input is NaN:
        // History: [100, NaN] (newest)
        // Only 100 is valid. It will be weighted by _weights[1] (if i=1 in loop).
        // Wait, loop:
        // i=0: buffer[newest] = NaN. Skipped.
        // i=1: buffer[oldest] = 100. Weight = _weights[1].
        // Result = 100 * w[1] / w[1] = 100.

        Assert.Equal(100.0, r1.Value);
    }

    [Fact]
    public void AllNaN_ReturnsInput_Or_NaN()
    {
        var hann = new Hann(5);
        var r = hann.Update(new TValue(DateTime.UtcNow, double.NaN));
        // Fallback in code: if wSum <= epsilon, result = input.Value
        Assert.True(double.IsNaN(r.Value));
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        int length = 10;
        var hannBatch = new Hann(length);
        var hannIter = new Hann(length);

        var series = new TSeries();
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        foreach (var bar in bars)
        {
            series.Add(bar.Time, bar.Close);
        }

        var batchResult = hannBatch.Update(series);

        for (int i = 0; i < series.Count; i++)
        {
            var iterResult = hannIter.Update(series[i]);
            Assert.Equal(batchResult[i].Value, iterResult.Value, 1e-9);
        }
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        int length = 10;
        var series = new TSeries();
        double[] input = new double[100];
        double[] output = new double[100];

        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        for (int i = 0; i < 100; i++)
        {
            input[i] = bars[i].Close;
            series.Add(bars[i].Time, bars[i].Close);
        }

        var tseriesResult = new Hann(length).Update(series);
        Hann.Calculate(input.AsSpan(), output.AsSpan(), length);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var hann = new Hann(5);
        hann.Update(new TValue(DateTime.UtcNow, 100));
        Assert.False(hann.IsHot);

        // Fill it
        for (int i = 0; i < 5; i++)
        {
            hann.Update(new TValue(DateTime.UtcNow, 100));
        }

        Assert.True(hann.IsHot);

        hann.Reset();
        Assert.False(hann.IsHot);
        Assert.Equal(0, hann.Last.Value);
    }
}
