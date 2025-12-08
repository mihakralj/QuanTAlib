using System;
using Xunit;

namespace QuanTAlib.Tests;

public class WmaCoverageTests
{
    [Fact]
    public void Wma_ResyncLogic_IsTriggeredAndCorrect()
    {
        // ResyncInterval is 1000. We need more than that to trigger it.
        int count = 2500;
        int period = 10;
        var wma = new Wma(period);
        
        // Use a constant value to make verification easy
        // WMA of constant X is X
        double constantValue = 100.0;
        
        for (int i = 0; i < count; i++)
        {
            wma.Update(new TValue(DateTime.UtcNow, constantValue));
            
            // After warmup, value should always be constantValue
            if (i >= period)
            {
                Assert.Equal(constantValue, wma.Last.Value, 1e-9);
            }
        }
    }

    [Fact]
    public void Wma_SpanCalc_LargeDataset_TriggersResync()
    {
        // ResyncInterval is 1000.
        int count = 5000;
        int period = 10;
        double[] source = new double[count];
        double[] output = new double[count];
        
        // Fill with constant value
        for (int i = 0; i < count; i++)
        {
            source[i] = 100.0;
        }

        Wma.Calculate(source.AsSpan(), output.AsSpan(), period);

        // Verify all outputs after warmup are correct
        for (int i = period; i < count; i++)
        {
            Assert.Equal(100.0, output[i], 1e-9);
        }
    }

    [Fact]
    public void Wma_SpanCalc_SimdThreshold_Boundary()
    {
        // SimdThreshold is 256.
        // Test just below and just above to ensure both paths work
        int[] lengths = { 250, 256, 260 };
        int period = 10;

        foreach (int len in lengths)
        {
            double[] source = new double[len];
            double[] output = new double[len];
            
            for (int i = 0; i < len; i++) source[i] = 100.0;

            Wma.Calculate(source.AsSpan(), output.AsSpan(), period);

            Assert.Equal(100.0, output[^1], 1e-9);
        }
    }

    [Fact]
    public void Wma_SpanCalc_Simd_WithResync()
    {
        // This targets the SIMD loop with resync
        // Need length > SimdThreshold (256) and enough data to hit ResyncInterval (1000)
        // But wait, the SIMD loop in CalculateSimdCore handles resync internally.
        // The loop structure is:
        // while (idx < simdEnd)
        //   nextSync = Math.Min(simdEnd, idx + ResyncInterval)
        //   ... process blocks ...
        
        int count = 3000;
        int period = 5;
        double[] source = new double[count];
        double[] output = new double[count];
        
        // Use a pattern that isn't constant to verify calculation accuracy
        // Linear increase: 0, 1, 2, ...
        for (int i = 0; i < count; i++) source[i] = i;

        Wma.Calculate(source.AsSpan(), output.AsSpan(), period);

        // Verify a few points
        // WMA(5) of x-4, x-3, x-2, x-1, x
        // = (1*(x-4) + 2*(x-3) + 3*(x-2) + 4*(x-1) + 5*x) / 15
        // = (x-4 + 2x-6 + 3x-6 + 4x-4 + 5x) / 15
        // = (15x - 20) / 15
        // = x - 20/15 = x - 1.333...
        
        for (int i = period; i < count; i++)
        {
            double expected = i - (20.0 / 15.0);
            Assert.Equal(expected, output[i], 1e-9);
        }
    }

    [Fact]
    public void Wma_Update_Resync_WithFloatingPointDrift()
    {
        // This test tries to accumulate error and see if resync fixes it (or at least doesn't break it)
        // It's hard to deterministically cause drift, but we can ensure the code path is executed.
        int period = 10;
        var wma = new Wma(period);
        
        // 1200 updates to trigger resync (at 1000)
        for (int i = 0; i < 1200; i++)
        {
            wma.Update(new TValue(DateTime.UtcNow, 1.0));
        }
        
        Assert.Equal(1.0, wma.Last.Value, 1e-9);
    }

    [Fact]
    public void Wma_Constructor_ThrowsOnInvalidPeriod()
    {
        Assert.Throws<ArgumentException>(() => new Wma(0));
        Assert.Throws<ArgumentException>(() => new Wma(-1));
    }

    [Fact]
    public void Wma_StaticCalculate_ThrowsOnInvalidArgs()
    {
        double[] source = new double[10];
        double[] output = new double[5]; // Mismatch
        Assert.Throws<ArgumentException>(() => Wma.Calculate(source.AsSpan(), output.AsSpan(), 5));
        
        double[] output2 = new double[10];
        Assert.Throws<ArgumentException>(() => Wma.Calculate(source.AsSpan(), output2.AsSpan(), 0));
    }

    [Fact]
    public void Wma_Calculate_EmptyInput_DoesNothing()
    {
        Wma.Calculate(ReadOnlySpan<double>.Empty, Span<double>.Empty, 5);
        // Should not throw
    }

    [Fact]
    public void Wma_Update_WithNaN_UsesLastValid()
    {
        var wma = new Wma(5);
        wma.Update(new TValue(DateTime.UtcNow, 1.0));
        wma.Update(new TValue(DateTime.UtcNow, 2.0));
        wma.Update(new TValue(DateTime.UtcNow, double.NaN)); // Should use 2.0
        
        // Buffer: 1, 2, 2
        // WMA(3) = (1*1 + 2*2 + 3*2) / 6 = (1 + 4 + 6) / 6 = 11/6 = 1.8333...
        // Wait, period is 5.
        // Buffer: 1, 2, 2
        // Sum = 5, WSum = 1*1 + 2*2 + 3*2 = 11
        // Divisor = 3*4/2 = 6
        // Result = 11/6
        
        Assert.Equal(11.0/6.0, wma.Last.Value, 1e-9);
    }

    [Fact]
    public void Wma_Update_IsNewFalse_UpdatesLastValue()
    {
        var wma = new Wma(3);
        wma.Update(new TValue(DateTime.UtcNow, 1.0));
        wma.Update(new TValue(DateTime.UtcNow, 2.0));
        
        // Update existing with 3.0 (replaces 2.0)
        wma.Update(new TValue(DateTime.UtcNow, 3.0), isNew: false);
        
        // Buffer should be: 1, 3
        // Sum = 4, WSum = 1*1 + 2*3 = 7
        // Divisor = 2*3/2 = 3
        // Result = 7/3 = 2.333...
        
        Assert.Equal(7.0/3.0, wma.Last.Value, 1e-9);
    }

    [Fact]
    public void Wma_TSeries_Empty_ReturnsEmpty()
    {
        var wma = new Wma(5);
        var result = wma.Update(new TSeries());
        Assert.Empty(result);
    }

    [Fact]
    public void Wma_TSeries_WithNaN_RestoresStateCorrectly()
    {
        // This tests the state restoration logic in Update(TSeries)
        // specifically the loop that looks for _lastValidValue
        var wma = new Wma(3);
        var series = new TSeries();
        series.Add(new TValue(DateTime.UtcNow, 1.0));
        series.Add(new TValue(DateTime.UtcNow, 2.0));
        series.Add(new TValue(DateTime.UtcNow, double.NaN));
        series.Add(new TValue(DateTime.UtcNow, 4.0));
        
        wma.Update(series);
        
        // After processing series, internal state should match having processed these sequentially
        // Last value was 4.0. Previous valid was 2.0 (since NaN used 2.0).
        // Buffer: 2.0, 2.0 (from NaN), 4.0
        
        // Let's add one more value to verify state is correct
        wma.Update(new TValue(DateTime.UtcNow, 5.0));
        
        // Buffer: 2.0, 4.0, 5.0
        // WMA(3) = (1*2 + 2*4 + 3*5) / 6 = (2 + 8 + 15) / 6 = 25/6 = 4.1666...
        
        Assert.Equal(25.0/6.0, wma.Last.Value, 1e-9);
    }

    [Fact]
    public void Wma_Reset_ClearsState()
    {
        var wma = new Wma(3);
        wma.Update(new TValue(DateTime.UtcNow, 1.0));
        wma.Update(new TValue(DateTime.UtcNow, 2.0));
        wma.Update(new TValue(DateTime.UtcNow, 3.0));
        
        wma.Reset();
        
        Assert.Equal(0, wma.Last.Value);
        
        // Start fresh
        wma.Update(new TValue(DateTime.UtcNow, 10.0));
        // Buffer: 10
        // WMA = 10
        Assert.Equal(10.0, wma.Last.Value);
    }

    [Fact]
    public void Wma_Calculate_ScalarFallback_WithNaN()
    {
        // Force scalar path by including NaN, even with large dataset
        int count = 1000;
        double[] source = new double[count];
        double[] output = new double[count];
        
        for (int i = 0; i < count; i++) source[i] = 1.0;
        source[500] = double.NaN; // This should trigger HasNonFiniteValues -> true
        
        Wma.Calculate(source.AsSpan(), output.AsSpan(), 10);
        
        // Check around the NaN
        // Index 500 is NaN, so it uses previous valid (1.0)
        // So effectively the stream is all 1.0s
        Assert.Equal(1.0, output[500], 1e-9);
        Assert.Equal(1.0, output[501], 1e-9);
    }
    
    [Fact]
    public void Wma_Constructor_WithSource_Subscribes()
    {
        var source = new Wma(10); // Just using Wma as a publisher
        var wma = new Wma(source, 5);
        
        source.Update(new TValue(DateTime.UtcNow, 10.0));
        
        Assert.Equal(10.0, wma.Last.Value);
    }
}
