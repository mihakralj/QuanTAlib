using Xunit;

namespace QuanTAlib.Tests;

public class AddTests
{
    private readonly GBM _gbm = new(sigma: 0.5, mu: 0.0, seed: 42);

    [Fact]
    public void Add_Constructor_Default()
    {
        var add = new Add();
        Assert.Equal("Add", add.Name);
        Assert.False(add.IsHot); // hot after the first bar (period = 1 under the hood)
        add.Update(1.0, 1.0);
        Assert.True(add.IsHot);
    }

    [Fact]
    public void Add_Update_ComputesSum()
    {
        var add = new Add();
        var time = DateTime.UtcNow;
        var result = add.Update(new TValue(time, 3.0), new TValue(time, 4.0));
        Assert.Equal(7.0, result.Value, 1e-10);
    }

    [Fact]
    public void Add_Update_HandlesNaNInA()
    {
        var add = new Add();
        var time = DateTime.UtcNow;
        add.Update(new TValue(time, 10.0), new TValue(time, 1.0));

        var result = add.Update(new TValue(time.AddSeconds(1), double.NaN), new TValue(time.AddSeconds(1), 2.0));
        // a substituted with last valid (10.0)
        Assert.Equal(12.0, result.Value, 1e-10);
    }

    [Fact]
    public void Add_Update_HandlesInfinityInB()
    {
        var add = new Add();
        var time = DateTime.UtcNow;
        add.Update(new TValue(time, 1.0), new TValue(time, 5.0));

        var result = add.Update(new TValue(time.AddSeconds(1), 2.0), new TValue(time.AddSeconds(1), double.PositiveInfinity));
        Assert.Equal(7.0, result.Value, 1e-10); // 2 + last valid b (5.0)
    }

    [Fact]
    public void Add_IsNew_False_CorrectsSameBar()
    {
        var add = new Add();
        var time = DateTime.UtcNow;

        var r1 = add.Update(new TValue(time, 1.0), new TValue(time, 2.0), isNew: true);
        Assert.Equal(3.0, r1.Value, 1e-10);

        var r2 = add.Update(new TValue(time, 5.0), new TValue(time, 2.0), isNew: false);
        Assert.Equal(7.0, r2.Value, 1e-10);
    }

    [Fact]
    public void Add_IsNew_False_DoesNotLeakIntoNextBar()
    {
        var add = new Add();
        var time = DateTime.UtcNow;

        add.Update(new TValue(time, 1.0), new TValue(time, 1.0), isNew: true);
        add.Update(new TValue(time, 100.0), new TValue(time, 100.0), isNew: false); // forming tick, corrected away below
        add.Update(new TValue(time, 1.0), new TValue(time, 1.0), isNew: false);     // back to original

        var next = add.Update(new TValue(time.AddSeconds(1), 2.0), new TValue(time.AddSeconds(1), 3.0), isNew: true);
        Assert.Equal(5.0, next.Value, 1e-10);
    }

    [Fact]
    public void Add_Reset_ClearsState()
    {
        var add = new Add();
        add.Update(10.0, 5.0);
        Assert.Equal(15.0, add.Last.Value, 1e-10);

        add.Reset();
        Assert.Equal(0.0, add.Last.Value);
    }

    [Fact]
    public void Add_Chaining_TimeJoin_EmitsOnceBothSourcesReport()
    {
        var a = new TSeries();
        var b = new TSeries();
        var add = new Add(a, b);

        int fireCount = 0;
        add.Pub += (object? _, in TValueEventArgs _) => fireCount++;

        var time = DateTime.UtcNow;
        a.Add(new TValue(time, 10.0), true);
        Assert.Equal(0, fireCount); // b hasn't reported for this bar yet

        b.Add(new TValue(time, 5.0), true);
        Assert.Equal(1, fireCount);
        Assert.Equal(15.0, add.Last.Value, 1e-10);
    }

    [Fact]
    public void Add_Chaining_TimeJoin_NewerBarDropsUnmatchedHalf()
    {
        var a = new TSeries();
        var b = new TSeries();
        var add = new Add(a, b);

        var t0 = DateTime.UtcNow;
        var t1 = t0.AddSeconds(1);

        a.Add(new TValue(t0, 1.0), true);   // a reports for t0, b has not yet
        a.Add(new TValue(t1, 2.0), true);   // a reports for t1 before b caught up on t0
        b.Add(new TValue(t1, 3.0), true);   // now both have reported for t1

        Assert.Equal(5.0, add.Last.Value, 1e-10);
    }

    [Fact]
    public void Add_Static_Batch_Series()
    {
        var a = new TSeries();
        var b = new TSeries();
        var time = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            a.Add(new TValue(time.AddSeconds(i), i), true);
            b.Add(new TValue(time.AddSeconds(i), i * 2.0), true);
        }

        var result = Add.Batch(a, b);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(i + (i * 2.0), result[i].Value, 1e-10);
        }
    }

    [Fact]
    public void Add_Static_Batch_Span()
    {
        double[] a = [1.0, 2.0, 3.0];
        double[] b = [10.0, 20.0, 30.0];
        double[] output = new double[3];

        Add.Batch(a, b, output);

        Assert.Equal(11.0, output[0], 1e-10);
        Assert.Equal(22.0, output[1], 1e-10);
        Assert.Equal(33.0, output[2], 1e-10);
    }

    [Fact]
    public void Add_Batch_Stream_Consistency()
    {
        var barsA = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var barsB = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var a = barsA.Close;
        var b = barsB.Close;

        var batchResult = Add.Batch(a, b);

        var stream = new Add();
        for (int i = 0; i < a.Count; i++)
        {
            var r = stream.Update(a[i], b[i], true);
            Assert.Equal(batchResult[i].Value, r.Value, 1e-10);
        }
    }
}
