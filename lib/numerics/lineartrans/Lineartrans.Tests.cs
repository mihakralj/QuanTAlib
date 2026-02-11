using Xunit;

namespace QuanTAlib.Tests;

public class LineartransTests
{
    private readonly GBM _gbm = new(sigma: 0.5, mu: 0.0, seed: 42);

    [Fact]
    public void Lineartrans_Constructor_DefaultParameters()
    {
        var linear = new Lineartrans();
        Assert.Equal("Lineartrans(1,0)", linear.Name);
        Assert.Equal(0, linear.WarmupPeriod);
        Assert.True(linear.IsHot);
    }

    [Fact]
    public void Lineartrans_Constructor_CustomParameters()
    {
        var linear = new Lineartrans(slope: 2.5, intercept: -10.0);
        Assert.Equal("Lineartrans(2.5,-10)", linear.Name);
    }

    [Fact]
    public void Lineartrans_Constructor_InvalidSlope_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new Lineartrans(slope: double.NaN));
        Assert.Throws<ArgumentException>(() => new Lineartrans(slope: double.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => new Lineartrans(slope: double.NegativeInfinity));
    }

    [Fact]
    public void Lineartrans_Constructor_InvalidIntercept_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new Lineartrans(slope: 1.0, intercept: double.NaN));
        Assert.Throws<ArgumentException>(() => new Lineartrans(slope: 1.0, intercept: double.PositiveInfinity));
    }

    [Fact]
    public void Lineartrans_Identity_ReturnsInputValue()
    {
        var linear = new Lineartrans(slope: 1.0, intercept: 0.0);
        var input = new TValue(DateTime.UtcNow, 100.0);
        var result = linear.Update(input);
        Assert.Equal(100.0, result.Value, 1e-10);
    }

    [Fact]
    public void Lineartrans_ScaleOnly_MultipliesValue()
    {
        var linear = new Lineartrans(slope: 2.0, intercept: 0.0);
        var input = new TValue(DateTime.UtcNow, 50.0);
        var result = linear.Update(input);
        Assert.Equal(100.0, result.Value, 1e-10);
    }

    [Fact]
    public void Lineartrans_OffsetOnly_AddsValue()
    {
        var linear = new Lineartrans(slope: 1.0, intercept: 25.0);
        var input = new TValue(DateTime.UtcNow, 75.0);
        var result = linear.Update(input);
        Assert.Equal(100.0, result.Value, 1e-10);
    }

    [Fact]
    public void Lineartrans_ScaleAndOffset_AppliesBoth()
    {
        var linear = new Lineartrans(slope: 2.0, intercept: 10.0);
        var input = new TValue(DateTime.UtcNow, 45.0);
        var result = linear.Update(input);
        // 2 * 45 + 10 = 100
        Assert.Equal(100.0, result.Value, 1e-10);
    }

    [Fact]
    public void Lineartrans_NegativeSlope_InvertsValue()
    {
        var linear = new Lineartrans(slope: -1.0, intercept: 0.0);
        var input = new TValue(DateTime.UtcNow, 50.0);
        var result = linear.Update(input);
        Assert.Equal(-50.0, result.Value, 1e-10);
    }

    [Fact]
    public void Lineartrans_ZeroSlope_ReturnsIntercept()
    {
        var linear = new Lineartrans(slope: 0.0, intercept: 42.0);
        var input = new TValue(DateTime.UtcNow, 999.0);
        var result = linear.Update(input);
        Assert.Equal(42.0, result.Value, 1e-10);
    }

    [Fact]
    public void Lineartrans_Update_HandlesNaN()
    {
        var linear = new Lineartrans(slope: 2.0, intercept: 5.0);

        // First valid value
        var valid = new TValue(DateTime.UtcNow, 10.0);
        var result1 = linear.Update(valid);
        Assert.Equal(25.0, result1.Value, 1e-10); // 2*10+5

        // NaN should return last valid
        var nan = new TValue(DateTime.UtcNow.AddSeconds(1), double.NaN);
        var result2 = linear.Update(nan);
        Assert.Equal(25.0, result2.Value, 1e-10);
    }

    [Fact]
    public void Lineartrans_Update_HandlesInfinity()
    {
        var linear = new Lineartrans(slope: 2.0, intercept: 5.0);

        var valid = new TValue(DateTime.UtcNow, 10.0);
        linear.Update(valid);

        var inf = new TValue(DateTime.UtcNow.AddSeconds(1), double.PositiveInfinity);
        var result = linear.Update(inf);
        Assert.Equal(25.0, result.Value, 1e-10); // Last valid
    }

    [Fact]
    public void Lineartrans_IsNew_True_AdvancesState()
    {
        var linear = new Lineartrans(slope: 2.0, intercept: 0.0);
        var time = DateTime.UtcNow;

        var result1 = linear.Update(new TValue(time, 10.0), isNew: true);
        Assert.Equal(20.0, result1.Value, 1e-10);

        var result2 = linear.Update(new TValue(time.AddSeconds(1), 20.0), isNew: true);
        Assert.Equal(40.0, result2.Value, 1e-10);
    }

    [Fact]
    public void Lineartrans_IsNew_False_CorrectsSameBar()
    {
        var linear = new Lineartrans(slope: 2.0, intercept: 0.0);
        var time = DateTime.UtcNow;

        var result1 = linear.Update(new TValue(time, 10.0), isNew: true);
        Assert.Equal(20.0, result1.Value, 1e-10);

        // Correct the same bar
        var result2 = linear.Update(new TValue(time, 15.0), isNew: false);
        Assert.Equal(30.0, result2.Value, 1e-10);

        // Correct again
        var result3 = linear.Update(new TValue(time, 12.0), isNew: false);
        Assert.Equal(24.0, result3.Value, 1e-10);
    }

    [Fact]
    public void Lineartrans_Reset_ClearsState()
    {
        var linear = new Lineartrans(slope: 2.0, intercept: 5.0);

        linear.Update(new TValue(DateTime.UtcNow, 10.0));
        Assert.Equal(25.0, linear.Last.Value, 1e-10);

        linear.Reset();
        Assert.Equal(0.0, linear.Last.Value);
    }

    [Fact]
    public void Lineartrans_TSeries_Update()
    {
        var linear = new Lineartrans(slope: 2.0, intercept: 10.0);
        var series = new TSeries();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            series.Add(new TValue(time.AddSeconds(i), i * 10.0), true);
        }

        var result = linear.Update(series);

        Assert.Equal(5, result.Count);
        Assert.Equal(10.0, result[0].Value, 1e-10);  // 2*0+10
        Assert.Equal(30.0, result[1].Value, 1e-10);  // 2*10+10
        Assert.Equal(50.0, result[2].Value, 1e-10);  // 2*20+10
        Assert.Equal(70.0, result[3].Value, 1e-10);  // 2*30+10
        Assert.Equal(90.0, result[4].Value, 1e-10);  // 2*40+10
    }

    [Fact]
    public void Lineartrans_Static_Calculate_TSeries()
    {
        var series = new TSeries();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 3; i++)
        {
            series.Add(new TValue(time.AddSeconds(i), 10.0 * (i + 1)), true);
        }

        var result = Lineartrans.Batch(series, slope: 0.5, intercept: 5.0);

        Assert.Equal(3, result.Count);
        Assert.Equal(10.0, result[0].Value, 1e-10);  // 0.5*10+5
        Assert.Equal(15.0, result[1].Value, 1e-10);  // 0.5*20+5
        Assert.Equal(20.0, result[2].Value, 1e-10);  // 0.5*30+5
    }

    [Fact]
    public void Lineartrans_Static_Calculate_Span()
    {
        double[] source = [10.0, 20.0, 30.0, 40.0, 50.0];
        double[] output = new double[5];

        Lineartrans.Batch(source, output, slope: 2.0, intercept: -5.0);

        Assert.Equal(15.0, output[0], 1e-10);  // 2*10-5
        Assert.Equal(35.0, output[1], 1e-10);  // 2*20-5
        Assert.Equal(55.0, output[2], 1e-10);  // 2*30-5
        Assert.Equal(75.0, output[3], 1e-10);  // 2*40-5
        Assert.Equal(95.0, output[4], 1e-10);  // 2*50-5
    }

    [Fact]
    public void Lineartrans_Static_Calculate_Span_ValidationErrors()
    {
        double[] source = [1.0, 2.0, 3.0];
        double[] output = new double[3];

        Assert.Throws<ArgumentException>(() => Lineartrans.Batch([], output));
        Assert.Throws<ArgumentException>(() => Lineartrans.Batch(source, new double[2]));
        Assert.Throws<ArgumentException>(() => Lineartrans.Batch(source, output, slope: double.NaN));
        Assert.Throws<ArgumentException>(() => Lineartrans.Batch(source, output, intercept: double.PositiveInfinity));
    }

    [Fact]
    public void Lineartrans_Chaining_Constructor()
    {
        var source = new TSeries();
        var linear = new Lineartrans(source, slope: 3.0, intercept: 1.0);

        bool eventFired = false;
        linear.Pub += (object? _, in TValueEventArgs _) => eventFired = true;

        source.Add(new TValue(DateTime.UtcNow, 10.0), true);
        Assert.True(eventFired);
        Assert.Equal(31.0, linear.Last.Value, 1e-10); // 3*10+1
    }

    [Fact]
    public void Lineartrans_Batch_Stream_Span_Consistency()
    {
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        double slope = 1.5;
        double intercept = -20.0;

        // Batch
        var batchResult = Lineartrans.Batch(series, slope, intercept);

        // Stream
        var streamIndicator = new Lineartrans(slope, intercept);
        var streamResult = new TSeries();
        for (int i = 0; i < series.Count; i++)
        {
            streamResult.Add(streamIndicator.Update(series[i], true), true);
        }

        // Span
        var spanOutput = new double[series.Count];
        Lineartrans.Batch(series.Values, spanOutput, slope, intercept);

        // Compare last 50 values
        for (int i = 50; i < series.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamResult[i].Value, 1e-10);
            Assert.Equal(batchResult[i].Value, spanOutput[i], 1e-10);
        }
    }
}
