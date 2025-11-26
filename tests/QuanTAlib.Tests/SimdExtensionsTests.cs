using System;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class SimdExtensionsTests
{
    [Fact]
    public void SumSIMD_EmptySpan_ReturnsZero()
    {
        var span = ReadOnlySpan<double>.Empty;
        Assert.Equal(0.0, span.SumSIMD());
    }

    [Fact]
    public void SumSIMD_SingleElement_ReturnsElement()
    {
        double[] data = [42.5];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(42.5, span.SumSIMD());
    }

    [Fact]
    public void SumSIMD_MultipleElements_ReturnsCorrectSum()
    {
        double[] data = [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(55.0, span.SumSIMD(), precision: 10);
    }

    [Fact]
    public void SumSIMD_LargeArray_ReturnsCorrectSum()
    {
        double[] data = new double[1000];
        for (int i = 0; i < data.Length; i++)
            data[i] = i + 1.0;
        
        var span = new ReadOnlySpan<double>(data);
        double expected = 1000.0 * 1001.0 / 2.0; // Sum of 1..1000
        Assert.Equal(expected, span.SumSIMD(), precision: 8);
    }

    [Fact]
    public void MinSIMD_EmptySpan_ReturnsNaN()
    {
        var span = ReadOnlySpan<double>.Empty;
        Assert.True(double.IsNaN(span.MinSIMD()));
    }

    [Fact]
    public void MinSIMD_SingleElement_ReturnsElement()
    {
        double[] data = [42.5];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(42.5, span.MinSIMD());
    }

    [Fact]
    public void MinSIMD_MultipleElements_ReturnsMinimum()
    {
        double[] data = [5.0, 2.0, 8.0, 1.0, 9.0, 3.0, 7.0, 4.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(1.0, span.MinSIMD());
    }

    [Fact]
    public void MaxSIMD_EmptySpan_ReturnsNaN()
    {
        var span = ReadOnlySpan<double>.Empty;
        Assert.True(double.IsNaN(span.MaxSIMD()));
    }

    [Fact]
    public void MaxSIMD_SingleElement_ReturnsElement()
    {
        double[] data = [42.5];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(42.5, span.MaxSIMD());
    }

    [Fact]
    public void MaxSIMD_MultipleElements_ReturnsMaximum()
    {
        double[] data = [5.0, 2.0, 8.0, 1.0, 9.0, 3.0, 7.0, 4.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(9.0, span.MaxSIMD());
    }

    [Fact]
    public void AverageSIMD_EmptySpan_ReturnsNaN()
    {
        var span = ReadOnlySpan<double>.Empty;
        Assert.True(double.IsNaN(span.AverageSIMD()));
    }

    [Fact]
    public void AverageSIMD_MultipleElements_ReturnsCorrectAverage()
    {
        double[] data = [1.0, 2.0, 3.0, 4.0, 5.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(3.0, span.AverageSIMD(), precision: 10);
    }

    [Fact]
    public void VarianceSIMD_LessThanTwoElements_ReturnsNaN()
    {
        double[] data = [42.5];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(double.IsNaN(span.VarianceSIMD()));
    }

    [Fact]
    public void VarianceSIMD_MultipleElements_ReturnsCorrectVariance()
    {
        double[] data = [2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0];
        var span = new ReadOnlySpan<double>(data);
        
        // Expected variance: 4.571428... (sample variance)
        double variance = span.VarianceSIMD();
        Assert.True(Math.Abs(variance - 4.571428) < 0.0001);
    }

    [Fact]
    public void StdDevSIMD_MultipleElements_ReturnsCorrectStdDev()
    {
        double[] data = [2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0];
        var span = new ReadOnlySpan<double>(data);
        
        // Expected std dev: sqrt(4.571428) ≈ 2.138
        double stdDev = span.StdDevSIMD();
        Assert.True(Math.Abs(stdDev - 2.138) < 0.01);
    }

    [Fact]
    public void MinMaxSIMD_EmptySpan_ReturnsBothNaN()
    {
        var span = ReadOnlySpan<double>.Empty;
        var (min, max) = span.MinMaxSIMD();
        Assert.True(double.IsNaN(min));
        Assert.True(double.IsNaN(max));
    }

    [Fact]
    public void MinMaxSIMD_SingleElement_ReturnsSameValue()
    {
        double[] data = [42.5];
        var span = new ReadOnlySpan<double>(data);
        var (min, max) = span.MinMaxSIMD();
        Assert.Equal(42.5, min);
        Assert.Equal(42.5, max);
    }

    [Fact]
    public void MinMaxSIMD_MultipleElements_ReturnsCorrectMinMax()
    {
        double[] data = [5.0, 2.0, 8.0, 1.0, 9.0, 3.0, 7.0, 4.0];
        var span = new ReadOnlySpan<double>(data);
        var (min, max) = span.MinMaxSIMD();
        Assert.Equal(1.0, min);
        Assert.Equal(9.0, max);
    }

    [Fact]
    public void SIMD_WorksWithTSeriesValues()
    {
        var series = new TSeries(100);
        
        for (int i = 0; i < 100; i++)
        {
            series.Add(DateTime.UtcNow.Ticks + i, i + 1.0);
        }

        var values = series.Values;
        
        double sum = values.SumSIMD();
        double avg = values.AverageSIMD();
        double min = values.MinSIMD();
        double max = values.MaxSIMD();
        var (minAlt, maxAlt) = values.MinMaxSIMD();

        Assert.Equal(5050.0, sum, precision: 8); // Sum of 1..100
        Assert.Equal(50.5, avg, precision: 8);
        Assert.Equal(1.0, min);
        Assert.Equal(100.0, max);
        Assert.Equal(min, minAlt);
        Assert.Equal(max, maxAlt);
    }

    [Fact]
    public void SIMD_WorksWithTBarSeriesClose()
    {
        var gbm = new GBM(startPrice: 100.0);
        long startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromMinutes(1);
        var bars = gbm.Fetch(1000, startTime, interval);

        var closeValues = bars.Close.Values;
        
        double sum = closeValues.SumSIMD();
        double avg = closeValues.AverageSIMD();
        double min = closeValues.MinSIMD();
        double max = closeValues.MaxSIMD();

        Assert.True(sum > 0);
        Assert.True(avg > 0);
        Assert.True(min > 0);
        Assert.True(max > min);
    }

    [Fact]
    public void SIMD_PerformanceTest_LargeDataset()
    {
        // Generate large dataset
        var gbm = new GBM(startPrice: 100.0);
        long startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromMinutes(1);
        var bars = gbm.Fetch(10000, startTime, interval);
        var closeValues = bars.Close.Values;

        // Warm up
        _ = closeValues.SumSIMD();

        // Test SIMD operations
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        double sum = closeValues.SumSIMD();
        double avg = closeValues.AverageSIMD();
        double min = closeValues.MinSIMD();
        double max = closeValues.MaxSIMD();
        var (minAlt, maxAlt) = closeValues.MinMaxSIMD();
        double variance = closeValues.VarianceSIMD();
        double stdDev = closeValues.StdDevSIMD();
        
        sw.Stop();

        // Verify results are valid
        Assert.True(sum > 0);
        Assert.True(avg > 0);
        Assert.True(min > 0);
        Assert.True(max > min);
        Assert.True(variance > 0);
        Assert.True(stdDev > 0);
        
        // Performance should be sub-millisecond for 10k elements
        Assert.True(sw.ElapsedMilliseconds < 10, 
            $"SIMD operations took {sw.ElapsedMilliseconds}ms, expected < 10ms");
    }
}
