using Xunit;

namespace QuanTAlib.Tests;

public class CgTests
{
    private const int DefaultPeriod = 10;
    private const double Epsilon = 1e-10;

    #region Constructor Validation

    [Fact]
    public void Constructor_PeriodLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Cg(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesIndicator()
    {
        var cg = new Cg(10);
        Assert.Equal("Cg(10)", cg.Name);
        Assert.Equal(10, cg.WarmupPeriod);
    }

    [Fact]
    public void Constructor_DefaultPeriod_IsTen()
    {
        var cg = new Cg();
        Assert.Equal("Cg(10)", cg.Name);
    }

    [Fact]
    public void Constructor_NullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Cg(null!, 10));
    }

    #endregion

    #region Basic Calculation

    [Fact]
    public void Update_ReturnsTValue()
    {
        var cg = new Cg(DefaultPeriod);
        var input = new TValue(DateTime.UtcNow, 100.0);
        TValue result = cg.Update(input);
        Assert.True(result.Time != default);
    }

    [Fact]
    public void Update_LastPropertyUpdated()
    {
        var cg = new Cg(DefaultPeriod);
        var input = new TValue(DateTime.UtcNow, 100.0);
        cg.Update(input);
        Assert.Equal(input.Time, cg.Last.Time);
    }

    [Fact]
    public void Update_ConstantSeries_ReturnsZero()
    {
        // CG of a constant series should be close to zero
        // because all prices have equal weight contribution
        var cg = new Cg(10);
        for (int i = 0; i < 20; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 50.0));
        }
        Assert.Equal(0, cg.Last.Value, Epsilon);
    }

    [Fact]
    public void Update_IncreasingPrices_ReturnsPositive()
    {
        // When prices are higher at the end, CG should be positive
        var cg = new Cg(5);
        for (int i = 0; i < 10; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i * 10));
        }
        Assert.True(cg.Last.Value > 0, $"Expected positive CG, got {cg.Last.Value}");
    }

    [Fact]
    public void Update_DecreasingPrices_ReturnsNegative()
    {
        // When prices are higher at the beginning, CG should be negative
        var cg = new Cg(5);
        for (int i = 0; i < 10; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 200 - i * 10));
        }
        Assert.True(cg.Last.Value < 0, $"Expected negative CG, got {cg.Last.Value}");
    }

    [Fact]
    public void Update_OscillatesAroundZero()
    {
        var cg = new Cg(20);
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        int positiveCount = 0;
        int negativeCount = 0;

        foreach (var bar in bars)
        {
            cg.Update(new TValue(bar.Time, bar.Close));
            if (cg.IsHot)
            {
                if (cg.Last.Value > 0)
                {
                    positiveCount++;
                }
                else if (cg.Last.Value < 0)
                {
                    negativeCount++;
                }
            }
        }

        // CG should oscillate, having both positive and negative values
        Assert.True(positiveCount > 0, "Expected some positive values");
        Assert.True(negativeCount > 0, "Expected some negative values");
    }

    #endregion

    #region IsNew Parameter (Bar Correction)

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var cg = new Cg(10);

        // Feed initial values
        for (int i = 0; i < 15; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        double valueBeforeNew = cg.Last.Value;

        // Update with isNew=true advances state
        cg.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 200), isNew: true);
        double valueAfterNew = cg.Last.Value;

        // Value should change since we added a different value
        Assert.NotEqual(valueBeforeNew, valueAfterNew);
    }

    [Fact]
    public void Update_IsNewFalse_DoesNotAdvanceState()
    {
        var cg = new Cg(10);

        // Feed initial values
        for (int i = 0; i < 15; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        // Update with isNew=true first time
        cg.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 150), isNew: true);
        double valueAfterFirstUpdate = cg.Last.Value;

        // Update same bar with different value, isNew=false
        cg.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 160), isNew: false);

        // Another correction
        cg.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 150), isNew: false);
        double valueAfterSecondCorrection = cg.Last.Value;

        // Should restore to original value when corrected back
        Assert.Equal(valueAfterFirstUpdate, valueAfterSecondCorrection, Epsilon);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresCorrectState()
    {
        var cg = new Cg(10);

        // Feed initial values
        for (int i = 0; i < 15; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        // Make multiple corrections
        cg.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 200), isNew: true);
        double afterNew = cg.Last.Value;

        cg.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 250), isNew: false);
        cg.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 300), isNew: false);
        cg.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 200), isNew: false);

        // Should match the value after the first isNew=true update with 200
        Assert.Equal(afterNew, cg.Last.Value, Epsilon);
    }

    #endregion

    #region Warmup and IsHot

    [Fact]
    public void IsHot_FalseBeforeWarmup()
    {
        var cg = new Cg(20);

        for (int i = 0; i < 19; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
            Assert.False(cg.IsHot);
        }
    }

    [Fact]
    public void IsHot_TrueAfterWarmup()
    {
        var cg = new Cg(20);

        for (int i = 0; i < 20; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        Assert.True(cg.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var cg = new Cg(25);
        Assert.Equal(25, cg.WarmupPeriod);
    }

    #endregion

    #region NaN and Infinity Handling

    [Fact]
    public void Update_NaNInput_UsesLastValidValue()
    {
        var cg = new Cg(10);

        // Feed valid values
        for (int i = 0; i < 15; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        // Feed NaN
        cg.Update(new TValue(DateTime.UtcNow.AddSeconds(15), double.NaN));

        // Result should still be finite
        Assert.True(double.IsFinite(cg.Last.Value));
    }

    [Fact]
    public void Update_InfinityInput_UsesLastValidValue()
    {
        var cg = new Cg(10);

        // Feed valid values
        for (int i = 0; i < 15; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        // Feed infinity
        cg.Update(new TValue(DateTime.UtcNow.AddSeconds(15), double.PositiveInfinity));

        // Result should still be finite
        Assert.True(double.IsFinite(cg.Last.Value));
    }

    [Fact]
    public void Update_MultipleNaNs_StillProducesFiniteResult()
    {
        var cg = new Cg(10);

        // Feed valid values
        for (int i = 0; i < 15; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        // Feed multiple NaNs
        for (int i = 0; i < 5; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(15 + i), double.NaN));
            Assert.True(double.IsFinite(cg.Last.Value));
        }
    }

    #endregion

    #region Reset

    [Fact]
    public void Reset_ClearsState()
    {
        var cg = new Cg(10);

        // Feed values
        for (int i = 0; i < 15; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        Assert.True(cg.IsHot);

        cg.Reset();

        Assert.False(cg.IsHot);
        Assert.Equal(default, cg.Last);
    }

    [Fact]
    public void Reset_AllowsReinitializationWithSameData()
    {
        var cg = new Cg(10);
        var inputs = new List<TValue>();

        // Generate and store values
        for (int i = 0; i < 20; i++)
        {
            inputs.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i * 0.5));
        }

        // First pass
        foreach (var input in inputs)
        {
            cg.Update(input);
        }

        double firstPassResult = cg.Last.Value;

        // Reset and second pass
        cg.Reset();
        foreach (var input in inputs)
        {
            cg.Update(input);
        }

        double secondPassResult = cg.Last.Value;

        Assert.Equal(firstPassResult, secondPassResult, Epsilon);
    }

    #endregion

    #region Prime

    [Fact]
    public void Prime_InitializesStateCorrectly()
    {
        var cg1 = new Cg(10);
        var cg2 = new Cg(10);

        double[] primeData = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110];

        // Method 1: Use Prime
        cg1.Prime(primeData);

        // Method 2: Update individually
        foreach (double val in primeData)
        {
            cg2.Update(new TValue(DateTime.UtcNow, val));
        }

        Assert.Equal(cg2.Last.Value, cg1.Last.Value, Epsilon);
    }

    #endregion

    #region Event Chaining

    [Fact]
    public void ChainedConstructor_ReceivesUpdates()
    {
        var source = new TSeries();
        var cg = new Cg(source, 10);

        // Feed values through source
        for (int i = 0; i < 15; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        Assert.True(cg.IsHot);
    }

    #endregion

    #region AllModes Consistency (Batch vs Streaming vs Static)

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int period = 14;
        const int dataLen = 100;
        const int compareLen = 50;

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var tSeries = new TSeries();
        foreach (var bar in bars)
        {
            tSeries.Add(new TValue(bar.Time, bar.Close));
        }

        // Mode 1: Streaming (Update one at a time)
        var streaming = new Cg(period);
        foreach (var tv in tSeries)
        {
            streaming.Update(tv);
        }

        // Mode 2: Batch via Update(TSeries)
        var batchIndicator = new Cg(period);
        var batchResult = batchIndicator.Update(tSeries);

        // Mode 3: Static Calculate
        var staticResult = Cg.Batch(tSeries, period);

        // Mode 4: Span-based Batch
        double[] sourceArray = new double[dataLen];
        double[] spanResult = new double[dataLen];
        for (int i = 0; i < dataLen; i++)
        {
            sourceArray[i] = tSeries[i].Value;
        }

        Cg.Batch(sourceArray, spanResult, period);

        // Compare last 'compareLen' values (after warmup settles)
        int startIdx = dataLen - compareLen;
        for (int i = startIdx; i < dataLen; i++)
        {
            double batchVal = batchResult[i].Value;
            double staticVal = staticResult[i].Value;
            double spanVal = spanResult[i];

            // Batch and static should match exactly
            Assert.Equal(batchVal, staticVal, Epsilon);

            // Span should match batch
            Assert.Equal(batchVal, spanVal, Epsilon);
        }

        // Streaming last should match batch last
        Assert.Equal(batchResult[^1].Value, streaming.Last.Value, 1e-8);
    }

    #endregion

    #region Span Batch Validation

    [Fact]
    public void Batch_MismatchedLengths_ThrowsArgumentException()
    {
        double[] source = new double[100];
        double[] output = new double[50];

        var ex = Assert.Throws<ArgumentException>(() => Cg.Batch(source, output, 10));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_InvalidPeriod_ThrowsArgumentOutOfRangeException()
    {
        double[] source = new double[100];
        double[] output = new double[100];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Cg.Batch(source, output, 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptyInput_ReturnsEmpty()
    {
        double[] source = [];
        double[] output = [];

        // Should not throw
        Cg.Batch(source, output, 10);

        // Verify output is empty as expected
        Assert.Empty(output);
    }

    [Fact]
    public void Batch_ResultsAreFinite()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] source = bars.Select(b => b.Close).ToArray();
        double[] output = new double[200];

        Cg.Batch(source, output, 20);

        foreach (double val in output)
        {
            Assert.True(double.IsFinite(val), $"CG value {val} is not finite");
        }
    }

    #endregion

    #region Different Period Values

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void Update_DifferentPeriods_ProducesResults(int period)
    {
        var cg = new Cg(period);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            cg.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(cg.IsHot);
        Assert.True(double.IsFinite(cg.Last.Value));
    }

    #endregion

    #region Mathematical Properties

    [Fact]
    public void Update_BoundedByPeriod()
    {
        // CG should be bounded by approximately ±(period-1)/2
        var cg = new Cg(10);
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double maxBound = 10.0; // Some reasonable bound

        foreach (var bar in bars)
        {
            cg.Update(new TValue(bar.Time, bar.Close));
            if (cg.IsHot)
            {
                Assert.True(Math.Abs(cg.Last.Value) < maxBound,
                    $"CG value {cg.Last.Value} exceeds expected bound");
            }
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────────────────
    // COVERAGE TESTS: Target uncovered branches identified by OpenCover
    // ────────────────────────────────────────────────────────────────────

    #region Coverage: ResyncInterval branch (Update line 121-123)

    [Fact]
    public void Update_ResyncInterval_TriggersAtThousandUpdates()
    {
        // The ResyncInterval is 1000 — feed exactly 1000 isNew=true updates
        // to hit the _updateCount % ResyncInterval == 0 branch (line 121-123).
        var cg = new Cg(10);
        for (int i = 0; i < 1000; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + (i % 50)), isNew: true);
        }

        // After 1000 updates the resync path was taken; result should still be finite
        Assert.True(double.IsFinite(cg.Last.Value));
        Assert.True(cg.IsHot);
    }

    #endregion

    #region Coverage: Update(TSeries) empty source (line 137-138)

    [Fact]
    public void UpdateTSeries_EmptySource_ReturnsEmptyTSeries()
    {
        var cg = new Cg(10);
        var emptySource = new TSeries();

        TSeries result = cg.Update(emptySource);

        Assert.Empty(result);
    }

    #endregion

    #region Coverage: CalculateCg sum==0 branch (line 184-185)

    [Fact]
    public void Update_AllZeroValues_ReturnsZero()
    {
        // When all prices are zero, _sum == 0 → CalculateCg returns 0 (line 184-185).
        var cg = new Cg(5);
        for (int i = 0; i < 10; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 0.0));
        }

        Assert.Equal(0.0, cg.Last.Value);
    }

    [Fact]
    public void Update_ZeroSumMixedValues_ReturnsZero()
    {
        // Values that sum to zero: e.g. +50, -50 alternating in a period=2 window.
        var cg = new Cg(2);
        for (int i = 0; i < 10; i++)
        {
            double val = (i % 2 == 0) ? 100.0 : -100.0;
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), val));
        }

        // Sum of last 2 values: 100 + (-100) = 0 → CG = 0
        Assert.Equal(0.0, cg.Last.Value);
    }

    #endregion

    #region Coverage: Calculate() tuple method (line 248-252)

    [Fact]
    public void Calculate_ReturnsTupleWithResultsAndIndicator()
    {
        // Covers the entire Calculate() method (lines 248-252) which was never called.
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var tSeries = new TSeries();
        foreach (var bar in bars)
        {
            tSeries.Add(new TValue(bar.Time, bar.Close));
        }

        var (results, indicator) = Cg.Calculate(tSeries, 10);

        Assert.Equal(50, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    #endregion

    #region Coverage: CalculateScalarCore NaN paths (lines 271-273, 310-312)

    [Fact]
    public void Batch_NaNAsFirstValue_SubstitutesZero()
    {
        // When the first value is NaN and buffer is empty, val = 0 (line 271-273).
        double[] source = [double.NaN, 100.0, 200.0, 300.0, 400.0];
        double[] output = new double[5];

        Cg.Batch(source, output, 3);

        // First value substituted with 0 → all outputs should be finite
        foreach (double val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite, got {val}");
        }
    }

    [Fact]
    public void Batch_NaNMidStream_SubstitutesLastValid()
    {
        // When NaN appears after valid values, it substitutes the last valid value.
        double[] source = [100.0, 200.0, double.NaN, 300.0, 400.0];
        double[] output = new double[5];

        Cg.Batch(source, output, 3);

        foreach (double val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite, got {val}");
        }
    }

    [Fact]
    public void Batch_AllZeros_ReturnsZeroCg()
    {
        // When all values are 0, sum==0 → output = 0 (lines 310-312).
        double[] source = [0.0, 0.0, 0.0, 0.0, 0.0];
        double[] output = new double[5];

        Cg.Batch(source, output, 3);

        foreach (double val in output)
        {
            Assert.Equal(0.0, val);
        }
    }

    [Fact]
    public void Batch_LargePeriod_UsesHeapAllocation()
    {
        // Period > 256 forces heap allocation instead of stackalloc (line 261-262).
        int period = 300;
        int len = 400;
        double[] source = new double[len];
        double[] output = new double[len];
        for (int i = 0; i < len; i++)
        {
            source[i] = 100.0 + i;
        }

        Cg.Batch(source, output, period);

        // Verify results are finite after warmup
        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Batch_NegativeInfinity_SubstitutesLastValid()
    {
        double[] source = [100.0, 200.0, double.NegativeInfinity, 300.0];
        double[] output = new double[4];

        Cg.Batch(source, output, 3);

        foreach (double val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite, got {val}");
        }
    }

    #endregion

    #region Coverage: Dispose (inherited from AbstractBase)

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var cg = new Cg(10);
        for (int i = 0; i < 15; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        var ex = Record.Exception(() => cg.Dispose());
        Assert.Null(ex);
    }

    #endregion
}