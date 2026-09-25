using Xunit;

namespace QuanTAlib.Tests;

public class MulTests
{
    private readonly GBM _gbm = new(sigma: 0.5, mu: 0.0, seed: 44);

    [Fact]
    public void Mul_Constructor_Default()
    {
        var mul = new Mul();
        Assert.Equal("Mul", mul.Name);
        Assert.False(mul.IsHot);
        mul.Update(1.0, 1.0);
        Assert.True(mul.IsHot);
    }

    [Fact]
    public void Mul_Update_ComputesProduct()
    {
        var mul = new Mul();
        var result = mul.Update(6.0, 7.0);
        Assert.Equal(42.0, result.Value, 1e-10);
    }

    [Fact]
    public void Mul_Update_HandlesInfinity()
    {
        var mul = new Mul();
        var time = DateTime.UtcNow;
        mul.Update(new TValue(time, 3.0), new TValue(time, 4.0));

        var result = mul.Update(new TValue(time.AddSeconds(1), 5.0), new TValue(time.AddSeconds(1), double.PositiveInfinity));
        Assert.Equal(20.0, result.Value, 1e-10); // 5 * last valid b (4.0)
    }

    [Fact]
    public void Mul_IsNew_False_CorrectsSameBar()
    {
        var mul = new Mul();
        var time = DateTime.UtcNow;

        var r1 = mul.Update(new TValue(time, 2.0), new TValue(time, 3.0), isNew: true);
        Assert.Equal(6.0, r1.Value, 1e-10);

        var r2 = mul.Update(new TValue(time, 4.0), new TValue(time, 3.0), isNew: false);
        Assert.Equal(12.0, r2.Value, 1e-10);
    }

    [Fact]
    public void Mul_Reset_ClearsState()
    {
        var mul = new Mul();
        mul.Update(2.0, 5.0);
        Assert.Equal(10.0, mul.Last.Value, 1e-10);

        mul.Reset();
        Assert.Equal(0.0, mul.Last.Value);
    }

    [Fact]
    public void Mul_Chaining_TimeJoin()
    {
        var a = new TSeries();
        var b = new TSeries();
        var mul = new Mul(a, b);

        var time = DateTime.UtcNow;
        a.Add(new TValue(time, 3.0), true);
        b.Add(new TValue(time, 4.0), true);

        Assert.Equal(12.0, mul.Last.Value, 1e-10);
    }

    [Fact]
    public void Mul_Static_Batch_Span()
    {
        double[] a = [2.0, 3.0, 4.0];
        double[] b = [5.0, 6.0, 7.0];
        double[] output = new double[3];

        Mul.Batch(a, b, output);

        Assert.Equal(10.0, output[0], 1e-10);
        Assert.Equal(18.0, output[1], 1e-10);
        Assert.Equal(28.0, output[2], 1e-10);
    }

    [Fact]
    public void Mul_Batch_Stream_Consistency()
    {
        var barsA = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var barsB = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var a = barsA.Close;
        var b = barsB.Close;

        var batchResult = Mul.Batch(a, b);

        var stream = new Mul();
        for (int i = 0; i < a.Count; i++)
        {
            var r = stream.Update(a[i], b[i], true);
            Assert.Equal(batchResult[i].Value, r.Value, 1e-10);
        }
    }
}
