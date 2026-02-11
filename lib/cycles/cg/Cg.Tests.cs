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
}