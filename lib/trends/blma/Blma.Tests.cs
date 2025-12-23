using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class BlmaTests
{
    private readonly GBM _gbm;

    public BlmaTests()
    {
        _gbm = new GBM();
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Blma(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Blma(-1));
    }

    [Fact]
    public void BasicCalculation_MatchesManual()
    {
        // Period 3
        // Weights:
        // n=3
        // i=0: 0.42 - 0.5*cos(0) + 0.08*cos(0) = 0.42 - 0.5 + 0.08 = 0
        // i=1: 0.42 - 0.5*cos(pi) + 0.08*cos(2pi) = 0.42 - 0.5(-1) + 0.08(1) = 0.42 + 0.5 + 0.08 = 1.0
        // i=2: 0.42 - 0.5*cos(2pi) + 0.08*cos(4pi) = 0.42 - 0.5(1) + 0.08(1) = 0
        // Wait, Blackman window is 0 at edges.
        // So for period 3, weights are [0, 1, 0].
        // Sum = 1.
        // Weighted Sum = 0*x0 + 1*x1 + 0*x2 = x1.
        // So BLMA(3) should return the middle value?
        // Let's verify.
        
        var blma = new Blma(3);
        var input = new[] { 10.0, 20.0, 30.0 };
        
        // Bar 1: Count=1. Weights for n=1: [1]. Result = 10.
        var r1 = blma.Update(new TValue(DateTime.UtcNow, input[0]));
        Assert.Equal(10.0, r1.Value);
        
        // Bar 2: Count=2. Weights for n=2:
        // i=0: 0.42 - 0.5*cos(0) + 0.08*cos(0) = 0
        // i=1: 0.42 - 0.5*cos(2pi) + 0.08*cos(4pi) = 0
        // Wait, for n=2, invNMinus1 = 1/(2-1) = 1.
        // i=0: ratio=0. w=0.
        // i=1: ratio=1. w=0.
        // Sum=0. Division by zero?
        // Let's check CalculateWeights logic.
        // If n=2, weights are 0, 0. Sum is 0.
        // This is a known issue with Blackman window for small N if we strictly follow formula.
        // However, usually N is odd or larger.
        // But for warmup, we encounter N=2.
        // If sum is 0, result is NaN or Infinity.
        // We should check if sum is 0 and handle it?
        // Or maybe the formula handles it?
        // Let's check the code.
        // If sum is 0, we divide by 0.
        // I should add a check in CalculateWeights or Update to handle zero sum?
        // Or maybe for N=2, we should use something else?
        // PineScript implementation:
        // If total_weight is 0, inv_total is Infinity.
        // Then weights become Infinity.
        // Then result is Infinity.
        // Does PineScript handle this?
        // "int p = math.min(bar_index + 1, period)"
        // If period=2, p=2.
        // If Blackman gives 0 weights, it fails.
        // But maybe `cos(2pi)` is not exactly 1 in float?
        // No, it's mathematically 0.
        // Let's see if I need to fix this in Blma.cs.
        // I will run this test and see if it fails.
        
        var r2 = blma.Update(new TValue(DateTime.UtcNow, input[1]));
        // For N=2, weights sum to 0. Fallback to average: (10+20)/2 = 15.
        Assert.Equal(15.0, r2.Value);
        
        var r3 = blma.Update(new TValue(DateTime.UtcNow, input[2]));
        // For N=3, weights [0, 1, 0]. Sum=1. Result=20.
        Assert.Equal(20.0, r3.Value, 1e-6);
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        int period = 10;
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = new Blma(period).Update(series);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Blma.Calculate(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Blma(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // Assert
        Assert.Equal(expected, spanResult, 1e-9);
        Assert.Equal(expected, streamingResult, 1e-9);
    }

    [Fact]
    public void NaN_Handling()
    {
        var blma = new Blma(5);
        
        blma.Update(new TValue(DateTime.UtcNow, 10));
        blma.Update(new TValue(DateTime.UtcNow, 20));
        // For N=2, weights sum to 0. Fallback to average: (10+20)/2 = 15.
        
        var result = blma.Update(new TValue(DateTime.UtcNow, double.NaN));
        
        Assert.Equal(15.0, result.Value); // Should return last valid value
        Assert.Equal(15.0, blma.Last.Value); // Should retain last valid value
    }
    
    [Fact]
    public void IsNew_Behavior()
    {
        var blma = new Blma(3);
        
        // Bar 1
        blma.Update(new TValue(DateTime.UtcNow, 10), isNew: true);
        
        // Bar 2
        blma.Update(new TValue(DateTime.UtcNow, 20), isNew: true);
        
        // Bar 3 (Update)
        blma.Update(new TValue(DateTime.UtcNow, 30), isNew: true);
        var val1 = blma.Last.Value;
        
        // Bar 3 (Correction)
        blma.Update(new TValue(DateTime.UtcNow, 40), isNew: false);
        var val2 = blma.Last.Value;
        
        // For Blackman window, the newest value (index N-1) has weight 0.
        // So changing the newest value does NOT change the current result.
        Assert.Equal(val1, val2);
        
        // However, the internal buffer MUST be updated.
        // We verify this by adding a 4th bar.
        // If Bar 3 was 30, Bar 4 result would be different than if Bar 3 is 40.
        
        // Case A: Bar 3 = 40 (current state)
        blma.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        var valWith40 = blma.Last.Value;
        
        // Case B: Reconstruct scenario with Bar 3 = 30
        var blma2 = new Blma(3);
        blma2.Update(new TValue(DateTime.UtcNow, 10), isNew: true);
        blma2.Update(new TValue(DateTime.UtcNow, 20), isNew: true);
        blma2.Update(new TValue(DateTime.UtcNow, 30), isNew: true);
        blma2.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        var valWith30 = blma2.Last.Value;
        
        Assert.NotEqual(valWith30, valWith40);
    }
}
