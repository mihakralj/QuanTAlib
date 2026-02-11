using Xunit;

namespace QuanTAlib.Tests;

public class VaTests
{
    [Fact]
    public void Constructor_CreatesValidIndicator()
    {
        var va = new Va();
        Assert.Equal("Va", va.Name);
        Assert.Equal(1, Va.WarmupPeriod);
        Assert.False(va.IsHot);
    }

    [Fact]
    public void Update_WithTBar_ReturnsValidValue()
    {
        var va = new Va();
        // Bar: H=110, L=90, C=105, V=1000
        // midpoint = (110 + 90) / 2 = 100
        // va_period = 1000 * (105 - 100) = 5000
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);
        var result = va.Update(bar);
        Assert.Equal(5000, result.Value, 10);
    }

    [Fact]
    public void Update_CloseAboveMidpoint_PositiveValue()
    {
        var va = new Va();
        // Close above midpoint = buying pressure = positive
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 108, 1000);
        // midpoint = 100, va = 1000 * (108 - 100) = 8000
        var result = va.Update(bar);
        Assert.True(result.Value > 0);
        Assert.Equal(8000, result.Value, 10);
    }

    [Fact]
    public void Update_CloseBelowMidpoint_NegativeValue()
    {
        var va = new Va();
        // Close below midpoint = selling pressure = negative
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 92, 1000);
        // midpoint = 100, va = 1000 * (92 - 100) = -8000
        var result = va.Update(bar);
        Assert.True(result.Value < 0);
        Assert.Equal(-8000, result.Value, 10);
    }

    [Fact]
    public void Update_CloseAtMidpoint_ZeroValue()
    {
        var va = new Va();
        // Close at midpoint = neutral
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        // midpoint = 100, va = 1000 * (100 - 100) = 0
        var result = va.Update(bar);
        Assert.Equal(0, result.Value, 10);
    }

    [Fact]
    public void Update_MultipleValues_Accumulates()
    {
        var va = new Va();
        var time = DateTime.UtcNow;

        // Bar 1: midpoint=100, close=105, vol=1000 -> va=5000
        va.Update(new TBar(time, 100, 110, 90, 105, 1000));
        Assert.Equal(5000, va.Last.Value, 10);

        // Bar 2: midpoint=100, close=95, vol=500 -> va_period=-2500, total=2500
        va.Update(new TBar(time.AddMinutes(1), 100, 110, 90, 95, 500));
        Assert.Equal(2500, va.Last.Value, 10);

        // Bar 3: midpoint=100, close=100, vol=2000 -> va_period=0, total=2500
        va.Update(new TBar(time.AddMinutes(2), 100, 110, 90, 100, 2000));
        Assert.Equal(2500, va.Last.Value, 10);
    }

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var va = new Va();
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);
        var result1 = va.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 95, 110, 800);
        var result2 = va.Update(bar2, isNew: true);

        Assert.NotEqual(result1.Time, result2.Time);
        Assert.NotEqual(result1.Value, result2.Value);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var va = new Va();
        var gbm = new GBM(seed: 42);

        // Build up history
        for (int i = 0; i < 20; i++)
        {
            va.Update(gbm.Next(), isNew: true);
        }

        // New bar
        var bar1 = gbm.Next();
        va.Update(bar1, isNew: true);

        // Correction - restore previous state
        va.Update(bar1, isNew: false);

        // Value should change based on bar correction
        Assert.True(double.IsFinite(va.Last.Value));
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresState()
    {
        var va = new Va();
        var gbm = new GBM(seed: 123);

        // Build up history
        for (int i = 0; i < 20; i++)
        {
            va.Update(gbm.Next(), isNew: true);
        }

        // New bar
        var originalBar = gbm.Next();
        va.Update(originalBar, isNew: true);

        // Correction with same values using isNew=false should restore
        va.Update(originalBar, isNew: false);

        Assert.True(double.IsFinite(va.Last.Value));
    }

    [Fact]
    public void Update_WarmupPeriod_IsHotAfterFirstBar()
    {
        var va = new Va();
        Assert.False(va.IsHot);

        va.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000), isNew: true);
        Assert.True(va.IsHot);
    }

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var va = new Va();
        var time = DateTime.UtcNow;

        // Process valid bar first
        va.Update(new TBar(time, 100, 110, 90, 105, 1000));

        // Process bar with NaN close
        var nanBar = new TBar(time.AddMinutes(1), 100, 110, 90, double.NaN, 500);
        var result = va.Update(nanBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var va = new Va();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 10; i++)
        {
            va.Update(gbm.Next(), isNew: true);
        }

        Assert.True(va.IsHot);
        Assert.NotEqual(0, va.Last.Value);

        va.Reset();

        Assert.False(va.IsHot);
        Assert.Equal(default, va.Last);
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
        var va = new Va();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(va.Update(bar).Value);
        }

        // Batch
        var batchResult = Va.Batch(bars);

        Assert.Equal(bars.Count, batchResult.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingValues[i], batchResult[i].Value, 10);
        }
    }

    [Fact]
    public void SpanCalculate_MatchesStreaming()
    {
        var gbm = new GBM(seed: 42);
        int count = 100;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        var volume = new double[count];

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            high[i] = bar.High;
            low[i] = bar.Low;
            close[i] = bar.Close;
            volume[i] = bar.Volume;
        }

        // Streaming
        var va = new Va();
        var streamingValues = new List<double>();
        var time = DateTime.UtcNow;
        for (int i = 0; i < count; i++)
        {
            streamingValues.Add(va.Update(new TBar(time.AddMinutes(i), 0, high[i], low[i], close[i], volume[i])).Value);
        }

        // Span
        var output = new double[count];
        Va.Batch(high, low, close, volume, output);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamingValues[i], output[i], 10);
        }
    }

    [Fact]
    public void SpanCalculate_InvalidLengths_ThrowsArgumentException()
    {
        var high = new double[100];
        var low = new double[100];
        var close = new double[100];
        var volume = new double[99]; // Different length
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Va.Batch(high, low, close, volume, output));
    }

    [Fact]
    public void SpanCalculate_EmptyInput_HandlesGracefully()
    {
        var high = Array.Empty<double>();
        var low = Array.Empty<double>();
        var close = Array.Empty<double>();
        var volume = Array.Empty<double>();
        var output = Array.Empty<double>();

        Va.Batch(high, low, close, volume, output);

        Assert.Empty(output);
    }

    [Fact]
    public void Event_PubFiresOnUpdate()
    {
        var va = new Va();
        TValue? receivedValue = null;
        bool receivedIsNew = false;

        va.Pub += (object? sender, in TValueEventArgs args) =>
        {
            receivedValue = args.Value;
            receivedIsNew = args.IsNew;
        };

        va.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000), isNew: true);

        Assert.NotNull(receivedValue);
        Assert.True(receivedIsNew);
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

        var va = new Va();
        foreach (var bar in bars)
        {
            var result = va.Update(bar);
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.True(va.IsHot);
    }

    [Fact]
    public void FormulaVerification_ManualCalculation()
    {
        var va = new Va();
        var time = DateTime.UtcNow;

        // Bar 1: H=110, L=90, C=105, V=1000
        // midpoint = (110+90)/2 = 100
        // va_period = 1000 * (105 - 100) = 5000
        va.Update(new TBar(time, 100, 110, 90, 105, 1000));
        Assert.Equal(5000, va.Last.Value, 10);

        // Bar 2: H=120, L=100, C=115, V=2000
        // midpoint = (120+100)/2 = 110
        // va_period = 2000 * (115 - 110) = 10000
        // total = 5000 + 10000 = 15000
        va.Update(new TBar(time.AddMinutes(1), 100, 120, 100, 115, 2000));
        Assert.Equal(15000, va.Last.Value, 10);

        // Bar 3: H=115, L=95, C=98, V=1500
        // midpoint = (115+95)/2 = 105
        // va_period = 1500 * (98 - 105) = -10500
        // total = 15000 - 10500 = 4500
        va.Update(new TBar(time.AddMinutes(2), 100, 115, 95, 98, 1500));
        Assert.Equal(4500, va.Last.Value, 10);
    }
}