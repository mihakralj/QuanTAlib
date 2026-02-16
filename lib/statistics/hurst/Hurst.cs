using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Hurst: Hurst Exponent via Rescaled Range (R/S) analysis.
/// </summary>
/// <remarks>
/// Estimates the Hurst exponent H from a sliding window of log returns using
/// the classical R/S method with OLS log-log regression. H measures long-range
/// dependence: H &gt; 0.5 indicates persistence (trending), H &lt; 0.5 indicates
/// anti-persistence (mean-reverting), and H ≈ 0.5 indicates a random walk.
///
/// Algorithm: for each sub-period size n in [10, period/2], divide the window
/// into floor(period/n) non-overlapping blocks, compute the rescaled range R/S
/// for each block, average, then regress log(R/S) on log(n). The slope is H.
///
/// Complexity: O(period²) per update — sub-period iteration is unavoidable.
/// </remarks>
[SkipLocalsInit]
public sealed class Hurst : AbstractBase
{
    private const int MinSubPeriod = 10;
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private double _prevPrice;
    private double _prevPriceSaved;
    private double _lastValidValue;
    private bool _hasPrevPrice;
    private bool _hasPrevPriceSaved;
    private int _inputCount;
    private int _inputCountSaved;

    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Creates a new Hurst Exponent indicator.
    /// </summary>
    /// <param name="period">The lookback period for log returns (must be &gt;= 20).</param>
    public Hurst(int period)
    {
        if (period < 20)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 20 for Hurst Exponent.");
        }
        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Hurst({period})";
        WarmupPeriod = period + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double value = input.Value;

        if (!double.IsFinite(value))
        {
            value = _lastValidValue;
        }
        else
        {
            _lastValidValue = value;
        }

        if (isNew)
        {
            _prevPriceSaved = _prevPrice;
            _hasPrevPriceSaved = _hasPrevPrice;
            _inputCountSaved = _inputCount;
        }
        else
        {
            _prevPrice = _prevPriceSaved;
            _hasPrevPrice = _hasPrevPriceSaved;
            _inputCount = _inputCountSaved;
        }

        double result;
        if (!_hasPrevPrice)
        {
            _prevPrice = value;
            _hasPrevPrice = true;
            _inputCount = 1;
            result = 0.5; // default — random walk assumption before data
        }
        else
        {
            double logReturn = (_prevPrice > 0 && value > 0)
                ? Math.Log(value / _prevPrice)
                : 0.0;

            if (isNew)
            {
                _buffer.Add(logReturn);
            }
            else
            {
                _buffer.UpdateNewest(logReturn);
            }

            _prevPrice = value;
            _inputCount++;

            result = ComputeHurst(_buffer.GetSpan());
        }

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Reset running state before priming
        _buffer.Clear();
        _prevPrice = 0;
        _hasPrevPrice = false;
        _lastValidValue = 0;
        _inputCount = 0;

        // Prime the state: replay enough bars to reconstruct internal state
        int primeStart = Math.Max(0, len - _period - 1);
        for (int i = primeStart; i < len; i++)
        {
            Update(source[i]);
        }

        return new TSeries(t, v);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _prevPrice = 0;
        _prevPriceSaved = 0;
        _hasPrevPrice = false;
        _hasPrevPriceSaved = false;
        _lastValidValue = 0;
        _inputCount = 0;
        _inputCountSaved = 0;
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        DateTime ts = DateTime.MinValue;
        foreach (double value in source)
        {
            Update(new TValue(ts, value));
            if (step.HasValue)
            {
                ts = ts.Add(step.Value);
            }
        }
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var hurst = new Hurst(period);
        return hurst.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period < 20)
        {
            throw new ArgumentException("Period must be greater than or equal to 20", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        CalculateScalarCore(source, output, period);
    }

    public static (TSeries Results, Hurst Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Hurst(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        const int StackallocThreshold = 256;

        // Buffer for log returns within the window
        double[]? rentedLr = null;
        scoped Span<double> lrBuf;
        if (period <= StackallocThreshold)
        {
            lrBuf = stackalloc double[period];
        }
        else
        {
            rentedLr = ArrayPool<double>.Shared.Rent(period);
            lrBuf = rentedLr.AsSpan(0, period);
        }

        try
        {
            for (int i = 0; i < len; i++)
            {
                if (i == 0)
                {
                    output[i] = 0.5; // No log return possible yet
                    continue;
                }

                // Compute log returns for the available window
                int windowStart = Math.Max(1, i - period + 1);
                int windowLen = i - windowStart + 1;

                double prevValid = 0;
                for (int j = 0; j < windowLen; j++)
                {
                    int srcIdx = windowStart + j;
                    double cur = source[srcIdx];
                    double prev = source[srcIdx - 1];

                    if (!double.IsFinite(cur)) { cur = prevValid; }
                    else { prevValid = cur; }

                    double prevP = prev;
                    if (!double.IsFinite(prevP)) { prevP = prevValid; }

                    lrBuf[j] = (prevP > 0 && cur > 0) ? Math.Log(cur / prevP) : 0.0;
                }

                output[i] = ComputeHurst(lrBuf[..windowLen]);
            }
        }
        finally
        {
            if (rentedLr is not null)
            {
                ArrayPool<double>.Shared.Return(rentedLr);
            }
        }
    }

    /// <summary>
    /// Computes the Hurst exponent from a span of log returns using R/S analysis.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeHurst(ReadOnlySpan<double> logReturns)
    {
        int length = logReturns.Length;
        int maxN = length / 2;

        if (maxN < MinSubPeriod)
        {
            return 0.5; // Not enough data for R/S analysis
        }

        // Collect log(n) and log(R/S) pairs for OLS regression
        // Max possible pairs: maxN - MinSubPeriod + 1
        int maxPairs = maxN - MinSubPeriod + 1;

        const int StackallocThreshold = 256;
        double[]? rentedLogN = null;
        double[]? rentedLogRS = null;
        scoped Span<double> logNValues;
        scoped Span<double> logRSValues;

        if (maxPairs <= StackallocThreshold)
        {
            logNValues = stackalloc double[maxPairs];
            logRSValues = stackalloc double[maxPairs];
        }
        else
        {
            rentedLogN = ArrayPool<double>.Shared.Rent(maxPairs);
            rentedLogRS = ArrayPool<double>.Shared.Rent(maxPairs);
            logNValues = rentedLogN.AsSpan(0, maxPairs);
            logRSValues = rentedLogRS.AsSpan(0, maxPairs);
        }

        try
        {
            int pairCount = 0;

            for (int n = MinSubPeriod; n <= maxN; n++)
            {
                int numSubPeriods = length / n;
                if (numSubPeriods == 0) { continue; }

                double rsSum = 0.0;
                int validSubPeriods = 0;

                for (int sp = 0; sp < numSubPeriods; sp++)
                {
                    int startIndex = sp * n;

                    // Compute mean of sub-period
                    double subSum = 0.0;
                    for (int j = 0; j < n; j++)
                    {
                        subSum += logReturns[startIndex + j];
                    }
                    double subMean = subSum / n;

                    // Compute cumulative deviations and std dev
                    double currentSum = 0.0;
                    double varianceSum = 0.0;
                    double cumMin = double.MaxValue;
                    double cumMax = double.MinValue;

                    for (int j = 0; j < n; j++)
                    {
                        double deviation = logReturns[startIndex + j] - subMean;
                        currentSum += deviation;
                        varianceSum += deviation * deviation;

                        if (currentSum < cumMin) { cumMin = currentSum; }
                        if (currentSum > cumMax) { cumMax = currentSum; }
                    }

                    double rangeVal = cumMax - cumMin;
                    double stdDev = Math.Sqrt(varianceSum / n);

                    if (stdDev > 1e-15)
                    {
                        rsSum += rangeVal / stdDev;
                        validSubPeriods++;
                    }
                }

                if (validSubPeriods > 0)
                {
                    double avgRS = rsSum / validSubPeriods;
                    if (avgRS > 0)
                    {
                        logNValues[pairCount] = Math.Log(n);
                        logRSValues[pairCount] = Math.Log(avgRS);
                        pairCount++;
                    }
                }
            }

            if (pairCount < 2)
            {
                return 0.5; // Insufficient data points for regression
            }

            // OLS linear regression: slope of log(R/S) vs log(n)
            return OlsSlope(logNValues[..pairCount], logRSValues[..pairCount]);
        }
        finally
        {
            if (rentedLogN is not null) { ArrayPool<double>.Shared.Return(rentedLogN); }
            if (rentedLogRS is not null) { ArrayPool<double>.Shared.Return(rentedLogRS); }
        }
    }

    /// <summary>
    /// Computes the OLS slope: β = (m·Σxy - Σx·Σy) / (m·Σx² - (Σx)²)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double OlsSlope(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        int m = x.Length;
        double sumX = 0, sumY = 0, sumXY = 0, sumXSq = 0;

        for (int i = 0; i < m; i++)
        {
            double xi = x[i];
            double yi = y[i];
            sumX += xi;
            sumY += yi;
            sumXY += xi * yi;
            sumXSq += xi * xi;
        }

        double denominator = Math.FusedMultiplyAdd(m, sumXSq, -(sumX * sumX));

        if (Math.Abs(denominator) < 1e-15)
        {
            return 0.5; // Degenerate — return random walk
        }

        return Math.FusedMultiplyAdd(m, sumXY, -(sumX * sumY)) / denominator;
    }
}
