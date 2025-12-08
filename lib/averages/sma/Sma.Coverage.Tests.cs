using System;
using Xunit;

namespace QuanTAlib.Tests;

public class SmaCoverageTests
{
    [Fact]
    public void Sma_ResyncLogic_IsTriggeredAndCorrect()
    {
        // ResyncInterval is 1000.
        int count = 2500;
        int period = 10;
        var sma = new Sma(period);
        
        double constantValue = 100.0;
        
        for (int i = 0; i < count; i++)
        {
            sma.Update(new TValue(DateTime.UtcNow, constantValue));
            
            if (i >= period)
            {
                Assert.Equal(constantValue, sma.Last.Value, 1e-9);
            }
        }
    }

    [Fact]
    public void Sma_SpanCalc_LargeDataset_TriggersResync()
    {
        int count = 5000;
        int period = 10;
        double[] source = new double[count];
        double[] output = new double[count];
        
        for (int i = 0; i < count; i++)
        {
            source[i] = 100.0;
        }

        Sma.Calculate(source.AsSpan(), output.AsSpan(), period);

        for (int i = period; i < count; i++)
        {
            Assert.Equal(100.0, output[i], 1e-9);
        }
    }

    [Fact]
    public void Sma_SpanCalc_SimdThreshold_Boundary()
    {
        // SimdThreshold is 256.
        int[] lengths = { 250, 256, 260 };
        int period = 10;

        foreach (int len in lengths)
        {
            double[] source = new double[len];
            double[] output = new double[len];
            
            for (int i = 0; i < len; i++) source[i] = 100.0;

            Sma.Calculate(source.AsSpan(), output.AsSpan(), period);

            Assert.Equal(100.0, output[^1], 1e-9);
        }
    }

    [Fact]
    public void Sma_SpanCalc_Simd_WithResync()
    {
        int count = 3000;
        int period = 5;
        double[] source = new double[count];
        double[] output = new double[count];
        
        // Linear increase: 0, 1, 2, ...
        for (int i = 0; i < count; i++) source[i] = i;

        Sma.Calculate(source.AsSpan(), output.AsSpan(), period);

        // SMA(5) of x-4, x-3, x-2, x-1, x
        // = (5x - 10) / 5 = x - 2
        
        for (int i = period; i < count; i++)
        {
            double expected = i - 2.0;
            Assert.Equal(expected, output[i], 1e-9);
        }
    }

    [Fact]
    public void Sma_Constructor_ThrowsOnInvalidPeriod()
    {
        Assert.Throws<ArgumentException>(() => new Sma(0));
        Assert.Throws<ArgumentException>(() => new Sma(-1));
    }

    [Fact]
    public void Sma_StaticCalculate_ThrowsOnInvalidArgs()
    {
        double[] source = new double[10];
        double[] output = new double[5]; // Mismatch
        Assert.Throws<ArgumentException>(() => Sma.Calculate(source.AsSpan(), output.AsSpan(), 5));
        
        double[] output2 = new double[10];
        Assert.Throws<ArgumentException>(() => Sma.Calculate(source.AsSpan(), output2.AsSpan(), 0));
    }

    [Fact]
    public void Sma_Calculate_EmptyInput_DoesNothing()
    {
        Sma.Calculate(ReadOnlySpan<double>.Empty, Span<double>.Empty, 5);
        // Should not throw
    }

    [Fact]
    public void Sma_Update_WithNaN_UsesLastValid()
    {
        var sma = new Sma(5);
        sma.Update(new TValue(DateTime.UtcNow, 1.0));
        sma.Update(new TValue(DateTime.UtcNow, 2.0));
        sma.Update(new TValue(DateTime.UtcNow, double.NaN)); // Should use 2.0
        
        // Buffer: 1, 2, 2
        // SMA(3) = (1 + 2 + 2) / 3 = 5/3 = 1.666...
        
        Assert.Equal(5.0/3.0, sma.Last.Value, 1e-9);
    }

    [Fact]
    public void Sma_Update_IsNewFalse_UpdatesLastValue()
    {
        var sma = new Sma(3);
        sma.Update(new TValue(DateTime.UtcNow, 1.0));
        sma.Update(new TValue(DateTime.UtcNow, 2.0));
        
        // Update existing with 3.0 (replaces 2.0)
        sma.Update(new TValue(DateTime.UtcNow, 3.0), isNew: false);
        
        // Buffer should be: 1, 3
        // SMA = (1 + 3) / 2 = 2
        
        Assert.Equal(2.0, sma.Last.Value, 1e-9);
    }

    [Fact]
    public void Sma_TSeries_Empty_ReturnsEmpty()
    {
        var sma = new Sma(5);
        var result = sma.Update(new TSeries());
        Assert.Empty(result);
    }

    [Fact]
    public void Sma_TSeries_WithNaN_RestoresStateCorrectly()
    {
        var sma = new Sma(3);
        var series = new TSeries();
        series.Add(new TValue(DateTime.UtcNow, 1.0));
        series.Add(new TValue(DateTime.UtcNow, 2.0));
        series.Add(new TValue(DateTime.UtcNow, double.NaN));
        series.Add(new TValue(DateTime.UtcNow, 4.0));
        
        sma.Update(series);
        
        // Buffer: 2.0, 2.0 (from NaN), 4.0
        // SMA(3) = (2 + 2 + 4) / 3 = 8/3 = 2.666...
        
        // Let's add one more value to verify state is correct
        sma.Update(new TValue(DateTime.UtcNow, 5.0));
        
        // Buffer: 2.0, 4.0, 5.0
        // SMA(3) = (2 + 4 + 5) / 3 = 11/3 = 3.666...
        
        Assert.Equal(11.0/3.0, sma.Last.Value, 1e-9);
    }

    [Fact]
    public void Sma_Reset_ClearsState()
    {
        var sma = new Sma(3);
        sma.Update(new TValue(DateTime.UtcNow, 1.0));
        sma.Update(new TValue(DateTime.UtcNow, 2.0));
        sma.Update(new TValue(DateTime.UtcNow, 3.0));
        
        sma.Reset();
        
        Assert.Equal(0, sma.Last.Value);
        
        // Start fresh
        sma.Update(new TValue(DateTime.UtcNow, 10.0));
        // Buffer: 10
        // SMA = 10
        Assert.Equal(10.0, sma.Last.Value);
    }

    [Fact]
    public void Sma_Calculate_ScalarFallback_WithNaN()
    {
        // Force scalar path by including NaN, even with large dataset
        int count = 1000;
        double[] source = new double[count];
        double[] output = new double[count];
        
        for (int i = 0; i < count; i++) source[i] = 1.0;
        source[500] = double.NaN; // This should trigger HasNonFiniteValues -> true
        
        Sma.Calculate(source.AsSpan(), output.AsSpan(), 10);
        
        // Check around the NaN
        // Index 500 is NaN, so it uses previous valid (1.0)
        // So effectively the stream is all 1.0s
        Assert.Equal(1.0, output[500], 1e-9);
        Assert.Equal(1.0, output[501], 1e-9);
    }
    
    [Fact]
    public void Sma_Constructor_WithSource_Subscribes()
    {
        var source = new Sma(10); // Just using Sma as a publisher
        var sma = new Sma(source, 5);
        
        source.Update(new TValue(DateTime.UtcNow, 10.0));
        
        Assert.Equal(10.0, sma.Last.Value);
    }
}
