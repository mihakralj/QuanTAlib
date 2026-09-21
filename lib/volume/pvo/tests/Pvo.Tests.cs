using Xunit;

namespace QuanTAlib.Tests;

public class PvoTests
{
    private const int DefaultFastPeriod = 12;
    private const int DefaultSlowPeriod = 26;
    private const int DefaultSignalPeriod = 9;

    [Fact]
    public void Constructor_DefaultParameters_CreatesValidIndicator()
    {
        var pvo = new Pvo();
        Assert.Equal($"Pvo({DefaultFastPeriod},{DefaultSlowPeriod},{DefaultSignalPeriod})", pvo.Name);
        Assert.Equal(DefaultSlowPeriod, pvo.WarmupPeriod);
        Assert.False(pvo.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters_CreatesValidIndicator()
    {
        var pvo = new Pvo(fastPeriod: 5, slowPeriod: 10, signalPeriod: 3);
        Assert.Equal("Pvo(5,10,3)", pvo.Name);
        Assert.Equal(10, pvo.WarmupPeriod);
    }

    [Fact]
    public void Constructor_InvalidFastPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Pvo(fastPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Pvo(fastPeriod: -1));
    }

    [Fact]
    public void Constructor_InvalidSlowPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Pvo(slowPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Pvo(slowPeriod: -1));
    }

    [Fact]
    public void Constructor_InvalidSignalPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Pvo(signalPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Pvo(signalPeriod: -1));
    }

    [Fact]
    public void Constructor_FastNotLessThanSlow_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Pvo(fastPeriod: 26, slowPeriod: 26));
        Assert.Throws<ArgumentException>(() => new Pvo(fastPeriod: 30, slowPeriod: 26));
    }

    [Fact]
    public void Update_WithTBar_ReturnsValidValue()
    {
        var pvo = new Pvo();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result = pvo.Update(bar);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_WithTValue_ReturnsValidValue()
    {
        var pvo = new Pvo();
        var value = new TValue(DateTime.UtcNow, 1000000);
        var result = pvo.Update(value);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_VolumeIncrease_ReturnsPositiveValue()
    {
        var pvo = new Pvo(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var time = DateTime.UtcNow;

        // Constant volume first
        for (int i = 0; i < 50; i++)
        {
            pvo.Update(new TBar(time.AddMinutes(i), 100, 105, 95, 102, 100000));
        }

        // Then increasing volume - fast EMA will be higher than slow
        for (int i = 50; i < 100; i++)
        {
            pvo.Update(new TBar(time.AddMinutes(i), 100, 105, 95, 102, 100000 + ((i - 50) * 50000)));
        }

        // Fast EMA responds quicker to volume increase, should be positive
        Assert.True(pvo.Last.Value > 0, "PVO should be positive when volume is increasing");
    }

    [Fact]
    public void Update_VolumeDecrease_ReturnsNegativeValue()
    {
        var pvo = new Pvo(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var time = DateTime.UtcNow;

        // High constant volume first
        for (int i = 0; i < 50; i++)
        {
            pvo.Update(new TBar(time.AddMinutes(i), 100, 105, 95, 102, 1000000));
        }

        // Then decreasing volume - fast EMA will be lower than slow
        for (int i = 50; i < 100; i++)
        {
            pvo.Update(new TBar(time.AddMinutes(i), 100, 105, 95, 102, 1000000 - ((i - 50) * 15000)));
        }

        // Fast EMA responds quicker to volume decrease, should be negative
        Assert.True(pvo.Last.Value < 0, "PVO should be negative when volume is decreasing");
    }

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var pvo = new Pvo();
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result1 = pvo.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 95, 110, 1100000);
        var result2 = pvo.Update(bar2, isNew: true);

        Assert.NotEqual(result1.Time, result2.Time);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var pvo = new Pvo();
        var time = DateTime.UtcNow;
        var bar1 = new TBar(time, 100, 110, 90, 105, 1000000);
        pvo.Update(bar1, isNew: true);

        var bar2 = new TBar(time.AddMinutes(1), 105, 115, 95, 110, 1100000);
        var result1 = pvo.Update(bar2, isNew: true);

        // Update same bar with different volume
        var bar2Updated = new TBar(time.AddMinutes(1), 105, 120, 95, 118, 2000000);
        var result2 = pvo.Update(bar2Updated, isNew: false);

        Assert.Equal(result1.Time, result2.Time);
        Assert.NotEqual(result1.Value, result2.Value);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresState()
    {
        var pvo = new Pvo(fastPeriod: 5, slowPeriod: 10, signalPeriod: 5);
        var time = DateTime.UtcNow;

        // Build up state
        for (int i = 0; i < 15; i++)
        {
            pvo.Update(new TBar(time.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000 + (i * 10000)), isNew: true);
        }

        // New bar
        var originalBar = new TBar(time.AddMinutes(15), 120, 130, 110, 125, 250000);
        var originalResult = pvo.Update(originalBar, isNew: true);

        // Correction with different volume
        var correctionBar = new TBar(time.AddMinutes(15), 110, 150, 90, 140, 500000);
        var correctedResult = pvo.Update(correctionBar, isNew: false);

        Assert.NotEqual(originalResult.Value, correctedResult.Value);
        Assert.True(double.IsFinite(correctedResult.Value));
    }

    [Fact]
    public void Update_WarmupPeriod_IsHotBecomesTrueAfterWarmup()
    {
        var pvo = new Pvo(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var time = DateTime.UtcNow;

        Assert.False(pvo.IsHot);

        // Feed many bars until compensators decay below threshold (1e-10)
        for (int i = 0; i < 100; i++)
        {
            pvo.Update(new TBar(time.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000), isNew: true);
        }

        Assert.True(pvo.IsHot);
    }

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var pvo = new Pvo(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var time = DateTime.UtcNow;

        // Process some valid bars first
        for (int i = 0; i < 10; i++)
        {
            pvo.Update(new TBar(time.AddMinutes(i), 100, 105, 95, 102, 100000));
        }

        // Process bar with NaN volume
        var nanBar = new TBar(time.AddMinutes(10), 105, 110, 100, 108, double.NaN);
        var result = pvo.Update(nanBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_ZeroVolume_HandlesGracefully()
    {
        var pvo = new Pvo(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var time = DateTime.UtcNow;

        pvo.Update(new TBar(time, 100, 110, 90, 105, 100000));
        var result = pvo.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 0));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Signal_CalculatedAlongsidePvo()
    {
        var pvo = new Pvo(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            pvo.Update(new TBar(time.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000 + (i * 10000)));
        }

        Assert.True(double.IsFinite(pvo.Signal.Value));
        Assert.Equal(pvo.Last.Time, pvo.Signal.Time);
    }

    [Fact]
    public void Histogram_CalculatedCorrectly()
    {
        var pvo = new Pvo(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            pvo.Update(new TBar(time.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000 + (i * 10000)));
        }

        Assert.True(double.IsFinite(pvo.Histogram.Value));
        Assert.Equal(pvo.Last.Value - pvo.Signal.Value, pvo.Histogram.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var pvo = new Pvo(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var time = DateTime.UtcNow;

        // Process many bars until IsHot becomes true
        for (int i = 0; i < 100; i++)
        {
            pvo.Update(new TBar(time.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000), isNew: true);
        }

        Assert.True(double.IsFinite(pvo.Last.Value));

        pvo.Reset();

        Assert.False(pvo.IsHot);
        Assert.Equal(default, pvo.Last);
        Assert.Equal(default, pvo.Signal);
        Assert.Equal(default, pvo.Histogram);
    }

    [Fact]
    public void UpdateWithSignal_ReturnsAllSeries()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 100; i++)
        {
            bars.Add(gbm.Next());
        }

        var pvo = new Pvo();
        var (pvoSeries, signalSeries, histogramSeries) = pvo.UpdateWithSignal(bars);

        Assert.Equal(bars.Count, pvoSeries.Count);
        Assert.Equal(bars.Count, signalSeries.Count);
        Assert.Equal(bars.Count, histogramSeries.Count);

        // Verify values are finite
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.True(double.IsFinite(pvoSeries[i].Value));
            Assert.True(double.IsFinite(signalSeries[i].Value));
            Assert.True(double.IsFinite(histogramSeries[i].Value));
        }
    }

    [Fact]
    public void BatchCalculate_MatchesStreaming()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 100; i++)
        {
            bars.Add(gbm.Next());
        }

        // Streaming
        var pvo = new Pvo();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(pvo.Update(bar).Value);
        }

        // Batch
        var batchResult = Pvo.Batch(bars);

        Assert.Equal(bars.Count, batchResult.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingValues[i], batchResult[i].Value, 10);
        }
    }

    [Fact]
    public void SpanCalculate_MatchesStreaming()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 100; i++)
        {
            bars.Add(gbm.Next());
        }

        // Streaming
        var pvo = new Pvo();
        var streamingPvo = new List<double>();
        var streamingSignal = new List<double>();
        var streamingHistogram = new List<double>();
        foreach (var bar in bars)
        {
            pvo.Update(bar);
            streamingPvo.Add(pvo.Last.Value);
            streamingSignal.Add(pvo.Signal.Value);
            streamingHistogram.Add(pvo.Histogram.Value);
        }

        // Span
        var volume = bars.Volume.Values.ToArray();
        var spanPvo = new double[bars.Count];
        var spanSignal = new double[bars.Count];
        var spanHistogram = new double[bars.Count];

        Pvo.Batch(volume, spanPvo, spanSignal, spanHistogram);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingPvo[i], spanPvo[i], 10);
            Assert.Equal(streamingSignal[i], spanSignal[i], 10);
            Assert.Equal(streamingHistogram[i], spanHistogram[i], 10);
        }
    }

    [Fact]
    public void SpanCalculate_InvalidLengths_ThrowsArgumentException()
    {
        var volume = new double[100];
        var output = new double[99]; // Different length
        var signal = new double[100];
        var histogram = new double[100];

        Assert.Throws<ArgumentException>(() => Pvo.Batch(volume, output, signal, histogram));
    }

    [Fact]
    public void SpanCalculate_InvalidFastPeriod_ThrowsArgumentException()
    {
        var volume = new double[100];
        var output = new double[100];
        var signal = new double[100];
        var histogram = new double[100];

        Assert.Throws<ArgumentException>(() => Pvo.Batch(volume, output, signal, histogram, fastPeriod: 0));
    }

    [Fact]
    public void SpanCalculate_InvalidSlowPeriod_ThrowsArgumentException()
    {
        var volume = new double[100];
        var output = new double[100];
        var signal = new double[100];
        var histogram = new double[100];

        Assert.Throws<ArgumentException>(() => Pvo.Batch(volume, output, signal, histogram, slowPeriod: 0));
    }

    [Fact]
    public void SpanCalculate_InvalidSignalPeriod_ThrowsArgumentException()
    {
        var volume = new double[100];
        var output = new double[100];
        var signal = new double[100];
        var histogram = new double[100];

        Assert.Throws<ArgumentException>(() => Pvo.Batch(volume, output, signal, histogram, signalPeriod: 0));
    }

    [Fact]
    public void SpanCalculate_FastNotLessThanSlow_ThrowsArgumentException()
    {
        var volume = new double[100];
        var output = new double[100];
        var signal = new double[100];
        var histogram = new double[100];

        Assert.Throws<ArgumentException>(() => Pvo.Batch(volume, output, signal, histogram, fastPeriod: 26, slowPeriod: 26));
    }

    [Fact]
    public void SpanCalculate_EmptyInput_HandlesGracefully()
    {
        var volume = Array.Empty<double>();
        var output = Array.Empty<double>();
        var signal = Array.Empty<double>();
        var histogram = Array.Empty<double>();

        // Should not throw
        Pvo.Batch(volume, output, signal, histogram);

        Assert.Empty(output);
    }

    [Fact]
    public void Event_PubFiresOnUpdate()
    {
        var pvo = new Pvo();
        TValue? receivedValue = null;
        bool receivedIsNew = false;

        pvo.Pub += (object? sender, in TValueEventArgs args) =>
        {
            receivedValue = args.Value;
            receivedIsNew = args.IsNew;
        };

        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        pvo.Update(bar, isNew: true);

        Assert.NotNull(receivedValue);
        Assert.True(receivedIsNew);
    }

    [Fact]
    public void CustomPeriods_AffectsResults()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 100; i++)
        {
            bars.Add(gbm.Next());
        }

        var pvo1 = new Pvo(fastPeriod: 5, slowPeriod: 10, signalPeriod: 3);
        var pvo2 = new Pvo(fastPeriod: 10, slowPeriod: 20, signalPeriod: 5);

        foreach (var bar in bars)
        {
            pvo1.Update(bar);
            pvo2.Update(bar);
        }

        // Different periods should produce different results
        Assert.NotEqual(pvo1.Last.Value, pvo2.Last.Value);
    }

    [Fact]
    public void LargeDataset_HandlesWithoutError()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 10000; i++)
        {
            bars.Add(gbm.Next());
        }

        var pvo = new Pvo();
        foreach (var bar in bars)
        {
            var result = pvo.Update(bar);
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.True(pvo.IsHot);
    }

    [Fact]
    public void ConstantVolume_PvoIsZero()
    {
        var pvo = new Pvo(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var time = DateTime.UtcNow;

        // With constant volume, fast and slow EMAs should converge to same value
        // resulting in PVO = 0
        for (int i = 0; i < 200; i++)
        {
            pvo.Update(new TBar(time.AddMinutes(i), 100, 105, 95, 102, 100000));
        }

        // After warmup with constant volume, PVO should be very close to 0
        Assert.True(Math.Abs(pvo.Last.Value) < 0.01, $"PVO should be ~0 with constant volume, but was {pvo.Last.Value}");
    }
}
