using System;
using Xunit;

namespace QuanTAlib.Tests;

public class HmaCoverageTests
{
    [Fact]
    public void Hma_CalculateIntermediate_Simd_Coverage()
    {
        // CalculateIntermediate uses SIMD if length >= Vector256<double>.Count (4)
        int count = 100;
        int period = 10;
        double[] source = new double[count];
        double[] output = new double[count];
        
        for(int i=0; i<count; i++) source[i] = 100.0;
        
        Hma.Calculate(source.AsSpan(), output.AsSpan(), period);
        
        Assert.Equal(100.0, output[^1], 1e-9);
    }

    [Fact]
    public void Hma_CalculateIntermediate_Scalar_Coverage()
    {
        // Force scalar path by using small length
        int count = 3; 
        int period = 2; // Min period is 2
        double[] source = new double[count];
        double[] output = new double[count];
        
        for(int i=0; i<count; i++) source[i] = 100.0;
        
        Hma.Calculate(source.AsSpan(), output.AsSpan(), period);
        
        Assert.Equal(100.0, output[^1], 1e-9);
    }

    [Fact]
    public void Hma_Reset_ClearsState()
    {
        var hma = new Hma(10);
        hma.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(0, hma.Last.Value);
        
        hma.Reset();
        
        Assert.Equal(0, hma.Last.Value);
        Assert.False(hma.IsHot);
    }

    [Fact]
    public void Hma_UpdateSeries_RestoresState()
    {
        var hma = new Hma(10);
        var series = new TSeries();
        for(int i=0; i<20; i++) series.Add(DateTime.UtcNow.AddMinutes(i), 100.0);
        
        hma.Update(series);
        
        // After batch update, the instance state should be consistent with the end of the series
        // So next update should continue correctly
        var nextVal = hma.Update(new TValue(DateTime.UtcNow.AddMinutes(20), 100.0));
        Assert.Equal(100.0, nextVal.Value, 1e-9);
    }
    
    [Fact]
    public void Hma_Constructor_Validation()
    {
        Assert.Throws<ArgumentException>(() => new Hma(1));
        Assert.Throws<ArgumentException>(() => new Hma(0));
        
        var hma = new Hma(2);
        Assert.NotNull(hma);
    }
}
