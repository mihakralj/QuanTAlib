using Xunit;

namespace QuanTAlib.Tests;

public class DivTests
{
    private readonly GBM _gbm = new(sigma: 0.5, mu: 0.0, seed: 45);

    [Fact]
    public void Div_Constructor_Default()
    {
        var div = new Div();
        Assert.Equal("Div", div.Name);
        Assert.False(div.IsHot);
        div.Update(1.0, 1.0);
        Assert.True(div.IsHot);
    }

    [Fact]
    public void Div_Update_ComputesQuotient()
    {
        var div = new Div();
        var result = div.Update(10.0, 4.0);
        Assert.Equal(2.5, result.Value, 1e-10);
    }

    [Fact]
    public void Div_Update_ZeroDenominator_ReturnsZero()
    {
        var div = new Div();
        var result = div.Update(10.0, 0.0);
        Assert.Equal(0.0, result.Value, 1e-10);
    }

    [Fact]
    public void Div_Update_TinyDenominatorWithinEpsilon_ReturnsZero()
    {
        var div = new Div();
        var result = div.Update(10.0, 1e-13);
        Assert.Equal(0.0, result.Value, 1e-10);
    }

    [Fact]
    public void Div_Update_ZeroDenominator_DoesNotPoisonSubsequentBars()
    {
        // Regression test for the Sum-poisoning hazard documented on Div: a zero-guarded
        // ComputeError result must always be finite, so the next bar recovers cleanly.
        var div = new Div();
        var time = DateTime.UtcNow;

        div.Update(new TValue(time, 10.0), new TValue(time, 0.0), isNew: true);
        var next = div.Update(new TValue(time.AddSeconds(1), 9.0), new TValue(time.AddSeconds(1), 3.0), isNew: true);

        Assert.Equal(3.0, next.Value, 1e-10);
        Assert.False(double.IsNaN(next.Value));
    }

    [Fact]
    public void Div_Update_HandlesNaNInNumerator()
    {
        var div = new Div();
        var time = DateTime.UtcNow;
        div.Update(new TValue(time, 8.0), new TValue(time, 2.0));

        var result = div.Update(new TValue(time.AddSeconds(1), double.NaN), new TValue(time.AddSeconds(1), 2.0));
        Assert.Equal(4.0, result.Value, 1e-10); // last valid a (8.0) / 2.0
    }

    [Fact]
    public void Div_IsNew_False_CorrectsSameBar()
    {
        var div = new Div();
        var time = DateTime.UtcNow;

        var r1 = div.Update(new TValue(time, 10.0), new TValue(time, 2.0), isNew: true);
        Assert.Equal(5.0, r1.Value, 1e-10);

        var r2 = div.Update(new TValue(time, 20.0), new TValue(time, 2.0), isNew: false);
        Assert.Equal(10.0, r2.Value, 1e-10);
    }

    [Fact]
    public void Div_Reset_ClearsState()
    {
        var div = new Div();
        div.Update(10.0, 2.0);
        Assert.Equal(5.0, div.Last.Value, 1e-10);

        div.Reset();
        Assert.Equal(0.0, div.Last.Value);
    }

    [Fact]
    public void Div_Chaining_TimeJoin()
    {
        var a = new TSeries();
        var b = new TSeries();
        var div = new Div(a, b);

        var time = DateTime.UtcNow;
        a.Add(new TValue(time, 20.0), true);
        b.Add(new TValue(time, 4.0), true);

        Assert.Equal(5.0, div.Last.Value, 1e-10);
    }

    [Fact]
    public void Div_Static_Batch_Span()
    {
        double[] a = [10.0, 20.0, 30.0];
        double[] b = [2.0, 4.0, 0.0];
        double[] output = new double[3];

        Div.Batch(a, b, output);

        Assert.Equal(5.0, output[0], 1e-10);
        Assert.Equal(5.0, output[1], 1e-10);
        Assert.Equal(0.0, output[2], 1e-10);
    }

    [Fact]
    public void Div_Batch_Stream_Consistency()
    {
        var barsA = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var barsB = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var a = barsA.Close;
        var b = barsB.Close;

        var batchResult = Div.Batch(a, b);

        var stream = new Div();
        for (int i = 0; i < a.Count; i++)
        {
            var r = stream.Update(a[i], b[i], true);
            Assert.Equal(batchResult[i].Value, r.Value, 1e-10);
        }
    }
}
