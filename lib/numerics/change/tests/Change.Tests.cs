using Xunit;

namespace QuanTAlib.Tests;

public class ChangeTests
{
    private readonly GBM _gbm;
    private readonly TSeries _source;

    public ChangeTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 60000);
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        _source = bars.Close;
    }

    [Fact]
    public void Change_Constructor_ThrowsOnInvalidPeriod()
    {
        Assert.Throws<ArgumentException>(() => new Change(0));
        Assert.Throws<ArgumentException>(() => new Change(-1));
    }

    [Fact]
    public void Change_Constructor_ValidPeriod()
    {
        var indicator = new Change(5);
        Assert.Equal("Change(5)", indicator.Name);
        Assert.Equal(6, indicator.WarmupPeriod);
    }

    [Fact]
    public void Change_Update_ReturnsValue()
    {
        var indicator = new Change(1);
        var result = indicator.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Change_BasicCalculation()
    {
        var indicator = new Change(1);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 100.0));
        indicator.Update(new TValue(time.AddMinutes(1), 110.0));

        // (110 - 100) / 100 = 0.1
        Assert.Equal(0.1, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void Change_NegativeChange()
    {
        var indicator = new Change(1);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 100.0));
        indicator.Update(new TValue(time.AddMinutes(1), 90.0));

        // (90 - 100) / 100 = -0.1
        Assert.Equal(-0.1, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void Change_Period2()
    {
        var indicator = new Change(2);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 100.0));
        indicator.Update(new TValue(time.AddMinutes(1), 105.0));
        indicator.Update(new TValue(time.AddMinutes(2), 120.0));

        // (120 - 100) / 100 = 0.2
        Assert.Equal(0.2, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void Change_IsHot_WhenWarmedUp()
    {
        var indicator = new Change(3);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 3; i++)
        {
            Assert.False(indicator.IsHot);
            indicator.Update(new TValue(time.AddMinutes(i), 100.0 + i));
        }

        indicator.Update(new TValue(time.AddMinutes(3), 110.0));
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Change_Reset_ClearsState()
    {
        var indicator = new Change(1);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 100.0));
        indicator.Update(new TValue(time.AddMinutes(1), 110.0));
        Assert.True(indicator.IsHot);

        indicator.Reset();
        Assert.False(indicator.IsHot);
        Assert.Equal(default, indicator.Last);
    }

    [Fact]
    public void Change_IsNew_False_RollsBack()
    {
        var indicator = new Change(1);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 100.0), true);
        indicator.Update(new TValue(time.AddMinutes(1), 110.0), true);

        // Update with isNew=false (correction)
        indicator.Update(new TValue(time.AddMinutes(1), 115.0), false);

        // Should recalculate: (115 - 100) / 100 = 0.15
        Assert.Equal(0.15, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void Change_NaN_HandledGracefully()
    {
        var indicator = new Change(1);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 100.0));
        indicator.Update(new TValue(time.AddMinutes(1), double.NaN));

        // Should use last valid value (100), so (100 - 100) / 100 = 0
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Change_ZeroDivision_ReturnsZero()
    {
        var indicator = new Change(1);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 0.0));
        indicator.Update(new TValue(time.AddMinutes(1), 100.0));

        // Division by zero returns 0
        Assert.Equal(0.0, indicator.Last.Value);
    }

    [Fact]
    public void Change_Batch_MatchesStreaming()
    {
        int period = 5;
        var batchResult = Change.Batch(_source, period);
        var indicator = new Change(period);

        for (int i = 0; i < _source.Count; i++)
        {
            indicator.Update(_source[i]);
        }

        // Compare last 10 values between batch and streaming
        var streamResult = new TSeries();
        for (int j = 0; j < _source.Count; j++)
        {
            streamResult.Add(indicator.Update(_source[j]), true);
        }

        for (int i = Math.Max(0, _source.Count - 10); i < _source.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamResult[i].Value, 1e-10);
        }

        // Ensure final values match
        Assert.Equal(batchResult[^1].Value, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void Change_Span_MatchesBatch()
    {
        int period = 5;
        var values = _source.Values.ToArray();
        var output = new double[values.Length];

        Change.Batch(values, output, period);
        var batchResult = Change.Batch(_source, period);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(batchResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void Change_Span_ThrowsOnInvalidArgs()
    {
        var source = new double[10];
        var output = new double[5];

        Assert.Throws<ArgumentException>(() => Change.Batch(ReadOnlySpan<double>.Empty, output, 1));
        Assert.Throws<ArgumentException>(() => Change.Batch(source, output, 1));
        Assert.Throws<ArgumentException>(() => Change.Batch(source, new double[10], 0));
    }

    [Fact]
    public void Change_EventChaining_Works()
    {
        var source = new Sma(5);
        var change = new Change(source, 1);

        var time = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            source.Update(new TValue(time.AddMinutes(i), 100.0 + i));
        }

        Assert.True(change.IsHot);
        Assert.NotEqual(0.0, change.Last.Value);
    }
}
