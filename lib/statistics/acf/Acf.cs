using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ACF: Autocorrelation Function - Measures the correlation of a time series with
/// a lagged copy of itself.
/// </summary>
/// <remarks>
/// ACF is fundamental for time series analysis, used to:
/// - Identify repeating patterns or seasonal effects
/// - Determine the order of ARMA/ARIMA models
/// - Detect non-randomness in data
/// - Assess stationarity
///
/// Formula:
/// r_k = γ_k / γ_0
///
/// where:
/// γ_k = (1/n) * Σ(x_t - μ)(x_{t-k} - μ) for t = k+1 to n (autocovariance at lag k)
/// γ_0 = (1/n) * Σ(x_t - μ)² (variance, autocovariance at lag 0)
///
/// Properties:
/// - r_0 = 1 (correlation with itself at lag 0)
/// - -1 ≤ r_k ≤ 1 for all k
/// - r_k = r_{-k} (symmetry)
///
/// Key Insight:
/// For stationary processes, ACF decays towards zero as lag increases.
/// For non-stationary processes, ACF decays slowly.
/// For MA(q) processes, ACF cuts off after lag q.
/// For AR(p) processes, ACF decays exponentially or sinusoidally.
/// </remarks>
[SkipLocalsInit]
public sealed class Acf : AbstractBase
{
    private readonly int _period;
    private readonly int _lag;
    private readonly RingBuffer _buffer;

    // Running sums for O(1) updates
    private double _sum;
    private double _sumSq;
    private double _sumLagged;

    // Snapshot state for bar correction
    private double _p_sum;
    private double _p_sumSq;
    private double _p_sumLagged;

    private int _updateCount;
    private const int ResyncInterval = 1000;

    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Creates a new Autocorrelation Function indicator.
    /// </summary>
    /// <param name="period">The lookback period for calculating ACF (must be > lag + 1).</param>
    /// <param name="lag">The lag at which to calculate autocorrelation (default = 1).</param>
    public Acf(int period, int lag = 1)
    {
        if (lag < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lag), "Lag must be at least 1.");
        }

        if (period <= lag + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), $"Period must be greater than lag + 1 (currently lag = {lag}).");
        }

        _period = period;
        _lag = lag;
        _buffer = new RingBuffer(period);
        Name = $"Acf({period},{lag})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Creates a chained Autocorrelation Function indicator.
    /// </summary>
    /// <param name="source">The source indicator to chain from.</param>
    /// <param name="period">The lookback period.</param>
    /// <param name="lag">The lag for autocorrelation.</param>
    public Acf(ITValuePublisher source, int period, int lag = 1) : this(period, lag)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Pub += HandleInput;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleInput(object? sender, in TValueEventArgs e)
    {
        Update(e.Value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double value = input.Value;
        if (!double.IsFinite(value))
        {
            value = _buffer.Count > 0 ? _buffer.Newest : 0;
        }

        if (isNew)
        {
            // Snapshot state for rollback
            _p_sum = _sum;
            _p_sumSq = _sumSq;
            _p_sumLagged = _sumLagged;
            _buffer.Snapshot();
        }
        else
        {
            // Restore state from snapshot
            _sum = _p_sum;
            _sumSq = _p_sumSq;
            _sumLagged = _p_sumLagged;
            _buffer.Restore();
        }

        // Remove oldest value if buffer is full
        if (_buffer.IsFull)
        {
            double oldVal = _buffer.Oldest;
            _sum -= oldVal;
            _sumSq = Math.FusedMultiplyAdd(-oldVal, oldVal, _sumSq);

            // Remove contribution to lagged sum
            if (_buffer.Count > _lag)
            {
                double oldLaggedVal = _buffer[_lag]; // value that was _lag positions from oldest
                _sumLagged -= oldVal * oldLaggedVal;
            }
        }

        // Add new value
        _buffer.Add(value);
        _sum += value;
        _sumSq = Math.FusedMultiplyAdd(value, value, _sumSq);

        // Update lagged sum: add product of new value and value at lag positions before
        if (_buffer.Count > _lag)
        {
            int lagIndex = _buffer.Count - 1 - _lag;
            double laggedVal = _buffer[lagIndex];
            _sumLagged += value * laggedVal;
        }

        if (isNew)
        {
            _updateCount++;
            if (_updateCount % ResyncInterval == 0)
            {
                Resync();
            }
        }

        // Calculate ACF
        double acf = CalculateAcf();

        Last = new TValue(input.Time, acf);
        PubEvent(Last);
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

        Batch(source.Values, vSpan, _period, _lag);
        source.Times.CopyTo(tSpan);

        // Prime state with last 'period' values
        int primeStart = Math.Max(0, len - _period);
        for (int i = primeStart; i < len; i++)
        {
            Update(source[i]);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateAcf()
    {
        int n = _buffer.Count;
        if (n <= _lag)
        {
            return 0;
        }

        double mean = _sum / n;

        // Variance (γ_0): using sum of squares formula
        // Var = (SumSq - n * mean²) / n = SumSq/n - mean²
        double variance = (_sumSq / n) - (mean * mean);
        if (variance <= 0 || !double.IsFinite(variance))
        {
            return 0;
        }

        // Autocovariance at lag k (γ_k):
        // γ_k = (1/(n-k)) * Σ(x_t - mean)(x_{t-k} - mean)
        // = (1/(n-k)) * [Σ(x_t * x_{t-k}) - mean * Σ(x_t) - mean * Σ(x_{t-k}) + (n-k) * mean²]
        // For a sliding window, we need to be careful about which values contribute

        // Recalculate properly using the buffer
        double autocovariance = CalculateAutocovariance(mean);

        // ACF = γ_k / γ_0
        double acf = autocovariance / variance;

        // Clamp to valid range
        return Math.Clamp(acf, -1.0, 1.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateAutocovariance(double mean)
    {
        int n = _buffer.Count;
        if (n <= _lag)
        {
            return 0;
        }

        double sum = 0;

        // Σ(x_t - mean)(x_{t-k} - mean) for t = lag to n-1
        for (int t = _lag; t < n; t++)
        {
            double xt = _buffer[t];
            double xtk = _buffer[t - _lag];
            sum += (xt - mean) * (xtk - mean);
        }

        return sum / n; // Biased estimator (divide by n, not n-k, for consistency with variance)
    }

    private void Resync()
    {
        int n = _buffer.Count;
        _sum = 0;
        _sumSq = 0;
        _sumLagged = 0;

        for (int i = 0; i < n; i++)
        {
            double val = _buffer[i];
            _sum += val;
            _sumSq += val * val;

            if (i >= _lag)
            {
                _sumLagged += val * _buffer[i - _lag];
            }
        }
    }

    public override void Reset()
    {
        _buffer.Clear();
        _sum = 0;
        _sumSq = 0;
        _sumLagged = 0;
        _p_sum = 0;
        _p_sumSq = 0;
        _p_sumLagged = 0;
        _updateCount = 0;
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.UtcNow, value));
        }
    }

    /// <summary>
    /// Calculates ACF for a time series.
    /// </summary>
    public static TSeries Calculate(TSeries source, int period, int lag = 1)
    {
        var acf = new Acf(period, lag);
        return acf.Update(source);
    }

    /// <summary>
    /// Calculates ACF in-place using a pre-allocated output span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, int lag = 1)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (lag < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lag), "Lag must be at least 1.");
        }

        if (period <= lag + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), $"Period must be greater than lag + 1.");
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        CalculateScalarCore(source, output, period, lag);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period, int lag)
    {
        int len = source.Length;

        const int StackAllocThreshold = 256;
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        int bufferIndex = 0;
        int bufferCount = 0;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val))
            {
                val = bufferCount > 0 ? buffer[(bufferIndex - 1 + period) % period] : 0;
            }

            // Add to circular buffer
            if (bufferCount < period)
            {
                buffer[bufferCount] = val;
                bufferCount++;
            }
            else
            {
                buffer[bufferIndex] = val;
                bufferIndex = (bufferIndex + 1) % period;
            }

            // Calculate ACF for current window
            if (bufferCount <= lag)
            {
                output[i] = 0;
                continue;
            }

            // Calculate mean
            double sum = 0;
            for (int j = 0; j < bufferCount; j++)
            {
                sum += buffer[j];
            }

            double mean = sum / bufferCount;

            // Calculate variance
            double variance = 0;
            for (int j = 0; j < bufferCount; j++)
            {
                double diff = buffer[j] - mean;
                variance += diff * diff;
            }

            variance /= bufferCount;

            if (variance <= 0)
            {
                output[i] = 0;
                continue;
            }

            // Calculate autocovariance at lag
            double autocovariance = 0;
            int effectiveStart = bufferCount < period ? 0 : bufferIndex;

            for (int t = lag; t < bufferCount; t++)
            {
                int currentIdx = (effectiveStart + t) % period;
                int laggedIdx = (effectiveStart + t - lag) % period;
                double xt = buffer[currentIdx];
                double xtk = buffer[laggedIdx];
                autocovariance += (xt - mean) * (xtk - mean);
            }

            autocovariance /= bufferCount;

            double acf = autocovariance / variance;
            output[i] = Math.Clamp(acf, -1.0, 1.0);
        }
    }
}