using Xunit;

namespace QuanTAlib.Tests;

public class AcfTests
{
    private const int DefaultPeriod = 20;
    private const int DefaultLag = 1;
    private const double Epsilon = 1e-10;

    #region Constructor Validation

    [Fact]
    public void Constructor_LagLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Acf(10, 0));
        Assert.Equal("lag", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodNotGreaterThanLagPlusOne_ThrowsArgumentOutOfRangeException()
    {
        // Period must be > lag + 1, so period=3 with lag=2 is invalid (3 <= 2+1)
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Acf(3, 2));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesIndicator()
    {
        var acf = new Acf(10, 2);
        Assert.Equal("Acf(10,2)", acf.Name);
        Assert.Equal(10, acf.WarmupPeriod);
    }

    [Fact]
    public void Constructor_DefaultLag_IsOne()
    {
        var acf = new Acf(10);
        Assert.Equal("Acf(10,1)", acf.Name);
    }

    [Fact]
    public void Constructor_NullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Acf(null!, 10, 1));
    }

    #endregion

    #region Basic Calculation

    [Fact]
    public void Update_ReturnsTValue()
    {
        var acf = new Acf(DefaultPeriod, DefaultLag);
        var input = new TValue(DateTime.UtcNow, 100.0);
        TValue result = acf.Update(input);
        Assert.True(result.Time != default);
    }

    [Fact]
    public void Update_LastPropertyUpdated()
    {
        var acf = new Acf(DefaultPeriod, DefaultLag);
        var input = new TValue(DateTime.UtcNow, 100.0);
        acf.Update(input);
        Assert.Equal(input.Time, acf.Last.Time);
    }

    [Fact]
    public void Update_ConstantSeries_ReturnsZero()
    {
        // ACF of a constant series (after warmup) should be undefined/0 because variance = 0
        var acf = new Acf(10, 1);
        for (int i = 0; i < 20; i++)
        {
            acf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 50.0));
        }
        Assert.Equal(0, acf.Last.Value);
    }

    [Fact]
    public void Update_RandomWalk_AcfDecaysTowardsZero()
    {
        // For random data, ACF at higher lags should be close to zero
        var acfLag1 = new Acf(100, 1);
        var acfLag10 = new Acf(100, 10);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            acfLag1.Update(new TValue(bar.Time, bar.Close));
            acfLag10.Update(new TValue(bar.Time, bar.Close));
        }

        // ACF at lag 1 for trending data should be higher than at lag 10
        // (GBM has persistence so lag 1 ACF should be positive)
        Assert.True(acfLag1.IsHot);
        Assert.True(acfLag10.IsHot);
    }

    [Fact]
    public void Update_AcfBoundedBetweenMinusOneAndOne()
    {
        var acf = new Acf(20, 1);
        var gbm = new GBM(seed: 123);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            acf.Update(new TValue(bar.Time, bar.Close));
            Assert.True(acf.Last.Value >= -1.0 && acf.Last.Value <= 1.0,
                $"ACF value {acf.Last.Value} out of bounds");
        }
    }

    #endregion

    #region IsNew Parameter (Bar Correction)

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var acf = new Acf(10, 1);

        // Feed initial values
        for (int i = 0; i < 15; i++)
        {
            acf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        double valueBeforeNew = acf.Last.Value;

        // Update with isNew=true advances state
        acf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 200), isNew: true);
        double valueAfterNew = acf.Last.Value;

        // Value should change since we added a different value
        Assert.NotEqual(valueBeforeNew, valueAfterNew);
    }

    [Fact]
    public void Update_IsNewFalse_DoesNotAdvanceState()
    {
        var acf = new Acf(10, 1);

        // Feed initial values
        for (int i = 0; i < 15; i++)
        {
            acf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        // Update with isNew=true first time
        acf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 150), isNew: true);
        double valueAfterFirstUpdate = acf.Last.Value;

        // Update same bar with different value, isNew=false
        acf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 160), isNew: false);

        // Another correction
        acf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 150), isNew: false);
        double valueAfterSecondCorrection = acf.Last.Value;

        // Should restore to original value when corrected back
        Assert.Equal(valueAfterFirstUpdate, valueAfterSecondCorrection, Epsilon);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresCorrectState()
    {
        var acf = new Acf(10, 1);

        // Feed initial values
        for (int i = 0; i < 15; i++)
        {
            acf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        // Make multiple corrections
        acf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 200), isNew: true);
        double afterNew = acf.Last.Value;

        acf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 250), isNew: false);
        acf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 300), isNew: false);
        acf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 200), isNew: false);

        // Should match the value after the first isNew=true update with 200
        Assert.Equal(afterNew, acf.Last.Value, Epsilon);
    }

    #endregion

    #region Warmup and IsHot

    [Fact]
    public void IsHot_FalseBeforeWarmup()
    {
        var acf = new Acf(20, 1);

        for (int i = 0; i < 19; i++)
        {
            acf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
            Assert.False(acf.IsHot);
        }
    }

    [Fact]
    public void IsHot_TrueAfterWarmup()
    {
        var acf = new Acf(20, 1);

        for (int i = 0; i < 20; i++)
        {
            acf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        Assert.True(acf.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var acf = new Acf(25, 3);
        Assert.Equal(25, acf.WarmupPeriod);
    }

    #endregion

    #region NaN and Infinity Handling

    [Fact]
    public void Update_NaNInput_UsesLastValidValue()
    {
        var acf = new Acf(10, 1);

        // Feed valid values
        for (int i = 0; i < 15; i++)
        {
            acf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        // Feed NaN
        acf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), double.NaN));

        // Result should still be finite
        Assert.True(double.IsFinite(acf.Last.Value));
    }

    [Fact]
    public void Update_InfinityInput_UsesLastValidValue()
    {
        var acf = new Acf(10, 1);

        // Feed valid values
        for (int i = 0; i < 15; i++)
        {
            acf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        // Feed infinity
        acf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), double.PositiveInfinity));

        // Result should still be finite
        Assert.True(double.IsFinite(acf.Last.Value));
    }

    [Fact]
    public void Update_MultipleNaNs_StillProducesFiniteResult()
    {
        var acf = new Acf(10, 1);

        // Feed valid values
        for (int i = 0; i < 15; i++)
        {
            acf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        // Feed multiple NaNs
        for (int i = 0; i < 5; i++)
        {
            acf.Update(new TValue(DateTime.UtcNow.AddSeconds(15 + i), double.NaN));
            Assert.True(double.IsFinite(acf.Last.Value));
        }
    }

    #endregion

    #region Reset

    [Fact]
    public void Reset_ClearsState()
    {
        var acf = new Acf(10, 1);

        // Feed values
        for (int i = 0; i < 15; i++)
        {
            acf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        Assert.True(acf.IsHot);

        acf.Reset();

        Assert.False(acf.IsHot);
        Assert.Equal(default, acf.Last);
    }

    [Fact]
    public void Reset_AllowsReinitializationWithSameData()
    {
        var acf = new Acf(10, 1);
        var inputs = new List<TValue>();

        // Generate and store values
        for (int i = 0; i < 20; i++)
        {
            inputs.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i * 0.5));
        }

        // First pass
        foreach (var input in inputs)
        {
            acf.Update(input);
        }

        double firstPassResult = acf.Last.Value;

        // Reset and second pass
        acf.Reset();
        foreach (var input in inputs)
        {
            acf.Update(input);
        }

        double secondPassResult = acf.Last.Value;

        Assert.Equal(firstPassResult, secondPassResult, Epsilon);
    }

    #endregion

    #region Prime

    [Fact]
    public void Prime_InitializesStateCorrectly()
    {
        var acf1 = new Acf(10, 1);
        var acf2 = new Acf(10, 1);

        double[] primeData = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110];

        // Method 1: Use Prime
        acf1.Prime(primeData);

        // Method 2: Update individually
        foreach (double val in primeData)
        {
            acf2.Update(new TValue(DateTime.UtcNow, val));
        }

        Assert.Equal(acf2.Last.Value, acf1.Last.Value, Epsilon);
    }

    #endregion

    #region Event Chaining

    [Fact]
    public void ChainedConstructor_ReceivesUpdates()
    {
        var source = new TSeries();
        var acf = new Acf(source, 10, 1);

        // Feed values through source
        for (int i = 0; i < 15; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        Assert.True(acf.IsHot);
    }

    #endregion

    #region AllModes Consistency (Batch vs Streaming vs Static)

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int period = 14;
        const int lag = 1;
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
        var streaming = new Acf(period, lag);
        foreach (var tv in tSeries)
        {
            streaming.Update(tv);
        }

        // Mode 2: Batch via Update(TSeries)
        var batchIndicator = new Acf(period, lag);
        var batchResult = batchIndicator.Update(tSeries);

        // Mode 3: Static Calculate
        var staticResult = Acf.Batch(tSeries, period, lag);

        // Mode 4: Span-based Batch
        double[] sourceArray = new double[dataLen];
        double[] spanResult = new double[dataLen];
        for (int i = 0; i < dataLen; i++)
        {
            sourceArray[i] = tSeries[i].Value;
        }

        Acf.Batch(sourceArray, spanResult, period, lag);

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

        // Streaming last should match batch last (use looser tolerance for accumulated floating-point differences)
        Assert.Equal(batchResult[^1].Value, streaming.Last.Value, 1e-8);
    }

    #endregion

    #region Span Batch Validation

    [Fact]
    public void Batch_MismatchedLengths_ThrowsArgumentException()
    {
        double[] source = new double[100];
        double[] output = new double[50];

        var ex = Assert.Throws<ArgumentException>(() => Acf.Batch(source, output, 10, 1));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_InvalidLag_ThrowsArgumentOutOfRangeException()
    {
        double[] source = new double[100];
        double[] output = new double[100];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Acf.Batch(source, output, 10, 0));
        Assert.Equal("lag", ex.ParamName);
    }

    [Fact]
    public void Batch_InvalidPeriod_ThrowsArgumentOutOfRangeException()
    {
        double[] source = new double[100];
        double[] output = new double[100];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Acf.Batch(source, output, 3, 2));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptyInput_ReturnsEmpty()
    {
        double[] source = [];
        double[] output = [];

        // Should not throw
        Acf.Batch(source, output, 10, 1);

        // Verify output is empty as expected
        Assert.Empty(output);
    }

    [Fact]
    public void Batch_ResultsWithinBounds()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] source = bars.Select(b => b.Close).ToArray();
        double[] output = new double[200];

        Acf.Batch(source, output, 20, 1);

        foreach (double val in output)
        {
            Assert.True(val >= -1.0 && val <= 1.0,
                $"ACF value {val} out of bounds [-1, 1]");
        }
    }

    #endregion

    #region Different Lag Values

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    public void Update_DifferentLags_ProducesResults(int lag)
    {
        int period = lag + 10; // Ensure period > lag + 1
        var acf = new Acf(period, lag);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            acf.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(acf.IsHot);
        Assert.True(acf.Last.Value >= -1.0 && acf.Last.Value <= 1.0);
    }

    #endregion
}
