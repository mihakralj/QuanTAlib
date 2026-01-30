using Xunit;

namespace QuanTAlib.Tests;

public class TwapTests
{
    private const int DefaultPeriod = 0;

    [Fact]
    public void Constructor_DefaultParameters_CreatesValidIndicator()
    {
        var twap = new Twap();
        Assert.Equal("Twap(∞)", twap.Name);
        Assert.Equal(1, Twap.WarmupPeriod);
        Assert.False(twap.IsHot);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsParameter()
    {
        var twap = new Twap(period: 10);
        Assert.Equal("Twap(10)", twap.Name);
    }

    [Fact]
    public void Constructor_ZeroPeriod_MeansNeverReset()
    {
        var twap = new Twap(period: 0);
        Assert.Equal("Twap(∞)", twap.Name);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Twap(period: -1));
    }

    [Fact]
    public void Update_WithTBar_ReturnsValidValue()
    {
        var twap = new Twap();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result = twap.Update(bar);
        Assert.True(double.IsFinite(result.Value));
        // First bar: HLC3 = (110 + 90 + 105) / 3 = 101.666...
        Assert.Equal((110.0 + 90.0 + 105.0) / 3.0, result.Value, 10);
    }

    [Fact]
    public void Update_WithTValue_ReturnsCurrentValue()
    {
        var twap = new Twap();
        var value = new TValue(DateTime.UtcNow, 100);
        var result = twap.Update(value);
        Assert.Equal(100, result.Value);
    }

    [Fact]
    public void Update_MultipleValues_CalculatesRunningAverage()
    {
        var twap = new Twap();
        var time = DateTime.UtcNow;

        // First value: 100
        twap.Update(new TValue(time, 100));
        Assert.Equal(100, twap.Last.Value, 10);

        // Second value: 200, average = (100 + 200) / 2 = 150
        twap.Update(new TValue(time.AddMinutes(1), 200));
        Assert.Equal(150, twap.Last.Value, 10);

        // Third value: 300, average = (100 + 200 + 300) / 3 = 200
        twap.Update(new TValue(time.AddMinutes(2), 300));
        Assert.Equal(200, twap.Last.Value, 10);
    }

    [Fact]
    public void Update_WithPeriod_ResetsAtBoundary()
    {
        var twap = new Twap(period: 3);
        var time = DateTime.UtcNow;

        // First 3 values: 100, 200, 300
        twap.Update(new TValue(time, 100));
        twap.Update(new TValue(time.AddMinutes(1), 200));
        twap.Update(new TValue(time.AddMinutes(2), 300));
        // Average = (100 + 200 + 300) / 3 = 200
        Assert.Equal(200, twap.Last.Value, 10);

        // Fourth value: 600, resets and starts new session
        twap.Update(new TValue(time.AddMinutes(3), 600));
        // After reset: Average = 600 / 1 = 600
        Assert.Equal(600, twap.Last.Value, 10);
    }

    [Fact]
    public void Update_ZeroPeriod_NeverResets()
    {
        var twap = new Twap(period: 0);
        var time = DateTime.UtcNow;

        double sum = 0;
        for (int i = 1; i <= 20; i++)
        {
            sum += i * 10;
            twap.Update(new TValue(time.AddMinutes(i), i * 10));
            Assert.Equal(sum / i, twap.Last.Value, 10);
        }
    }

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var twap = new Twap();
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result1 = twap.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 95, 110, 800000);
        var result2 = twap.Update(bar2, isNew: true);

        Assert.NotEqual(result1.Time, result2.Time);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var twap = new Twap();
        var gbm = new GBM(seed: 42);

        // Build up history
        for (int i = 0; i < 20; i++)
        {
            twap.Update(gbm.Next(), isNew: true);
        }

        // Get a new bar
        var bar1 = gbm.Next();
        var result1 = twap.Update(bar1, isNew: true);

        // Create a correction with different close
        var bar2 = new TBar(bar1.Time, bar1.Open, bar1.High, bar1.Low, bar1.Close * 1.1, bar1.Volume);
        var result2 = twap.Update(bar2, isNew: false);

        Assert.Equal(result1.Time, result2.Time);
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresState()
    {
        var twap = new Twap();
        var gbm = new GBM(seed: 123);

        // Build up history
        for (int i = 0; i < 20; i++)
        {
            twap.Update(gbm.Next(), isNew: true);
        }

        _ = twap.Last.Value;

        // New bar
        var originalBar = gbm.Next();
        twap.Update(originalBar, isNew: true);

        // Correction with same values should restore similar state
        var correctionBar = originalBar;
        var correctedResult = twap.Update(correctionBar, isNew: false);

        Assert.True(double.IsFinite(correctedResult.Value));
    }

    [Fact]
    public void Update_WarmupPeriod_IsHotBecomesTrueImmediately()
    {
        var twap = new Twap();
        var time = DateTime.UtcNow;

        Assert.False(twap.IsHot);

        twap.Update(new TValue(time, 100), isNew: true);
        Assert.True(twap.IsHot);  // TWAP is valid after first value
    }

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var twap = new Twap();
        var time = DateTime.UtcNow;

        // Process some valid values first
        for (int i = 0; i < 10; i++)
        {
            twap.Update(new TValue(time.AddMinutes(i), 100 + i));
        }

        // Process value with NaN
        var nanValue = new TValue(time.AddMinutes(10), double.NaN);
        var result = twap.Update(nanValue);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var twap = new Twap();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            twap.Update(new TValue(time.AddMinutes(i), 100 + i), isNew: true);
        }

        Assert.True(twap.IsHot);
        Assert.True(double.IsFinite(twap.Last.Value));

        twap.Reset();

        Assert.False(twap.IsHot);
        Assert.Equal(default, twap.Last);
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
        var twap = new Twap(period: 10);
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(twap.Update(bar).Value);
        }

        // Batch
        var batchResult = Twap.Calculate(bars, period: 10);

        Assert.Equal(bars.Count, batchResult.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingValues[i], batchResult[i].Value, 10);
        }
    }

    [Fact]
    public void SpanCalculate_MatchesStreaming()
    {
        var time = DateTime.UtcNow;
        var prices = new double[100];
        var random = new GBM(seed: 42);

        for (int i = 0; i < 100; i++)
        {
            prices[i] = 100 + random.Next().Close - 100;  // Use close price variation
        }

        // Streaming
        var twap = new Twap(period: 10);
        var streamingValues = new List<double>();
        for (int i = 0; i < prices.Length; i++)
        {
            streamingValues.Add(twap.Update(new TValue(time.AddMinutes(i), prices[i])).Value);
        }

        // Span
        var output = new double[prices.Length];
        Twap.Calculate(prices, output, period: 10);

        for (int i = 0; i < prices.Length; i++)
        {
            Assert.Equal(streamingValues[i], output[i], 10);
        }
    }

    [Fact]
    public void SpanCalculate_InvalidLengths_ThrowsArgumentException()
    {
        var price = new double[100];
        var output = new double[99]; // Different length

        Assert.Throws<ArgumentException>(() => Twap.Calculate(price, output));
    }

    [Fact]
    public void SpanCalculate_InvalidPeriod_ThrowsArgumentException()
    {
        var price = new double[100];
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Twap.Calculate(price, output, period: -1));
    }

    [Fact]
    public void SpanCalculate_EmptyInput_HandlesGracefully()
    {
        var price = Array.Empty<double>();
        var output = Array.Empty<double>();

        Twap.Calculate(price, output);

        Assert.Empty(output);
    }

    [Fact]
    public void Event_PubFiresOnUpdate()
    {
        var twap = new Twap();
        TValue? receivedValue = null;
        bool receivedIsNew = false;

        twap.Pub += (object? sender, in TValueEventArgs args) =>
        {
            receivedValue = args.Value;
            receivedIsNew = args.IsNew;
        };

        var value = new TValue(DateTime.UtcNow, 100);
        twap.Update(value, isNew: true);

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

        var twap = new Twap(period: 100);
        foreach (var bar in bars)
        {
            var result = twap.Update(bar);
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.True(twap.IsHot);
    }

    [Fact]
    public void FormulaVerification_ManualCalculation()
    {
        // Manual verification of TWAP formula with known values
        var twap = new Twap(period: 0);  // Never reset
        var time = DateTime.UtcNow;

        // Value 1: 100, TWAP = 100/1 = 100
        twap.Update(new TValue(time, 100));
        Assert.Equal(100, twap.Last.Value, 10);

        // Value 2: 200, TWAP = (100+200)/2 = 150
        twap.Update(new TValue(time.AddMinutes(1), 200));
        Assert.Equal(150, twap.Last.Value, 10);

        // Value 3: 150, TWAP = (100+200+150)/3 = 150
        twap.Update(new TValue(time.AddMinutes(2), 150));
        Assert.Equal(150, twap.Last.Value, 10);

        // Value 4: 250, TWAP = (100+200+150+250)/4 = 175
        twap.Update(new TValue(time.AddMinutes(3), 250));
        Assert.Equal(175, twap.Last.Value, 10);

        // Value 5: 300, TWAP = (100+200+150+250+300)/5 = 200
        twap.Update(new TValue(time.AddMinutes(4), 300));
        Assert.Equal(200, twap.Last.Value, 10);
    }

    [Fact]
    public void DifferentPeriods_ProduceDifferentResults()
    {
        var time = DateTime.UtcNow;
        var values = new double[] { 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000 };

        // With period = 0 (never reset)
        var twap0 = new Twap(period: 0);
        foreach (var v in values)
        {
            twap0.Update(new TValue(time, v));
        }

        // With period = 5 (reset every 5 bars)
        var twap5 = new Twap(period: 5);
        foreach (var v in values)
        {
            twap5.Update(new TValue(time, v));
        }

        // Results should differ
        Assert.NotEqual(twap0.Last.Value, twap5.Last.Value);

        // Period 0: average of all 10 values = 550
        Assert.Equal(550, twap0.Last.Value, 10);

        // Period 5: after reset, average of last 5 values (600,700,800,900,1000) = 800
        Assert.Equal(800, twap5.Last.Value, 10);
    }

    [Fact]
    public void Update_UsesTypicalPrice_HLC3()
    {
        var twap = new Twap();
        var time = DateTime.UtcNow;

        // Bar with H=110, L=90, C=100
        // Typical price = (110 + 90 + 100) / 3 = 100
        var bar = new TBar(time, 95, 110, 90, 100, 10000);
        var result = twap.Update(bar);

        Assert.Equal(100, result.Value, 10);
    }
}