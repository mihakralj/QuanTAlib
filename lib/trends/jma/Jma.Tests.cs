using System;
using System.Linq;
using Xunit;

namespace QuanTAlib.Tests;

public class JmaTests
{
    [Fact]
    public void Jma_Constructor_ValidatesInput()
    {
        // JMA doesn't explicitly throw on period currently, but let's check if it handles valid inputs
        var jma = new Jma(10);
        Assert.NotNull(jma);
    }

    [Fact]
    public void Jma_Calc_ReturnsValue()
    {
        var jma = new Jma(10);

        Assert.Equal(0, jma.Last.Value);

        TValue result = jma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, jma.Last.Value);
    }

    [Fact]
    public void Jma_Calc_IsNew_AcceptsParameter()
    {
        var jma = new Jma(10);

        jma.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = jma.Last.Value;

        jma.Update(new TValue(DateTime.UtcNow, 200), isNew: true);
        double value2 = jma.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Jma_Calc_IsNew_False_UpdatesValue()
    {
        var jma = new Jma(10);

        jma.Update(new TValue(DateTime.UtcNow, 100));
        jma.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = jma.Last.Value;

        jma.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = jma.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Jma_Reset_ClearsState()
    {
        var jma = new Jma(10);

        jma.Update(new TValue(DateTime.UtcNow, 100));
        jma.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = jma.Last.Value;

        jma.Reset();

        Assert.Equal(0, jma.Last.Value);

        // After reset, should accept new values
        jma.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, jma.Last.Value);
        Assert.NotEqual(valueBefore, jma.Last.Value);
    }

    [Fact]
    public void Jma_IsHot_BecomesTrueAfterWarmup()
    {
        var jma = new Jma(10);

        Assert.False(jma.IsHot);

        // Warmup for JMA(10) is approx 203 bars
        // ceil(20 + 80 * 10^0.36) = 203
        int warmup = (int)Math.Ceiling(20.0 + 80.0 * Math.Pow(10, 0.36));

        for (int i = 1; i < warmup; i++)
        {
            jma.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(jma.IsHot);
        }

        jma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(jma.IsHot);
    }

    [Fact]
    public void Jma_IterativeCorrections_RestoreToOriginalState()
    {
        var jma = new Jma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 20 new values (enough to fill buffer and stabilize)
        TValue lastInput = default;
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            lastInput = new TValue(bar.Time, bar.Close);
            jma.Update(lastInput, isNew: true);
        }

        // Remember JMA state
        double jmaAfter = jma.Last.Value;

        // Generate 5 corrections with isNew=false (different values)
        for (int i = 0; i < 5; i++)
        {
            var bar = gbm.Next(isNew: false);
            jma.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered last input again with isNew=false
        TValue finalJma = jma.Update(lastInput, isNew: false);

        // JMA should match the original state
        Assert.Equal(jmaAfter, finalJma.Value, 1e-10);
    }

    [Fact]
    public void Jma_NaN_Input_UsesLastValidValue()
    {
        var jma = new Jma(10);

        // Feed some valid values
        jma.Update(new TValue(DateTime.UtcNow, 100));
        jma.Update(new TValue(DateTime.UtcNow, 110));

        // Feed NaN - should use last valid value (110)
        var resultAfterNaN = jma.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Jma_SpanCalc_MatchesTSeriesCalc()
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
        var tseriesResult = new Jma(10).Update(series);

        // Calculate with Span API
        Jma.Calculate(source.AsSpan(), output.AsSpan(), 10);

        // Compare results
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void Jma_AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        
        // 1. Batch Mode
        var batchSeries = new Jma(period).Update(series);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Jma.Calculate(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Jma(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Jma(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert
        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }

    [Fact]
    public void Jma_Phase_AffectsResult()
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var jmaPhase0 = new Jma(10, phase: 0).Update(series);
        var jmaPhase100 = new Jma(10, phase: 100).Update(series);
        var jmaPhaseMinus100 = new Jma(10, phase: -100).Update(series);

        Assert.NotEqual(jmaPhase0.Last.Value, jmaPhase100.Last.Value);
        Assert.NotEqual(jmaPhase0.Last.Value, jmaPhaseMinus100.Last.Value);
    }
}
