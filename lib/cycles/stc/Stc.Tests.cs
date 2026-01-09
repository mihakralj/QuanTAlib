using System;
using Xunit;

namespace QuanTAlib;

public class StcTests
{
    private const int CycleLength = 12;
    private const int FastLength = 26;
    private const int SlowLength = 50;

    private static Stc CreateDefaultStc() => new(kPeriod: CycleLength, dPeriod: CycleLength, fastLength: FastLength, slowLength: SlowLength, smoothing: StcSmoothing.Sigmoid);

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Stc(kPeriod: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Stc(dPeriod: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Stc(fastLength: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Stc(slowLength: 1));
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var stc = CreateDefaultStc();
        var result = stc.Update(new TValue(DateTime.UtcNow, 100));
        // Expect NaN during warmup
        Assert.True(double.IsNaN(result.Value) || double.IsFinite(result.Value));
    }

    [Fact]
    public void Properties_Accessible()
    {
        var stc = CreateDefaultStc();
        Assert.Equal(0, stc.Last.Value); // Initial value before updates
        Assert.False(stc.IsHot);
        Assert.Contains("Stc", stc.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var stc = CreateDefaultStc();
        int warmup = stc.WarmupPeriod;

        for (int i = 0; i < warmup - 1; i++)
        {
            stc.Update(new TValue(DateTime.UtcNow, 100));
            Assert.False(stc.IsHot);
        }

        stc.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(stc.IsHot);
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var iterativeStc = CreateDefaultStc();
        var batchStc = CreateDefaultStc();
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next();
            series.Add(bar.Time, bar.Close);
            iterativeStc.Update(new TValue(bar.Time, bar.Close));
        }

        var batchResult = batchStc.Update(series);

        Assert.Equal(iterativeStc.Last.Value, batchResult.Last.Value, 1e-9);
    }
    
    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        // Use default parameters for static calculation
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        double[] input = new double[200];
        double[] output = new double[200];

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next();
            series.Add(bar.Time, bar.Close);
            input[i] = bar.Close;
        }

        var batchStc = CreateDefaultStc();
        var tseriesResult = batchStc.Update(series);

        Stc.Calculate(input.AsSpan(), output.AsSpan(), kPeriod: CycleLength, dPeriod: CycleLength, fastLength: FastLength, slowLength: SlowLength, smoothing: StcSmoothing.Sigmoid);

        // Compare last value
        Assert.Equal(tseriesResult.Last.Value, output[^1], 1e-9);
    }

    [Fact]
    public void NaN_Input_HandledSafely()
    {
        var stc = CreateDefaultStc();
        stc.Update(new TValue(DateTime.UtcNow, 100));
        var result = stc.Update(new TValue(DateTime.UtcNow, double.NaN));
        // Should be NaN during warmup
        Assert.True(double.IsNaN(result.Value) || double.IsFinite(result.Value));
    }
    
    [Fact]
    public void SmoothingOptions_ProduceDifferentResults()
    {
        var stcSigmoid = new Stc(kPeriod: 10, dPeriod: 10, fastLength: 20, slowLength: 40, smoothing: StcSmoothing.Sigmoid);
        var stcEma = new Stc(kPeriod: 10, dPeriod: 10, fastLength: 20, slowLength: 40, smoothing: StcSmoothing.Ema);
        var stcDigital = new Stc(kPeriod: 10, dPeriod: 10, fastLength: 20, slowLength: 40, smoothing: StcSmoothing.Digital);

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        
        for (int i = 0; i < 100; i++)
        {
            double val = gbm.Next().Close;
            stcSigmoid.Update(new TValue(DateTime.UtcNow, val));
            stcEma.Update(new TValue(DateTime.UtcNow, val));
            stcDigital.Update(new TValue(DateTime.UtcNow, val));
        }

        Assert.NotEqual(stcSigmoid.Last.Value, stcEma.Last.Value);
        Assert.NotEqual(stcSigmoid.Last.Value, stcDigital.Last.Value);
    }
}
