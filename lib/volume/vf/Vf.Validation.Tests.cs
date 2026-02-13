// Vf: Mathematical property validation tests
// Volume Force is a QuanTAlib-specific indicator combining price change with volume
// and EMA smoothing. No standard external library equivalents. Validation uses
// mathematical property testing.

namespace QuanTAlib.Tests;

using Xunit;

public class VfValidationTests
{
    private const int DefaultPeriod = 14;
    private const int TestDataLength = 500;

    [Fact]
    public void Vf_Output_IsFiniteForGbmData()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var vf = new Vf(DefaultPeriod);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = vf.Update(bars[i], isNew: true);
            Assert.True(double.IsFinite(result.Value),
                $"Vf output must be finite at bar {i}, got {result.Value}");
        }
    }

    [Fact]
    public void Vf_FirstBar_ReturnsZero()
    {
        var vf = new Vf(DefaultPeriod);
        var bar = new TBar(DateTime.UtcNow, 100, 101, 99, 100, 1000);

        var result = vf.Update(bar, isNew: true);

        // First bar has no previous close, so raw VF = 0
        Assert.Equal(0.0, result.Value, precision: 10);
    }

    [Fact]
    public void Vf_RisingPrice_PositiveForce()
    {
        var vf = new Vf(DefaultPeriod);

        // First bar
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1000);
        vf.Update(bar1, isNew: true);

        // Rising price: positive raw VF
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 105, 100, 105, 1000);
        var result = vf.Update(bar2, isNew: true);

        // rawVF = (105 - 100) * 1000 = 5000, EMA of that should be positive
        Assert.True(result.Value > 0,
            $"Vf should be positive for rising price, got {result.Value}");
    }

    [Fact]
    public void Vf_FallingPrice_NegativeForce()
    {
        var vf = new Vf(DefaultPeriod);

        // First bar
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1000);
        vf.Update(bar1, isNew: true);

        // Falling price: negative raw VF
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 95, 100, 95, 95, 1000);
        var result = vf.Update(bar2, isNew: true);

        // rawVF = (95 - 100) * 1000 = -5000, EMA of that should be negative
        Assert.True(result.Value < 0,
            $"Vf should be negative for falling price, got {result.Value}");
    }

    [Fact]
    public void Vf_ConstantPrice_ZeroForce()
    {
        var vf = new Vf(DefaultPeriod);

        // Feed constant-price bars
        for (int i = 0; i < 50; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                100, 100, 100, 100, 1000);
            vf.Update(bar, isNew: true);
        }

        // No price change → raw VF = 0 each bar → EMA converges to 0
        Assert.Equal(0.0, vf.Last.Value, precision: 8);
    }

    [Fact]
    public void Vf_HighVolume_AmplifiesForce()
    {
        // Low volume
        var vfLow = new Vf(DefaultPeriod);
        var bar1Low = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 100);
        vfLow.Update(bar1Low, isNew: true);
        var bar2Low = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 105, 100, 105, 100);
        vfLow.Update(bar2Low, isNew: true);

        // High volume
        var vfHigh = new Vf(DefaultPeriod);
        var bar1High = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 10000);
        vfHigh.Update(bar1High, isNew: true);
        var bar2High = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 105, 100, 105, 10000);
        vfHigh.Update(bar2High, isNew: true);

        // Higher volume should produce larger absolute VF
        Assert.True(System.Math.Abs(vfHigh.Last.Value) > System.Math.Abs(vfLow.Last.Value),
            $"High volume VF ({vfHigh.Last.Value}) should exceed low volume VF ({vfLow.Last.Value})");
    }

    [Fact]
    public void Vf_BatchAndStreaming_ProduceSameResults()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Batch
        var batchResults = Vf.Batch(bars, DefaultPeriod);

        // Streaming
        var streamVf = new Vf(DefaultPeriod);
        var streamResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            var result = streamVf.Update(bars[i], isNew: true);
            streamResults[i] = result.Value;
        }

        Assert.Equal(batchResults.Count, bars.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(batchResults.Values[i], streamResults[i], precision: 8);
        }
    }

    [Fact]
    public void Vf_SpanAndStreaming_ProduceSameResults()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var spanOutput = new double[bars.Count];

        Vf.Batch(bars.Close.Values, bars.Volume.Values, spanOutput, DefaultPeriod);

        // Streaming
        var streamVf = new Vf(DefaultPeriod);
        for (int i = 0; i < bars.Count; i++)
        {
            var result = streamVf.Update(bars[i], isNew: true);
            Assert.Equal(spanOutput[i], result.Value, precision: 8);
        }
    }

    [Fact]
    public void Vf_DifferentPeriods_ProduceDifferentSmoothing()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var vf3 = new Vf(period: 3);
        var vf50 = new Vf(period: 50);

        for (int i = 0; i < bars.Count; i++)
        {
            vf3.Update(bars[i], isNew: true);
            vf50.Update(bars[i], isNew: true);
        }

        Assert.NotEqual(vf3.Last.Value, vf50.Last.Value);
    }

    [Fact]
    public void Vf_BarCorrection_IsNewFalse_RestoresState()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var vf = new Vf(DefaultPeriod);

        for (int i = 0; i < 30; i++)
        {
            vf.Update(bars[i], isNew: true);
        }

        vf.Update(bars[30], isNew: true);
        double afterNew = vf.Last.Value;

        vf.Update(bars[30], isNew: false);
        double afterCorrection = vf.Last.Value;

        Assert.Equal(afterNew, afterCorrection, precision: 10);
    }
}
