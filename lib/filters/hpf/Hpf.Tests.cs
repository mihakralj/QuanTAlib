using Xunit;

namespace QuanTAlib;

public class HpfTests
{
    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hpf(length: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hpf(length: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hpf(length: -10));
    }

    [Fact]
    public void Properties_AreAccessible()
    {
        var hpf = new Hpf(40);
        Assert.Equal(40, hpf.Length);
        Assert.StartsWith("HPF", hpf.Name, StringComparison.Ordinal);
    }


    [Fact]
    public void Calc_ReturnsValue()
    {
        var hpf = new Hpf(40);
        var res = hpf.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(0, res.Value); // First value should be 0 as per logic
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var hpfIterative = new Hpf(40);
        var hpfBatch = new Hpf(40);

        var source = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 123);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(bar.Time, bar.Close);
        }

        var batchResult = hpfBatch.Update(source);

        for (int i = 0; i < source.Count; i++)
        {
            var item = source[i];
            var iterativeResult = hpfIterative.Update(item);
            Assert.Equal(batchResult[i].Value, iterativeResult.Value, 1e-9);
        }
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        var hpf = new Hpf(40);
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 456);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(bar.Time, bar.Close);
        }

        var tseriesResult = hpf.Update(source);
        var spanOutput = new double[source.Count];

        Hpf.Calculate(source.Values, spanOutput, 40, out _);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, spanOutput[i], 1e-9);
        }
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var hpf = new Hpf(40);

        // Feed initial values
        hpf.Update(new TValue(DateTime.UtcNow, 100));
        hpf.Update(new TValue(DateTime.UtcNow, 110));

        // This is the "committed" state after 2 bars
        _ = hpf.Last.Value;

        // Update with isNew=true (new bar)
        var newVal = hpf.Update(new TValue(DateTime.UtcNow, 120), isNew: true).Value;

        // Now update the SAME bar with isNew=false
        var correctedVal = hpf.Update(new TValue(DateTime.UtcNow, 125), isNew: false).Value;

        Assert.NotEqual(newVal, correctedVal);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var hpf = new Hpf(40);
        hpf.Update(new TValue(DateTime.UtcNow, 100));
        hpf.Update(new TValue(DateTime.UtcNow, 110));

        hpf.Reset();

        // After reset, first value should be 0 (init value)
        var res = hpf.Update(new TValue(DateTime.UtcNow, 120));
        Assert.Equal(0, res.Value);
    }

    [Fact]
    public void SpanBatch_ValidatesInput()
    {
        double[] src = new double[10];
        double[] dst = new double[5];
        Assert.Throws<ArgumentException>(() => Hpf.Calculate(src, dst, 40, out _));
    }
}
