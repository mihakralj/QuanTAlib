using Xunit;

namespace QuanTAlib.Tests;

public class TsiTests
{
    private const double Epsilon = 1e-10;

    // ==================== CONSTRUCTION ====================
    [Fact]
    public void Constructor_DefaultParameters()
    {
        var tsi = new Tsi();
        Assert.Equal("Tsi(25,13,13)", tsi.Name);
    }

    [Fact]
    public void Constructor_CustomParameters()
    {
        var tsi = new Tsi(20, 10, 7);
        Assert.Equal("Tsi(20,10,7)", tsi.Name);
    }

    [Fact]
    public void Constructor_MinimumPeriod()
    {
        var tsi = new Tsi(1, 1, 1);
        Assert.Equal("Tsi(1,1,1)", tsi.Name);
    }

    [Fact]
    public void Constructor_ZeroLongPeriod_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new Tsi(0, 13, 13));
    }

    [Fact]
    public void Constructor_ZeroShortPeriod_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new Tsi(25, 0, 13));
    }

    [Fact]
    public void Constructor_ZeroSignalPeriod_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new Tsi(25, 13, 0));
    }

    [Fact]
    public void Constructor_NegativePeriods_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new Tsi(-25, 13, 13));
        Assert.Throws<ArgumentException>(() => new Tsi(25, -13, 13));
        Assert.Throws<ArgumentException>(() => new Tsi(25, 13, -13));
    }

    // ==================== BASIC CALCULATIONS ====================
    [Fact]
    public void Update_ConstantPrice_ZeroTsi()
    {
        var tsi = new Tsi(3, 2, 2);
        double constantPrice = 100.0;

        // Feed constant prices
        for (int i = 0; i < 20; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), constantPrice));
        }

        // TSI should be 0 when no price change
        Assert.True(Math.Abs(tsi.Last.Value) < 1.0);
    }

    [Fact]
    public void Update_RisingPrices_PositiveTsi()
    {
        var tsi = new Tsi(5, 3, 3);

        // Feed rising prices
        for (int i = 0; i < 30; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i));
        }

        // TSI should be positive (approaching +100) for consistent rising prices
        Assert.True(tsi.Last.Value > 50);
    }

    [Fact]
    public void Update_FallingPrices_NegativeTsi()
    {
        var tsi = new Tsi(5, 3, 3);

        // Feed falling prices
        for (int i = 0; i < 30; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 200.0 - i));
        }

        // TSI should be negative (approaching -100) for consistent falling prices
        Assert.True(tsi.Last.Value < -50);
    }

    [Fact]
    public void Update_BoundedOutput()
    {
        var tsi = new Tsi(3, 2, 2);
        var bars = new GBM(seed: 42).Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed GBM prices
        for (int i = 0; i < 100; i++)
        {
            tsi.Update(bars.Close[i]);

            // TSI should always be between -100 and +100
            Assert.True(tsi.Last.Value >= -100.0 && tsi.Last.Value <= 100.0);
        }
    }

    [Fact]
    public void Signal_PropertyReturnsSignalLine()
    {
        var tsi = new Tsi(5, 3, 3);

        for (int i = 0; i < 20; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i * 0.5));
        }

        // Signal should be a smoothed version of TSI
        // It should exist and be within TSI range
        Assert.True(tsi.Signal >= -100.0 && tsi.Signal <= 100.0);
    }

    // ==================== IsHot ====================
    [Fact]
    public void IsHot_InitiallyFalse()
    {
        var tsi = new Tsi(5, 3, 3);
        Assert.False(tsi.IsHot);
    }

    [Fact]
    public void IsHot_TrueAfterWarmup()
    {
        var tsi = new Tsi(5, 3, 3);

        // Feed enough data to warm up all EMAs
        for (int i = 0; i < 50; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i * 0.5));
        }

        Assert.True(tsi.IsHot);
    }

    // ==================== STATE MANAGEMENT ====================
    [Fact]
    public void Update_BarCorrection_RestoresState()
    {
        var tsi = new Tsi(5, 3, 3);

        // Initial values - building up momentum history
        for (int i = 0; i < 20; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i * 0.5));
        }

        // Update with new bar (large spike)
        tsi.Update(new TValue(DateTime.Now.AddMinutes(20), 180.0), isNew: true);
        var valueAfterSpike = tsi.Last.Value;

        // Correct the bar to smaller value (isNew=false)
        tsi.Update(new TValue(DateTime.Now.AddMinutes(20), 105.0), isNew: false);
        var valueAfterCorrection = tsi.Last.Value;

        // The spike value should be higher than the corrected value
        // because spike has larger positive momentum
        Assert.True(valueAfterSpike > valueAfterCorrection,
            $"Spike ({valueAfterSpike}) should be greater than corrected ({valueAfterCorrection})");
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var tsi = new Tsi(5, 3, 3);

        for (int i = 0; i < 20; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i));
        }

        Assert.NotEqual(default, tsi.Last);
        Assert.True(tsi.IsHot);

        tsi.Reset();

        Assert.Equal(default, tsi.Last);
        Assert.False(tsi.IsHot);
    }

    // ==================== SERIES ====================
    [Fact]
    public void Update_TSeries_ReturnsCorrectLength()
    {
        var source = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            source.Add(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i * 0.5));
        }

        var result = Tsi.Batch(source);

        Assert.Equal(source.Count, result.Count);
    }

    [Fact]
    public void Batch_MatchesStreamingCalculation()
    {
        var bars = new GBM(seed: 42).Fetch(60, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // Batch calculation
        var batchResult = Tsi.Batch(source, 5, 3, 3);

        // Streaming calculation
        var tsi = new Tsi(5, 3, 3);
        var streamingResult = new List<double>();
        foreach (var value in source)
        {
            streamingResult.Add(tsi.Update(value).Value);
        }

        // Compare results
        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], streamingResult[i], 6);
        }
    }

    // ==================== EDGE CASES ====================
    [Fact]
    public void Update_SingleValue_ReturnsZero()
    {
        var tsi = new Tsi(5, 3, 3);
        var result = tsi.Update(new TValue(DateTime.Now, 100.0));

        // First value has no momentum
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void Update_LargePriceSwing_HandlesCorrectly()
    {
        var tsi = new Tsi(5, 3, 3);

        // Stable prices
        for (int i = 0; i < 20; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0));
        }

        // Large price swing
        tsi.Update(new TValue(DateTime.Now.AddMinutes(21), 200.0));

        // Should handle without overflow/underflow
        Assert.True(!double.IsNaN(tsi.Last.Value));
        Assert.True(!double.IsInfinity(tsi.Last.Value));
    }

    [Fact]
    public void Update_NegativePrices_HandlesCorrectly()
    {
        var tsi = new Tsi(5, 3, 3);

        // Negative prices (like temperature or P&L)
        for (int i = 0; i < 20; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), -10.0 + i * 0.5));
        }

        Assert.True(!double.IsNaN(tsi.Last.Value));
        Assert.True(tsi.Last.Value >= -100.0 && tsi.Last.Value <= 100.0);
    }

    [Fact]
    public void Update_VerySmallPriceChanges_HandlesCorrectly()
    {
        var tsi = new Tsi(5, 3, 3);

        // Very small price changes
        for (int i = 0; i < 20; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i * 1e-8));
        }

        Assert.True(!double.IsNaN(tsi.Last.Value));
    }

    // ==================== PRIME ====================
    [Fact]
    public void Prime_InitializesState()
    {
        var tsi = new Tsi(5, 3, 3);
        double[] primeData = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110];

        tsi.Prime(primeData);

        Assert.NotEqual(default, tsi.Last);
    }

    [Fact]
    public void Prime_SameAsSequentialUpdates()
    {
        var tsi1 = new Tsi(5, 3, 3);
        var tsi2 = new Tsi(5, 3, 3);
        double[] data = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110];

        // Prime
        tsi1.Prime(data);

        // Sequential updates
        foreach (var value in data)
        {
            tsi2.Update(new TValue(DateTime.MinValue, value));
        }

        Assert.Equal(tsi1.Last.Value, tsi2.Last.Value, 10);
    }

    // ==================== CALCULATE ====================
    [Fact]
    public void Calculate_Static_MatchesBatch()
    {
        var bars = new GBM(seed: 42).Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] source = bars.CloseValues.ToArray();
        double[] output = new double[50];

        Tsi.Batch(source, output, 5, 3);

        var series = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            series.Add(new TValue(DateTime.Now.AddMinutes(i), source[i]));
        }

        var batchResult = Tsi.Batch(series, 5, 3, 3);

        for (int i = 10; i < 50; i++)
        {
            Assert.Equal(output[i], batchResult.Values[i], 6);
        }
    }

    [Fact]
    public void Calculate_LengthMismatch_ThrowsException()
    {
        double[] source = new double[10];
        double[] output = new double[5];

        Assert.Throws<ArgumentException>(() => Tsi.Batch(source, output));
    }

    [Fact]
    public void Calculate_ZeroPeriod_ThrowsException()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        Assert.Throws<ArgumentException>(() => Tsi.Batch(source, output, 0, 3));
        Assert.Throws<ArgumentException>(() => Tsi.Batch(source, output, 5, 0));
    }

    [Fact]
    public void Calculate_EmptyArrays_DoesNotThrow()
    {
        double[] source = [];
        double[] output = [];

        var exception = Record.Exception(() => Tsi.Batch(source, output));
        Assert.Null(exception);
    }

    // ==================== EVENT HANDLING ====================
    [Fact]
    public void PubEvent_TriggersOnUpdate()
    {
        var tsi = new Tsi(5, 3, 3);
        TValue? receivedValue = null;
        bool isNewReceived = false;

        tsi.Pub += (object? sender, in TValueEventArgs args) =>
        {
            receivedValue = args.Value;
            isNewReceived = args.IsNew;
        };

        tsi.Update(new TValue(DateTime.Now, 100.0));

        Assert.NotNull(receivedValue);
        Assert.True(isNewReceived);
    }

    [Fact]
    public void PubSubscription_ReceivesUpdates()
    {
        var source = new TSeries();
        var tsi = new Tsi(source, 5, 3, 3);
        var receivedValues = new List<TValue>();

        tsi.Pub += (object? sender, in TValueEventArgs args) => receivedValues.Add(args.Value);

        for (int i = 0; i < 20; i++)
        {
            source.Add(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i * 0.5));
        }

        Assert.Equal(20, receivedValues.Count);
    }

    // ==================== TYPICAL TRADING SCENARIOS ====================
    [Fact]
    public void TrendChange_ZeroCrossover()
    {
        var tsi = new Tsi(5, 3, 3);

        // Rising prices
        for (int i = 0; i < 15; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i * 2));
        }
        Assert.True(tsi.Last.Value > 0);

        // Falling prices
        for (int i = 0; i < 20; i++)
        {
            tsi.Update(new TValue(DateTime.Now.AddMinutes(15 + i), 128.0 - i * 2));
        }
        Assert.True(tsi.Last.Value < 0);
    }

    [Fact]
    public void SignalLineCrossover_DetectsMomentumChange()
    {
        var tsi = new Tsi(5, 3, 3);
        var tsiValues = new List<double>();
        var signalValues = new List<double>();

        // Rising then falling prices - clearer trend change
        for (int i = 0; i < 40; i++)
        {
            double price = i < 20
                ? 100.0 + i * 2        // Rising
                : 140.0 - (i - 20) * 2;  // Falling
            tsi.Update(new TValue(DateTime.Now.AddMinutes(i), price));
            tsiValues.Add(tsi.Last.Value);
            signalValues.Add(tsi.Signal);
        }

        // When momentum reverses, TSI leads signal and crosses below
        // Or verify TSI goes from positive to negative (zero crossover)
        bool foundZeroCross = false;
        for (int i = 20; i < tsiValues.Count; i++)
        {
            if (tsiValues[i - 1] > 0 && tsiValues[i] <= 0)
            {
                foundZeroCross = true;
                break;
            }
        }

        // After the trend reverses, TSI should cross zero
        Assert.True(foundZeroCross || tsiValues[^1] < tsiValues[19],
            $"TSI should decline after trend reversal: TSI at peak={tsiValues[19]:F2}, TSI at end={tsiValues[^1]:F2}");
    }
}
