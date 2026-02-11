// Yang-Zhang Volatility (YZV) Unit Tests

using Xunit;

namespace QuanTAlib.Tests;

public class YzvTests
{
    private readonly GBM _gbm;
    private const double Tolerance = 1e-10;
    private const int DefaultPeriod = 20;

    public YzvTests()
    {
        _gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
    }

    private TBarSeries GenerateBarData(int count)
    {
        _gbm.Reset(DateTime.UtcNow.Ticks);
        return _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultParameters_SetsCorrectValues()
    {
        var yzv = new Yzv();
        Assert.Equal(DefaultPeriod, yzv.Period);
        Assert.Equal($"Yzv({DefaultPeriod})", yzv.Name);
        Assert.Equal(DefaultPeriod, yzv.WarmupPeriod);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsCorrectValues()
    {
        var yzv = new Yzv(period: 30);
        Assert.Equal(30, yzv.Period);
        Assert.Equal("Yzv(30)", yzv.Name);
        Assert.Equal(30, yzv.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ZeroPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Yzv(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Yzv(period: -5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithTBarSeriesSource_PrimesIndicator()
    {
        var bars = GenerateBarData(50);
        var yzv = new Yzv(bars, period: 10);
        Assert.True(yzv.IsHot);
        Assert.True(double.IsFinite(yzv.Last.Value));
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_SingleBar_ReturnsNonNegativeValue()
    {
        var yzv = new Yzv();
        var bar = new TBar(DateTime.UtcNow, 100.0, 102.0, 98.0, 101.0, 1000);
        var result = yzv.Update(bar);
        Assert.True(result.Value >= 0);
    }

    [Fact]
    public void Update_ConstantPrices_ProducesLowVolatility()
    {
        var yzv = new Yzv(period: 5);
        for (int i = 0; i < 30; i++)
        {
            // Constant OHLC = no volatility components
            yzv.Update(new TBar(DateTime.UtcNow, 100.0, 100.0, 100.0, 100.0, 1000));
        }
        // With constant prices, volatility should be very low
        Assert.True(yzv.Last.Value < 0.001, $"Expected near zero, got {yzv.Last.Value}");
    }

    [Fact]
    public void Update_ReturnsNonNegativeValue()
    {
        var yzv = new Yzv();
        var bars = GenerateBarData(100);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = yzv.Update(bars[i]);
            Assert.True(result.Value >= 0, $"YZV should be non-negative, got {result.Value}");
        }
    }

    [Fact]
    public void Update_HighVolatility_ProducesHigherValues()
    {
        var yzvLow = new Yzv(period: 10);
        var yzvHigh = new Yzv(period: 10);

        // Low volatility: small H-L range
        for (int i = 0; i < 30; i++)
        {
            double price = 100.0 + (i % 2) * 0.1;
            yzvLow.Update(new TBar(DateTime.UtcNow, price, price + 0.05, price - 0.05, price, 1000));
        }

        // High volatility: large H-L range
        for (int i = 0; i < 30; i++)
        {
            double price = 100.0 + (i % 2) * 5.0;
            yzvHigh.Update(new TBar(DateTime.UtcNow, price, price + 5.0, price - 5.0, price + 2.0, 1000));
        }

        Assert.True(yzvHigh.Last.Value > yzvLow.Last.Value,
            $"High vol ({yzvHigh.Last.Value}) should exceed low vol ({yzvLow.Last.Value})");
    }

    [Fact]
    public void Update_OvernightGaps_IncorporatesGapVolatility()
    {
        var yzvNoGap = new Yzv(period: 10);
        var yzvWithGap = new Yzv(period: 10);

        // No gaps: open = prev close
        double prevClose = 100.0;
        for (int i = 0; i < 30; i++)
        {
            yzvNoGap.Update(new TBar(DateTime.UtcNow, prevClose, prevClose + 1, prevClose - 1, prevClose + 0.5, 1000));
            prevClose = prevClose + 0.5;
        }

        // With gaps: open != prev close
        prevClose = 100.0;
        for (int i = 0; i < 30; i++)
        {
            double open = prevClose + (i % 2 == 0 ? 2.0 : -2.0); // Gap up or down
            yzvWithGap.Update(new TBar(DateTime.UtcNow, open, open + 1, open - 1, open + 0.5, 1000));
            prevClose = open + 0.5;
        }

        // YZV with gaps should show higher volatility due to overnight component
        Assert.True(yzvWithGap.Last.Value > yzvNoGap.Last.Value,
            $"Gap YZV ({yzvWithGap.Last.Value}) should exceed no-gap YZV ({yzvNoGap.Last.Value})");
    }

    #endregion

    #region IsHot and Warmup Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var yzv = new Yzv(period: 10);
        for (int i = 0; i < 5; i++)
        {
            yzv.Update(new TBar(DateTime.UtcNow, 100.0 + i, 102.0 + i, 98.0 + i, 101.0 + i, 1000));
        }
        Assert.False(yzv.IsHot);
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var yzv = new Yzv(period: 10);
        for (int i = 0; i < 15; i++)
        {
            yzv.Update(new TBar(DateTime.UtcNow, 100.0 + i, 102.0 + i, 98.0 + i, 101.0 + i, 1000));
        }
        Assert.True(yzv.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsToPeriod()
    {
        var yzv = new Yzv(period: 15);
        Assert.Equal(15, yzv.WarmupPeriod);
    }

    #endregion

    #region Bar Correction (isNew) Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var yzv = new Yzv(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            yzv.Update(new TBar(time.AddSeconds(i), 100 + i, 102 + i, 98 + i, 101 + i, 1000), isNew: true);
        }

        double valueBeforeNew = yzv.Last.Value;
        yzv.Update(new TBar(time.AddSeconds(10), 150, 155, 145, 152, 1000), isNew: true);

        Assert.NotEqual(valueBeforeNew, yzv.Last.Value);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var yzv = new Yzv(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            yzv.Update(new TBar(time.AddSeconds(i), 100 + i, 102 + i, 98 + i, 101 + i, 1000), isNew: true);
        }

        double valueBeforeCorrection = yzv.Last.Value;

        // First correction
        yzv.Update(new TBar(time.AddSeconds(15), 200, 210, 190, 205, 1000), isNew: false);
        double valueAfterCorrection1 = yzv.Last.Value;

        // Second correction to different value
        yzv.Update(new TBar(time.AddSeconds(15), 50, 55, 45, 52, 1000), isNew: false);
        double valueAfterCorrection2 = yzv.Last.Value;

        Assert.NotEqual(valueBeforeCorrection, valueAfterCorrection1);
        Assert.NotEqual(valueAfterCorrection1, valueAfterCorrection2);
    }

    [Fact]
    public void Update_MultipleCorrections_RestoresPreviousState()
    {
        var yzv = new Yzv(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            yzv.Update(new TBar(time.AddSeconds(i), 100 + i, 102 + i, 98 + i, 101 + i, 1000), isNew: true);
        }

        // Add a new bar
        var newBar = new TBar(time.AddSeconds(15), 115, 117, 113, 116, 1000);
        yzv.Update(newBar, isNew: true);
        double baseValue = yzv.Last.Value;

        // Multiple corrections should all restore to same base state
        yzv.Update(new TBar(time.AddSeconds(15), 200, 210, 190, 205, 1000), isNew: false);
        yzv.Update(newBar, isNew: false);
        double restoredValue = yzv.Last.Value;

        Assert.Equal(baseValue, restoredValue, 10);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsAllState()
    {
        var yzv = new Yzv(period: 5);
        var bars = GenerateBarData(20);

        for (int i = 0; i < bars.Count; i++)
        {
            yzv.Update(bars[i]);
        }

        Assert.True(yzv.IsHot);

        yzv.Reset();

        Assert.False(yzv.IsHot);
        Assert.Equal(default, yzv.Last);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var yzv = new Yzv(period: 5);
        var bars = GenerateBarData(20);

        for (int i = 0; i < bars.Count; i++)
        {
            yzv.Update(bars[i]);
        }
        double firstRunValue = yzv.Last.Value;

        yzv.Reset();

        for (int i = 0; i < bars.Count; i++)
        {
            yzv.Update(bars[i]);
        }
        double secondRunValue = yzv.Last.Value;

        Assert.Equal(firstRunValue, secondRunValue, 10);
    }

    #endregion

    #region NaN and Infinity Handling Tests

    [Fact]
    public void Update_NaNInput_UsesLastValidValue()
    {
        var yzv = new Yzv(period: 5);

        for (int i = 0; i < 15; i++)
        {
            yzv.Update(new TBar(DateTime.UtcNow, 100 + i, 102 + i, 98 + i, 101 + i, 1000));
        }

        // Update with NaN
        yzv.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 1000));
        Assert.True(double.IsFinite(yzv.Last.Value));
    }

    [Fact]
    public void Update_InfinityInput_UsesLastValidValue()
    {
        var yzv = new Yzv(period: 5);

        for (int i = 0; i < 15; i++)
        {
            yzv.Update(new TBar(DateTime.UtcNow, 100 + i, 102 + i, 98 + i, 101 + i, 1000));
        }

        yzv.Update(new TBar(DateTime.UtcNow, double.PositiveInfinity, double.PositiveInfinity, 98, 101, 1000));
        Assert.True(double.IsFinite(yzv.Last.Value));
    }

    [Fact]
    public void Update_MultipleNaNs_StaysFinite()
    {
        var yzv = new Yzv(period: 5);

        for (int i = 0; i < 15; i++)
        {
            yzv.Update(new TBar(DateTime.UtcNow, 100 + i, 102 + i, 98 + i, 101 + i, 1000));
        }

        for (int i = 0; i < 5; i++)
        {
            yzv.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 1000));
        }

        Assert.True(double.IsFinite(yzv.Last.Value));
    }

    #endregion

    #region TBarSeries and Batch Tests

    [Fact]
    public void Update_TBarSeries_ReturnsCorrectLength()
    {
        var yzv = new Yzv();
        var bars = GenerateBarData(100);

        var result = yzv.Update(bars);
        Assert.Equal(bars.Count, result.Count);
    }

    [Fact]
    public void Calculate_Static_ProducesValidResults()
    {
        var bars = GenerateBarData(100);

        var result = Yzv.Batch(bars, period: 10);

        Assert.Equal(bars.Count, result.Count);
        for (int i = 0; i < result.Count; i++)
        {
            Assert.True(double.IsFinite(result.Values[i]));
            Assert.True(result.Values[i] >= 0);
        }
    }

    [Fact]
    public void Batch_ProducesConsistentResults()
    {
        var bars = GenerateBarData(100);

        double[] output = new double[100];
        Yzv.Batch(bars, output, period: 10);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
            Assert.True(output[i] >= 0);
        }
    }

    [Fact]
    public void Batch_ZeroPeriod_ThrowsArgumentException()
    {
        var bars = GenerateBarData(10);
        double[] output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Yzv.Batch(bars, output, period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_OutputTooSmall_ThrowsArgumentException()
    {
        var bars = GenerateBarData(10);
        double[] output = new double[5];
        var ex = Assert.Throws<ArgumentException>(() => Yzv.Batch(bars, output));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptySource_DoesNotThrow()
    {
        var bars = new TBarSeries();
        double[] output = [];
        Yzv.Batch(bars, output);
        Assert.Empty(output);
    }

    [Fact]
    public void Batch_OhlcArrays_ProducesValidResults()
    {
        int len = 50;
        double[] open = new double[len];
        double[] high = new double[len];
        double[] low = new double[len];
        double[] close = new double[len];
        double[] output = new double[len];

        for (int i = 0; i < len; i++)
        {
            open[i] = 100 + i;
            high[i] = 102 + i;
            low[i] = 98 + i;
            close[i] = 101 + i;
        }

        Yzv.Batch(open, high, low, close, output, period: 10);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
            Assert.True(output[i] >= 0);
        }
    }

    #endregion

    #region Mode Consistency Tests

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        var bars = GenerateBarData(100);
        int period = 10;

        // Mode 1: Streaming
        var streamingYzv = new Yzv(period);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingYzv.Update(bars[i], isNew: true);
        }

        // Mode 2: TBarSeries batch
        var batchResult = Yzv.Batch(bars, period);

        // Mode 3: Span batch
        double[] spanOutput = new double[bars.Count];
        Yzv.Batch(bars, spanOutput, period);

        // Compare last 50 values (after warmup)
        int compareStart = bars.Count - 50;
        for (int i = compareStart; i < bars.Count; i++)
        {
            double batch = batchResult[i].Value;
            double span = spanOutput[i];

            Assert.Equal(batch, span, Tolerance);
        }

        // Final values should match
        Assert.Equal(streamingYzv.Last.Value, batchResult[bars.Count - 1].Value, 1e-8);
        Assert.Equal(streamingYzv.Last.Value, spanOutput[bars.Count - 1], 1e-8);
    }

    #endregion

    #region Event Tests

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var yzv = new Yzv(period: 5);
        int eventCount = 0;

        yzv.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        var time = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            yzv.Update(new TBar(time.AddSeconds(i), 100 + i, 102 + i, 98 + i, 101 + i, 1000));
        }

        Assert.Equal(5, eventCount);
    }

    #endregion

    #region TValue Input Tests

    [Fact]
    public void Update_TValue_CreatesSyntheticBar()
    {
        var yzv1 = new Yzv(period: 5);
        var yzv2 = new Yzv(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            // TValue input creates bar with O=H=L=C
            yzv1.Update(new TValue(time.AddSeconds(i), 100.0 + i));
            yzv2.Update(new TBar(time.AddSeconds(i), 100.0 + i, 100.0 + i, 100.0 + i, 100.0 + i, 0));
        }

        Assert.Equal(yzv1.Last.Value, yzv2.Last.Value, Tolerance);
    }

    #endregion

    #region Large Period Tests

    [Fact]
    public void LargeDataset_NoStackOverflow()
    {
        var bars = GenerateBarData(10000);

        double[] output = new double[10000];
        Yzv.Batch(bars, output, period: 20);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
            Assert.True(output[i] >= 0);
        }
    }

    #endregion

    #region Prime Tests

    [Fact]
    public void Prime_SetsInitialState()
    {
        var yzv = new Yzv(period: 5);
        double[] warmupData = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109];

        yzv.Prime(warmupData);

        Assert.True(yzv.IsHot);
    }

    #endregion

    #region Yang-Zhang Specific Tests

    [Fact]
    public void Update_RogersStatchellComponent_ContributesToResult()
    {
        // Test that intraday high-low movement contributes to volatility
        var yzvSmallRange = new Yzv(period: 10);
        var yzvLargeRange = new Yzv(period: 10);

        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100.0;
            // Small H-L range
            yzvSmallRange.Update(new TBar(DateTime.UtcNow, basePrice, basePrice + 0.1, basePrice - 0.1, basePrice, 1000));
            // Large H-L range (same open/close)
            yzvLargeRange.Update(new TBar(DateTime.UtcNow, basePrice, basePrice + 5.0, basePrice - 5.0, basePrice, 1000));
        }

        Assert.True(yzvLargeRange.Last.Value > yzvSmallRange.Last.Value,
            $"Large range YZV ({yzvLargeRange.Last.Value}) should exceed small range ({yzvSmallRange.Last.Value})");
    }

    [Fact]
    public void Update_BiasCorrection_WorksDuringWarmup()
    {
        var yzv = new Yzv(period: 20);
        var bars = GenerateBarData(5);

        // During warmup, bias correction should prevent extreme values
        for (int i = 0; i < bars.Count; i++)
        {
            var result = yzv.Update(bars[i]);
            Assert.True(double.IsFinite(result.Value), $"Value at index {i} should be finite");
            Assert.True(result.Value >= 0, $"Value at index {i} should be non-negative");
        }
    }

    #endregion
}