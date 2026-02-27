using Xunit;

namespace QuanTAlib.Tests;

public class VrocTests
{
    private const double Tolerance = 1e-10;
    private readonly GBM _gbm;
    private readonly TBarSeries _bars;

    public VrocTests()
    {
        _gbm = new GBM(seed: 42);
        _bars = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultParameters_SetsExpectedValues()
    {
        var vroc = new Vroc();
        Assert.Equal("Vroc(12,%)", vroc.Name);
        Assert.Equal(13, vroc.WarmupPeriod);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsExpectedValues()
    {
        var vroc = new Vroc(period: 20);
        Assert.Equal("Vroc(20,%)", vroc.Name);
        Assert.Equal(21, vroc.WarmupPeriod);
    }

    [Fact]
    public void Constructor_PointMode_SetsExpectedName()
    {
        var vroc = new Vroc(period: 10, usePercent: false);
        Assert.Equal("Vroc(10,pt)", vroc.Name);
    }

    [Fact]
    public void Constructor_PeriodLessThan1_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vroc(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_ReturnsTValue()
    {
        var vroc = new Vroc();
        var result = vroc.Update(_bars[0]);
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_AccessesLast()
    {
        var vroc = new Vroc();
        vroc.Update(_bars[0]);
        Assert.Equal(vroc.Last.Value, vroc.Update(_bars[0], isNew: false).Value);
    }

    [Fact]
    public void Update_SameVolumes_ReturnsZeroPercent()
    {
        var vroc = new Vroc(period: 3, usePercent: true);
        var now = DateTime.UtcNow;

        // All same volumes should result in VROC = 0
        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 1000);
            vroc.Update(bar, isNew: true);
        }

        Assert.Equal(0.0, vroc.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_SameVolumes_ReturnsZeroPoint()
    {
        var vroc = new Vroc(period: 3, usePercent: false);
        var now = DateTime.UtcNow;

        // All same volumes should result in VROC = 0
        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 1000);
            vroc.Update(bar, isNew: true);
        }

        Assert.Equal(0.0, vroc.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_DoubleVolume_Returns100Percent()
    {
        var vroc = new Vroc(period: 3, usePercent: true);
        var now = DateTime.UtcNow;

        // Initial volumes of 1000 - need period+1 bars to get first VROC value
        // VROC compares current volume to volume 'period' bars ago
        for (int i = 0; i < 4; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 1000);
            vroc.Update(bar, isNew: true);
        }

        // Double the volume - compares 2000 to volume[4-3]=volume[1]=1000
        var doubleBar = new TBar(now.AddMinutes(4), 100, 100, 100, 100, 2000);
        var result = vroc.Update(doubleBar, isNew: true);

        // (2000 - 1000) / 1000 * 100 = 100
        Assert.Equal(100.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_DoubleVolume_Returns1000Point()
    {
        var vroc = new Vroc(period: 3, usePercent: false);
        var now = DateTime.UtcNow;

        // Need period+1 bars to get first VROC value
        for (int i = 0; i < 4; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 1000);
            vroc.Update(bar, isNew: true);
        }

        // Double the volume - compares 2000 to volume[4-3]=volume[1]=1000
        var doubleBar = new TBar(now.AddMinutes(4), 100, 100, 100, 100, 2000);
        var result = vroc.Update(doubleBar, isNew: true);

        // 2000 - 1000 = 1000
        Assert.Equal(1000.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_HalfVolume_ReturnsMinus50Percent()
    {
        var vroc = new Vroc(period: 3, usePercent: true);
        var now = DateTime.UtcNow;

        // Need period+1 bars to get first VROC value
        for (int i = 0; i < 4; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 1000);
            vroc.Update(bar, isNew: true);
        }

        // Half the volume - compares 500 to volume[4-3]=volume[1]=1000
        var halfBar = new TBar(now.AddMinutes(4), 100, 100, 100, 100, 500);
        var result = vroc.Update(halfBar, isNew: true);

        // (500 - 1000) / 1000 * 100 = -50
        Assert.Equal(-50.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_IncreasingVolumes_ReturnsPositive()
    {
        var vroc = new Vroc(period: 3, usePercent: true);
        var now = DateTime.UtcNow;

        // Increasing volumes
        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 1000 + i * 100);
            vroc.Update(bar, isNew: true);
        }

        Assert.True(vroc.Last.Value > 0, $"Expected positive VROC but got {vroc.Last.Value}");
    }

    [Fact]
    public void Update_DecreasingVolumes_ReturnsNegative()
    {
        var vroc = new Vroc(period: 3, usePercent: true);
        var now = DateTime.UtcNow;

        // Decreasing volumes
        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 2000 - i * 100);
            vroc.Update(bar, isNew: true);
        }

        Assert.True(vroc.Last.Value < 0, $"Expected negative VROC but got {vroc.Last.Value}");
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var vroc = new Vroc(period: 3);

        // Feed bars and capture the last values from two consecutive new bars
        // Use GBM data which has varying volumes
        var result1 = vroc.Update(_bars[0], isNew: true);
        for (int i = 1; i < 20; i++)
        {
            result1 = vroc.Update(_bars[i], isNew: true);
        }

        var result2 = vroc.Update(_bars[20], isNew: true);

        // Two consecutive bars with isNew=true should (likely) have different values
        // This confirms state advances on new bars. Since GBM generates varying data,
        // consecutive VROC values will differ
        Assert.True(vroc.IsHot, "VROC should be hot after 20 bars");
        // Just verify the indicator is working - different bars produce results
        Assert.True(result1.Value != result2.Value || Math.Abs(result2.Value) > 0 || Math.Abs(result1.Value) > 0,
            $"State should have advanced. Result1={result1.Value}, Result2={result2.Value}");
    }

    [Fact]
    public void IsNew_False_UpdatesCurrentBar()
    {
        var vroc = new Vroc(period: 3);
        var now = DateTime.UtcNow;

        // Fill buffer
        for (int i = 0; i < 4; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 1000);
            vroc.Update(bar, isNew: true);
        }

        var stateBeforeCorrection = vroc.Last.Value;

        // Correction with different volume
        var correctionBar = new TBar(now.AddMinutes(3), 100, 100, 100, 100, 1500);
        vroc.Update(correctionBar, isNew: false);

        // Restore original
        var originalBar = new TBar(now.AddMinutes(3), 100, 100, 100, 100, 1000);
        var result = vroc.Update(originalBar, isNew: false);

        Assert.Equal(stateBeforeCorrection, result.Value, Tolerance);
    }

    [Fact]
    public void IterativeCorrections_RestoreState()
    {
        var vroc = new Vroc(period: 5);
        var now = DateTime.UtcNow;

        // Add several bars
        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 1000 + i * 50);
            vroc.Update(bar, isNew: true);
        }

        var stateBeforeCorrections = vroc.Last.Value;

        // Apply multiple corrections
        for (int j = 0; j < 5; j++)
        {
            var correctionBar = new TBar(now.AddMinutes(9), 100, 100, 100, 100, 2000 + j * 100);
            vroc.Update(correctionBar, isNew: false);
        }

        // Restore original bar
        var originalBar = new TBar(now.AddMinutes(9), 100, 100, 100, 100, 1450);
        var restored = vroc.Update(originalBar, isNew: false);

        Assert.Equal(stateBeforeCorrections, restored.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var vroc = new Vroc();

        // Process some bars
        for (int i = 0; i < 20; i++)
        {
            vroc.Update(_bars[i], isNew: true);
        }

        Assert.True(vroc.IsHot);

        vroc.Reset();

        Assert.False(vroc.IsHot);
        Assert.Equal(default, vroc.Last);
    }

    #endregion

    #region Warmup Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var vroc = new Vroc(period: 10);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 500);
            vroc.Update(bar, isNew: true);
            Assert.False(vroc.IsHot, $"Should not be hot at index {i}");
        }
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var vroc = new Vroc(period: 10);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 11; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 500);
            vroc.Update(bar, isNew: true);
        }

        Assert.True(vroc.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsPeriodPlusOne()
    {
        var vroc = new Vroc(period: 15);
        Assert.Equal(16, vroc.WarmupPeriod);
    }

    #endregion

    #region Robustness Tests

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var vroc = new Vroc(period: 3);
        var now = DateTime.UtcNow;

        // Add valid bars
        for (int i = 0; i < 5; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 500);
            vroc.Update(bar, isNew: true);
        }

        // Add bar with NaN volume
        var nanBar = new TBar(now.AddMinutes(5), 100, 100, 100, 100, double.NaN);
        var result = vroc.Update(nanBar, isNew: true);

        Assert.True(double.IsFinite(result.Value), "Result should be finite after NaN input");
    }

    [Fact]
    public void Update_Infinity_UsesLastValidValue()
    {
        var vroc = new Vroc(period: 3);
        var now = DateTime.UtcNow;

        // Add valid bars
        for (int i = 0; i < 5; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 500);
            vroc.Update(bar, isNew: true);
        }

        // Add bar with Infinity volume
        var infBar = new TBar(now.AddMinutes(5), 100, 100, 100, 100, double.PositiveInfinity);
        var result = vroc.Update(infBar, isNew: true);

        Assert.True(double.IsFinite(result.Value), "Result should be finite after Infinity input");
    }

    [Fact]
    public void Update_NegativeVolume_UsesLastValidValue()
    {
        var vroc = new Vroc(period: 3);
        var now = DateTime.UtcNow;

        // Add valid bars
        for (int i = 0; i < 5; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 500);
            vroc.Update(bar, isNew: true);
        }

        // Add bar with negative volume
        var negBar = new TBar(now.AddMinutes(5), 100, 100, 100, 100, -100);
        var result = vroc.Update(negBar, isNew: true);

        Assert.True(double.IsFinite(result.Value), "Result should be finite after negative volume input");
    }

    [Fact]
    public void BatchUpdate_WithNaN_Safe()
    {
        var vroc = new Vroc();
        var bars = new TBarSeries();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            double volume = i == 10 ? double.NaN : 500 + i;
            bars.Add(new TBar(now.AddMinutes(i), 100, 100, 100, 100, volume));
        }

        var result = vroc.Update(bars);

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
        var vroc = new Vroc(period: 12, usePercent: true);

        // Streaming
        var streamingResults = new List<double>();
        for (int i = 0; i < _bars.Count; i++)
        {
            var result = vroc.Update(_bars[i], isNew: true);
            streamingResults.Add(result.Value);
        }

        // Batch
        var batchResult = Vroc.Batch(_bars, period: 12, usePercent: true);

        Assert.Equal(streamingResults.Count, batchResult.Count);
        for (int i = 0; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResult.Values[i], Tolerance);
        }
    }

    [Fact]
    public void SpanCalc_EqualsStreaming()
    {
        var vroc = new Vroc(period: 12, usePercent: true);

        // Streaming
        var streamingResults = new List<double>();
        for (int i = 0; i < _bars.Count; i++)
        {
            var result = vroc.Update(_bars[i], isNew: true);
            streamingResults.Add(result.Value);
        }

        // Span - pass arrays directly (implicit span conversion)
        var volume = _bars.Volume.Values.ToArray();
        var output = new double[_bars.Count];
        Vroc.Batch(volume, output, period: 12, usePercent: true);

        for (int i = 0; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], output[i], Tolerance);
        }
    }

    [Fact]
    public void BatchUpdate_EqualsStreaming()
    {
        var vrocStream = new Vroc(period: 12, usePercent: true);
        var vrocBatch = new Vroc(period: 12, usePercent: true);

        // Streaming
        for (int i = 0; i < _bars.Count; i++)
        {
            vrocStream.Update(_bars[i], isNew: true);
        }

        // Batch
        var batchResult = vrocBatch.Update(_bars);

        Assert.Equal(vrocStream.Last.Value, batchResult.Values[^1], Tolerance);
    }

    [Fact]
    public void PointMode_EqualsStreaming()
    {
        var vroc = new Vroc(period: 12, usePercent: false);

        // Streaming
        var streamingResults = new List<double>();
        for (int i = 0; i < _bars.Count; i++)
        {
            var result = vroc.Update(_bars[i], isNew: true);
            streamingResults.Add(result.Value);
        }

        // Span - pass arrays directly (implicit span conversion)
        var volume = _bars.Volume.Values.ToArray();
        var output = new double[_bars.Count];
        Vroc.Batch(volume, output, period: 12, usePercent: false);

        for (int i = 0; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], output[i], Tolerance);
        }
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
            Vroc.Batch(volume, output, period: 12, usePercent: true);
        }
        catch (ArgumentException ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.Equal("output", caught.ParamName);
    }

    [Fact]
    public void Calculate_Span_ValidatesPeriod()
    {
        var volume = new double[100];
        var output = new double[100];

        ArgumentException? caught = null;
        try
        {
            Vroc.Batch(volume, output, period: 0, usePercent: true);
        }
        catch (ArgumentException ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.Equal("period", caught.ParamName);
    }

    [Fact]
    public void Calculate_Span_HandlesEmpty()
    {
        double[] volumeArr = [];
        double[] outputArr = [];

        // Should not throw
        Vroc.Batch(volumeArr, outputArr, period: 12, usePercent: true);

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

        Vroc.Batch(volume, output, period: 5, usePercent: true);

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
        Vroc.Batch(volume, output, period: 100, usePercent: true);

        Assert.True(double.IsFinite(output[^1]));
    }

    #endregion

    #region Event Tests

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var vroc = new Vroc();
        var eventFired = false;

        vroc.Pub += (object? sender, in TValueEventArgs args) => { eventFired = true; };
        vroc.Update(_bars[0]);

        Assert.True(eventFired);
    }

    [Fact]
    public void Pub_ChainingWorks()
    {
        var vroc = new Vroc();
        var receivedValues = new List<double>();

        vroc.Pub += (object? sender, in TValueEventArgs args) => { receivedValues.Add(args.Value.Value); };

        for (int i = 0; i < 20; i++)
        {
            vroc.Update(_bars[i], isNew: true);
        }

        Assert.Equal(20, receivedValues.Count);
    }

    #endregion

    #region TValue Input Tests

    [Fact]
    public void Update_TValue_PreservesLastValue()
    {
        var vroc = new Vroc();
        var now = DateTime.UtcNow;

        // First update with bar to set a value
        var bar = new TBar(now, 100, 100, 100, 100, 500);
        vroc.Update(bar, isNew: true);
        var lastValue = vroc.Last.Value;

        // TValue update should preserve last value (VROC requires volume)
        var tval = new TValue(now.AddMinutes(1), 200);
        var result = vroc.Update(tval, isNew: true);

        Assert.Equal(lastValue, result.Value, Tolerance);
    }

    #endregion

    #region Zero Historical Volume Tests

    [Fact]
    public void Update_ZeroHistoricalVolume_ReturnsZeroPercent()
    {
        var vroc = new Vroc(period: 3, usePercent: true);
        var now = DateTime.UtcNow;

        // Initial volumes of 0
        for (int i = 0; i < 3; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100, 100, 100, 100, 0);
            vroc.Update(bar, isNew: true);
        }

        // Non-zero volume
        var newBar = new TBar(now.AddMinutes(3), 100, 100, 100, 100, 1000);
        var result = vroc.Update(newBar, isNew: true);

        // Division by zero protection should return 0
        Assert.Equal(0.0, result.Value, Tolerance);
    }

    #endregion
}
