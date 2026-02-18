namespace QuanTAlib;

public class VossTests
{
    private readonly GBM _gbm;

    public VossTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    // --- A) Constructor Validation ---

    [Fact]
    public void Constructor_ValidatesPeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Voss(period: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Voss(period: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Voss(period: -1));
    }

    [Fact]
    public void Constructor_ValidatesPredict()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Voss(predict: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Voss(predict: -1));
    }

    [Fact]
    public void Constructor_ValidatesBandwidth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Voss(bandwidth: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Voss(bandwidth: -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Voss(bandwidth: 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Voss(bandwidth: 1.5));
    }

    [Fact]
    public void Constructor_SetsName()
    {
        var ind = new Voss(20, 3, 0.25);
        Assert.Equal("VOSS(20,3,0.25)", ind.Name);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var ind = new Voss(20, 3, 0.25);
        Assert.Equal(20, ind.WarmupPeriod);
    }

    [Fact]
    public void Constructor_DefaultParameters()
    {
        var ind = new Voss();
        Assert.Equal(20, ind.Period);
        Assert.Equal(3, ind.Predict);
        Assert.Equal(0.25, ind.Bandwidth);
        Assert.Equal(9, ind.Order); // 3 * 3
    }

    [Fact]
    public void Constructor_OrderIs3TimesPredict()
    {
        var ind = new Voss(period: 20, predict: 5);
        Assert.Equal(15, ind.Order);
    }

    // --- B) Basic Calculation ---

    [Fact]
    public void Calc_ReturnsValue()
    {
        var ind = new Voss(20, 3, 0.25);
        var result = ind.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Calc_PropertiesAccessible()
    {
        var ind = new Voss(20, 3, 0.25);
        // Feed several bars to pass warmup
        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(double.IsFinite(ind.Last.Value));
        Assert.True(ind.IsHot); // Count > 5 after 10 bars
        Assert.Equal("VOSS(20,3,0.25)", ind.Name);
        _ = ind.IsNew;
        Assert.True(double.IsFinite(ind.LastFilt));
    }

    [Fact]
    public void ConstantInput_ConvergesToZero()
    {
        // Bandpass filter applied to DC input → output should converge to zero
        var ind = new Voss(20, 3, 0.25);
        double lastVal = 0;
        for (int i = 0; i < 500; i++)
        {
            lastVal = ind.Update(new TValue(DateTime.UtcNow, 100)).Value;
        }
        Assert.True(Math.Abs(lastVal) < 1e-6, $"Constant input should yield ~0, got {lastVal}");
    }

    [Fact]
    public void ConstantInput_FiltConvergesToZero()
    {
        var ind = new Voss(20, 3, 0.25);
        for (int i = 0; i < 500; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100));
        }
        Assert.True(Math.Abs(ind.LastFilt) < 1e-6, $"Constant input Filt should yield ~0, got {ind.LastFilt}");
    }

    // --- C) State + Bar Correction ---

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var ind = new Voss(20, 3, 0.25);
        // Feed enough bars past warmup (Count > 5) so Filt is not clamped to 0
        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i * 2), isNew: true);
        }
        double val1 = ind.Last.Value;

        ind.Update(new TValue(DateTime.UtcNow, 200), isNew: false);
        double val2 = ind.Last.Value;
        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var ind = new Voss(10, 2, 0.3);
        // Feed enough bars past warmup (Count > 5) so Filt is not clamped to 0
        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i * 3), isNew: true);
        }
        double val1 = ind.Last.Value;

        ind.Update(new TValue(DateTime.UtcNow, 200), isNew: false);
        double val2 = ind.Last.Value;
        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var ind = new Voss(10, 2, 0.3);
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // Feed N values
        for (int i = 0; i < series.Count; i++)
        {
            ind.Update(series[i]);
        }
        double originalValue = ind.Last.Value;

        // Feed M corrections with isNew=false
        ind.Update(new TValue(DateTime.UtcNow, 200), isNew: false);
        ind.Update(new TValue(DateTime.UtcNow, 300), isNew: false);
        ind.Update(new TValue(DateTime.UtcNow, 400), isNew: false);

        // Restore with original last value
        ind.Update(series[^1], isNew: false);
        double restoredValue = ind.Last.Value;

        Assert.Equal(originalValue, restoredValue, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var ind = new Voss(10, 2, 0.3);
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        foreach (var item in data.Close)
        {
            ind.Update(item);
        }

        ind.Reset();

        var ind2 = new Voss(10, 2, 0.3);
        var result1 = ind.Update(new TValue(DateTime.UtcNow, 100));
        var result2 = ind2.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(result2.Value, result1.Value, 10);
    }

    // --- D) Warmup/Convergence ---

    [Fact]
    public void IsHot_FalseBeforeWarmup()
    {
        // IsHot => Count > 5
        var ind = new Voss(20, 3, 0.25);
        Assert.False(ind.IsHot); // No bars yet

        // Feed 5 bars → Count = 5, still not hot
        for (int i = 0; i < 5; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.False(ind.IsHot); // Count == 5, need > 5

        // 6th bar → hot
        ind.Update(new TValue(DateTime.UtcNow, 106));
        Assert.True(ind.IsHot);
    }

    [Fact]
    public void IsHot_StaysTrueAfterWarmup()
    {
        var ind = new Voss(20, 3, 0.25);
        for (int i = 0; i < 100; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(ind.IsHot);
    }

    // --- E) Robustness ---

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var ind = new Voss(20, 3, 0.25);
        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, 105));

        var result = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var ind = new Voss(20, 3, 0.25);
        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, 105));

        var result = ind.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));

        var result2 = ind.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var ind = new Voss(20, 3, 0.25);
        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, 105));

        for (int i = 0; i < 10; i++)
        {
            var result = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void BatchCalc_HandlesNaN()
    {
        double[] input = [100, 105, double.NaN, 110, double.NaN, 115, 120, 125, 130, 135];
        double[] output = new double[input.Length];

        Voss.Batch(input, output, 5, 1, 0.25);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] should be finite");
        }
    }

    // --- F) Consistency ---

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int period = 20;
        const int predict = 3;
        const double bandwidth = 0.25;
        var data = _gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // 1. Span Mode
        double[] spanOutput = new double[series.Count];
        Voss.Batch(series.Values.ToArray(), spanOutput, period, predict, bandwidth);

        // 2. TSeries Batch Mode
        var vossBatch = new Voss(period, predict, bandwidth);
        var batchResult = vossBatch.Update(series);

        // 3. Streaming Mode
        var vossStream = new Voss(period, predict, bandwidth);
        var streamResults = new List<double>();
        foreach (var item in series)
        {
            streamResults.Add(vossStream.Update(item).Value);
        }

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var vossEvent = new Voss(pubSource, period, predict, bandwidth);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }

        // Assert all modes match
        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(spanOutput[i], batchResult[i].Value, 1e-9);
            Assert.Equal(spanOutput[i], streamResults[i], 1e-9);
        }
        Assert.Equal(spanOutput[^1], vossEvent.Last.Value, 1e-9);
    }

    // --- G) Span API ---

    [Fact]
    public void SpanCalc_ValidatesLength()
    {
        double[] source = new double[10];
        double[] output = new double[5]; // Mismatched!

        Assert.Throws<ArgumentException>(() => Voss.Batch(source, output));
    }

    [Fact]
    public void SpanCalc_ConstantInput_ConvergesToZero()
    {
        double[] input = Enumerable.Repeat(100.0, 500).ToArray();
        double[] output = new double[500];

        Voss.Batch(input, output, 20, 3, 0.25);

        Assert.True(Math.Abs(output[^1]) < 1e-6, $"Expected ~0 for constant input, got {output[^1]}");
    }

    [Fact]
    public void SpanCalc_MatchesTSeriesCalc()
    {
        var data = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // Span
        double[] spanOutput = new double[series.Count];
        Voss.Batch(series.Values.ToArray(), spanOutput, 20, 3, 0.25);

        // TSeries
        var ind = new Voss(20, 3, 0.25);
        var tseriesResult = ind.Update(series);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(spanOutput[i], tseriesResult[i].Value, 1e-9);
        }
    }

    // --- H) Chainability ---

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var ind = new Voss(20, 3, 0.25);
        int fireCount = 0;
        ind.Pub += (object? _, in TValueEventArgs _) => fireCount++;

        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, 105));

        Assert.Equal(2, fireCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var source = new TSeries();
        var ind = new Voss(source, 20, 3, 0.25);

        source.Add(new TValue(DateTime.UtcNow, 100));
        source.Add(new TValue(DateTime.UtcNow, 105));

        Assert.True(double.IsFinite(ind.Last.Value));
    }

    // --- Additional: Different Parameters ---

    [Fact]
    public void DifferentParameters_ProduceDifferentResults()
    {
        var data = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        var ind1 = new Voss(20, 3, 0.25);
        var ind2 = new Voss(10, 5, 0.4);

        foreach (var item in series)
        {
            ind1.Update(item);
            ind2.Update(item);
        }

        Assert.NotEqual(ind1.Last.Value, ind2.Last.Value);
    }

    [Fact]
    public void LargeDataset_DoesNotThrow()
    {
        var data = _gbm.Fetch(10000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        double[] input = series.Values.ToArray();
        double[] output = new double[input.Length];

        Voss.Batch(input, output, 20, 3, 0.25);

        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void LastFilt_TracksCurrentBandpassValue()
    {
        var ind = new Voss(20, 3, 0.25);
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var item in data.Close)
        {
            ind.Update(item);
        }

        // LastFilt should be finite and generally different from Voss output
        Assert.True(double.IsFinite(ind.LastFilt));
    }
}
