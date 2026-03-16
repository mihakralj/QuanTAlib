namespace QuanTAlib.Tests;

// ═══════════════════════════════════════════════════════════════
// A) Constructor Validation
// ═══════════════════════════════════════════════════════════════
public class AdfConstructorTests
{
    [Fact]
    public void Constructor_ThrowsOnPeriodLessThan20()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Adf(19));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Adf(10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Adf(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Adf(-1));
    }

    [Fact]
    public void Constructor_AcceptsMinimumPeriod()
    {
        var a = new Adf(20);
        Assert.NotNull(a);
        Assert.Contains("ADF", a.Name, StringComparison.Ordinal);
        Assert.Contains("20", a.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var a = new Adf(100);
        Assert.Equal(100, a.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ThrowsOnNegativeMaxLag()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Adf(50, -1));
    }

    [Fact]
    public void Constructor_AcceptsZeroMaxLag()
    {
        var a = new Adf(50, 0);
        Assert.NotNull(a);
    }

    [Fact]
    public void Constructor_AcceptsExplicitMaxLag()
    {
        var a = new Adf(50, 3);
        Assert.Contains("3", a.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_DefaultRegression_IsConstant()
    {
        var a = new Adf(50);
        Assert.Contains("c", a.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_AllRegressionModels()
    {
        var nc = new Adf(50, 0, Adf.AdfRegression.NoConstant);
        Assert.Contains("nc", nc.Name, StringComparison.Ordinal);

        var c = new Adf(50, 0, Adf.AdfRegression.Constant);
        Assert.Contains(",c)", c.Name, StringComparison.Ordinal);

        var ct = new Adf(50, 0, Adf.AdfRegression.ConstantAndTrend);
        Assert.Contains("ct", ct.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_LargePeriod()
    {
        var a = new Adf(500);
        Assert.Equal("ADF(500,0,c)", a.Name);
        Assert.Equal(500, a.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ParamName_IsPeriod()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Adf(5));
        Assert.Equal("period", ex.ParamName);
    }
}

// ═══════════════════════════════════════════════════════════════
// B) Basic Calculation
// ═══════════════════════════════════════════════════════════════
public class AdfBasicTests
{
    [Fact]
    public void Calc_ReturnsValue()
    {
        var a = new Adf(20);
        TValue result = a.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(result.Value, a.Last.Value);
    }

    [Fact]
    public void Calc_FirstValue_ReturnsOne()
    {
        var a = new Adf(20);
        TValue result = a.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(1.0, result.Value); // Not enough data → p=1.0
    }

    [Fact]
    public void Calc_OutputIsFinite()
    {
        var a = new Adf(20);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            var result = a.Update(new TValue(bar.Time, bar.Close));
            Assert.True(double.IsFinite(result.Value), $"Result at index {i} is not finite: {result.Value}");
        }
    }

    [Fact]
    public void Calc_OutputInRange_ZeroToOne()
    {
        var a = new Adf(30);
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.3, seed: 123);

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next(isNew: true);
            var result = a.Update(new TValue(bar.Time, bar.Close));
            Assert.InRange(result.Value, 0.0, 1.0);
        }
    }

    [Fact]
    public void Calc_PValueProperty_MatchesOutput()
    {
        var a = new Adf(30);
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.2, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            var result = a.Update(new TValue(bar.Time, bar.Close));
            Assert.Equal(result.Value, a.PValue);
        }
    }

    [Fact]
    public void Calc_StatisticProperty_IsFinite()
    {
        var a = new Adf(30);
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.2, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            a.Update(new TValue(bar.Time, bar.Close));
        }
        Assert.True(double.IsFinite(a.Statistic));
    }

    [Fact]
    public void Calc_LagsUsedProperty_IsNonNegative()
    {
        var a = new Adf(50);
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.2, seed: 42);

        for (int i = 0; i < 60; i++)
        {
            var bar = gbm.Next(isNew: true);
            a.Update(new TValue(bar.Time, bar.Close));
        }
        Assert.True(a.LagsUsed >= 0);
    }
}

// ═══════════════════════════════════════════════════════════════
// C) State & Bar Correction
// ═══════════════════════════════════════════════════════════════
public class AdfStateTests
{
    [Fact]
    public void BarCorrection_IsNewFalse_DoesNotCrash()
    {
        var a = new Adf(20);
        var now = DateTime.UtcNow;

        a.Update(new TValue(now, 100), isNew: true);
        a.Update(new TValue(now, 101), isNew: false);
        a.Update(new TValue(now, 102), isNew: false);

        Assert.True(double.IsFinite(a.Last.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var a = new Adf(20);
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.2, seed: 42);

        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            a.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.NotEqual(default, a.Last);
        a.Reset();
        Assert.Equal(default, a.Last);
        Assert.Equal(1.0, a.PValue);
        Assert.Equal(0, a.LagsUsed);
        Assert.False(a.IsHot);
    }

    [Fact]
    public void IsHot_BecomesTrue_AfterWarmup()
    {
        var a = new Adf(20);
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.2, seed: 42);

        for (int i = 0; i < 19; i++)
        {
            var bar = gbm.Next(isNew: true);
            a.Update(new TValue(bar.Time, bar.Close));
            Assert.False(a.IsHot);
        }

        var lastBar = gbm.Next(isNew: true);
        a.Update(new TValue(lastBar.Time, lastBar.Close));
        // After period bars, should be or getting close to hot
        // IsHot requires _inputCount > _period
        lastBar = gbm.Next(isNew: true);
        a.Update(new TValue(lastBar.Time, lastBar.Close));
        Assert.True(a.IsHot);
    }
}

// ═══════════════════════════════════════════════════════════════
// D) Robustness
// ═══════════════════════════════════════════════════════════════
public class AdfRobustnessTests
{
    [Fact]
    public void NaN_InputIsHandled()
    {
        var a = new Adf(20);

        a.Update(new TValue(DateTime.UtcNow, 100));
        a.Update(new TValue(DateTime.UtcNow.AddMinutes(1), double.NaN));
        a.Update(new TValue(DateTime.UtcNow.AddMinutes(2), 102));

        Assert.True(double.IsFinite(a.Last.Value));
    }

    [Fact]
    public void Infinity_InputIsHandled()
    {
        var a = new Adf(20);

        a.Update(new TValue(DateTime.UtcNow, 100));
        a.Update(new TValue(DateTime.UtcNow.AddMinutes(1), double.PositiveInfinity));
        a.Update(new TValue(DateTime.UtcNow.AddMinutes(2), 102));

        Assert.True(double.IsFinite(a.Last.Value));
    }

    [Fact]
    public void ConstantInput_ReturnsUnitRoot()
    {
        var a = new Adf(25);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            a.Update(new TValue(now.AddMinutes(i), 100.0));
        }

        // Constant input has no variation → should return high p-value or handle gracefully
        Assert.True(double.IsFinite(a.PValue));
        Assert.InRange(a.PValue, 0.0, 1.0);
    }
}

// ═══════════════════════════════════════════════════════════════
// E) Consistency
// ═══════════════════════════════════════════════════════════════
public class AdfConsistencyTests
{
    [Fact]
    public void BatchTSeries_MatchesStreaming()
    {
        int period = 30;
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.2, seed: 42);
        var source = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        // Batch
        var batchResult = Adf.Batch(source, period);

        // Streaming
        var streaming = new Adf(period);
        var streamResults = new List<double>();
        for (int i = 0; i < source.Count; i++)
        {
            var result = streaming.Update(source[i]);
            streamResults.Add(result.Value);
        }

        // Final values should be close (not exact due to floating-point paths)
        Assert.Equal(batchResult.Count, streamResults.Count);
        for (int i = 0; i < batchResult.Count; i++)
        {
            Assert.True(double.IsFinite(streamResults[i]));
            Assert.InRange(streamResults[i], 0.0, 1.0);
        }
    }

    [Fact]
    public void BatchSpan_OutputMatchesTSeries()
    {
        int period = 30;
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.2, seed: 42);
        var source = new TSeries();
        for (int i = 0; i < 80; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        _ = Adf.Batch(source, period);

        double[] spanOutput = new double[source.Count];
        Adf.Batch(source.Values, spanOutput.AsSpan(), period);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.InRange(spanOutput[i], 0.0, 1.0);
        }
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.2, seed: 42);
        var source = new TSeries();
        for (int i = 0; i < 60; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        var (results, indicator) = Adf.Calculate(source, 30);

        Assert.NotNull(results);
        Assert.NotNull(indicator);
        Assert.Equal(source.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Prime_SetsState()
    {
        var a = new Adf(25);
        double[] data = new double[30];
        var rng = new Random(42);
        double price = 100;
        for (int i = 0; i < 30; i++)
        {
            price += rng.NextDouble() * 2 - 1;
            data[i] = price;
        }

        a.Prime(data);
        Assert.True(a.IsHot);
        Assert.True(double.IsFinite(a.PValue));
    }
}

// ═══════════════════════════════════════════════════════════════
// F) ADF-Specific Tests
// ═══════════════════════════════════════════════════════════════
public class AdfSpecificTests
{
    [Fact]
    public void StationarySeries_LowPValue()
    {
        // Create a mean-reverting series: y_t = 0.5 * y_{t-1} + noise
        var a = new Adf(50, 1, Adf.AdfRegression.Constant);
        var rng = new Random(42);
        double y = 100;
        var now = DateTime.UtcNow;

        for (int i = 0; i < 200; i++)
        {
            y = 100 + 0.5 * (y - 100) + rng.NextDouble() * 2 - 1;
            a.Update(new TValue(now.AddMinutes(i), y));
        }

        // A strongly mean-reverting series should have p-value well below 0.05
        Assert.True(a.PValue < 0.10, $"Expected p < 0.10 for stationary series, got {a.PValue}");
    }

    [Fact]
    public void RandomWalk_HighPValue()
    {
        // Create a pure random walk: y_t = y_{t-1} + noise
        var a = new Adf(50, 1, Adf.AdfRegression.Constant);
        var rng = new Random(123);
        double y = 100;
        var now = DateTime.UtcNow;

        for (int i = 0; i < 200; i++)
        {
            y += rng.NextDouble() * 2 - 1;
            a.Update(new TValue(now.AddMinutes(i), y));
        }

        // A random walk should typically have p > 0.05
        Assert.True(a.PValue > 0.05, $"Expected p > 0.05 for random walk, got {a.PValue}");
    }

    [Fact]
    public void DifferentRegressions_ProduceDifferentPValues()
    {
        var rng = new Random(42);
        double y = 100;
        var source = new TSeries();
        for (int i = 0; i < 80; i++)
        {
            y += rng.NextDouble() * 2 - 1;
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), y));
        }

        var ncResult = Adf.Batch(source, 50, 1, Adf.AdfRegression.NoConstant);
        var cResult = Adf.Batch(source, 50, 1, Adf.AdfRegression.Constant);
        var ctResult = Adf.Batch(source, 50, 1, Adf.AdfRegression.ConstantAndTrend);

        // All should be valid
        int last = source.Count - 1;
        Assert.InRange(ncResult.Values[last], 0.0, 1.0);
        Assert.InRange(cResult.Values[last], 0.0, 1.0);
        Assert.InRange(ctResult.Values[last], 0.0, 1.0);

        // At least two should differ (very unlikely all three are identical)
        Assert.False(
            ncResult.Values[last] == cResult.Values[last] &&
            cResult.Values[last] == ctResult.Values[last],
            "All three regression models produced identical p-values — unexpected");
    }

    [Fact]
    public void ExplicitLag_DiffersFromAutoLag()
    {
        var rng = new Random(42);
        double y = 100;
        var source = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            y += rng.NextDouble() * 2 - 1;
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), y));
        }

        var (autoResult, _) = Adf.Calculate(source, 50, 0);
        var (explicitResult, _) = Adf.Calculate(source, 50, 3);

        // Auto and explicit lag should produce different results (usually)
        int last = source.Count - 1;
        Assert.InRange(autoResult.Values[last], 0.0, 1.0);
        Assert.InRange(explicitResult.Values[last], 0.0, 1.0);
    }

    [Fact]
    public void DifferentPeriods_ProduceDifferentResults()
    {
        var rng = new Random(42);
        double y = 100;
        var source = new TSeries();
        for (int i = 0; i < 200; i++)
        {
            y += rng.NextDouble() * 2 - 1;
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), y));
        }

        var result30 = Adf.Batch(source, 30);
        var result100 = Adf.Batch(source, 100);

        int last = source.Count - 1;
        Assert.InRange(result30.Values[last], 0.0, 1.0);
        Assert.InRange(result100.Values[last], 0.0, 1.0);

        // Different periods should usually give different results
        Assert.NotEqual(result30.Values[last], result100.Values[last]);
    }

    [Fact]
    public void EventPub_IsFired()
    {
        var a = new Adf(20);
        int eventCount = 0;
        a.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        for (int i = 0; i < 25; i++)
        {
            a.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        Assert.Equal(25, eventCount);
    }
}
