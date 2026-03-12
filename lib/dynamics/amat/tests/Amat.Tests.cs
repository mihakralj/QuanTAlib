namespace QuanTAlib.Tests;

public class AmatTests
{
    private readonly GBM _gbm;
    private readonly TSeries _testData;

    public AmatTests()
    {
        _gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = _gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        _testData = bars.Close;
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Amat(0, 50));
        Assert.Throws<ArgumentException>(() => new Amat(-1, 50));
        Assert.Throws<ArgumentException>(() => new Amat(10, 0));
        Assert.Throws<ArgumentException>(() => new Amat(10, -1));
        Assert.Throws<ArgumentException>(() => new Amat(50, 10)); // fast >= slow
        Assert.Throws<ArgumentException>(() => new Amat(10, 10)); // fast == slow

        var amat = new Amat(10, 50);
        Assert.NotNull(amat);
    }

    [Fact]
    public void Constructor_ValidBoundaryValues()
    {
        var amat1 = new Amat(1, 2);
        Assert.NotNull(amat1);
        Assert.Equal("Amat(1,2)", amat1.Name);

        var amat2 = new Amat(10, 50);
        Assert.Equal("Amat(10,50)", amat2.Name);
        Assert.Equal(50, amat2.WarmupPeriod);
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var amat = new Amat(10, 50);

        Assert.Equal(0, amat.Last.Value);

        TValue result = amat.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(result.Value, amat.Last.Value);
    }

    [Fact]
    public void FirstValue_ReturnsZero()
    {
        var amat = new Amat(10, 50);
        TValue result = amat.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(0.0, result.Value); // First value is 0 (neutral) - not enough data for trend
    }

    [Fact]
    public void Properties_Accessible()
    {
        var amat = new Amat(10, 50);

        Assert.Equal(0, amat.Last.Value);
        Assert.False(amat.IsHot);
        Assert.Contains("Amat", amat.Name, StringComparison.Ordinal);

        amat.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(amat.Last.Value));
        Assert.True(double.IsFinite(amat.Strength.Value));
        Assert.True(double.IsFinite(amat.FastEma.Value));
        Assert.True(double.IsFinite(amat.SlowEma.Value));
    }

    [Fact]
    public void TrendValues_AreValid()
    {
        var amat = new Amat(5, 10);

        // Feed rising prices to create bullish trend
        for (int i = 0; i < 20; i++)
        {
            amat.Update(new TValue(DateTime.UtcNow, 100 + i * 2));
        }

        // Trend should be +1, -1, or 0
        Assert.True(amat.Last.Value >= -1 && amat.Last.Value <= 1);
        Assert.True(Math.Abs(amat.Last.Value - (-1)) < 1e-10 || Math.Abs(amat.Last.Value) < 1e-10 || Math.Abs(amat.Last.Value - 1) < 1e-10);
    }

    [Fact]
    public void BullishTrend_WhenPricesRising()
    {
        var amat = new Amat(3, 10);

        // Feed steadily rising prices
        for (int i = 0; i < 50; i++)
        {
            amat.Update(new TValue(DateTime.UtcNow, 100 + i * 3));
        }

        // Should be bullish when fast EMA > slow EMA and both rising
        Assert.True(amat.FastEma.Value > amat.SlowEma.Value);
        Assert.Equal(1.0, amat.Last.Value);
    }

    [Fact]
    public void BearishTrend_WhenPricesFalling()
    {
        var amat = new Amat(3, 10);

        // Start with a stable price
        for (int i = 0; i < 20; i++)
        {
            amat.Update(new TValue(DateTime.UtcNow, 200));
        }

        // Feed steadily falling prices
        for (int i = 0; i < 50; i++)
        {
            amat.Update(new TValue(DateTime.UtcNow, 200 - i * 3));
        }

        // Should be bearish when fast EMA < slow EMA and both falling
        Assert.True(amat.FastEma.Value < amat.SlowEma.Value);
        Assert.Equal(-1.0, amat.Last.Value);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var amat = new Amat(10, 50);

        amat.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = amat.Last.Value;

        amat.Update(new TValue(DateTime.UtcNow, 200), isNew: true);
        double value2 = amat.Last.Value;

        // Values may or may not change depending on trend conditions
        Assert.True(double.IsFinite(value1));
        Assert.True(double.IsFinite(value2));
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var amat = new Amat(5, 10);

        // Build up some history
        for (int i = 0; i < 20; i++)
        {
            amat.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        double emaBeforeUpdate = amat.FastEma.Value;

        // Update with new value (isNew=false should update but allow rollback)
        amat.Update(new TValue(DateTime.UtcNow, 200), isNew: false);
        double emaAfterUpdate = amat.FastEma.Value;

        Assert.NotEqual(emaBeforeUpdate, emaAfterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var amat = new Amat(5, 10);

        // Feed 15 new values
        TValue fifteenthInput = default;
        for (int i = 0; i < 15; i++)
        {
            var bar = _gbm.Next(isNew: true);
            fifteenthInput = new TValue(bar.Time, bar.Close);
            amat.Update(fifteenthInput, isNew: true);
        }

        // Remember state after 15 values
        double stateAfterFifteen = amat.FastEma.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = _gbm.Next(isNew: false);
            amat.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 15th input again with isNew=false
        amat.Update(fifteenthInput, isNew: false);

        // State should match the original state after 15 values
        Assert.Equal(stateAfterFifteen, amat.FastEma.Value, 1e-10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var amat = new Amat(10, 50);

        for (int i = 0; i < 20; i++)
        {
            amat.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        double fastEmaBefore = amat.FastEma.Value;

        amat.Reset();

        Assert.Equal(0, amat.Last.Value);
        Assert.Equal(0, amat.Strength.Value);
        Assert.Equal(0, amat.FastEma.Value);
        Assert.Equal(0, amat.SlowEma.Value);
        Assert.False(amat.IsHot);

        // After reset, should accept new values
        amat.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, amat.FastEma.Value);
        Assert.NotEqual(fastEmaBefore, amat.FastEma.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var amat = new Amat(5, 20);

        Assert.False(amat.IsHot);

        // Feed values until warmup complete
        int count = 0;
        while (!amat.IsHot && count < 200)
        {
            amat.Update(new TValue(DateTime.UtcNow, 100 + count));
            count++;
        }

        Assert.True(amat.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var amat = new Amat(5, 10);

        amat.Update(new TValue(DateTime.UtcNow, 100));
        amat.Update(new TValue(DateTime.UtcNow, 110));
        _ = amat.FastEma.Value;

        var resultAfterNaN = amat.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.True(double.IsFinite(amat.FastEma.Value));
        Assert.True(double.IsFinite(amat.SlowEma.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var amat = new Amat(5, 10);

        amat.Update(new TValue(DateTime.UtcNow, 100));
        amat.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterPosInf = amat.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));
        Assert.True(double.IsFinite(amat.FastEma.Value));

        var resultAfterNegInf = amat.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
        Assert.True(double.IsFinite(amat.FastEma.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var amat = new Amat(5, 10);

        amat.Update(new TValue(DateTime.UtcNow, 100));
        amat.Update(new TValue(DateTime.UtcNow, 110));
        amat.Update(new TValue(DateTime.UtcNow, 120));

        var r1 = amat.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = amat.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = amat.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var amatIterative = new Amat(10, 30);
        var amatBatch = new Amat(10, 30);

        // Calculate iteratively
        var iterativeResults = new List<double>();
        foreach (var item in _testData)
        {
            iterativeResults.Add(amatIterative.Update(item).Value);
        }

        // Calculate batch
        var batchResults = amatBatch.Update(_testData);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i], batchResults[i].Value, 1e-10);
        }
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int fastPeriod = 10;
        int slowPeriod = 30;

        // 1. Batch Mode (static method)
        var batchSeries = Amat.Batch(_testData, fastPeriod, slowPeriod);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode (static method with spans)
        var tValues = _testData.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Amat.Batch(spanInput, spanOutput, fastPeriod, slowPeriod);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode (instance, one value at a time)
        var streamingInd = new Amat(fastPeriod, slowPeriod);
        for (int i = 0; i < _testData.Count; i++)
        {
            streamingInd.Update(_testData[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode (chained via ITValuePublisher)
        var pubSource = new TSeries();
        var eventingInd = new Amat(pubSource, fastPeriod, slowPeriod);
        for (int i = 0; i < _testData.Count; i++)
        {
            pubSource.Add(_testData[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert all modes produce identical results
        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }

    [Fact]
    public void SpanCalc_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] trend = new double[5];
        double[] strength = new double[5];
        double[] wrongSize = new double[3];

        Assert.Throws<ArgumentException>(() =>
            Amat.Batch(source.AsSpan(), wrongSize.AsSpan(), strength.AsSpan(), 5, 10));
        Assert.Throws<ArgumentException>(() =>
            Amat.Batch(source.AsSpan(), trend.AsSpan(), wrongSize.AsSpan(), 5, 10));
        Assert.Throws<ArgumentException>(() =>
            Amat.Batch(source.AsSpan(), trend.AsSpan(), strength.AsSpan(), 0, 10));
        Assert.Throws<ArgumentException>(() =>
            Amat.Batch(source.AsSpan(), trend.AsSpan(), strength.AsSpan(), 10, 5)); // fast >= slow
    }

    [Fact]
    public void SpanCalc_MatchesTSeriesCalc()
    {
        double[] source = _testData.Values.ToArray();
        double[] trend = new double[source.Length];

        var tseriesResult = Amat.Batch(_testData, 10, 30);
        Amat.Batch(source.AsSpan(), trend.AsSpan(), 10, 30);

        // Since trend values are discrete (-1, 0, 1), check after warmup where
        // both methods should converge. Early values may differ due to EMA initialization.
        int warmup = 30 * 2; // Allow extra warmup
        int matched = 0;
        for (int i = warmup; i < source.Length; i++)
        {
            if (Math.Abs(tseriesResult[i].Value - trend[i]) < 0.01)
            {
                matched++;
            }
        }
        // At least 95% of values after warmup should match
        double matchRate = (double)matched / (source.Length - warmup);
        Assert.True(matchRate > 0.95, $"Match rate {matchRate:P1} is below 95%");
    }

    [Fact]
    public void SpanCalc_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130, 140, 150, 160, 170, 180];
        double[] trend = new double[10];
        double[] strength = new double[10];

        Amat.Batch(source.AsSpan(), trend.AsSpan(), strength.AsSpan(), 3, 5);

        foreach (var val in trend)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
        foreach (var val in strength)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        var (results, indicator) = Amat.Calculate(_testData, 10, 30);

        Assert.Equal(_testData.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.Equal(results.Last.Value, indicator.Last.Value);
    }

    [Fact]
    public void Chainability_Works()
    {
        var source = new TSeries();
        var amat = new Amat(source, 10, 30);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(amat.Last.Value));
        Assert.True(double.IsFinite(amat.FastEma.Value));
    }

    [Fact]
    public void Pub_EventFires()
    {
        var amat = new Amat(10, 30);
        bool eventFired = false;
        amat.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        amat.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(eventFired);
    }

    [Fact]
    public void FlatLine_ReturnsNeutral()
    {
        var amat = new Amat(5, 10);

        // Flat prices - neither rising nor falling
        for (int i = 0; i < 50; i++)
        {
            amat.Update(new TValue(DateTime.UtcNow, 100));
        }

        // Should be neutral (0) when EMAs are not clearly rising or falling
        Assert.Equal(0, amat.Last.Value);
    }

    [Fact]
    public void Strength_CalculatesCorrectly()
    {
        var amat = new Amat(3, 10);

        // Feed rising prices to create divergence
        for (int i = 0; i < 30; i++)
        {
            amat.Update(new TValue(DateTime.UtcNow, 100 + i * 5));
        }

        // Strength should be positive when there's divergence
        Assert.True(amat.Strength.Value > 0);

        // Strength formula: |fast - slow| / slow * 100
        double expectedStrength = Math.Abs(amat.FastEma.Value - amat.SlowEma.Value) / amat.SlowEma.Value * 100;
        Assert.Equal(expectedStrength, amat.Strength.Value, 1e-10);
    }
}
