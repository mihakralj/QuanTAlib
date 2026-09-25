namespace QuanTAlib.Tests;

/// <summary>
/// Tests for ConfirmOnClose (Section 8.6 of the strategy-primitives spec): a node that
/// re-publishes a bar's settled value once, when the following bar opens, and stays silent
/// (and cold) until then.
/// </summary>
public class ConfirmOnCloseTests
{
    [Fact]
    public void Constructor_Default()
    {
        var c = new ConfirmOnClose();
        Assert.Equal("ConfirmOnClose", c.Name);
        Assert.False(c.IsHot);
        Assert.True(double.IsNaN(c.Last.Value));
    }

    [Fact]
    public void FirstBar_DoesNotPublish_StaysCold()
    {
        var c = new ConfirmOnClose();
        var time = DateTime.UtcNow;

        var r = c.Update(new TValue(time, 100.0), isNew: true);

        Assert.False(c.IsHot);
        Assert.True(double.IsNaN(r.Value));
    }

    [Fact]
    public void SecondBarOpening_ConfirmsFirstBarsSettledValue()
    {
        var c = new ConfirmOnClose();
        var t0 = DateTime.UtcNow;
        var t1 = t0.AddSeconds(1);

        c.Update(new TValue(t0, 100.0), isNew: true);
        var confirmed = c.Update(new TValue(t1, 200.0), isNew: true);

        Assert.True(c.IsHot);
        Assert.Equal(100.0, confirmed.Value, 1e-10);
        Assert.Equal(t0, confirmed.AsDateTime);
    }

    [Fact]
    public void IntrabarCorrections_AreNotPublished_OnlyTheFinalOneCounts()
    {
        var c = new ConfirmOnClose();
        var t0 = DateTime.UtcNow;
        var t1 = t0.AddSeconds(1);

        c.Update(new TValue(t0, 100.0), isNew: true);
        c.Update(new TValue(t0, 105.0), isNew: false); // forming-bar correction, not published
        c.Update(new TValue(t0, 103.0), isNew: false); // another correction, not published

        var confirmed = c.Update(new TValue(t1, 200.0), isNew: true);

        Assert.Equal(103.0, confirmed.Value, 1e-10); // last correction wins, not the first tick
    }

    [Fact]
    public void ThreeBarSequence_ConfirmsOneBarBehind()
    {
        var c = new ConfirmOnClose();
        var t0 = DateTime.UtcNow;

        var firstEmission = c.Update(new TValue(t0, 10.0), isNew: true);
        Assert.True(double.IsNaN(firstEmission.Value));

        var secondEmission = c.Update(new TValue(t0.AddSeconds(1), 20.0), isNew: true);
        Assert.Equal(10.0, secondEmission.Value, 1e-10);

        var thirdEmission = c.Update(new TValue(t0.AddSeconds(2), 30.0), isNew: true);
        Assert.Equal(20.0, thirdEmission.Value, 1e-10);
    }

    [Fact]
    public void Chaining_EventFires_OnlyOnConfirmation()
    {
        var source = new TSeries();
        var c = new ConfirmOnClose(source);

        int fireCount = 0;
        c.Pub += (object? _, in TValueEventArgs _) => fireCount++;

        var t0 = DateTime.UtcNow;
        source.Add(new TValue(t0, 100.0), true);
        Assert.Equal(0, fireCount); // first bar buffered, not confirmed yet

        source.Add(new TValue(t0, 101.0), false); // intrabar correction of the same bar
        Assert.Equal(0, fireCount);

        source.Add(new TValue(t0.AddSeconds(1), 200.0), true); // opens the next bar
        Assert.Equal(1, fireCount); // exactly one confirmation event
        Assert.Equal(101.0, c.Last.Value, 1e-10);
    }

    [Fact]
    public void Reset_ReturnsToCold()
    {
        var c = new ConfirmOnClose();
        var t0 = DateTime.UtcNow;

        c.Update(new TValue(t0, 10.0), isNew: true);
        c.Update(new TValue(t0.AddSeconds(1), 20.0), isNew: true);
        Assert.True(c.IsHot);

        c.Reset();

        Assert.False(c.IsHot);
        Assert.True(double.IsNaN(c.Last.Value));
    }

    [Fact]
    public void Reset_ThenReplay_BehavesLikeFreshInstance()
    {
        var c = new ConfirmOnClose();
        var t0 = DateTime.UtcNow;

        c.Update(new TValue(t0, 10.0), isNew: true);
        c.Update(new TValue(t0.AddSeconds(1), 20.0), isNew: true);
        c.Reset();

        var r1 = c.Update(new TValue(t0, 999.0), isNew: true);
        Assert.False(c.IsHot);
        Assert.True(double.IsNaN(r1.Value));

        var r2 = c.Update(new TValue(t0.AddSeconds(1), 888.0), isNew: true);
        Assert.True(c.IsHot);
        Assert.Equal(999.0, r2.Value, 1e-10);
    }

    [Fact]
    public void Update_TSeries_OutputIsOneBarBehindAndOneShorterThanInput()
    {
        var c = new ConfirmOnClose();
        var series = new TSeries();
        var t0 = DateTime.UtcNow;

        double[] values = [10.0, 20.0, 30.0, 40.0];
        for (int i = 0; i < values.Length; i++)
        {
            series.Add(new TValue(t0.AddSeconds(i), values[i]), true);
        }

        var result = c.Update(series);

        // Confirm-on-close is always one bar behind: the last input bar is never confirmed
        // within this batch, so the output has exactly one fewer element than the input.
        Assert.Equal(values.Length - 1, result.Count);
        for (int i = 0; i < result.Count; i++)
        {
            Assert.Equal(values[i], result[i].Value, 1e-10);
        }
    }

    [Fact]
    public void Update_TSeries_MatchesStreamingReplay()
    {
        var series = new TSeries();
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            series.Add(new TValue(t0.AddSeconds(i), 100.0 + i), true);
        }

        var batch = new ConfirmOnClose().Update(series);

        var stream = new ConfirmOnClose();
        var streamResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            var r = stream.Update(series[i], true);
            if (stream.IsHot)
            {
                streamResults.Add(r.Value);
            }
        }

        Assert.Equal(batch.Count, streamResults.Count);
        for (int i = 0; i < batch.Count; i++)
        {
            Assert.Equal(streamResults[i], batch[i].Value, 1e-10);
        }
    }
}
