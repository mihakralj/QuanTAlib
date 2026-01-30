using Xunit;

namespace QuanTAlib.Tests;

public class VoTests
{
    private const double Tolerance = 1e-10;
    private readonly GBM _gbm;
    private readonly TBarSeries _bars;

    public VoTests()
    {
        _gbm = new GBM(seed: 42);
        _bars = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultPeriods_SetsExpectedValues()
    {
        var vo = new Vo();
        Assert.Equal("Vo(5,10,10)", vo.Name);
        Assert.Equal(10, vo.WarmupPeriod);
    }

    [Fact]
    public void Constructor_CustomPeriods_SetsExpectedValues()
    {
        var vo = new Vo(shortPeriod: 3, longPeriod: 7, signalPeriod: 5);
        Assert.Equal("Vo(3,7,5)", vo.Name);
        Assert.Equal(7, vo.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ShortPeriodLessThan1_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vo(shortPeriod: 0));
        Assert.Equal("shortPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_LongPeriodLessThan1_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vo(shortPeriod: 2, longPeriod: 0));
        Assert.Equal("longPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShortPeriodGreaterOrEqualLongPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vo(shortPeriod: 10, longPeriod: 10));
        Assert.Equal("shortPeriod", ex.ParamName);

        ex = Assert.Throws<ArgumentException>(() => new Vo(shortPeriod: 15, longPeriod: 10));
        Assert.Equal("shortPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_SignalPeriodLessThan1_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vo(shortPeriod: 5, longPeriod: 10, signalPeriod: 0));
        Assert.Equal("signalPeriod", ex.ParamName);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_ReturnsTValue()
    {
        var vo = new Vo();
        var result = vo.Update(_bars[0]);
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_AccessesLastAndSignal()
    {
        var vo = new Vo();
        vo.Update(_bars[0]);
        Assert.Equal(vo.Last.Value, vo.Update(_bars[0], isNew: false).Value);
        _ = vo.Signal; // Access signal property
    }

    [Fact]
    public void Update_SameVolumes_ReturnsZero()
    {
        var vo = new Vo(shortPeriod: 2, longPeriod: 4, signalPeriod: 2);
        var now = DateTime.UtcNow;

        // All same volumes should result in VO = 0
        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 1000);
            vo.Update(bar, isNew: true);
        }

        Assert.Equal(0.0, vo.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_IncreasingVolumes_ReturnsPositive()
    {
        var vo = new Vo(shortPeriod: 2, longPeriod: 4, signalPeriod: 2);
        var now = DateTime.UtcNow;

        // Create a pattern where short MA > long MA at the end
        // Volumes: 100, 100, 100, 100, 500, 1000
        // At bar 5 (index 5): short SMA (2) = (500+1000)/2 = 750
        //                     long SMA (4) = (100+100+500+1000)/4 = 425
        // VO = ((750 - 425) / 425) * 100 = 76.47% (positive)
        double[] volumes = [100, 100, 100, 100, 500, 1000];
        for (int i = 0; i < volumes.Length; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, volumes[i]);
            vo.Update(bar, isNew: true);
        }

        Assert.True(vo.Last.Value > 0, $"Expected positive VO but got {vo.Last.Value}");
    }

    [Fact]
    public void Update_DecreasingVolumes_ReturnsNegative()
    {
        var vo = new Vo(shortPeriod: 2, longPeriod: 4, signalPeriod: 2);
        var now = DateTime.UtcNow;

        // Create a pattern where short MA < long MA at the end
        // Volumes: 1000, 1000, 1000, 1000, 500, 100
        // At bar 5 (index 5): short SMA (2) = (500+100)/2 = 300
        //                     long SMA (4) = (1000+1000+500+100)/4 = 650
        // VO = ((300 - 650) / 650) * 100 = -53.85% (negative)
        double[] volumes = [1000, 1000, 1000, 1000, 500, 100];
        for (int i = 0; i < volumes.Length; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, volumes[i]);
            vo.Update(bar, isNew: true);
        }

        Assert.True(vo.Last.Value < 0, $"Expected negative VO but got {vo.Last.Value}");
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var vo = new Vo(shortPeriod: 2, longPeriod: 4, signalPeriod: 2);
        var now = DateTime.UtcNow;

        // Feed enough bars to get past warmup with varying volumes
        // to ensure state advances (index changes)
        double[] volumes = [100, 200, 300, 400, 500];
        for (int i = 0; i < volumes.Length; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, volumes[i]);
            vo.Update(bar, isNew: true);
        }

        var stateBeforeNewBar = vo.Last.Value;

        // Add another bar with different volume
        var newBar = new TBar(now.AddMinutes(5), 100, 100, 100, 100, 1000);
        vo.Update(newBar, isNew: true);

        // State should have advanced (different value due to new volume in moving averages)
        Assert.NotEqual(stateBeforeNewBar, vo.Last.Value);
    }

    [Fact]
    public void IsNew_False_UpdatesCurrentBar()
    {
        var vo = new Vo(shortPeriod: 2, longPeriod: 4, signalPeriod: 2);
        var now = DateTime.UtcNow;

        var bar1 = new TBar(now, 100, 100, 100, 100, 500);
        vo.Update(bar1, isNew: true);

        var bar2 = new TBar(now, 100, 100, 100, 100, 600);
        vo.Update(bar2, isNew: false);

        var bar3 = new TBar(now, 100, 100, 100, 100, 500);
        var result = vo.Update(bar3, isNew: false);

        Assert.Equal(vo.Update(bar1, isNew: false).Value, result.Value, Tolerance);
    }

    [Fact]
    public void IterativeCorrections_RestoreState()
    {
        var vo = new Vo(shortPeriod: 3, longPeriod: 6, signalPeriod: 3);
        var now = DateTime.UtcNow;

        // Add several bars
        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 500 + i * 10);
            vo.Update(bar, isNew: true);
        }

        var stateBeforeCorrections = vo.Last.Value;

        // Apply multiple corrections
        for (int j = 0; j < 5; j++)
        {
            var correctionBar = new TBar(now.AddMinutes(9), 100, 100, 100, 100, 700 + j * 10);
            vo.Update(correctionBar, isNew: false);
        }

        // Restore original bar
        var originalBar = new TBar(now.AddMinutes(9), 100, 100, 100, 100, 590);
        var restored = vo.Update(originalBar, isNew: false);

        Assert.Equal(stateBeforeCorrections, restored.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var vo = new Vo();

        // Process some bars
        for (int i = 0; i < 20; i++)
        {
            vo.Update(_bars[i], isNew: true);
        }

        Assert.True(vo.IsHot);

        vo.Reset();

        Assert.False(vo.IsHot);
        Assert.Equal(default, vo.Last);
    }

    #endregion

    #region Warmup Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var vo = new Vo(shortPeriod: 3, longPeriod: 10, signalPeriod: 5);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 9; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 500);
            vo.Update(bar, isNew: true);
            Assert.False(vo.IsHot, $"Should not be hot at index {i}");
        }
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var vo = new Vo(shortPeriod: 3, longPeriod: 10, signalPeriod: 5);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 500);
            vo.Update(bar, isNew: true);
        }

        Assert.True(vo.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsLongPeriod()
    {
        var vo = new Vo(shortPeriod: 5, longPeriod: 15, signalPeriod: 10);
        Assert.Equal(15, vo.WarmupPeriod);
    }

    #endregion

    #region Robustness Tests

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var vo = new Vo(shortPeriod: 2, longPeriod: 4, signalPeriod: 2);
        var now = DateTime.UtcNow;

        // Add valid bars
        for (int i = 0; i < 5; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 500);
            vo.Update(bar, isNew: true);
        }

        // Add bar with NaN volume
        var nanBar = new TBar(now.AddMinutes(5), 100, 100, 100, 100, double.NaN);
        var result = vo.Update(nanBar, isNew: true);

        Assert.True(double.IsFinite(result.Value), "Result should be finite after NaN input");
    }

    [Fact]
    public void Update_Infinity_UsesLastValidValue()
    {
        var vo = new Vo(shortPeriod: 2, longPeriod: 4, signalPeriod: 2);
        var now = DateTime.UtcNow;

        // Add valid bars
        for (int i = 0; i < 5; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 500);
            vo.Update(bar, isNew: true);
        }

        // Add bar with Infinity volume
        var infBar = new TBar(now.AddMinutes(5), 100, 100, 100, 100, double.PositiveInfinity);
        var result = vo.Update(infBar, isNew: true);

        Assert.True(double.IsFinite(result.Value), "Result should be finite after Infinity input");
    }

    [Fact]
    public void BatchUpdate_WithNaN_Safe()
    {
        var vo = new Vo();
        var bars = new TBarSeries();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            double volume = i == 10 ? double.NaN : 500 + i;
            bars.Add(new TBar(now.AddMinutes(i), 100, 100, 100, 100, volume));
        }

        var result = vo.Update(bars);

        Assert.Equal(20, result.Count);
        foreach (var val in result.Values)
        {
            Assert.True(double.IsFinite(val), "All values should be finite");
        }
    }

    #endregion

    #region Consistency Tests

    [Fact]
    public void BatchCalc_EqualsStreaming()
    {
        var vo = new Vo(shortPeriod: 5, longPeriod: 10, signalPeriod: 10);

        // Streaming
        var streamingResults = new List<double>();
        for (int i = 0; i < _bars.Count; i++)
        {
            var result = vo.Update(_bars[i], isNew: true);
            streamingResults.Add(result.Value);
        }

        // Batch
        var batchResult = Vo.Calculate(_bars, shortPeriod: 5, longPeriod: 10, signalPeriod: 10);

        Assert.Equal(streamingResults.Count, batchResult.Count);
        for (int i = 0; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResult.Values[i], Tolerance);
        }
    }

    [Fact]
    public void SpanCalc_EqualsStreaming()
    {
        var vo = new Vo(shortPeriod: 5, longPeriod: 10, signalPeriod: 10);

        // Streaming
        var streamingResults = new List<double>();
        for (int i = 0; i < _bars.Count; i++)
        {
            var result = vo.Update(_bars[i], isNew: true);
            streamingResults.Add(result.Value);
        }

        // Span - pass arrays directly (implicit span conversion)
        var volume = _bars.Volume.Values.ToArray();
        var output = new double[_bars.Count];
        Vo.Calculate(volume, output, shortPeriod: 5, longPeriod: 10);

        for (int i = 0; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], output[i], Tolerance);
        }
    }

    [Fact]
    public void BatchUpdate_EqualsStreaming()
    {
        var voStream = new Vo(shortPeriod: 5, longPeriod: 10, signalPeriod: 10);
        var voBatch = new Vo(shortPeriod: 5, longPeriod: 10, signalPeriod: 10);

        // Streaming
        for (int i = 0; i < _bars.Count; i++)
        {
            voStream.Update(_bars[i], isNew: true);
        }

        // Batch
        var batchResult = voBatch.Update(_bars);

        Assert.Equal(voStream.Last.Value, batchResult.Values[^1], Tolerance);
    }

    #endregion

    #region Span API Tests

    [Fact]
    public void Calculate_Span_ValidatesLengths()
    {
        var volume = new double[100];
        var output = new double[50]; // Wrong length

        ArgumentException? caught = null;
        try
        {
            Vo.Calculate(volume, output, shortPeriod: 5, longPeriod: 10);
        }
        catch (ArgumentException ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.Equal("output", caught.ParamName);
    }

    [Fact]
    public void Calculate_Span_ValidatesShortPeriod()
    {
        var volume = new double[100];
        var output = new double[100];

        ArgumentException? caught = null;
        try
        {
            Vo.Calculate(volume, output, shortPeriod: 0, longPeriod: 10);
        }
        catch (ArgumentException ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.Equal("shortPeriod", caught.ParamName);
    }

    [Fact]
    public void Calculate_Span_ValidatesLongPeriod()
    {
        var volume = new double[100];
        var output = new double[100];

        ArgumentException? caught = null;
        try
        {
            Vo.Calculate(volume, output, shortPeriod: 5, longPeriod: 0);
        }
        catch (ArgumentException ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.Equal("longPeriod", caught.ParamName);
    }

    [Fact]
    public void Calculate_Span_ValidatesShortLessThanLong()
    {
        var volume = new double[100];
        var output = new double[100];

        ArgumentException? caught = null;
        try
        {
            Vo.Calculate(volume, output, shortPeriod: 10, longPeriod: 5);
        }
        catch (ArgumentException ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.Equal("shortPeriod", caught.ParamName);
    }

    [Fact]
    public void Calculate_Span_HandlesEmpty()
    {
        double[] volumeArr = [];
        double[] outputArr = [];

        // Should not throw
        Vo.Calculate(volumeArr, outputArr, shortPeriod: 5, longPeriod: 10);

        Assert.Empty(outputArr);
    }

    [Fact]
    public void Calculate_Span_HandlesNaN()
    {
        var volume = new double[20];
        var output = new double[20];

        for (int i = 0; i < 20; i++)
        {
            volume[i] = i == 10 ? double.NaN : 500 + i;
        }

        Vo.Calculate(volume, output, shortPeriod: 5, longPeriod: 10);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), "All values should be finite");
        }
    }

    [Fact]
    public void Calculate_Span_LargeData_NoStackOverflow()
    {
        var volume = new double[10000];
        var output = new double[10000];

        for (int i = 0; i < 10000; i++)
        {
            volume[i] = 500 + (i % 100);
        }

        // Should not throw stack overflow
        Vo.Calculate(volume, output, shortPeriod: 50, longPeriod: 200);

        Assert.True(double.IsFinite(output[^1]));
    }

    #endregion

    #region Event Tests

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var vo = new Vo();
        var eventFired = false;

        vo.Pub += (object? sender, in TValueEventArgs args) => { eventFired = true; };
        vo.Update(_bars[0]);

        Assert.True(eventFired);
    }

    [Fact]
    public void Pub_ChainingWorks()
    {
        var vo = new Vo();
        var receivedValues = new List<double>();

        vo.Pub += (object? sender, in TValueEventArgs args) => { receivedValues.Add(args.Value.Value); };

        for (int i = 0; i < 20; i++)
        {
            vo.Update(_bars[i], isNew: true);
        }

        Assert.Equal(20, receivedValues.Count);
    }

    #endregion

    #region TValue Input Tests

    [Fact]
    public void Update_TValue_PreservesLastValue()
    {
        var vo = new Vo();
        var now = DateTime.UtcNow;

        // First update with bar to set a value
        var bar = new TBar(now, 100, 100, 100, 100, 500);
        vo.Update(bar, isNew: true);
        var lastValue = vo.Last.Value;

        // TValue update should preserve last value (VO requires volume)
        var tval = new TValue(now.AddMinutes(1), 200);
        var result = vo.Update(tval, isNew: true);

        Assert.Equal(lastValue, result.Value, Tolerance);
    }

    #endregion
}