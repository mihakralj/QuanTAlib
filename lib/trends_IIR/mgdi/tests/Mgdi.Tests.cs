
namespace QuanTAlib.Tests;

public class MgdiTests
{
    private readonly GBM _gbm;

    public MgdiTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    // ── Constructor ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsDefaults()
    {
        var mgdi = new Mgdi();
        Assert.Equal("Mgdi(14,0.6)", mgdi.Name);
        Assert.Equal(14, mgdi.WarmupPeriod);
        Assert.False(mgdi.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters()
    {
        var mgdi = new Mgdi(20, 0.8);
        Assert.Equal("Mgdi(20,0.8)", mgdi.Name);
        Assert.Equal(20, mgdi.WarmupPeriod);
    }

    [Fact]
    public void Constructor_PeriodZero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Mgdi(0));
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Mgdi(-1));
    }

    [Fact]
    public void Constructor_ZeroK_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Mgdi(14, 0));
    }

    [Fact]
    public void Constructor_NegativeK_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Mgdi(14, -1));
    }

    [Fact]
    public void Calculate_InvalidK_ThrowsArgumentOutOfRangeException()
    {
        var source = new double[10];
        var output = new double[10];

        Assert.Throws<ArgumentOutOfRangeException>(() => Mgdi.Batch(source, output, 14, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Mgdi.Batch(source, output, 14, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Mgdi.Batch(source, output, 14, double.NegativeInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Mgdi.Batch(source, output, 14, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Mgdi.Batch(source, output, 14, -1));
    }

    [Fact]
    public void Constructor_NaNK_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Mgdi(14, double.NaN));
    }

    [Fact]
    public void Constructor_InfinityK_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Mgdi(14, double.PositiveInfinity));
    }

    [Fact]
    public void Constructor_WithPublisher_Subscribes()
    {
        var source = new TSeries();
        var mgdi = new Mgdi(source, 14, 0.6);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(mgdi.Last.Value));
    }

    // ── IsHot ───────────────────────────────────────────────────────────

    [Fact]
    public void IsHot_BecomesTrue_AfterPeriodUpdates()
    {
        var mgdi = new Mgdi(14, 0.6);
        for (int i = 0; i < 13; i++)
        {
            mgdi.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.False(mgdi.IsHot);

        mgdi.Update(new TValue(DateTime.UtcNow, 113));
        Assert.True(mgdi.IsHot);
    }

    // ── NaN handling ────────────────────────────────────────────────────

    [Fact]
    public void NaN_FirstValue_DoesNotInitializeToZero()
    {
        var mgdi = new Mgdi(14, 0.6);

        // First value is NaN
        var result = mgdi.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Should be NaN, not 0.0
        Assert.True(double.IsNaN(result.Value), $"Expected NaN but got {result.Value}");
    }

    [Fact]
    public void NaN_Sequence_InitializesOnFirstValid()
    {
        var mgdi = new Mgdi(14, 0.6);

        // Sequence of NaNs
        mgdi.Update(new TValue(DateTime.UtcNow, double.NaN));
        mgdi.Update(new TValue(DateTime.UtcNow, double.NaN));

        // First valid value
        const double firstValid = 100.0;
        var result = mgdi.Update(new TValue(DateTime.UtcNow, firstValid));

        Assert.Equal(firstValid, result.Value);
    }

    [Fact]
    public void NaN_AfterValid_UsesLastValid()
    {
        var mgdi = new Mgdi(14, 0.6);
        mgdi.Update(new TValue(DateTime.UtcNow, 100.0));
        mgdi.Update(new TValue(DateTime.UtcNow, 101.0));

        var result = mgdi.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_AfterValid_UsesLastValid()
    {
        var mgdi = new Mgdi(14, 0.6);
        mgdi.Update(new TValue(DateTime.UtcNow, 100.0));

        var result = mgdi.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    // ── Calculation ─────────────────────────────────────────────────────

    [Fact]
    public void Standard_Calculation()
    {
        var mgdi = new Mgdi(14, 0.6);
        mgdi.Update(new TValue(DateTime.UtcNow, 100.0));
        var result = mgdi.Update(new TValue(DateTime.UtcNow, 101.0));

        Assert.True(result.Value > 100.0);
        Assert.True(result.Value < 101.0);
    }

    [Fact]
    public void ConstantPrice_ConvergesToPrice()
    {
        var mgdi = new Mgdi(14, 0.6);
        for (int i = 0; i < 100; i++)
        {
            mgdi.Update(new TValue(DateTime.UtcNow, 50.0));
        }
        Assert.Equal(50.0, mgdi.Last.Value, 1e-6);
    }

    [Fact]
    public void RisingPrices_MgdiFollowsBelow()
    {
        var mgdi = new Mgdi(14, 0.6);
        double lastPrice = 0;
        for (int i = 1; i <= 50; i++)
        {
            lastPrice = 100 + i;
            mgdi.Update(new TValue(DateTime.UtcNow, lastPrice));
        }
        // MGDI is a lagging indicator - in a rising market it should be below price
        Assert.True(mgdi.Last.Value < lastPrice,
            $"MGDI {mgdi.Last.Value} should lag below price {lastPrice}");
    }

    // ── Bar Correction ──────────────────────────────────────────────────

    [Fact]
    public void BarCorrection_RestoresState()
    {
        var mgdi = new Mgdi(14, 0.6);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            mgdi.Update(new TValue(now.AddMinutes(i), 100 + i));
        }

        // New bar
        var result1 = mgdi.Update(new TValue(now.AddMinutes(20), 200), isNew: true);

        // Correction back to same value
        mgdi.Update(new TValue(now.AddMinutes(20), 150), isNew: false);
        mgdi.Update(new TValue(now.AddMinutes(20), 200), isNew: false);
        var restored = mgdi.Last;

        Assert.Equal(result1.Value, restored.Value, 1e-10);
    }

    // ── Reset ───────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsState()
    {
        var mgdi = new Mgdi(14, 0.6);
        for (int i = 0; i < 20; i++)
        {
            mgdi.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(mgdi.IsHot);

        mgdi.Reset();
        Assert.False(mgdi.IsHot);
        Assert.Equal(default, mgdi.Last);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var mgdi = new Mgdi(14, 0.6);
        for (int i = 0; i < 20; i++)
        {
            mgdi.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        var firstResult = mgdi.Last.Value;

        mgdi.Reset();
        for (int i = 0; i < 20; i++)
        {
            mgdi.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.Equal(firstResult, mgdi.Last.Value, 1e-10);
    }

    // ── Batch ───────────────────────────────────────────────────────────

    [Fact]
    public void Batch_TSeries_MatchesStreaming()
    {
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        var batchResult = Mgdi.Batch(series, 14, 0.6);

        var streaming = new Mgdi(14, 0.6);
        var streamingResults = new TSeries();
        foreach (var item in series)
        {
            streamingResults.Add(streaming.Update(item));
        }

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResults[i].Value, 1e-10);
        }
    }

    [Fact]
    public void Batch_Span_MatchesStreaming()
    {
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var values = data.Close.Values.ToArray();

        var spanOutput = new double[values.Length];
        Mgdi.Batch(values, spanOutput, 14, 0.6);

        var streaming = new Mgdi(14, 0.6);
        for (int i = 0; i < values.Length; i++)
        {
            var result = streaming.Update(new TValue(DateTime.UtcNow, values[i]));
            Assert.Equal(result.Value, spanOutput[i], 1e-10);
        }
    }

    [Fact]
    public void Batch_Span_MismatchedLength_Throws()
    {
        var source = new double[10];
        var output = new double[5];
        Assert.Throws<ArgumentException>(() => Mgdi.Batch(source, output, 14, 0.6));
    }

    [Fact]
    public void Batch_Span_Empty_NoThrow()
    {
        var source = Array.Empty<double>();
        var output = Array.Empty<double>();
        Mgdi.Batch(source, output, 14, 0.6); // Should not throw
        Assert.True(true); // S2699: explicit assertion for no-throw test
    }

    [Fact]
    public void Update_EmptyTSeries_ReturnsEmpty()
    {
        var mgdi = new Mgdi(14, 0.6);
        var result = mgdi.Update(new TSeries());
        Assert.Empty(result);
    }

    // ── Calculate ───────────────────────────────────────────────────────

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var (results, indicator) = Mgdi.Calculate(data.Close, 14, 0.6);
        Assert.Equal(50, results.Count);
        Assert.True(indicator.IsHot);
    }

    // ── Prime ───────────────────────────────────────────────────────────

    [Fact]
    public void Prime_WarmsUpIndicator()
    {
        var mgdi = new Mgdi(14, 0.6);
        var values = new double[20];
        for (int i = 0; i < 20; i++)
        {
            values[i] = 100 + i;
        }

        mgdi.Prime(values);
        Assert.True(mgdi.IsHot);
    }
}
