using System;
using System.Collections.Generic;
using Xunit;

namespace QuanTAlib;

public class AroonOscTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var aroon = new AroonOsc(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            aroon.Update(bars[i]);
        }

        Assert.True(double.IsFinite(aroon.Last.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var aroon = new AroonOsc(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            aroon.Update(bars[i]);
        }

        // Update with 100th point (isNew=true)
        aroon.Update(bars[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 10.0, bars[99].Low - 10.0, bars[99].Close, bars[99].Volume);
        var val2 = aroon.Update(modifiedBar, false);

        // Create new instance and feed up to modified
        var aroon2 = new AroonOsc(14);
        for (int i = 0; i < 99; i++)
        {
            aroon2.Update(bars[i]);
        }
        var val3 = aroon2.Update(modifiedBar, true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void Reset_Works()
    {
        var aroon = new AroonOsc(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            aroon.Update(bars[i]);
        }

        aroon.Reset();
        Assert.Equal(0, aroon.Last.Value);
        Assert.False(aroon.IsHot);

        // Feed again
        for (int i = 0; i < bars.Count; i++)
        {
            aroon.Update(bars[i]);
        }

        Assert.True(double.IsFinite(aroon.Last.Value));
    }

    [Fact]
    public void TBarSeries_Update_Matches_Streaming()
    {
        var aroon = new AroonOsc(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(aroon.Update(bars[i]).Value);
        }

        var aroon2 = new AroonOsc(14);
        var seriesResults = aroon2.Update(bars);

        Assert.Equal(streamingResults.Count, seriesResults.Count);
        for (int i = 0; i < seriesResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], seriesResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void StaticCalculate_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var aroon = new AroonOsc(14);
        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(aroon.Update(bars[i]).Value);
        }

        var staticResults = AroonOsc.Batch(bars, 14);

        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < staticResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new AroonOsc(0));
        Assert.Throws<ArgumentException>(() => new AroonOsc(-1));
    }

    [Fact]
    public void ManualCalculation_Verify()
    {
        // Simple manual test
        // Period = 2
        // Highs: 10, 12, 11
        // Lows:  8, 9, 7

        // T=0: H=10, L=8. Not enough data.
        // T=1: H=12, L=9. Not enough data.
        // T=2: H=11, L=7.
        // Window Highs: [10, 12, 11]. Max is 12 at index 1 (1 day ago).
        // Window Lows:  [8, 9, 7]. Min is 7 at index 2 (0 days ago).

        // Up = ((2 - 1) / 2) * 100 = 50
        // Down = ((2 - 0) / 2) * 100 = 100
        // Osc = 50 - 100 = -50

        var aroon = new AroonOsc(2);
        var time = DateTime.UtcNow;

        aroon.Update(new TBar(time, 10, 10, 8, 9, 100));
        aroon.Update(new TBar(time.AddMinutes(1), 11, 12, 9, 10, 100));
        var result = aroon.Update(new TBar(time.AddMinutes(2), 10, 11, 7, 8, 100));

        Assert.Equal(-50.0, result.Value, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var aroon = new AroonOsc(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 20 new values
        TBar twentiethInput = default;
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            twentiethInput = bar;
            aroon.Update(bar, isNew: true);
        }

        // Remember state after 20 values
        double stateAfterTwenty = aroon.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            aroon.Update(bar, isNew: false);
        }

        // Feed the remembered 20th input again with isNew=false
        TValue finalResult = aroon.Update(twentiethInput, isNew: false);

        // State should match the original state after 20 values
        Assert.Equal(stateAfterTwenty, finalResult.Value, 1e-10);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var aroon = new AroonOsc(5);
        var gbm = new GBM();

        Assert.False(aroon.IsHot);

        // Feed bars until IsHot becomes true
        int count = 0;
        while (!aroon.IsHot && count < 50)
        {
            var bar = gbm.Next(isNew: true);
            aroon.Update(bar, isNew: true);
            count++;
        }

        Assert.True(aroon.IsHot);
        Assert.True(count >= 5); // Should take at least period bars
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var aroon = new AroonOsc(5);
        var gbm = new GBM();
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed some valid bars first
        for (int i = 0; i < 15; i++)
        {
            aroon.Update(bars[i]);
        }

        // Create a bar with NaN values
        var nanBar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        var result = aroon.Update(nanBar);

        // Should not crash and should return a finite value
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var aroon = new AroonOsc(5);
        var gbm = new GBM();
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed some valid bars first
        for (int i = 0; i < 15; i++)
        {
            aroon.Update(bars[i]);
        }

        // Create a bar with Infinity values
        var infBar = new TBar(DateTime.UtcNow, double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var result = aroon.Update(infBar);

        // Should not crash and should return a finite value
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 14;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // 1. Batch Mode (static method)
        var batchSeries = AroonOsc.Batch(bars, period);
        double expected = batchSeries.Last.Value;

        // 2. Streaming Mode (instance, one bar at a time)
        var streamingInd = new AroonOsc(period);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingInd.Update(bars[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 3. Instance Update with TBarSeries
        var instanceInd = new AroonOsc(period);
        var instanceResult = instanceInd.Update(bars);
        double instanceValue = instanceResult.Last.Value;

        // Assert all modes produce identical results
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, instanceValue, precision: 9);
    }
}
