using Xunit;

namespace QuanTAlib;

public class HpTests
{
    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hp(lambda: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hp(lambda: -10));
    }

    [Fact]
    public void Properties_AreAccessible()
    {
        var hp = new Hp(1600);
        Assert.Equal(1600, hp.Lambda);
        Assert.StartsWith("HP", hp.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var hp = new Hp(1600);
        var res = hp.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, res.Value);
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var hpIterative = new Hp(1600);
        var hpBatch = new Hp(1600);

        var source = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 123);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(bar.Time, bar.Close);
        }

        var batchResult = hpBatch.Update(source);

        for (int i = 0; i < source.Count; i++)
        {
            var item = source[i];
            var iterativeResult = hpIterative.Update(item);
            Assert.Equal(batchResult[i].Value, iterativeResult.Value, 1e-9);
        }
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        var hp = new Hp(1600);
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 456);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(bar.Time, bar.Close);
        }

        var tseriesResult = hp.Update(source);
        var spanOutput = new double[source.Count];

        Hp.Batch(source.Values, spanOutput, 1600);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, spanOutput[i], 1e-9);
        }
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var hp = new Hp(1600);

        // Feed initial values
        hp.Update(new TValue(DateTime.UtcNow, 100));
        hp.Update(new TValue(DateTime.UtcNow, 110));

        // This is the "committed" state after 2 bars
        _ = hp.Last.Value;

        // Update with isNew=true (new bar)
        var newVal = hp.Update(new TValue(DateTime.UtcNow, 120), isNew: true).Value;

        // Now update the SAME bar with isNew=false
        var correctedVal = hp.Update(new TValue(DateTime.UtcNow, 125), isNew: false).Value;

        Assert.NotEqual(newVal, correctedVal);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var hp = new Hp(1600);
        hp.Update(new TValue(DateTime.UtcNow, 100));
        hp.Update(new TValue(DateTime.UtcNow, 110));

        hp.Reset();

        // After reset, first value should be effectively init value (input itself)
        var res = hp.Update(new TValue(DateTime.UtcNow, 120));
        Assert.Equal(120, res.Value);
    }

    [Fact]
    public void SpanBatch_ValidatesInput()
    {
        double[] src = new double[10];
        double[] dst = new double[5];
        Assert.Throws<ArgumentException>(() => Hp.Batch(src, dst, 1600));
    }
}
