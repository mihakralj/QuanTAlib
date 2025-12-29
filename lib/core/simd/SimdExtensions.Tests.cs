
namespace QuanTAlib.Tests;

public class SimdExtensionsTests
{
    // ContainsNonFinite tests
    [Fact]
    public void ContainsNonFinite_EmptySpan_ReturnsFalse()
    {
        var span = ReadOnlySpan<double>.Empty;
        Assert.False(span.ContainsNonFinite());
    }

    [Fact]
    public void ContainsNonFinite_AllFinite_ReturnsFalse()
    {
        double[] data = [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.False(span.ContainsNonFinite());
    }

    [Fact]
    public void ContainsNonFinite_ContainsNaN_ReturnsTrue()
    {
        double[] data = [1.0, 2.0, double.NaN, 4.0, 5.0, 6.0, 7.0, 8.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(span.ContainsNonFinite());
    }

    [Fact]
    public void ContainsNonFinite_ContainsPositiveInfinity_ReturnsTrue()
    {
        double[] data = [1.0, 2.0, 3.0, double.PositiveInfinity, 5.0, 6.0, 7.0, 8.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(span.ContainsNonFinite());
    }

    [Fact]
    public void ContainsNonFinite_ContainsNegativeInfinity_ReturnsTrue()
    {
        double[] data = [1.0, 2.0, 3.0, 4.0, double.NegativeInfinity, 6.0, 7.0, 8.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(span.ContainsNonFinite());
    }

    [Fact]
    public void ContainsNonFinite_NonFiniteInRemainder_ReturnsTrue()
    {
        double[] data = [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, double.NaN];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(span.ContainsNonFinite());
    }

    [Fact]
    public void ContainsNonFinite_SingleNaN_ReturnsTrue()
    {
        double[] data = [double.NaN];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(span.ContainsNonFinite());
    }

    [Fact]
    public void ContainsNonFinite_TwoElements_AllFinite_ReturnsFalse()
    {
        double[] data = [1.0, 2.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.False(span.ContainsNonFinite());
    }

    [Fact]
    public void ContainsNonFinite_TwoElements_OneNaN_ReturnsTrue()
    {
        double[] data = [1.0, double.NaN];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(span.ContainsNonFinite());
    }

    // SumSIMD tests
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
    public void SumSIMD_TwoElements_ReturnsSum()
    {
        double[] data = [1.5, 2.5];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(4.0, span.SumSIMD());
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
        double expected = 1000.0 * 1001.0 / 2.0;
        Assert.Equal(expected, span.SumSIMD(), precision: 8);
    }

    [Fact]
    public void SumSIMD_ContainsNaN_ReturnsNaN()
    {
        double[] data = [1.0, 2.0, double.NaN, 4.0, 5.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(double.IsNaN(span.SumSIMD()));
    }

    [Fact]
    public void SumSIMD_ContainsInfinity_ReturnsNaN()
    {
        double[] data = [1.0, 2.0, double.PositiveInfinity, 4.0, 5.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(double.IsNaN(span.SumSIMD()));
    }

    [Fact]
    public void SumSIMD_NegativeValues_ReturnsCorrectSum()
    {
        double[] data = [-1.0, -2.0, -3.0, -4.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(-10.0, span.SumSIMD());
    }

    // MinSIMD tests
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
    public void MinSIMD_TwoElements_ReturnsMinimum()
    {
        double[] data = [5.0, 2.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(2.0, span.MinSIMD());
    }

    [Fact]
    public void MinSIMD_MultipleElements_ReturnsMinimum()
    {
        double[] data = [5.0, 2.0, 8.0, 1.0, 9.0, 3.0, 7.0, 4.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(1.0, span.MinSIMD());
    }

    [Fact]
    public void MinSIMD_ContainsNaN_ReturnsNaN()
    {
        double[] data = [5.0, 2.0, double.NaN, 1.0, 9.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(double.IsNaN(span.MinSIMD()));
    }

    [Fact]
    public void MinSIMD_MinInRemainder_ReturnsCorrectMin()
    {
        double[] data = [5.0, 2.0, 8.0, 6.0, 9.0, 3.0, 7.0, 4.0, 0.5];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(0.5, span.MinSIMD());
    }

    [Fact]
    public void MinSIMD_NegativeValues_ReturnsMinimum()
    {
        double[] data = [-5.0, -2.0, -8.0, -1.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(-8.0, span.MinSIMD());
    }

    // MaxSIMD tests
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
    public void MaxSIMD_TwoElements_ReturnsMaximum()
    {
        double[] data = [5.0, 9.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(9.0, span.MaxSIMD());
    }

    [Fact]
    public void MaxSIMD_MultipleElements_ReturnsMaximum()
    {
        double[] data = [5.0, 2.0, 8.0, 1.0, 9.0, 3.0, 7.0, 4.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(9.0, span.MaxSIMD());
    }

    [Fact]
    public void MaxSIMD_ContainsNaN_ReturnsNaN()
    {
        double[] data = [5.0, 2.0, double.NaN, 1.0, 9.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(double.IsNaN(span.MaxSIMD()));
    }

    [Fact]
    public void MaxSIMD_MaxInRemainder_ReturnsCorrectMax()
    {
        double[] data = [5.0, 2.0, 8.0, 6.0, 4.0, 3.0, 7.0, 1.0, 99.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(99.0, span.MaxSIMD());
    }

    [Fact]
    public void MaxSIMD_NegativeValues_ReturnsMaximum()
    {
        double[] data = [-5.0, -2.0, -8.0, -1.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(-1.0, span.MaxSIMD());
    }

    // AverageSIMD tests
    [Fact]
    public void AverageSIMD_EmptySpan_ReturnsNaN()
    {
        var span = ReadOnlySpan<double>.Empty;
        Assert.True(double.IsNaN(span.AverageSIMD()));
    }

    [Fact]
    public void AverageSIMD_TwoElements_ReturnsAverage()
    {
        double[] data = [2.0, 4.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(3.0, span.AverageSIMD());
    }

    [Fact]
    public void AverageSIMD_MultipleElements_ReturnsCorrectAverage()
    {
        double[] data = [1.0, 2.0, 3.0, 4.0, 5.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(3.0, span.AverageSIMD(), precision: 10);
    }

    [Fact]
    public void AverageSIMD_ContainsNaN_ReturnsNaN()
    {
        double[] data = [1.0, double.NaN, 3.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(double.IsNaN(span.AverageSIMD()));
    }

    // VarianceSIMD tests
    [Fact]
    public void VarianceSIMD_LessThanTwoElements_ReturnsNaN()
    {
        double[] data = [42.5];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(double.IsNaN(span.VarianceSIMD()));
    }

    [Fact]
    public void VarianceSIMD_EmptySpan_ReturnsNaN()
    {
        var span = ReadOnlySpan<double>.Empty;
        Assert.True(double.IsNaN(span.VarianceSIMD()));
    }

    [Fact]
    public void VarianceSIMD_TwoElements_ReturnsCorrect()
    {
        double[] data = [1.0, 3.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(2.0, span.VarianceSIMD(), precision: 10);
    }

    [Fact]
    public void VarianceSIMD_ThreeElements_ReturnsCorrect()
    {
        double[] data = [1.0, 2.0, 3.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(1.0, span.VarianceSIMD(), precision: 10);
    }

    [Fact]
    public void VarianceSIMD_MultipleElements_ReturnsCorrectVariance()
    {
        double[] data = [2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0];
        var span = new ReadOnlySpan<double>(data);

        double variance = span.VarianceSIMD();
        Assert.True(Math.Abs(variance - 4.571428) < 0.0001);
    }

    [Fact]
    public void VarianceSIMD_WithProvidedMean_UsesProvidedMean()
    {
        double[] data = [2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0];
        var span = new ReadOnlySpan<double>(data);

        double mean = 5.0;
        double variance = span.VarianceSIMD(mean);

        Assert.True(variance > 0);
    }

    [Fact]
    public void VarianceSIMD_ContainsNaN_ReturnsNaN()
    {
        double[] data = [2.0, double.NaN, 4.0, 5.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(double.IsNaN(span.VarianceSIMD()));
    }

    [Fact]
    public void VarianceSIMD_WithNaNMean_ReturnsNaN()
    {
        double[] data = [2.0, 4.0, 4.0, 5.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(double.IsNaN(span.VarianceSIMD(double.NaN)));
    }

    [Fact]
    public void VarianceSIMD_WithInfinityMean_ReturnsNaN()
    {
        double[] data = [2.0, 4.0, 4.0, 5.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(double.IsNaN(span.VarianceSIMD(double.PositiveInfinity)));
    }

    // StdDevSIMD tests
    [Fact]
    public void StdDevSIMD_TwoElements_ReturnsCorrect()
    {
        double[] data = [1.0, 3.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(Math.Abs(span.StdDevSIMD() - 1.414) < 0.01);
    }

    [Fact]
    public void StdDevSIMD_MultipleElements_ReturnsCorrectStdDev()
    {
        double[] data = [2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0];
        var span = new ReadOnlySpan<double>(data);

        double stdDev = span.StdDevSIMD();
        Assert.True(Math.Abs(stdDev - 2.138) < 0.01);
    }

    [Fact]
    public void StdDevSIMD_WithProvidedMean_Works()
    {
        double[] data = [2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0];
        var span = new ReadOnlySpan<double>(data);

        double stdDev = span.StdDevSIMD(5.0);
        Assert.True(stdDev > 0);
    }

    [Fact]
    public void StdDevSIMD_ContainsNaN_ReturnsNaN()
    {
        double[] data = [2.0, double.NaN, 4.0, 5.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(double.IsNaN(span.StdDevSIMD()));
    }

    // MinMaxSIMD tests
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
    public void MinMaxSIMD_TwoElements_ReturnsCorrect()
    {
        double[] data = [5.0, 2.0];
        var span = new ReadOnlySpan<double>(data);
        var (min, max) = span.MinMaxSIMD();
        Assert.Equal(2.0, min);
        Assert.Equal(5.0, max);
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
    public void MinMaxSIMD_ContainsNaN_ReturnsBothNaN()
    {
        double[] data = [5.0, 2.0, double.NaN, 1.0, 9.0];
        var span = new ReadOnlySpan<double>(data);
        var (min, max) = span.MinMaxSIMD();
        Assert.True(double.IsNaN(min));
        Assert.True(double.IsNaN(max));
    }

    [Fact]
    public void MinMaxSIMD_MinMaxInRemainder_ReturnsCorrect()
    {
        double[] data = [5.0, 2.0, 8.0, 6.0, 4.0, 3.0, 7.0, 5.0, 0.1, 99.0];
        var span = new ReadOnlySpan<double>(data);
        var (min, max) = span.MinMaxSIMD();
        Assert.Equal(0.1, min);
        Assert.Equal(99.0, max);
    }

    [Fact]
    public void MinMaxSIMD_NegativeValues_ReturnsCorrect()
    {
        double[] data = [-5.0, -2.0, -8.0, -1.0];
        var span = new ReadOnlySpan<double>(data);
        var (min, max) = span.MinMaxSIMD();
        Assert.Equal(-8.0, min);
        Assert.Equal(-1.0, max);
    }

    // Add/Subtract tests
    [Fact]
    public void Add_SameLength_CorrectResult()
    {
        double[] left = [1.0, 2.0, 3.0, 4.0, 5.0];
        double[] right = [10.0, 20.0, 30.0, 40.0, 50.0];
        double[] result = new double[5];

        SimdExtensions.Add(left, right, result);

        Assert.Equal(11.0, result[0]);
        Assert.Equal(22.0, result[1]);
        Assert.Equal(33.0, result[2]);
        Assert.Equal(44.0, result[3]);
        Assert.Equal(55.0, result[4]);
    }

    [Fact]
    public void Add_DifferentLengths_ThrowsArgumentException()
    {
        double[] left = [1.0, 2.0];
        double[] right = [1.0];
        double[] result = new double[2];

        Assert.Throws<ArgumentException>(() => SimdExtensions.Add(left, right, result));
    }

    [Fact]
    public void Subtract_SameLength_CorrectResult()
    {
        double[] left = [10.0, 20.0, 30.0, 40.0, 50.0];
        double[] right = [1.0, 2.0, 3.0, 4.0, 5.0];
        double[] result = new double[5];

        SimdExtensions.Subtract(left, right, result);

        Assert.Equal(9.0, result[0]);
        Assert.Equal(18.0, result[1]);
        Assert.Equal(27.0, result[2]);
        Assert.Equal(36.0, result[3]);
        Assert.Equal(45.0, result[4]);
    }

    [Fact]
    public void Subtract_DifferentLengths_ThrowsArgumentException()
    {
        double[] left = [1.0, 2.0];
        double[] right = [1.0];
        double[] result = new double[2];

        Assert.Throws<ArgumentException>(() => SimdExtensions.Subtract(left, right, result));
    }

    // DotProduct tests
    [Fact]
    public void DotProduct_SameLength_CorrectResult()
    {
        double[] a = [1.0, 2.0, 3.0];
        double[] b = [4.0, 5.0, 6.0];
        // 1*4 + 2*5 + 3*6 = 4 + 10 + 18 = 32
        Assert.Equal(32.0, SimdExtensions.DotProduct(a, b));
    }

    [Fact]
    public void DotProduct_DifferentLengths_ThrowsArgumentException()
    {
        double[] a = [1.0, 2.0];
        double[] b = [1.0];
        Assert.Throws<ArgumentException>(() => SimdExtensions.DotProduct(a, b));
    }

    [Fact]
    public void DotProduct_EmptySpans_ReturnsZero()
    {
        double[] a = [];
        double[] b = [];
        Assert.Equal(0.0, SimdExtensions.DotProduct(a, b));
    }

    // Integration tests
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

        Assert.Equal(5050.0, sum, precision: 8);
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
        var gbm = new GBM(startPrice: 100.0);
        long startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromMinutes(1);
        var bars = gbm.Fetch(10000, startTime, interval);
        var closeValues = bars.Close.Values;

        _ = closeValues.SumSIMD();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        double sum = closeValues.SumSIMD();
        double avg = closeValues.AverageSIMD();
        double min = closeValues.MinSIMD();
        double max = closeValues.MaxSIMD();
        var (minAlt, maxAlt) = closeValues.MinMaxSIMD();
        double variance = closeValues.VarianceSIMD();
        double stdDev = closeValues.StdDevSIMD();

        sw.Stop();

        Assert.True(sum > 0);
        Assert.True(avg > 0);
        Assert.True(min > 0);
        Assert.True(max > min);
        Assert.Equal(min, minAlt);
        Assert.Equal(max, maxAlt);
        Assert.True(variance > 0);
        Assert.True(stdDev > 0);

        Assert.True(sw.ElapsedMilliseconds < 50,
            $"SIMD operations took {sw.ElapsedMilliseconds}ms, expected < 50ms");
    }

    [Fact]
    public void SIMD_ScalarFallback_SmallArray()
    {
        double[] data = [1.0, 2.0, 3.0];
        var span = new ReadOnlySpan<double>(data);

        Assert.Equal(6.0, span.SumSIMD());
        Assert.Equal(1.0, span.MinSIMD());
        Assert.Equal(3.0, span.MaxSIMD());
        Assert.Equal(2.0, span.AverageSIMD());

        var (min, max) = span.MinMaxSIMD();
        Assert.Equal(1.0, min);
        Assert.Equal(3.0, max);
    }
}

// Tests for internal scalar implementations
public class SimdScalarFallbackTests
{
    [Fact]
    public void ContainsNonFiniteScalar_AllFinite_ReturnsFalse()
    {
        double[] data = [1.0, 2.0, 3.0, 4.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.False(SimdExtensions.ContainsNonFiniteScalar(span));
    }

    [Fact]
    public void ContainsNonFiniteScalar_ContainsNaN_ReturnsTrue()
    {
        double[] data = [1.0, double.NaN, 3.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(SimdExtensions.ContainsNonFiniteScalar(span));
    }

    [Fact]
    public void ContainsNonFiniteScalar_ContainsInfinity_ReturnsTrue()
    {
        double[] data = [1.0, double.PositiveInfinity, 3.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.True(SimdExtensions.ContainsNonFiniteScalar(span));
    }

    [Fact]
    public void ContainsNonFiniteScalar_Empty_ReturnsFalse()
    {
        var span = ReadOnlySpan<double>.Empty;
        Assert.False(SimdExtensions.ContainsNonFiniteScalar(span));
    }

    [Fact]
    public void SumScalar_MultipleElements_ReturnsCorrectSum()
    {
        double[] data = [1.0, 2.0, 3.0, 4.0, 5.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(15.0, SimdExtensions.SumScalar(span));
    }

    [Fact]
    public void SumScalar_NegativeValues_ReturnsCorrectSum()
    {
        double[] data = [-1.0, -2.0, 3.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(0.0, SimdExtensions.SumScalar(span));
    }

    [Fact]
    public void SumScalar_Empty_ReturnsZero()
    {
        var span = ReadOnlySpan<double>.Empty;
        Assert.Equal(0.0, SimdExtensions.SumScalar(span));
    }

    [Fact]
    public void MinScalar_MultipleElements_ReturnsMinimum()
    {
        double[] data = [5.0, 2.0, 8.0, 1.0, 9.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(1.0, SimdExtensions.MinScalar(span));
    }

    [Fact]
    public void MinScalar_NegativeValues_ReturnsMinimum()
    {
        double[] data = [-5.0, -2.0, -8.0, -1.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(-8.0, SimdExtensions.MinScalar(span));
    }

    [Fact]
    public void MinScalar_SingleElement_ReturnsElement()
    {
        double[] data = [42.5];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(42.5, SimdExtensions.MinScalar(span));
    }

    [Fact]
    public void MaxScalar_MultipleElements_ReturnsMaximum()
    {
        double[] data = [5.0, 2.0, 8.0, 1.0, 9.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(9.0, SimdExtensions.MaxScalar(span));
    }

    [Fact]
    public void MaxScalar_NegativeValues_ReturnsMaximum()
    {
        double[] data = [-5.0, -2.0, -8.0, -1.0];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(-1.0, SimdExtensions.MaxScalar(span));
    }

    [Fact]
    public void MaxScalar_SingleElement_ReturnsElement()
    {
        double[] data = [42.5];
        var span = new ReadOnlySpan<double>(data);
        Assert.Equal(42.5, SimdExtensions.MaxScalar(span));
    }

    [Fact]
    public void VarianceScalar_MultipleElements_ReturnsCorrectVariance()
    {
        double[] data = [2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0];
        var span = new ReadOnlySpan<double>(data);
        double mean = 5.0;
        double variance = SimdExtensions.VarianceScalar(span, mean);
        Assert.True(Math.Abs(variance - 4.571428) < 0.0001);
    }

    [Fact]
    public void VarianceScalar_TwoElements_ReturnsCorrectVariance()
    {
        double[] data = [1.0, 3.0];
        var span = new ReadOnlySpan<double>(data);
        double mean = 2.0;
        Assert.Equal(2.0, SimdExtensions.VarianceScalar(span, mean), precision: 10);
    }

    [Fact]
    public void MinMaxScalar_MultipleElements_ReturnsCorrectMinMax()
    {
        double[] data = [5.0, 2.0, 8.0, 1.0, 9.0, 3.0, 7.0, 4.0];
        var span = new ReadOnlySpan<double>(data);
        var (min, max) = SimdExtensions.MinMaxScalar(span);
        Assert.Equal(1.0, min);
        Assert.Equal(9.0, max);
    }

    [Fact]
    public void MinMaxScalar_NegativeValues_ReturnsCorrectMinMax()
    {
        double[] data = [-5.0, -2.0, -8.0, -1.0];
        var span = new ReadOnlySpan<double>(data);
        var (min, max) = SimdExtensions.MinMaxScalar(span);
        Assert.Equal(-8.0, min);
        Assert.Equal(-1.0, max);
    }

    [Fact]
    public void MinMaxScalar_SingleElement_ReturnsSameValue()
    {
        double[] data = [42.5];
        var span = new ReadOnlySpan<double>(data);
        var (min, max) = SimdExtensions.MinMaxScalar(span);
        Assert.Equal(42.5, min);
        Assert.Equal(42.5, max);
    }
}
