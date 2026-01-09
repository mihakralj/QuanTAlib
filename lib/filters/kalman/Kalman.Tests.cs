namespace QuanTAlib;

public class KalmanTests
{
    private readonly GBM _gbm;

    public KalmanTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Kalman(q: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Kalman(q: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Kalman(r: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Kalman(r: -1));
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var filter = new Kalman(q: 0.01, r: 0.1);
        var result = filter.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100.0, result.Value, 1e-9);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var filter = new Kalman(q: 0.01, r: 0.1);
        filter.Update(new TValue(DateTime.UtcNow, 100), isNew: true); // Init
        
        var v1 = filter.Update(new TValue(DateTime.UtcNow, 101), isNew: true);
        var v2 = filter.Update(new TValue(DateTime.UtcNow, 102), isNew: true);
        
        Assert.NotEqual(v1.Value, v2.Value);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var filter = new Kalman(q: 0.01, r: 0.1);
        // Bar 0
        filter.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        // Bar 1 - first tick
        var res1 = filter.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        // Bar 1 - update tick (correction)
        var res2 = filter.Update(new TValue(DateTime.UtcNow, 110), isNew: false);
        
        Assert.NotEqual(res1.Value, res2.Value);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var filter = new Kalman(q: 0.01, r: 0.1);
        
        // Feed 10 values
        TValue lastInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = _gbm.Next();
            lastInput = new TValue(bar.Time, bar.Close);
            filter.Update(lastInput, isNew: true);
        }

        double stateAfter10 = filter.Last.Value;

        // Feed corrections
        for (int i = 0; i < 5; i++)
        {
            var bar = _gbm.Next(); // Random noise
            filter.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Restore
        var restored = filter.Update(lastInput, isNew: false);

        Assert.Equal(stateAfter10, restored.Value, 1e-9);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var filter = new Kalman(q: 0.01, r: 0.1);
        filter.Update(new TValue(DateTime.UtcNow, 100));
        filter.Update(new TValue(DateTime.UtcNow, 200));
        
        filter.Reset();
        
        // Should behave as new
        var res = filter.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50, res.Value); // First value init
        Assert.False(filter.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var filter = new Kalman(q: 0.01, r: 0.1);
        filter.Update(new TValue(DateTime.UtcNow, 100)); // Init
        var v1 = filter.Update(new TValue(DateTime.UtcNow, 101));
        
        var vNaN = filter.Update(new TValue(DateTime.UtcNow, double.NaN));
        
        Assert.Equal(v1.Value, vNaN.Value, 1e-9);
    }
    
    [Fact]
    public void AllModes_ProduceSameResult()
    {
        var filter = new Kalman(q: 0.01, r: 0.1);
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. TSeries Update
        var tseriesResult = filter.Update(series);

        // 2. Span Calculate
        var spanOutput = new double[series.Count];
        Kalman.Calculate(series.Values.ToArray(), spanOutput, 0.01, 0.1);

        // 3. Streaming Update
        filter.Reset();
        var streamingResults = new List<double>();
        foreach(var item in series)
        {
            streamingResults.Add(filter.Update(item).Value);
        }

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, spanOutput[i], 1e-9);
            Assert.Equal(tseriesResult[i].Value, streamingResults[i], 1e-9);
        }
    }
}