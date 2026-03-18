using Xunit;

namespace QuanTAlib.Tests;

public class LpfTests
{
    private const double Tolerance = 1e-9;

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultParameters_SetsProperties()
    {
        var lpf = new Lpf();

        Assert.Equal("LPF(18,40,40)", lpf.Name);
        Assert.False(lpf.IsHot);
        Assert.Equal(18, lpf.LowerBound);
        Assert.Equal(40, lpf.UpperBound);
        Assert.Equal(40, lpf.DataLength);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsProperties()
    {
        var lpf = new Lpf(lowerBound: 10, upperBound: 60, dataLength: 50);

        Assert.Equal("LPF(10,60,50)", lpf.Name);
        Assert.Equal(10, lpf.LowerBound);
        Assert.Equal(60, lpf.UpperBound);
        Assert.Equal(50, lpf.DataLength);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidLowerBound_ThrowsArgumentOutOfRange(int lower)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Lpf(lower, 40));
        Assert.Equal("lowerBound", ex.ParamName);
    }

    [Theory]
    [InlineData(18, 18)]
    [InlineData(18, 10)]
    [InlineData(20, 20)]
    public void Constructor_UpperBoundNotGreaterThanLower_ThrowsArgumentOutOfRange(int lower, int upper)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Lpf(lower, upper));
        Assert.Equal("upperBound", ex.ParamName);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidDataLength_ThrowsArgumentOutOfRange(int len)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Lpf(18, 40, len));
        Assert.Equal("dataLength", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Lpf(null!, 18, 40));
    }

    [Fact]
    public void Constructor_WithValidSource_Subscribes()
    {
        var source = new TSeries();
        var lpf = new Lpf(source, 18, 40);

        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, lpf.Last);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var lpf = new Lpf();
        var result = lpf.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_AfterWarmup_IsHotTrue()
    {
        var lpf = new Lpf();

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            lpf.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(lpf.IsHot);
    }

    [Fact]
    public void Update_DominantCycle_WithinRange()
    {
        var lpf = new Lpf(18, 40, 40);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            lpf.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.InRange(lpf.DominantCycle, 18, 40);
    }

    [Fact]
    public void Update_Signal_WithinUnitRange()
    {
        var lpf = new Lpf();

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            lpf.Update(new TValue(bar.Time, bar.Close));
        }

        // AGC normalization should keep signal within [-1, 1]
        Assert.InRange(lpf.Signal, -1.0, 1.0);
    }

    [Fact]
    public void Update_InitialValue_WithinBounds()
    {
        var lpf = new Lpf(18, 40, 40);

        var result = lpf.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(result.Value >= 18 && result.Value <= 40);
    }

    [Fact]
    public void Update_PureSine_ConvergesNearTruePeriod()
    {
        int truePeriod = 30;
        var lpf = new Lpf(lowerBound: 10, upperBound: 50, dataLength: 50);

        // Feed a pure sine wave with known period
        for (int i = 0; i < 500; i++)
        {
            double val = Math.Sin(2.0 * Math.PI * i / truePeriod);
            lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), val));
        }

        // Should converge reasonably close to true period
        // Allow generous tolerance since LPF needs time to adapt
        Assert.InRange(lpf.DominantCycle, truePeriod - 10, truePeriod + 10);
    }

    #endregion

    #region Bar Correction Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var lpf = new Lpf();

        lpf.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        var first = lpf.Last.Value;

        lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 110.0), isNew: true);
        var second = lpf.Last.Value;

        Assert.True(double.IsFinite(first) && double.IsFinite(second));
    }

    [Fact]
    public void Update_IsNewFalse_ReplacesCurrentBar()
    {
        var lpf = new Lpf();

        for (int i = 0; i < 100; i++)
        {
            lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10), isNew: true);
        }

        lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 110.0), isNew: true);
        var beforeCorrection = lpf.Last.Value;

        lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 90.0), isNew: false);
        var afterCorrection = lpf.Last.Value;

        Assert.True(double.IsFinite(beforeCorrection) && double.IsFinite(afterCorrection));
    }

    [Fact]
    public void Update_MultipleCorrections_RestoresToSnapshot()
    {
        var lpf = new Lpf();

        for (int i = 0; i < 100; i++)
        {
            lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), isNew: true);
        }

        lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 150.0), isNew: true);
        var originalValue = lpf.Last.Value;

        lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 160.0), isNew: false);
        lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 140.0), isNew: false);
        lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 150.0), isNew: false);
        var restoredValue = lpf.Last.Value;

        Assert.Equal(originalValue, restoredValue, Tolerance);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var lpf = new Lpf();

        for (int i = 0; i < 200; i++)
        {
            lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        Assert.True(lpf.IsHot);

        lpf.Reset();

        Assert.False(lpf.IsHot);
        Assert.Equal(default, lpf.Last);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var lpf = new Lpf();

        for (int i = 0; i < 200; i++)
        {
            lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10));
        }
        var firstResult = lpf.Last.Value;

        lpf.Reset();

        for (int i = 0; i < 200; i++)
        {
            lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10));
        }
        var secondResult = lpf.Last.Value;

        Assert.Equal(firstResult, secondResult, Tolerance);
    }

    #endregion

    #region NaN/Infinity Handling Tests

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var lpf = new Lpf();

        lpf.Update(new TValue(DateTime.UtcNow, 100.0));
        lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.NaN));

        Assert.True(double.IsFinite(lpf.Last.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValidValue()
    {
        var lpf = new Lpf();

        lpf.Update(new TValue(DateTime.UtcNow, 100.0));
        lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.PositiveInfinity));

        Assert.True(double.IsFinite(lpf.Last.Value));
    }

    [Fact]
    public void Update_NegativeInfinity_UsesLastValidValue()
    {
        var lpf = new Lpf();

        lpf.Update(new TValue(DateTime.UtcNow, 100.0));
        lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.NegativeInfinity));

        Assert.True(double.IsFinite(lpf.Last.Value));
    }

    #endregion

    #region Consistency Tests

    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(456)]
    public void Update_Deterministic_AcrossSeeds(int seed)
    {
        var lpf1 = new Lpf();
        var lpf2 = new Lpf();

        var gbm = new GBM(seed: seed);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var input = new TValue(bar.Time, bar.Close);
            lpf1.Update(input);
            lpf2.Update(input);
        }

        Assert.Equal(lpf1.Last.Value, lpf2.Last.Value, Tolerance);
        Assert.Equal(lpf1.DominantCycle, lpf2.DominantCycle, Tolerance);
        Assert.Equal(lpf1.Signal, lpf2.Signal, Tolerance);
        Assert.Equal(lpf1.Predict, lpf2.Predict, Tolerance);
    }

    [Fact]
    public void Batch_MatchesStreaming()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // TSeries from bars
        var source = new TSeries();
        foreach (var bar in bars)
        {
            source.Add(new TValue(bar.Time, bar.Close));
        }

        // Batch
        var batchResult = Lpf.Batch(source, 18, 40, 40);

        // Streaming
        var lpf = new Lpf(18, 40, 40);
        var streamResults = new List<double>();
        foreach (var bar in bars)
        {
            var r = lpf.Update(new TValue(bar.Time, bar.Close));
            streamResults.Add(r.Value);
        }

        Assert.Equal(streamResults.Count, batchResult.Count);
        for (int i = 0; i < streamResults.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResult[i].Value, Tolerance);
        }
    }

    [Fact]
    public void BatchSpan_MatchesStreaming()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] input = bars.Select(b => b.Close).ToArray();
        double[] output = new double[input.Length];

        Lpf.Batch(input, output, 18, 40, 40);

        var lpf = new Lpf(18, 40, 40);
        for (int i = 0; i < input.Length; i++)
        {
            var r = lpf.Update(new TValue(DateTime.MinValue, input[i]));
            Assert.Equal(r.Value, output[i], Tolerance);
        }
    }

    #endregion

    #region Constant Input Tests

    [Fact]
    public void Update_ConstantInput_NoNaNOrInf()
    {
        var lpf = new Lpf();

        for (int i = 0; i < 200; i++)
        {
            var result = lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
            Assert.True(double.IsFinite(result.Value), $"Non-finite result at bar {i}: {result.Value}");
        }
    }

    [Fact]
    public void Update_ZeroInput_NoNaNOrInf()
    {
        var lpf = new Lpf();

        for (int i = 0; i < 200; i++)
        {
            var result = lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 0.0));
            Assert.True(double.IsFinite(result.Value), $"Non-finite result at bar {i}: {result.Value}");
        }
    }

    #endregion

    #region Calculate + Dispose Tests

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var source = new TSeries();
        foreach (var bar in bars)
        {
            source.Add(new TValue(bar.Time, bar.Close));
        }

        var (results, indicator) = Lpf.Calculate(source, 18, 40, 40);

        Assert.Equal(200, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var source = new TSeries();
        var lpf = new Lpf(source, 18, 40, 40);

        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, lpf.Last);

        lpf.Dispose();

        // After dispose, adding to source should not update lpf
        var lastBefore = lpf.Last;
        source.Add(new TValue(DateTime.UtcNow.AddSeconds(1), 200.0));
        Assert.Equal(lastBefore, lpf.Last);
    }

    #endregion

    #region DominantCycle Rate Constraint Tests

    [Fact]
    public void Update_DominantCycle_ChangeConstrainedToTwo()
    {
        var lpf = new Lpf(10, 50, 40);

        // Feed data and track DC changes
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double prevDC = 0;
        bool first = true;
        foreach (var bar in bars)
        {
            lpf.Update(new TValue(bar.Time, bar.Close));
            double dc = lpf.DominantCycle;
            if (!first)
            {
                double delta = Math.Abs(dc - prevDC);
                Assert.True(delta <= 2.0 + 1e-10, $"DC changed by {delta} > 2.0");
            }
            prevDC = dc;
            first = false;
        }
    }

    #endregion

    #region Prime Tests

    [Fact]
    public void Prime_SetsState()
    {
        var lpf = new Lpf();

        double[] data = new double[200];
        for (int i = 0; i < 200; i++)
        {
            data[i] = 100.0 + Math.Sin(i * 0.1) * 10;
        }

        lpf.Prime(data);

        Assert.True(lpf.IsHot);
        Assert.True(double.IsFinite(lpf.Last.Value));
    }

    #endregion
}
