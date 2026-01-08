using Xunit;

namespace QuanTAlib.Tests;

public class WienerTests
{
    private readonly GBM _gbm;

    public WienerTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Wiener(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Wiener(10, smoothPeriod: 1));
        
        var wiener = new Wiener(10, smoothPeriod: 10);
        Assert.NotNull(wiener);
        Assert.Equal("Wiener(10,10)", wiener.Name);
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        var data = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;
        int period = 20;
        int smoothPeriod = 5;

        // 1. Batch Mode
        var batchResult = new Wiener(period, smoothPeriod).Update(series);

        // 2. Streaming Mode
        var streaming = new Wiener(period, smoothPeriod);
        var streamingResults = new TSeries();
        foreach (var item in series)
        {
            streamingResults.Add(streaming.Update(item));
        }

        // 3. Span Mode
        double[] spanInput = series.Values.ToArray();
        double[] spanOutput = new double[spanInput.Length];
        Wiener.Calculate(spanInput, spanOutput, period, smoothPeriod);

        // Assert
        for (int i = 0; i < series.Count; i++)
        {
            double batchVal = batchResult[i].Value;
            double streamVal = streamingResults[i].Value;
            double spanVal = spanOutput[i];

            if (double.IsNaN(batchVal))
            {
                Assert.True(double.IsNaN(streamVal));
                Assert.True(double.IsNaN(spanVal));
            }
            else
            {
                Assert.Equal(batchVal, streamVal, 1e-9);
                Assert.Equal(batchVal, spanVal, 1e-9);
            }
        }
    }

    [Fact]
    public void HandlesNaN()
    {
        var wiener = new Wiener(5, 5);
        
        wiener.Update(new TValue(DateTime.UtcNow, 100));
        wiener.Update(new TValue(DateTime.UtcNow, double.NaN));
        wiener.Update(new TValue(DateTime.UtcNow, 102));
        
        // Should produce valid result if sufficient valid data exists within window (or handle it gracefully)
        Assert.True(double.IsFinite(wiener.Last.Value));
    }

    [Fact]
    public void WarmupPeriod_IsCorrect()
    {
        int period = 20;
        int smooth = 10;
        var wiener = new Wiener(period, smooth);
        // Requirement: WarmupPeriod = Math.Max(period, smooth)
        Assert.Equal(Math.Max(period, smooth), wiener.WarmupPeriod);
        Assert.False(wiener.IsHot);

        for (int i = 0; i < Math.Max(period, smooth); i++)
        {
            wiener.Update(new TValue(DateTime.UtcNow, 100));
        }
        
        Assert.True(wiener.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var wiener = new Wiener(10, 5);
        int warmup = Math.Max(10, 5);

        // Fill up to make it Hot
        for (int i = 0; i < warmup; i++)
        {
            wiener.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(wiener.IsHot);

        wiener.Reset();
        Assert.False(wiener.IsHot);
    }
}
