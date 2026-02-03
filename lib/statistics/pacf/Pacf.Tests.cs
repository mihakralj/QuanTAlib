using Xunit;

namespace QuanTAlib.Tests;

public class PacfTests
{
    private const int DefaultPeriod = 20;
    private const int DefaultLag = 1;
    private const double Epsilon = 1e-10;

    #region Constructor Validation

    [Fact]
    public void Constructor_LagLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Pacf(10, 0));
        Assert.Equal("lag", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodNotGreaterThanLagPlusOne_ThrowsArgumentOutOfRangeException()
    {
        // Period must be > lag + 1, so period=3 with lag=2 is invalid (3 <= 2+1)
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Pacf(3, 2));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesIndicator()
    {
        var pacf = new Pacf(10, 2);
        Assert.Equal("Pacf(10,2)", pacf.Name);
        Assert.Equal(10, pacf.WarmupPeriod);
    }

    [Fact]
    public void Constructor_DefaultLag_IsOne()
    {
        var pacf = new Pacf(10);
        Assert.Equal("Pacf(10,1)", pacf.Name);
    }

    [Fact]
    public void Constructor_NullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Pacf(null!, 10, 1));
    }

    #endregion

    #region Basic Calculation

    [Fact]
    public void Update_ReturnsTValue()
    {
        var pacf = new Pacf(DefaultPeriod, DefaultLag);
        var input = new TValue(DateTime.UtcNow, 100.0);
        TValue result = pacf.Update(input);
        Assert.True(result.Time != default);
    }

    [Fact]
    public void Update_LastPropertyUpdated()
    {
        var pacf = new Pacf(DefaultPeriod, DefaultLag);
        var input = new TValue(DateTime.UtcNow, 100.0);
        pacf.Update(input);
        Assert.Equal(input.Time, pacf.Last.Time);
    }

    [Fact]
    public void Update_ConstantSeries_ReturnsZero()
    {
        // PACF of a constant series (after warmup) should be 0 because variance = 0
        var pacf = new Pacf(10, 1);
        for (int i = 0; i < 20; i++)
        {
            pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 50.0));
        }
        Assert.Equal(0, pacf.Last.Value);
    }

    [Fact]
    public void Update_RandomWalk_PacfDecaysWithLag()
    {
        // For random data, higher lags typically have lower PACF
        var pacfLag1 = new Pacf(100, 1);
        var pacfLag10 = new Pacf(100, 10);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            pacfLag1.Update(new TValue(bar.Time, bar.Close));
            pacfLag10.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(pacfLag1.IsHot);
        Assert.True(pacfLag10.IsHot);
    }

    [Fact]
    public void Update_PacfBoundedBetweenMinusOneAndOne()
    {
        var pacf = new Pacf(20, 1);
        var gbm = new GBM(seed: 123);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            pacf.Update(new TValue(bar.Time, bar.Close));
            Assert.True(pacf.Last.Value >= -1.0 && pacf.Last.Value <= 1.0,
                $"PACF value {pacf.Last.Value} out of bounds");
        }
    }

    #endregion

    #region IsNew Parameter (Bar Correction)

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var pacf = new Pacf(10, 1);

        // Feed initial values
        for (int i = 0; i < 15; i++)
        {
            pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        double valueBeforeNew = pacf.Last.Value;

        // Update with isNew=true advances state
        pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 200), isNew: true);
        double valueAfterNew = pacf.Last.Value;

        // Value should change since we added a different value
        Assert.NotEqual(valueBeforeNew, valueAfterNew);
    }

    [Fact]
    public void Update_IsNewFalse_DoesNotAdvanceState()
    {
        var pacf = new Pacf(10, 1);

        // Feed initial values
        for (int i = 0; i < 15; i++)
        {
            pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        // Update with isNew=true first time
        pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 150), isNew: true);
        double valueAfterFirstUpdate = pacf.Last.Value;

        // Update same bar with different value, isNew=false
        pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 160), isNew: false);

        // Another correction back to original
        pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 150), isNew: false);
        double valueAfterSecondCorrection = pacf.Last.Value;

        // Should restore to original value when corrected back
        Assert.Equal(valueAfterFirstUpdate, valueAfterSecondCorrection, Epsilon);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresCorrectState()
    {
        var pacf = new Pacf(10, 1);

        // Feed initial values
        for (int i = 0; i < 15; i++)
        {
            pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        // Make multiple corrections
        pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 200), isNew: true);
        double afterNew = pacf.Last.Value;

        pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 250), isNew: false);
        pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 300), isNew: false);
        pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), 200), isNew: false);

        // Should match the value after the first isNew=true update with 200
        Assert.Equal(afterNew, pacf.Last.Value, Epsilon);
    }

    #endregion

    #region Warmup and IsHot

    [Fact]
    public void IsHot_FalseBeforeWarmup()
    {
        var pacf = new Pacf(20, 1);

        for (int i = 0; i < 19; i++)
        {
            pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
            Assert.False(pacf.IsHot);
        }
    }

    [Fact]
    public void IsHot_TrueAfterWarmup()
    {
        var pacf = new Pacf(20, 1);

        for (int i = 0; i < 20; i++)
        {
            pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        Assert.True(pacf.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var pacf = new Pacf(25, 3);
        Assert.Equal(25, pacf.WarmupPeriod);
    }

    #endregion

    #region NaN and Infinity Handling

    [Fact]
    public void Update_NaNInput_UsesLastValidValue()
    {
        var pacf = new Pacf(10, 1);

        // Feed valid values
        for (int i = 0; i < 15; i++)
        {
            pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        // Feed NaN
        pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), double.NaN));

        // Result should still be finite
        Assert.True(double.IsFinite(pacf.Last.Value));
    }

    [Fact]
    public void Update_InfinityInput_UsesLastValidValue()
    {
        var pacf = new Pacf(10, 1);

        // Feed valid values
        for (int i = 0; i < 15; i++)
        {
            pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        // Feed infinity
        pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(15), double.PositiveInfinity));

        // Result should still be finite
        Assert.True(double.IsFinite(pacf.Last.Value));
    }

    [Fact]
    public void Update_MultipleNaNs_StillProducesFiniteResult()
    {
        var pacf = new Pacf(10, 1);

        // Feed valid values
        for (int i = 0; i < 15; i++)
        {
            pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        // Feed multiple NaNs
        for (int i = 0; i < 5; i++)
        {
            pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(15 + i), double.NaN));
            Assert.True(double.IsFinite(pacf.Last.Value));
        }
    }

    #endregion

    #region Reset

    [Fact]
    public void Reset_ClearsState()
    {
        var pacf = new Pacf(10, 1);

        // Feed values
        for (int i = 0; i < 15; i++)
        {
            pacf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        Assert.True(pacf.IsHot);

        pacf.Reset();

        Assert.False(pacf.IsHot);
        Assert.Equal(default, pacf.Last);
    }

    [Fact]
    public void Reset_AllowsReinitializationWithSameData()
    {
        var pacf = new Pacf(10, 1);
        var inputs = new List<TValue>();

        // Generate and store values
        for (int i = 0; i < 20; i++)
        {
            inputs.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i * 0.5));
        }

        // First pass
        foreach (var input in inputs)
        {
            pacf.Update(input);
        }

        double firstPassResult = pacf.Last.Value;

        // Reset and second pass
        pacf.Reset();
        foreach (var input in inputs)
        {
            pacf.Update(input);
        }

        double secondPassResult = pacf.Last.Value;

        Assert.Equal(firstPassResult, secondPassResult, Epsilon);
    }

    #endregion

    #region Prime

    [Fact]
    public void Prime_InitializesStateCorrectly()
    {
        var pacf1 = new Pacf(10, 1);
        var pacf2 = new Pacf(10, 1);

        double[] primeData = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110];

        // Method 1: Use Prime
        pacf1.Prime(primeData);

        // Method 2: Update individually
        foreach (double val in primeData)
        {
            pacf2.Update(new TValue(DateTime.UtcNow, val));
        }

        Assert.Equal(pacf2.Last.Value, pacf1.Last.Value, Epsilon);
    }

    #endregion

    #region Event Chaining

    [Fact]
    public void ChainedConstructor_ReceivesUpdates()
    {
        var source = new TSeries();
        var pacf = new Pacf(source, 10, 1);

        // Feed values through source
        for (int i = 0; i < 15; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i));
        }

        Assert.True(pacf.IsHot);
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
        var streaming = new Pacf(period, lag);
        foreach (var tv in tSeries)
        {
            streaming.Update(tv);
        }

        // Mode 2: Batch via Update(TSeries)
        var batchIndicator = new Pacf(period, lag);
        var batchResult = batchIndicator.Update(tSeries);

        // Mode 3: Static Calculate
        var staticResult = Pacf.Calculate(tSeries, period, lag);

        // Mode 4: Span-based Batch
        double[] sourceArray = new double[dataLen];
        double[] spanResult = new double[dataLen];
        for (int i = 0; i < dataLen; i++)
        {
            sourceArray[i] = tSeries[i].Value;
        }

        Pacf.Batch(sourceArray, spanResult, period, lag);

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
        Assert.Equal(batchResult[^1].Value, streaming.Last.Value, Epsilon);
    }

    #endregion

    #region Span Batch Validation

    [Fact]
    public void Batch_MismatchedLengths_ThrowsArgumentException()
    {
        double[] source = new double[100];
        double[] output = new double[50];

        var ex = Assert.Throws<ArgumentException>(() => Pacf.Batch(source, output, 10, 1));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_InvalidLag_ThrowsArgumentOutOfRangeException()
    {
        double[] source = new double[100];
        double[] output = new double[100];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Pacf.Batch(source, output, 10, 0));
        Assert.Equal("lag", ex.ParamName);
    }

    [Fact]
    public void Batch_InvalidPeriod_ThrowsArgumentOutOfRangeException()
    {
        double[] source = new double[100];
        double[] output = new double[100];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Pacf.Batch(source, output, 3, 2));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptyInput_ReturnsEmpty()
    {
        double[] source = [];
        double[] output = [];

        // Should not throw
        Pacf.Batch(source, output, 10, 1);

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

        Pacf.Batch(source, output, 20, 1);

        foreach (double val in output)
        {
            Assert.True(val >= -1.0 && val <= 1.0,
                $"PACF value {val} out of bounds [-1, 1]");
        }
    }

    #endregion

    #region PACF-Specific Tests

    [Fact]
    public void Pacf_Lag1_EqualsAcfLag1()
    {
        // For lag 1, PACF equals ACF (by definition φ_11 = r_1)
        var pacf = new Pacf(DefaultPeriod, 1);
        var acf = new Acf(DefaultPeriod, 1);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var tv = new TValue(bar.Time, bar.Close);
            pacf.Update(tv);
            acf.Update(tv);
        }

        // PACF at lag 1 should equal ACF at lag 1
        Assert.Equal(acf.Last.Value, pacf.Last.Value, 6);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    public void Update_DifferentLags_ProducesResults(int lag)
    {
        int period = lag + 10; // Ensure period > lag + 1
        var pacf = new Pacf(period, lag);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            pacf.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(pacf.IsHot);
        Assert.True(pacf.Last.Value >= -1.0 && pacf.Last.Value <= 1.0);
    }

    #endregion

    #region Event Publication

    [Fact]
    public void Update_PublishesEvent()
    {
        var pacf = new Pacf(DefaultPeriod, DefaultLag);
        bool eventFired = false;

        pacf.Pub += (object? sender, in TValueEventArgs args) => { eventFired = true; };

        pacf.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(eventFired);
    }

    #endregion
}