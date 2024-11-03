using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// Median: Central Tendency Measure
/// A robust statistical measure that finds the middle value in a sorted dataset.
/// The median is less sensitive to outliers than the mean, making it particularly
/// useful for analyzing price data with extreme values.
/// </summary>
/// <remarks>
/// The Median calculation process:
/// 1. Collects values over specified period
/// 2. Sorts values in ascending order
/// 3. Finds middle value(s)
/// 4. Averages two middle values if even count
///
/// Key characteristics:
/// - Robust to outliers
/// - Always represents actual data point
/// - Splits dataset in half
/// - More stable than mean
/// - Maintains data scale
///
/// Formula:
/// For odd n: median = value at position (n+1)/2
/// For even n: median = (value at n/2 + value at (n/2)+1) / 2
///
/// Market Applications:
/// - Price distribution analysis
/// - Trend identification
/// - Outlier detection
/// - Support/resistance levels
/// - Filter extreme movements
///
/// Sources:
///     https://en.wikipedia.org/wiki/Median
///     "Statistics for Trading" - Technical Analysis of Financial Markets
///
/// Note: More robust than mean for non-normal distributions
/// </remarks>
[SkipLocalsInit]
public sealed class Median : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _buffer;

    /// <param name="period">The number of points to consider for median calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Median(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 1.");
        }
        Period = period;
        WarmupPeriod = period;
        _buffer = new CircularBuffer(period);
        Name = $"Median(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for median calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Median(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _buffer.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void QuickSort(Span<double> arr, int left, int right)
    {
        if (left < right)
        {
            int pivotIndex = Partition(arr, left, right);
            QuickSort(arr, left, pivotIndex - 1);
            QuickSort(arr, pivotIndex + 1, right);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Partition(Span<double> arr, int left, int right)
    {
        double pivot = arr[right];
        int i = left - 1;

        for (int j = left; j < right; j++)
        {
            if (arr[j] <= pivot)
            {
                i++;
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
        }

        (arr[i + 1], arr[right]) = (arr[right], arr[i + 1]);
        return i + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateMedian(Span<double> sortedValues)
    {
        int middleIndex = sortedValues.Length / 2;
        return (sortedValues.Length % 2 == 0)
            ? (sortedValues[middleIndex - 1] + sortedValues[middleIndex]) / 2.0
            : sortedValues[middleIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        double median;
        if (_index >= Period)
        {
            // Create a temporary buffer on the stack
            Span<double> values = stackalloc double[Period];
            _buffer.GetSpan().CopyTo(values);

            // Sort values in-place
            QuickSort(values, 0, values.Length - 1);

            // Calculate median based on odd/even count
            median = CalculateMedian(values);
        }
        else
        {
            // Not enough data, use average as temporary measure
            median = _buffer.Average();
        }

        IsHot = _index >= WarmupPeriod;
        return median;
    }
}
