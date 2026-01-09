namespace QuanTAlib;

public class BpfTests
{
    private readonly GBM _gbm;

    public BpfTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        // Period check
        Assert.Throws<ArgumentOutOfRangeException>(() => new Bpf(0, 20));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Bpf(10, 0));

        // Allowed
        var bpf = new Bpf(10, 20);
        Assert.Equal("BPF(10,20)", bpf.Name);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var bpf = new Bpf(10, 20);
        Assert.Equal(20, bpf.WarmupPeriod);
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        // Arrange
        const int lowerPeriod = 10;
        int upperPeriod = 20;
        var data = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // 1. Span Mode
        double[] spanOutput = new double[series.Count];
        Bpf.Calculate(series.Values.ToArray(), spanOutput, lowerPeriod, upperPeriod);

        // 2. TSeries Batch Mode
        var bpfBatch = new Bpf(lowerPeriod, upperPeriod);
        var batchResult = bpfBatch.Update(series);

        // 3. Streaming Mode
        var bpfStream = new Bpf(lowerPeriod, upperPeriod);
        var streamResults = new List<double>();
        foreach (var item in series)
        {
            streamResults.Add(bpfStream.Update(item).Value);
        }

        // Assert
        for (int i = 0; i < series.Count; i++)
        {
            // Compare Span vs Batch
            Assert.Equal(spanOutput[i], batchResult[i].Value, 1e-9);

            // Compare Span vs Streaming
            Assert.Equal(spanOutput[i], streamResults[i], 1e-9);
        }
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var bpf = new Bpf(5, 10);

        // Update 1 (New)
        bpf.Update(new TValue(DateTime.UtcNow, 100), isNew: true);

        // Update 2 (New)
        bpf.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        double val1 = bpf.Last.Value;

        // Update 2 (Same bar)
        bpf.Update(new TValue(DateTime.UtcNow, 110), isNew: false);
        double val2 = bpf.Last.Value;

        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var bpf = new Bpf(5, 10);

        bpf.Update(new TValue(DateTime.UtcNow, 100));
        bpf.Update(new TValue(DateTime.UtcNow, 105));

        // Feed NaN
        var result = bpf.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Should produce a valid number (using previous 105 as input effectively)
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void SpanCalc_MatchesReferenece()
    {
        // Simple manual check or regression test
        // 2nd order HP + 2nd order LP
        // If we feed constant, HP part should eventually go to 0.
        // 2nd order HP: 2 zeros, 2 poles.
        // H(z) has zero at z=1. Thus DC gain is 0.
        // So a constant input should yield 0 output.

        double[] input = Enumerable.Repeat(100.0, 500).ToArray();
        double[] output = new double[500];

        Bpf.Calculate(input, output, 10, 20);

        // Last value should be close to 0
        Assert.True(Math.Abs(output[^1]) < 1e-6);
    }
}