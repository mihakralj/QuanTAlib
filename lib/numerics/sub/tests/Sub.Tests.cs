using Xunit;

namespace QuanTAlib.Tests;

public class SubTests
{
    private readonly GBM _gbm = new(sigma: 0.5, mu: 0.0, seed: 43);

    [Fact]
    public void Sub_Constructor_Default()
    {
        var sub = new Sub();
        Assert.Equal("Sub", sub.Name);
        Assert.False(sub.IsHot);
        sub.Update(1.0, 1.0);
        Assert.True(sub.IsHot);
    }

    [Fact]
    public void Sub_Update_ComputesDifference()
    {
        var sub = new Sub();
        var result = sub.Update(10.0, 4.0);
        Assert.Equal(6.0, result.Value, 1e-10);
    }

    [Fact]
    public void Sub_Update_HandlesNaN()
    {
        var sub = new Sub();
        var time = DateTime.UtcNow;
        sub.Update(new TValue(time, 10.0), new TValue(time, 3.0));

        var result = sub.Update(new TValue(time.AddSeconds(1), double.NaN), new TValue(time.AddSeconds(1), 1.0));
        Assert.Equal(9.0, result.Value, 1e-10); // last valid a (10.0) - 1.0
    }

    [Fact]
    public void Sub_IsNew_False_CorrectsSameBar()
    {
        var sub = new Sub();
        var time = DateTime.UtcNow;

        var r1 = sub.Update(new TValue(time, 10.0), new TValue(time, 4.0), isNew: true);
        Assert.Equal(6.0, r1.Value, 1e-10);

        var r2 = sub.Update(new TValue(time, 20.0), new TValue(time, 4.0), isNew: false);
        Assert.Equal(16.0, r2.Value, 1e-10);
    }

    [Fact]
    public void Sub_Reset_ClearsState()
    {
        var sub = new Sub();
        sub.Update(10.0, 3.0);
        Assert.Equal(7.0, sub.Last.Value, 1e-10);

        sub.Reset();
        Assert.Equal(0.0, sub.Last.Value);
    }

    [Fact]
    public void Sub_Chaining_TimeJoin()
    {
        var a = new TSeries();
        var b = new TSeries();
        var sub = new Sub(a, b);

        var time = DateTime.UtcNow;
        a.Add(new TValue(time, 10.0), true);
        b.Add(new TValue(time, 3.0), true);

        Assert.Equal(7.0, sub.Last.Value, 1e-10);
    }

    [Fact]
    public void Sub_Static_Batch_Span()
    {
        double[] a = [10.0, 20.0, 30.0];
        double[] b = [1.0, 2.0, 3.0];
        double[] output = new double[3];

        Sub.Batch(a, b, output);

        Assert.Equal(9.0, output[0], 1e-10);
        Assert.Equal(18.0, output[1], 1e-10);
        Assert.Equal(27.0, output[2], 1e-10);
    }

    [Fact]
    public void Sub_Batch_Stream_Consistency()
    {
        var barsA = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var barsB = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var a = barsA.Close;
        var b = barsB.Close;

        var batchResult = Sub.Batch(a, b);

        var stream = new Sub();
        for (int i = 0; i < a.Count; i++)
        {
            var r = stream.Update(a[i], b[i], true);
            Assert.Equal(batchResult[i].Value, r.Value, 1e-10);
        }
    }
}
