using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// HURST: Hurst Exponent
/// A measure of long-term memory of time series that relates to the
/// autocorrelations of the time series, and the rate at which these
/// decrease as the lag between pairs of values increases.
/// </summary>
/// <remarks>
/// The Hurst Exponent calculation process:
/// 1. Calculate log returns of the series
/// 2. Create subsequences of different lengths
/// 3. For each length:
///    - Calculate range (max-min) of cumulative deviations
///    - Calculate standard deviation
///    - Calculate R/S ratio
/// 4. Fit log(R/S) vs log(length) to find H
///
/// Key characteristics:
/// - H = 0.5: Random walk (Brownian motion)
/// - 0.5 < H ≤ 1.0: Trending (persistent) series
/// - 0 ≤ H < 0.5: Mean-reverting (anti-persistent) series
/// - Default minimum length is 10
/// - Default maximum length is period/2
///
/// Formula:
/// R(n)/S(n) = c * n^H
/// where:
/// R(n) = range of cumulative deviations
/// S(n) = standard deviation
/// n = subsequence length
/// H = Hurst exponent
///
/// Market Applications:
/// - Market efficiency analysis
/// - Trend strength measurement
/// - Trading strategy development
/// - Risk assessment
/// - Market regime identification
///
/// Sources:
///     H.E. Hurst (1951)
///     "Long-term Storage Capacity of Reservoirs"
///     Transactions of the American Society of Civil Engineers, 116, 770-799
///
/// Note: Returns a value between 0 and 1
/// </remarks>

[SkipLocalsInit]
public sealed class Hurst : AbstractBase
{
    private readonly int _period;
    private readonly int _minLength;
    private readonly CircularBuffer _prices;
    private readonly CircularBuffer _logReturns;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hurst(int period = 100, int minLength = 10)
    {
        if (minLength < 10)
        {
            throw new ArgumentOutOfRangeException(nameof(minLength), "Minimum length must be at least 10.");
        }
        if (period <= minLength * 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be at least twice the minimum length.");
        }

        _period = period;
        _minLength = minLength;
        WarmupPeriod = period + 1;  // Need one extra period for returns
        Name = $"HURST({_period})";
        _prices = new CircularBuffer(period);
        _logReturns = new CircularBuffer(period);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hurst(object source, int period = 100, int minLength = 10) : this(period, minLength)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prices.Clear();
        _logReturns.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Value;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static (double range, double stdDev) CalculateRangeAndStdDev(ReadOnlySpan<double> data)
    {
        int n = data.Length;
        if (n == 0) return (0, 0);

        // Calculate mean
        double mean = 0;
        for (int i = 0; i < n; i++)
        {
            mean += data[i];
        }
        mean /= n;

        // Calculate cumulative deviations and std dev
        double max = double.MinValue;
        double min = double.MaxValue;
        double sumSquaredDev = 0;
        double cumDev = 0;

        for (int i = 0; i < n; i++)
        {
            double dev = data[i] - mean;
            cumDev += dev;
            max = Math.Max(max, cumDev);
            min = Math.Min(min, cumDev);
            sumSquaredDev += dev * dev;
        }

        double range = max - min;
        double stdDev = Math.Sqrt(sumSquaredDev / n);

        return (range, stdDev);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Add price and calculate log return
        _prices.Add(BarInput.Close);
        if (_index > 1)
        {
            double logReturn = Math.Log(BarInput.Close / _prices[1]);
            _logReturns.Add(logReturn);
        }

        // Need enough values for calculation
        if (_index <= _period)
        {
            return 0.5; // Return random walk value until we have enough data
        }

        // Calculate R/S values for different lengths
        int maxLength = _period / 2;
        int numPoints = 0;
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;

        for (int length = _minLength; length <= maxLength; length *= 2)
        {
            var (range, stdDev) = CalculateRangeAndStdDev(_logReturns.GetSpan()[..length]);
            if (stdDev > 0)
            {
                double rs = range / stdDev;
                if (rs > 0)
                {
                    double x = Math.Log(length);
                    double y = Math.Log(rs);
                    sumX += x;
                    sumY += y;
                    sumXY += x * y;
                    sumX2 += x * x;
                    numPoints++;
                }
            }
        }

        // Calculate Hurst exponent using linear regression
        double hurst = 0.5; // Default to random walk
        if (numPoints > 1)
        {
            double slope = (numPoints * sumXY - sumX * sumY) / (numPoints * sumX2 - sumX * sumX);
            hurst = Math.Max(0, Math.Min(1, slope)); // Clamp between 0 and 1
        }

        IsHot = _index >= WarmupPeriod;
        return hurst;
    }
}
