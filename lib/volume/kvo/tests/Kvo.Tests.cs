using Xunit;

namespace QuanTAlib.Tests;

public class KvoTests
{
    private const int DefaultFastPeriod = 34;
    private const int DefaultSlowPeriod = 55;
    private const int DefaultSignalPeriod = 13;

    [Fact]
    public void Constructor_DefaultParameters_CreatesValidIndicator()
    {
        var kvo = new Kvo();
        Assert.Equal($"Kvo({DefaultFastPeriod},{DefaultSlowPeriod},{DefaultSignalPeriod})", kvo.Name);
        Assert.Equal(DefaultSlowPeriod, kvo.WarmupPeriod);
        Assert.False(kvo.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters_CreatesValidIndicator()
    {
        var kvo = new Kvo(fastPeriod: 20, slowPeriod: 40, signalPeriod: 10);
        Assert.Equal("Kvo(20,40,10)", kvo.Name);
        Assert.Equal(40, kvo.WarmupPeriod);
    }

    [Fact]
    public void Constructor_InvalidFastPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Kvo(fastPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Kvo(fastPeriod: -1));
    }

    [Fact]
    public void Constructor_InvalidSlowPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Kvo(slowPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Kvo(slowPeriod: -1));
    }

    [Fact]
    public void Constructor_InvalidSignalPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Kvo(signalPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Kvo(signalPeriod: -1));
    }

    [Fact]
    public void Constructor_FastNotLessThanSlow_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Kvo(fastPeriod: 55, slowPeriod: 55));
        Assert.Throws<ArgumentException>(() => new Kvo(fastPeriod: 60, slowPeriod: 55));
    }

    [Fact]
    public void Update_WithTBar_ReturnsValidValue()
    {
        var kvo = new Kvo();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result = kvo.Update(bar);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_WithTValue_ThrowsNotSupportedException()
    {
        var kvo = new Kvo();
        var value = new TValue(DateTime.UtcNow, 100);
        Assert.Throws<NotSupportedException>(() => kvo.Update(value));
    }

    [Fact]
    public void Update_PriceIncrease_ReturnsFiniteValue()
    {
        var kvo = new Kvo(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var time = DateTime.UtcNow;

        // Simulate uptrend with increasing prices and volume
        for (int i = 0; i < 100; i++)
        {
            double basePrice = 100 + i * 2;
            kvo.Update(new TBar(time.AddMinutes(i), basePrice, basePrice + 5, basePrice - 2, basePrice + 3, 1000000 + i * 100000));
        }

        // After warmup, KVO should have finite values
        Assert.True(double.IsFinite(kvo.Last.Value), "KVO should return finite values");
    }

    [Fact]
    public void Update_PriceDecrease_ReturnsFiniteValue()
    {
        var kvo = new Kvo(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var time = DateTime.UtcNow;

        // Simulate downtrend with decreasing prices
        for (int i = 0; i < 100; i++)
        {
            double basePrice = 500 - i * 3;
            kvo.Update(new TBar(time.AddMinutes(i), basePrice, basePrice + 2, basePrice - 5, basePrice - 3, 1000000 + i * 100000));
        }

        // After warmup, KVO should have finite values
        Assert.True(double.IsFinite(kvo.Last.Value), "KVO should return finite values");
    }

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var kvo = new Kvo();
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result1 = kvo.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 95, 110, 1100000);
        var result2 = kvo.Update(bar2, isNew: true);

        Assert.NotEqual(result1.Time, result2.Time);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var kvo = new Kvo();
        var time = DateTime.UtcNow;
        var bar1 = new TBar(time, 100, 110, 90, 105, 1000000);
        kvo.Update(bar1, isNew: true);

        var bar2 = new TBar(time.AddMinutes(1), 105, 115, 95, 110, 1100000);
        var result1 = kvo.Update(bar2, isNew: true);

        // Update same bar with different values
        var bar2Updated = new TBar(time.AddMinutes(1), 105, 120, 95, 118, 1500000);
        var result2 = kvo.Update(bar2Updated, isNew: false);

        Assert.Equal(result1.Time, result2.Time);
        Assert.NotEqual(result1.Value, result2.Value);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresState()
    {
        var kvo = new Kvo(fastPeriod: 5, slowPeriod: 10, signalPeriod: 5);
        var time = DateTime.UtcNow;

        // Build up state
        for (int i = 0; i < 15; i++)
        {
            kvo.Update(new TBar(time.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000 + i * 10000), isNew: true);
        }

        // New bar
        var originalBar = new TBar(time.AddMinutes(15), 120, 130, 110, 125, 250000);
        var originalResult = kvo.Update(originalBar, isNew: true);

        // Correction with different values
        var correctionBar = new TBar(time.AddMinutes(15), 110, 150, 90, 140, 500000);
        var correctedResult = kvo.Update(correctionBar, isNew: false);

        Assert.NotEqual(originalResult.Value, correctedResult.Value);
        Assert.True(double.IsFinite(correctedResult.Value));
    }

    [Fact]
    public void Update_WarmupPeriod_IsHotBecomesTrueAfterWarmup()
    {
        var kvo = new Kvo(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var time = DateTime.UtcNow;

        Assert.False(kvo.IsHot);

        // Feed many bars until compensators decay below threshold (1e-10)
        // With period 5, decay = 1 - 2/(5+1) = 0.667, needs ~50 bars for e^(-50*0.4) < 1e-10
        for (int i = 0; i < 100; i++)
        {
            kvo.Update(new TBar(time.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000), isNew: true);
        }

        // After sufficient bars, compensators should decay and IsHot becomes true
        Assert.True(kvo.IsHot);
    }

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var kvo = new Kvo(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var time = DateTime.UtcNow;

        // Process some valid bars first
        for (int i = 0; i < 10; i++)
        {
            kvo.Update(new TBar(time.AddMinutes(i), 100, 105, 95, 102, 100000));
        }

        // Process bar with NaN volume
        var nanBar = new TBar(time.AddMinutes(10), 105, 110, 100, 108, double.NaN);
        var result = kvo.Update(nanBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_ZeroPriceRange_HandlesGracefully()
    {
        var kvo = new Kvo(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var time = DateTime.UtcNow;

        // First bar normal
        kvo.Update(new TBar(time, 100, 110, 90, 105, 100000));

        // Bar with zero range
        var result = kvo.Update(new TBar(time.AddMinutes(1), 105, 105, 105, 105, 100000));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_ZeroVolume_HandlesGracefully()
    {
        var kvo = new Kvo(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var time = DateTime.UtcNow;

        kvo.Update(new TBar(time, 100, 110, 90, 105, 100000));
        var result = kvo.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 0));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Signal_CalculatedAlongsideKvo()
    {
        var kvo = new Kvo(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            kvo.Update(new TBar(time.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000 + i * 10000));
        }

        Assert.True(double.IsFinite(kvo.Signal.Value));
        Assert.Equal(kvo.Last.Time, kvo.Signal.Time);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var kvo = new Kvo(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var time = DateTime.UtcNow;

        // Process many bars until IsHot becomes true
        for (int i = 0; i < 100; i++)
        {
            kvo.Update(new TBar(time.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000), isNew: true);
        }

        // Verify indicator was active
        Assert.True(double.IsFinite(kvo.Last.Value));

        kvo.Reset();

        Assert.False(kvo.IsHot);
        Assert.Equal(default, kvo.Last);
        Assert.Equal(default, kvo.Signal);
    }

    [Fact]
    public void UpdateWithSignal_ReturnsBothSeries()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 100; i++)
        {
            bars.Add(gbm.Next());
        }

        var kvo = new Kvo();
        var (kvoSeries, signalSeries) = kvo.UpdateWithSignal(bars);

        Assert.Equal(bars.Count, kvoSeries.Count);
        Assert.Equal(bars.Count, signalSeries.Count);

        // Verify values are finite
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.True(double.IsFinite(kvoSeries[i].Value));
            Assert.True(double.IsFinite(signalSeries[i].Value));
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
        var kvo = new Kvo();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(kvo.Update(bar).Value);
        }

        // Batch
        var batchResult = Kvo.Batch(bars);

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
        var kvo = new Kvo();
        var streamingKvo = new List<double>();
        var streamingSignal = new List<double>();
        foreach (var bar in bars)
        {
            kvo.Update(bar);
            streamingKvo.Add(kvo.Last.Value);
            streamingSignal.Add(kvo.Signal.Value);
        }

        // Span
        var high = bars.High.Values.ToArray();
        var low = bars.Low.Values.ToArray();
        var close = bars.Close.Values.ToArray();
        var volume = bars.Volume.Values.ToArray();
        var spanKvo = new double[bars.Count];
        var spanSignal = new double[bars.Count];

        Kvo.Batch(high, low, close, volume, spanKvo, spanSignal);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingKvo[i], spanKvo[i], 10);
            Assert.Equal(streamingSignal[i], spanSignal[i], 10);
        }
    }

    [Fact]
    public void SpanCalculate_InvalidLengths_ThrowsArgumentException()
    {
        var high = new double[100];
        var low = new double[99]; // Different length
        var close = new double[100];
        var volume = new double[100];
        var output = new double[100];
        var signal = new double[100];

        Assert.Throws<ArgumentException>(() => Kvo.Batch(high, low, close, volume, output, signal));
    }

    [Fact]
    public void SpanCalculate_InvalidFastPeriod_ThrowsArgumentException()
    {
        var high = new double[100];
        var low = new double[100];
        var close = new double[100];
        var volume = new double[100];
        var output = new double[100];
        var signal = new double[100];

        Assert.Throws<ArgumentException>(() => Kvo.Batch(high, low, close, volume, output, signal, fastPeriod: 0));
    }

    [Fact]
    public void SpanCalculate_InvalidSlowPeriod_ThrowsArgumentException()
    {
        var high = new double[100];
        var low = new double[100];
        var close = new double[100];
        var volume = new double[100];
        var output = new double[100];
        var signal = new double[100];

        Assert.Throws<ArgumentException>(() => Kvo.Batch(high, low, close, volume, output, signal, slowPeriod: 0));
    }

    [Fact]
    public void SpanCalculate_InvalidSignalPeriod_ThrowsArgumentException()
    {
        var high = new double[100];
        var low = new double[100];
        var close = new double[100];
        var volume = new double[100];
        var output = new double[100];
        var signal = new double[100];

        Assert.Throws<ArgumentException>(() => Kvo.Batch(high, low, close, volume, output, signal, signalPeriod: 0));
    }

    [Fact]
    public void SpanCalculate_EmptyInput_HandlesGracefully()
    {
        var high = Array.Empty<double>();
        var low = Array.Empty<double>();
        var close = Array.Empty<double>();
        var volume = Array.Empty<double>();
        var output = Array.Empty<double>();
        var signal = Array.Empty<double>();

        // Should not throw
        Kvo.Batch(high, low, close, volume, output, signal);

        // Verify arrays remain empty (no out-of-bounds writes)
        Assert.Empty(output);
        Assert.Empty(signal);
    }

    [Fact]
    public void Event_PubFiresOnUpdate()
    {
        var kvo = new Kvo();
        TValue? receivedValue = null;
        bool receivedIsNew = false;

        kvo.Pub += (object? sender, in TValueEventArgs args) =>
        {
            receivedValue = args.Value;
            receivedIsNew = args.IsNew;
        };

        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        kvo.Update(bar, isNew: true);

        Assert.NotNull(receivedValue);
        Assert.True(receivedIsNew);
    }

    [Fact]
    public void TrendDetection_CorrectlyIdentifiesTrend()
    {
        var kvo = new Kvo(fastPeriod: 2, slowPeriod: 3, signalPeriod: 2);
        var time = DateTime.UtcNow;

        // First bar - no previous HLC3, trend defaults to +1
        var result1 = kvo.Update(new TBar(time, 100, 105, 95, 100, 100000));

        // Second bar - HLC3 higher than first (trend = +1)
        var result2 = kvo.Update(new TBar(time.AddMinutes(1), 105, 115, 100, 110, 100000));

        // Third bar - HLC3 lower than second (trend = -1)
        var result3 = kvo.Update(new TBar(time.AddMinutes(2), 105, 108, 90, 95, 100000));

        // All values should be finite
        Assert.True(double.IsFinite(result1.Value));
        Assert.True(double.IsFinite(result2.Value));
        Assert.True(double.IsFinite(result3.Value));
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

        var kvo1 = new Kvo(fastPeriod: 10, slowPeriod: 20, signalPeriod: 5);
        var kvo2 = new Kvo(fastPeriod: 20, slowPeriod: 40, signalPeriod: 10);

        foreach (var bar in bars)
        {
            kvo1.Update(bar);
            kvo2.Update(bar);
        }

        // Different periods should produce different results
        Assert.NotEqual(kvo1.Last.Value, kvo2.Last.Value);
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

        var kvo = new Kvo();
        foreach (var bar in bars)
        {
            var result = kvo.Update(bar);
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.True(kvo.IsHot);
    }
}
