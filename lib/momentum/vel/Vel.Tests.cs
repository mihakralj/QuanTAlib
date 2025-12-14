using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class VelTests
{
    [Fact]
    public void Vel_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Vel(0));
        Assert.Throws<ArgumentException>(() => new Vel(-1));

        var vel = new Vel(10);
        Assert.NotNull(vel);
    }

    [Fact]
    public void Vel_Calc_ReturnsValue()
    {
        var vel = new Vel(10);

        Assert.Equal(0, vel.Last.Value);

        TValue result = vel.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(result.Value, vel.Last.Value);
    }

    [Fact]
    public void Vel_Calc_IsNew_AcceptsParameter()
    {
        var vel = new Vel(10);

        vel.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = vel.Last.Value;

        vel.Update(new TValue(DateTime.UtcNow, 200), isNew: true);
        double value2 = vel.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Vel_Calc_IsNew_False_UpdatesValue()
    {
        var vel = new Vel(10);

        vel.Update(new TValue(DateTime.UtcNow, 100));
        vel.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = vel.Last.Value;

        vel.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = vel.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Vel_Reset_ClearsState()
    {
        var vel = new Vel(10);

        vel.Update(new TValue(DateTime.UtcNow, 100));
        vel.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = vel.Last.Value;

        vel.Reset();

        Assert.Equal(0, vel.Last.Value);

        // After reset, should accept new values
        vel.Update(new TValue(DateTime.UtcNow, 50));
        // First value is 0 because PWMA(50) = 50 and WMA(50) = 50
        Assert.Equal(0, vel.Last.Value);
        
        vel.Update(new TValue(DateTime.UtcNow, 60));
        Assert.NotEqual(0, vel.Last.Value);
        Assert.NotEqual(valueBefore, vel.Last.Value);
    }

    [Fact]
    public void Vel_IsHot_BecomesTrueWhenBufferFull()
    {
        var vel = new Vel(5);

        Assert.False(vel.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            vel.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(vel.IsHot);
        }

        vel.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(vel.IsHot);
    }

    [Fact]
    public void Vel_CalculatesCorrectValue()
    {
        var vel = new Vel(3);

        vel.Update(new TValue(DateTime.UtcNow, 10));
        vel.Update(new TValue(DateTime.UtcNow, 20));
        vel.Update(new TValue(DateTime.UtcNow, 30));

        // PWMA(3) of 10,20,30 = 360/14 = 25.7142857...
        // WMA(3) of 10,20,30 = 140/6 = 23.3333333...
        // VEL = PWMA - WMA = 2.38095238...
        
        double expectedPwma = 360.0 / 14.0;
        double expectedWma = 140.0 / 6.0;
        double expectedVel = expectedPwma - expectedWma;

        Assert.Equal(expectedVel, vel.Last.Value, 1e-10);
    }

    [Fact]
    public void Vel_StaticCalculate_Works()
    {
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 10);
        series.Add(DateTime.UtcNow.Ticks + 1, 20);
        series.Add(DateTime.UtcNow.Ticks + 2, 30);

        var results = Vel.Calculate(series, 3);

        Assert.Equal(3, results.Count);
        
        double expectedPwma = 360.0 / 14.0;
        double expectedWma = 140.0 / 6.0;
        double expectedVel = expectedPwma - expectedWma;

        Assert.Equal(expectedVel, results.Last.Value, 1e-10);
    }

    [Fact]
    public void Vel_SpanCalc_MatchesTSeriesCalc()
    {
        var series = new TSeries();
        double[] source = new double[100];
        double[] output = new double[100];

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source[i] = bar.Close;
            series.Add(bar.Time, bar.Close);
        }

        // Calculate with TSeries API
        var tseriesResult = Vel.Calculate(series, 10);

        // Calculate with Span API
        Vel.Calculate(source.AsSpan(), output.AsSpan(), 10);

        // Compare results
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void Vel_AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        
        // 1. Batch Mode
        var batchSeries = Vel.Calculate(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Vel.Calculate(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Vel(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Vel(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert
        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 8);
        Assert.Equal(expected, eventingResult, precision: 8);
    }
}
