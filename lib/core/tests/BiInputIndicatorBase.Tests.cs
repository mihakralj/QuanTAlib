namespace QuanTAlib.Tests;

/// <summary>
/// Tests for BiInputIndicatorBase abstract class, exercised through Mae (simplest subclass).
/// Covers: constructor validation, Period/IsHot/Name/WarmupPeriod properties,
/// Update(TValue,TValue), Update(double,double), Update(TValue) throws, Update(TSeries) throws,
/// Prime throws, Reset, SanitizeActual/Predicted (NaN, Infinity, first-value-NaN),
/// ProcessNewBar, ProcessBarCorrection (isNew=false), sliding window, resync,
/// CalculateImpl, ValidateBatchInputs, Dispose, Pub event, PostProcess (via Rmse).
/// </summary>
public class BiInputIndicatorBaseTests
{
    // ═══════════════════════════════ Constructor ═══════════════════════════════

    [Fact]
    public void Constructor_ZeroPeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Mae(0));
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Mae(-1));
    }

    [Fact]
    public void Constructor_LargeNegativePeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Mae(-100));
    }

    [Fact]
    public void Constructor_ValidPeriod_Succeeds()
    {
        var indicator = new Mae(10);
        Assert.NotNull(indicator);
    }

    [Fact]
    public void Constructor_PeriodOne_IsValid()
    {
        var indicator = new Mae(1);
        Assert.NotNull(indicator);
        Assert.Equal(1, indicator.Period);
    }

    // ═══════════════════════════════ Properties ═══════════════════════════════

    [Fact]
    public void Period_ReturnsConstructorValue()
    {
        Assert.Equal(5, new Mae(5).Period);
        Assert.Equal(20, new Mae(20).Period);
        Assert.Equal(100, new Mae(100).Period);
    }

    [Fact]
    public void WarmupPeriod_EqualsPeriod()
    {
        var indicator = new Mae(14);
        Assert.Equal(14, indicator.WarmupPeriod);
    }

    [Fact]
    public void Name_ContainsIndicatorName()
    {
        var indicator = new Mae(10);
        Assert.Contains("Mae", indicator.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void IsHot_FalseInitially()
    {
        var indicator = new Mae(5);
        Assert.False(indicator.IsHot);
    }

    [Fact]
    public void IsHot_FalseBeforePeriodReached()
    {
        var indicator = new Mae(5);
        for (int i = 0; i < 4; i++)
        {
            indicator.Update(i * 10.0, (i * 10.0) + 5.0);
            Assert.False(indicator.IsHot);
        }
    }

    [Fact]
    public void IsHot_TrueAfterPeriodReached()
    {
        var indicator = new Mae(5);
        for (int i = 0; i < 5; i++)
        {
            indicator.Update(i * 10.0, (i * 10.0) + 5.0);
        }
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void IsHot_StaysTrueAfterMoreUpdates()
    {
        var indicator = new Mae(3);
        for (int i = 0; i < 20; i++)
        {
            indicator.Update(i * 10.0, (i * 10.0) + 5.0);
        }
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Last_DefaultBeforeUpdate()
    {
        var indicator = new Mae(5);
        Assert.Equal(0.0, indicator.Last.Value);
    }

    // ═══════════════════════════════ Update(double, double) ═══════════════════

    [Fact]
    public void Update_DoubleDouble_ReturnsResult()
    {
        var indicator = new Mae(3);
        var result = indicator.Update(100.0, 110.0);
        Assert.Equal(10.0, result.Value, 10);
    }

    [Fact]
    public void Update_DoubleDouble_SetsLast()
    {
        var indicator = new Mae(3);
        indicator.Update(100.0, 110.0);
        Assert.Equal(10.0, indicator.Last.Value, 10);
    }

    [Fact]
    public void Update_DoubleDouble_IsNewDefaultTrue()
    {
        var indicator = new Mae(3);
        indicator.Update(100.0, 110.0);
        indicator.Update(200.0, 220.0);
        // Two distinct updates means 2 bars were added
        Assert.Equal(15.0, indicator.Last.Value, 10); // (10 + 20) / 2
    }

    // ═══════════════════════════════ Update(TValue, TValue) ═══════════════════

    [Fact]
    public void Update_TValueTValue_ReturnsResult()
    {
        var indicator = new Mae(3);
        var now = DateTime.UtcNow;
        var result = indicator.Update(
            new TValue(now, 100.0),
            new TValue(now, 110.0));
        Assert.Equal(10.0, result.Value, 10);
    }

    [Fact]
    public void Update_TValueTValue_PreservesTime()
    {
        var indicator = new Mae(3);
        var now = DateTime.UtcNow;
        var result = indicator.Update(
            new TValue(now, 50.0),
            new TValue(now, 60.0));
        Assert.Equal(now.Ticks, result.Time);
    }

    // ═══════════════════════════════ Single-input throws ═══════════════════════

    [Fact]
    public void Update_SingleTValue_Throws()
    {
        var indicator = new Mae(5);
        Assert.Throws<NotSupportedException>(() =>
            indicator.Update(new TValue(DateTime.UtcNow, 100.0)));
    }

    [Fact]
    public void Update_SingleTSeries_Throws()
    {
        var indicator = new Mae(5);
        Assert.Throws<NotSupportedException>(() =>
            indicator.Update(new TSeries()));
    }

    [Fact]
    public void Prime_SingleSpan_Throws()
    {
        var indicator = new Mae(5);
        Assert.Throws<NotSupportedException>(() =>
            indicator.Prime(new double[] { 1, 2, 3 }));
    }

    // ═══════════════════════════════ Sliding Window ═══════════════════════════

    [Fact]
    public void SlidingWindow_DropsOldestValue()
    {
        var indicator = new Mae(3);

        // |10-15|=5, |20-30|=10, |30-25|=5 → mean=(5+10+5)/3=6.667
        indicator.Update(10.0, 15.0);
        indicator.Update(20.0, 30.0);
        indicator.Update(30.0, 25.0);
        Assert.Equal(20.0 / 3.0, indicator.Last.Value, 10);

        // Window slides: drop 5, add |40-50|=10 → mean=(10+5+10)/3=8.333
        indicator.Update(40.0, 50.0);
        Assert.Equal(25.0 / 3.0, indicator.Last.Value, 10);
    }

    [Fact]
    public void SlidingWindow_PeriodOne_AlwaysLatestError()
    {
        var indicator = new Mae(1);

        indicator.Update(10.0, 15.0);
        Assert.Equal(5.0, indicator.Last.Value, 10);

        indicator.Update(20.0, 30.0);
        Assert.Equal(10.0, indicator.Last.Value, 10);

        indicator.Update(100.0, 100.0);
        Assert.Equal(0.0, indicator.Last.Value, 10);
    }

    // ═══════════════════════════════ Bar Correction (isNew=false) ═════════════

    [Fact]
    public void BarCorrection_OverwritesLastBar()
    {
        var indicator = new Mae(5);

        indicator.Update(100.0, 110.0); // error=10
        indicator.Update(200.0, 220.0); // error=20

        // Correct last bar
        indicator.Update(200.0, 210.0, isNew: false); // error=10

        // Mean = (10 + 10) / 2 = 10
        Assert.Equal(10.0, indicator.Last.Value, 10);
    }

    [Fact]
    public void BarCorrection_MultipleCorrections_LastOneWins()
    {
        var indicator = new Mae(5);

        indicator.Update(100.0, 110.0); // error=10
        indicator.Update(200.0, 220.0, isNew: true); // error=20

        // Multiple corrections to same bar
        indicator.Update(200.0, 215.0, isNew: false); // error=15
        indicator.Update(200.0, 205.0, isNew: false); // error=5
        indicator.Update(200.0, 203.0, isNew: false); // error=3

        // Mean = (10 + 3) / 2 = 6.5
        Assert.Equal(6.5, indicator.Last.Value, 10);
    }

    [Fact]
    public void BarCorrection_RestoresToOriginalWhenSameValue()
    {
        var indicator = new Mae(5);

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(i * 10.0, (i * 10.0) + 5.0);
        }
        double original = indicator.Last.Value;

        // Correct with different values
        indicator.Update(999.0, 888.0, isNew: false);
        Assert.NotEqual(original, indicator.Last.Value);

        // Restore original
        indicator.Update(90.0, 95.0, isNew: false);
        Assert.Equal(original, indicator.Last.Value, 10);
    }

    // ═══════════════════════════════ NaN/Infinity Sanitization ════════════════

    [Fact]
    public void NaN_Actual_UsesLastValidActual()
    {
        var indicator = new Mae(5);
        indicator.Update(100.0, 110.0);
        indicator.Update(double.NaN, 120.0);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void NaN_Predicted_UsesLastValidPredicted()
    {
        var indicator = new Mae(5);
        indicator.Update(100.0, 110.0);
        indicator.Update(120.0, double.NaN);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void NaN_Both_UsesLastValidValues()
    {
        var indicator = new Mae(5);
        indicator.Update(100.0, 110.0);
        indicator.Update(120.0, 130.0);
        var result = indicator.Update(double.NaN, double.NaN);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void PositiveInfinity_Sanitized()
    {
        var indicator = new Mae(5);
        indicator.Update(100.0, 110.0);
        var result = indicator.Update(double.PositiveInfinity, 120.0);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void NegativeInfinity_Sanitized()
    {
        var indicator = new Mae(5);
        indicator.Update(100.0, 110.0);
        var result = indicator.Update(120.0, double.NegativeInfinity);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void FirstValue_NaN_ReturnsZeroSubstitute()
    {
        var indicator = new Mae(5);
        var result = indicator.Update(double.NaN, double.NaN);
        // When no last valid value exists, 0.0 is substituted
        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(0.0, result.Value, 10);
    }

    [Fact]
    public void MultipleConsecutiveNaN_AllFinite()
    {
        var indicator = new Mae(5);
        indicator.Update(100.0, 110.0);

        for (int i = 0; i < 10; i++)
        {
            var result = indicator.Update(double.NaN, double.NaN);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ═══════════════════════════════ Reset ════════════════════════════════════

    [Fact]
    public void Reset_ClearsIsHot()
    {
        var indicator = new Mae(3);
        for (int i = 0; i < 5; i++)
        {
            indicator.Update(i * 10.0, (i * 10.0) + 5.0);
        }
        Assert.True(indicator.IsHot);

        indicator.Reset();
        Assert.False(indicator.IsHot);
    }

    [Fact]
    public void Reset_ClearsLast()
    {
        var indicator = new Mae(3);
        indicator.Update(100.0, 110.0);
        Assert.NotEqual(0.0, indicator.Last.Value);

        indicator.Reset();
        Assert.Equal(0.0, indicator.Last.Value);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var indicator = new Mae(3);

        // First use
        for (int i = 0; i < 5; i++)
        {
            indicator.Update(100.0, 110.0);
        }
        double firstResult = indicator.Last.Value;

        indicator.Reset();

        // Second use - should produce same results
        for (int i = 0; i < 5; i++)
        {
            indicator.Update(100.0, 110.0);
        }
        double secondResult = indicator.Last.Value;

        Assert.Equal(firstResult, secondResult, 10);
    }

    // ═══════════════════════════════ Resync ═══════════════════════════════════

    [Fact]
    public void Resync_After1000Updates_MaintainsAccuracy()
    {
        var indicator = new Mae(5);

        for (int i = 0; i < 1100; i++)
        {
            indicator.Update(i * 1.0, i + 10.0);
        }

        // Constant error of 10, so MAE should be 10
        Assert.Equal(10.0, indicator.Last.Value, 8);
    }

    // ═══════════════════════════════ Pub Event ════════════════════════════════

    [Fact]
    public void PubEvent_FiredOnUpdate()
    {
        var indicator = new Mae(3);
        int eventCount = 0;
        TValuePublishedHandler handler = (object? sender, in TValueEventArgs args) => eventCount++;
        indicator.Pub += handler;

        indicator.Update(100.0, 110.0);
        Assert.Equal(1, eventCount);

        indicator.Update(200.0, 220.0);
        Assert.Equal(2, eventCount);

        indicator.Pub -= handler;
    }

    [Fact]
    public void PubEvent_FiredOnBarCorrection()
    {
        var indicator = new Mae(3);
        int eventCount = 0;
        TValuePublishedHandler handler = (object? sender, in TValueEventArgs args) => eventCount++;
        indicator.Pub += handler;

        indicator.Update(100.0, 110.0);
        indicator.Update(100.0, 120.0, isNew: false); // correction

        Assert.Equal(2, eventCount);
        indicator.Pub -= handler;
    }

    // ═══════════════════════════════ PostProcess (via Rmse) ═══════════════════

    [Fact]
    public void PostProcess_Rmse_AppliesSqrt()
    {
        var rmse = new Rmse(3);

        // |10-15|²=25, RMSE=sqrt(25/1)=5
        var result = rmse.Update(10.0, 15.0);
        Assert.Equal(5.0, result.Value, 10);
    }

    [Fact]
    public void PostProcess_Mae_ReturnsUnchanged()
    {
        var mae = new Mae(3);

        // |10-15|=5, MAE=5/1=5
        var result = mae.Update(10.0, 15.0);
        Assert.Equal(5.0, result.Value, 10);
    }

    // ═══════════════════════════════ ValidateBatchInputs ═════════════════════

    [Fact]
    public void BatchValidation_MismatchedLengths_Throws()
    {
        double[] actual = [1, 2, 3];
        double[] predicted = [1, 2, 3, 4, 5];
        double[] output = new double[3];

        Assert.Throws<ArgumentException>(() =>
            Mae.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 3));
    }

    [Fact]
    public void BatchValidation_MismatchedOutput_Throws()
    {
        double[] actual = [1, 2, 3, 4, 5];
        double[] predicted = [1, 2, 3, 4, 5];
        double[] output = new double[3];

        Assert.Throws<ArgumentException>(() =>
            Mae.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 3));
    }

    [Fact]
    public void BatchValidation_ZeroPeriod_Throws()
    {
        double[] actual = [1, 2, 3];
        double[] predicted = [1, 2, 3];
        double[] output = new double[3];

        Assert.Throws<ArgumentException>(() =>
            Mae.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
    }

    [Fact]
    public void BatchValidation_NegativePeriod_Throws()
    {
        double[] actual = [1, 2, 3];
        double[] predicted = [1, 2, 3];
        double[] output = new double[3];

        Assert.Throws<ArgumentException>(() =>
            Mae.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), -5));
    }

    [Fact]
    public void BatchValidation_EmptyInput_NoException()
    {
        double[] actual = [];
        double[] predicted = [];
        double[] output = [];

        Mae.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 3);
        Assert.True(true); // Verify no exception thrown
    }

    // ═══════════════════════════════ CalculateImpl (via Batch TSeries) ════════

    [Fact]
    public void CalculateImpl_MismatchedSeries_Throws()
    {
        var actual = new TSeries();
        var predicted = new TSeries();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            actual.Add(now.AddMinutes(i), i * 10.0);
        }
        for (int i = 0; i < 5; i++)
        {
            predicted.Add(now.AddMinutes(i), i * 10.0);
        }

        Assert.Throws<ArgumentException>(() => Mae.Batch(actual, predicted, 3));
    }

    [Fact]
    public void CalculateImpl_ValidSeries_ReturnsCorrectCount()
    {
        var actual = new TSeries();
        var predicted = new TSeries();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            actual.Add(now.AddMinutes(i), i * 10.0);
            predicted.Add(now.AddMinutes(i), (i * 10.0) + 5.0);
        }

        var result = Mae.Batch(actual, predicted, 5);
        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void CalculateImpl_ConstantError_AllWindowedValuesEqual()
    {
        var actual = new TSeries();
        var predicted = new TSeries();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            actual.Add(now.AddMinutes(i), 100.0);
            predicted.Add(now.AddMinutes(i), 107.0);
        }

        var result = Mae.Batch(actual, predicted, 5);
        // Once window is full (index >= 4), all values should be 7.0
        for (int i = 4; i < 20; i++)
        {
            Assert.Equal(7.0, result[i].Value, 10);
        }
    }

    // ═══════════════════════════════ Calculate static ═════════════════════════

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var actual = new TSeries();
        var predicted = new TSeries();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            actual.Add(now.AddMinutes(i), i * 10.0);
            predicted.Add(now.AddMinutes(i), (i * 10.0) + 3.0);
        }

        var (results, indicator) = Mae.Calculate(actual, predicted, 5);
        Assert.Equal(10, results.Count);
        Assert.NotNull(indicator);
        Assert.Equal(5, indicator.Period);
    }

    // ═══════════════════════════════ Dispose ═════════════════════════════════

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var indicator = new Mae(5);
        indicator.Update(100.0, 110.0);
        indicator.Dispose();
        Assert.True(true); // Verify no exception thrown
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_NoException()
    {
        var indicator = new Mae(5);
        indicator.Dispose();
        indicator.Dispose();
        Assert.True(true); // Verify no exception thrown
    }

    // ═══════════════════════════════ Batch vs Streaming ═════════════════════

    [Fact]
    public void Batch_MatchesStreaming_RandomData()
    {
        const int period = 7;
        const int count = 200;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        double[] actual = new double[count];
        double[] predicted = new double[count];
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            actual[i] = bar.Close;
            predicted[i] = (bar.Close * 1.03) + 1.0;
        }

        // Streaming
        var mae = new Mae(period);
        double[] streamResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamResults[i] = mae.Update(actual[i], predicted[i]).Value;
        }

        // Batch
        double[] batchResults = new double[count];
        Mae.Batch(actual, predicted, batchResults, period);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i], 9);
        }
    }

    // ═══════════════════════════════ Edge Cases ═══════════════════════════════

    [Fact]
    public void Update_LargeValues_NoOverflow()
    {
        var indicator = new Mae(3);
        indicator.Update(1e300, 1e300 + 1e290);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Update_VerySmallValues_NoUnderflow()
    {
        var indicator = new Mae(3);
        indicator.Update(1e-300, 2e-300);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Update_ZeroValues_ReturnsZero()
    {
        var indicator = new Mae(3);
        indicator.Update(0.0, 0.0);
        Assert.Equal(0.0, indicator.Last.Value, 10);
    }

    [Fact]
    public void Update_NegativeValues_HandledCorrectly()
    {
        var indicator = new Mae(3);
        var result = indicator.Update(-100.0, -110.0);
        Assert.Equal(10.0, result.Value, 10);
    }

    [Fact]
    public void Update_MixedSignValues_AbsoluteError()
    {
        var indicator = new Mae(1);
        var result = indicator.Update(-50.0, 50.0);
        Assert.Equal(100.0, result.Value, 10);
    }
}
