namespace QuanTAlib.Tests;

using Xunit;

public class IlrsTests
{
    private const double Tolerance = 1e-9;

    private static TSeries MakeSeries(int count = 500)
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.5, seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
    }

    private readonly TSeries _data = MakeSeries();

    // ── A) Constructor validation ──────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_InvalidPeriod_Throws(int period)
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ilrs(period));
        Assert.Equal("period", ex.ParamName);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(14)]
    [InlineData(100)]
    public void Constructor_ValidPeriod_Succeeds(int period)
    {
        var ilrs = new Ilrs(period);
        Assert.Equal($"Ilrs({period})", ilrs.Name);
        Assert.Equal(period, ilrs.WarmupPeriod);
    }

    [Fact]
    public void Constructor_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Ilrs(null!, 14));
    }

    // ── B) Basic calculation ───────────────────────────────────────────

    [Fact]
    public void Update_ReturnsFiniteValue()
    {
        var ilrs = new Ilrs(14);
        var result = ilrs.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_FirstValue_EqualsInput()
    {
        var ilrs = new Ilrs(14);
        var result = ilrs.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.Equal(42.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_ConstantInput_IntegralStaysConstant()
    {
        // Constant input → slope = 0 → integral stays at initial value
        const int period = 5;
        const double price = 100.0;
        var ilrs = new Ilrs(period);

        double result = 0;
        for (int i = 0; i < 50; i++)
        {
            result = ilrs.Update(new TValue(DateTime.UtcNow, price)).Value;
        }

        Assert.Equal(price, result, 1e-6);
    }

    [Fact]
    public void Update_LinearTrend_IntegralFollows()
    {
        // For y = x (linear trend), slope = 1, so integral grows by 1 each bar
        const int period = 5;
        var ilrs = new Ilrs(period);

        for (int i = 0; i < 20; i++)
        {
            var result = ilrs.Update(new TValue(DateTime.UtcNow, (double)i));
            Assert.True(double.IsFinite(result.Value));
        }

        // After warmup, integral should be growing
        Assert.True(ilrs.Last.Value > 10);
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var ilrs = new Ilrs(5);
        ilrs.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(ilrs.Last.Value));
    }

    [Fact]
    public void Name_IsCorrect()
    {
        var ilrs = new Ilrs(7);
        Assert.Equal("Ilrs(7)", ilrs.Name);
    }

    // ── C) State + bar correction ──────────────────────────────────────

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var ilrs = new Ilrs(5);
        ilrs.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        ilrs.Update(new TValue(DateTime.UtcNow, 101.0), isNew: true);

        var v1 = ilrs.Last.Value;
        ilrs.Update(new TValue(DateTime.UtcNow, 102.0), isNew: true);
        Assert.NotEqual(v1, ilrs.Last.Value);
    }

    [Fact]
    public void IsNew_False_RewritesCurrentBar()
    {
        var ilrs = new Ilrs(5);
        for (int i = 0; i < 8; i++)
        {
            ilrs.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        var before = ilrs.Last.Value;
        ilrs.Update(new TValue(DateTime.UtcNow, 200.0), isNew: false);
        Assert.NotEqual(before, ilrs.Last.Value);
    }

    [Fact]
    public void IterativeCorrections_Restore()
    {
        var ilrs = new Ilrs(5);
        for (int i = 0; i < 10; i++)
        {
            ilrs.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        var baseline = ilrs.Last.Value;
        // Apply multiple corrections then revert
        ilrs.Update(new TValue(DateTime.UtcNow, 200.0), isNew: false);
        ilrs.Update(new TValue(DateTime.UtcNow, 300.0), isNew: false);
        ilrs.Update(new TValue(DateTime.UtcNow, 109.0), isNew: false); // Original value
        Assert.Equal(baseline, ilrs.Last.Value, 1e-6);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var ilrs = new Ilrs(5);
        for (int i = 0; i < 10; i++)
        {
            ilrs.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        ilrs.Reset();
        Assert.False(ilrs.IsHot);
        Assert.Equal(0, ilrs.Last.Value);
    }

    // ── D) Warmup/convergence ──────────────────────────────────────────

    [Fact]
    public void IsHot_FlipsAtPeriod()
    {
        const int period = 5;
        var ilrs = new Ilrs(period);

        for (int i = 1; i <= period; i++)
        {
            ilrs.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            if (i < period)
            {
                Assert.False(ilrs.IsHot, $"Should not be hot at bar {i}");
            }
            else
            {
                Assert.True(ilrs.IsHot, $"Should be hot at bar {i}");
            }
        }
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var ilrs = new Ilrs(10);
        Assert.Equal(10, ilrs.WarmupPeriod);
    }

    // ── E) Robustness ──────────────────────────────────────────────────

    [Fact]
    public void NaN_UsesLastValidValue()
    {
        var ilrs = new Ilrs(5);
        ilrs.Update(new TValue(DateTime.UtcNow, 100.0));
        ilrs.Update(new TValue(DateTime.UtcNow, 101.0));
        ilrs.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(ilrs.Last.Value));
    }

    [Fact]
    public void Infinity_UsesLastValidValue()
    {
        var ilrs = new Ilrs(5);
        ilrs.Update(new TValue(DateTime.UtcNow, 100.0));
        ilrs.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));

        Assert.True(double.IsFinite(ilrs.Last.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var ilrs = new Ilrs(5);
        for (int i = 0; i < 10; i++)
        {
            double val = i == 5 ? double.NaN : 100.0 + i;
            ilrs.Update(new TValue(DateTime.UtcNow, val));
        }

        Assert.True(double.IsFinite(ilrs.Last.Value));
    }

    // ── F) Consistency (4 API modes) ───────────────────────────────────

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        const int period = 7;

        // Mode 1: Streaming
        var ilrsStream = new Ilrs(period);
        var streamResults = new double[_data.Count];
        for (int i = 0; i < _data.Count; i++)
        {
            streamResults[i] = ilrsStream.Update(_data[i]).Value;
        }

        // Mode 2: Batch (TSeries)
        var batchSeries = Ilrs.Batch(_data, period);

        // Mode 3: Span
        var spanOutput = new double[_data.Count];
        Ilrs.Batch(_data.Values, spanOutput, period);

        // Mode 4: Event-based
        var source = new TSeries();
        var ilrsEvent = new Ilrs(source, period);
        var eventResults = new double[_data.Count];
        for (int i = 0; i < _data.Count; i++)
        {
            source.Add(_data[i]);
            eventResults[i] = ilrsEvent.Last.Value;
        }

        // Compare all modes
        for (int i = 0; i < _data.Count; i++)
        {
            Assert.Equal(streamResults[i], batchSeries.Values[i], 1e-6);
            Assert.Equal(streamResults[i], spanOutput[i], 1e-6);
            Assert.Equal(streamResults[i], eventResults[i], 1e-6);
        }
    }

    // ── G) Span API tests ──────────────────────────────────────────────

    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[2];
        var ex = Assert.Throws<ArgumentException>(() => Ilrs.Batch(src, output, period: 5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_PeriodTooSmall_Throws()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() => Ilrs.Batch(src, output, period: 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_EmptyInput_NoOp()
    {
        Ilrs.Batch(ReadOnlySpan<double>.Empty, Span<double>.Empty, period: 5);
        Assert.True(true); // no-throw is the assertion
    }

    // ── H) Chainability ────────────────────────────────────────────────

    [Fact]
    public void Pub_Fires()
    {
        var ilrs = new Ilrs(5);
        bool fired = false;
        ilrs.Pub += (object? sender, in TValueEventArgs e) => fired = true;
        ilrs.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(fired);
    }

    [Fact]
    public void EventBased_Chaining()
    {
        var source = new TSeries();
        var ilrs = new Ilrs(source, period: 5);

        for (int i = 0; i < 10; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }
        Assert.True(ilrs.IsHot);
        Assert.True(double.IsFinite(ilrs.Last.Value));
    }

    // ── I) Dispose ─────────────────────────────────────────────────────

    [Fact]
    public void Dispose_Idempotent()
    {
        var ilrs = new Ilrs(5);
        ilrs.Dispose();
        ilrs.Dispose(); // Should not throw
        Assert.True(true); // no-throw is the assertion
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var source = new TSeries();
        var ilrs = new Ilrs(source, period: 5);
        ilrs.Dispose();

        source.Add(new TValue(DateTime.UtcNow, 999.0));
        Assert.False(ilrs.IsHot);
    }

    // ── J) ILRS-specific: Integration behavior ────────────────────────

    [Fact]
    public void PositiveSlope_IntegralIncreases()
    {
        var ilrs = new Ilrs(5);
        // Feed increasing prices
        for (int i = 0; i < 10; i++)
        {
            ilrs.Update(new TValue(DateTime.UtcNow, 100.0 + (i * 10)));
        }

        // Integral should be well above starting value
        Assert.True(ilrs.Last.Value > 100.0);
    }

    [Fact]
    public void NegativeSlope_IntegralDecreases()
    {
        var ilrs = new Ilrs(5);
        // Feed decreasing prices
        for (int i = 0; i < 10; i++)
        {
            ilrs.Update(new TValue(DateTime.UtcNow, 200.0 - (i * 10)));
        }

        // Integral should be below starting value
        Assert.True(ilrs.Last.Value < 200.0);
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var (results, indicator) = Ilrs.Calculate(_data, 14);
        Assert.Equal(_data.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Prime_SetsState()
    {
        var ilrs = new Ilrs(5);
        double[] values = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109];
        ilrs.Prime(values);
        Assert.True(ilrs.IsHot);
        Assert.True(double.IsFinite(ilrs.Last.Value));
    }
}
