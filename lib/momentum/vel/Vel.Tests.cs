using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class VelTests
{
    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Vel(0));
        Assert.Throws<ArgumentException>(() => new Vel(-1));

        var vel = new Vel(10);
        Assert.NotNull(vel);
    }

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var vel = new Vel(10);

        Assert.Equal(0, vel.Last.Value);

        TValue result = vel.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(result.Value, vel.Last.Value);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var vel = new Vel(10);
        var time = DateTime.UtcNow;

        // Update with isNew=true
        var val1 = vel.Update(new TValue(time, 100), true);

        // Update with isNew=false (same time, different value)
        vel.Update(new TValue(time, 105), false);

        // Update with isNew=false (same time, original value) - should match val1 if state rollback works
        var val3 = vel.Update(new TValue(time, 100), false);

        Assert.Equal(val1.Value, val3.Value, 1e-9);
    }

    [Fact]
    public void Reset_Works()
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
    public void IsHot_BecomesTrueWhenBufferFull()
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
    public void CalculatesCorrectValue()
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
    public void StaticBatch_Matches_Streaming()
    {
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 10);
        series.Add(DateTime.UtcNow.Ticks + 1, 20);
        series.Add(DateTime.UtcNow.Ticks + 2, 30);

        var results = Vel.Batch(series, 3);

        Assert.Equal(3, results.Count);

        double expectedPwma = 360.0 / 14.0;
        double expectedWma = 140.0 / 6.0;
        double expectedVel = expectedPwma - expectedWma;

        Assert.Equal(expectedVel, results.Last.Value, 1e-10);
    }

    [Fact]
    public void SpanBatch_Matches_Streaming()
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
        var tseriesResult = Vel.Batch(series, 10);

        // Calculate with Span API
        Vel.Batch(source.AsSpan(), output.AsSpan(), 10);

        // Compare results
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void Chainability_Works()
    {
        var vel = new Vel(10);
        var vel2 = new Vel(vel, 10);

        vel.Update(new TValue(DateTime.UtcNow, 100));
        Assert.False(double.IsNaN(vel2.Last.Value));
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var vel = new Vel(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 20 new values
        TValue twentiethInput = default;
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            twentiethInput = new TValue(bar.Time, bar.Close);
            vel.Update(twentiethInput, isNew: true);
        }

        // Remember state after 20 values
        double stateAfterTwenty = vel.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            vel.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 20th input again with isNew=false
        TValue finalResult = vel.Update(twentiethInput, isNew: false);

        // State should match the original state after 20 values
        Assert.Equal(stateAfterTwenty, finalResult.Value, 1e-10);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var vel = new Vel(5);
        var gbm = new GBM();
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed some valid values first
        for (int i = 0; i < 15; i++)
        {
            vel.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        // Feed NaN
        var result = vel.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Should not crash and should return a finite value
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var vel = new Vel(5);
        var gbm = new GBM();
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed some valid values first
        for (int i = 0; i < 15; i++)
        {
            vel.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        // Feed Infinity
        var resultPos = vel.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultPos.Value));

        var resultNeg = vel.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultNeg.Value));
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode (static method)
        var batchSeries = Vel.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode (static method with spans)
        var spanInput = series.Values.ToArray();
        var spanOutput = new double[spanInput.Length];
        Vel.Batch(spanInput.AsSpan(), spanOutput.AsSpan(), period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode (instance, one value at a time)
        var streamingInd = new Vel(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode (chained via ITValuePublisher)
        var pubSource = new TSeries();
        var eventingInd = new Vel(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert all modes produce identical results
        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }
}
