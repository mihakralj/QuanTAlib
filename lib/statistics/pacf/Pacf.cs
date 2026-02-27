using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// PACF: Partial Autocorrelation Function - Measures the correlation at lag k after
/// removing the effects of correlations at shorter lags.
/// </summary>
/// <remarks>
/// PACF is essential for time series analysis, used to:
/// - Determine the order of AR processes (AR(p) has PACF cutoff after lag p)
/// - Distinguish between AR and MA processes
/// - Identify mixed ARMA models
/// - Detect direct causal relationships at specific lags
///
/// Calculation:
/// Uses the Durbin-Levinson recursion algorithm to compute PACF efficiently.
/// The PACF at lag k (φ_kk) is the last coefficient of the AR(k) model.
///
/// Properties:
/// - φ_11 = r_1 (first PACF equals first ACF)
/// - For AR(p), PACF cuts off after lag p
/// - For MA(q), PACF decays gradually
/// - -1 ≤ φ_kk ≤ 1 for all k
///
/// Key Insight:
/// Unlike ACF which shows total correlation, PACF shows direct correlation,
/// making it crucial for identifying the true order of autoregressive processes.
/// </remarks>
[SkipLocalsInit]
public sealed class Pacf : AbstractBase
{
    private readonly int _period;
    private readonly int _lag;
    private readonly RingBuffer _buffer;

    // Running sums for O(1) mean calculation
    private double _sum;
    private double _p_sum;

    private int _updateCount;
    private const int ResyncInterval = 1000;

    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Creates a new Partial Autocorrelation Function indicator.
    /// </summary>
    /// <param name="period">The lookback period for calculating PACF (must be > lag + 1).</param>
    /// <param name="lag">The lag at which to calculate partial autocorrelation (default = 1).</param>
    public Pacf(int period, int lag = 1)
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
        Name = $"Pacf({period},{lag})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Creates a chained Partial Autocorrelation Function indicator.
    /// </summary>
    /// <param name="source">The source indicator to chain from.</param>
    /// <param name="period">The lookback period.</param>
    /// <param name="lag">The lag for partial autocorrelation.</param>
    public Pacf(ITValuePublisher source, int period, int lag = 1) : this(period, lag)
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
            _p_sum = _sum;
            _buffer.Snapshot();
        }
        else
        {
            _sum = _p_sum;
            _buffer.Restore();
        }

        // Remove oldest value if buffer is full
        if (_buffer.IsFull)
        {
            _sum -= _buffer.Oldest;
        }

        // Add new value
        _buffer.Add(value);
        _sum += value;

        if (isNew)
        {
            _updateCount++;
            if (_updateCount % ResyncInterval == 0)
            {
                Resync();
            }
        }

        // Calculate PACF using Durbin-Levinson recursion
        double pacf = CalculatePacf();

        Last = new TValue(input.Time, pacf);
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
    private double CalculatePacf()
    {
        int n = _buffer.Count;
        if (n <= _lag)
        {
            return 0;
        }

        // Calculate mean
        double mean = _sum / n;

        // Calculate ACF values for lags 1 to _lag using Durbin-Levinson
        // We need ACF values: r[1], r[2], ..., r[_lag]
        const int StackAllocThreshold = 256;
        Span<double> acf = _lag + 1 <= StackAllocThreshold
            ? stackalloc double[_lag + 1]
            : new double[_lag + 1];

        // Calculate variance (ACF at lag 0 = 1, but we need the raw variance)
        double variance = 0;
        for (int i = 0; i < n; i++)
        {
            double diff = _buffer[i] - mean;
            variance += diff * diff;
        }
        variance /= n;

        if (variance <= 0 || !double.IsFinite(variance))
        {
            return 0;
        }

        // Calculate ACF for each lag
        acf[0] = 1.0; // r[0] = 1 by definition
        for (int k = 1; k <= _lag; k++)
        {
            double autocovariance = 0;
            for (int t = k; t < n; t++)
            {
                autocovariance += (_buffer[t] - mean) * (_buffer[t - k] - mean);
            }
            autocovariance /= n;
            acf[k] = autocovariance / variance;
        }

        // Apply Durbin-Levinson recursion to get PACF at lag _lag
        return DurbinLevinson(acf, _lag);
    }

    /// <summary>
    /// Durbin-Levinson recursion algorithm to compute PACF.
    /// Returns φ_kk (the partial autocorrelation at lag k).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DurbinLevinson(ReadOnlySpan<double> acf, int targetLag)
    {
        if (targetLag == 1)
        {
            return acf[1]; // PACF at lag 1 equals ACF at lag 1
        }

        const int StackAllocThreshold = 256;
        Span<double> phi = targetLag + 1 <= StackAllocThreshold
            ? stackalloc double[targetLag + 1]
            : new double[targetLag + 1];
        Span<double> phiPrev = targetLag + 1 <= StackAllocThreshold
            ? stackalloc double[targetLag + 1]
            : new double[targetLag + 1];

        // Initialize: φ_11 = r_1
        phi[1] = acf[1];

        // Iterate for k = 2 to targetLag
        for (int k = 2; k <= targetLag; k++)
        {
            // Copy current phi to phiPrev
            phi.CopyTo(phiPrev);

            // Calculate numerator: r_k - sum(φ_{k-1,j} * r_{k-j}) for j=1 to k-1
            double numerator = acf[k];
            for (int j = 1; j < k; j++)
            {
                numerator -= phiPrev[j] * acf[k - j];
            }

            // Calculate denominator: 1 - sum(φ_{k-1,j} * r_j) for j=1 to k-1
            double denominator = 1.0;
            for (int j = 1; j < k; j++)
            {
                denominator -= phiPrev[j] * acf[j];
            }

            if (Math.Abs(denominator) < 1e-15)
            {
                return 0; // Avoid division by zero
            }

            // φ_kk = numerator / denominator
            phi[k] = numerator / denominator;

            // Update coefficients: φ_kj = φ_{k-1,j} - φ_kk * φ_{k-1,k-j}
            for (int j = 1; j < k; j++)
            {
                phi[j] = phiPrev[j] - phi[k] * phiPrev[k - j];
            }
        }

        // Return PACF at target lag, clamped to valid range
        return Math.Clamp(phi[targetLag], -1.0, 1.0);
    }

    private void Resync()
    {
        _sum = 0;
        for (int i = 0; i < _buffer.Count; i++)
        {
        _sum += _buffer[i];
        }
    }

    public override void Reset()
    {
        _buffer.Clear();
        _sum = 0;
        _p_sum = 0;
        _updateCount = 0;
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        Reset();
        foreach (double value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    /// <summary>
    /// Calculates PACF for a time series.
    /// </summary>
    public static TSeries Batch(TSeries source, int period, int lag = 1)
    {
        var pacf = new Pacf(period, lag);
        return pacf.Update(source);
    }

    /// <summary>
    /// Calculates PACF in-place using a pre-allocated output span.
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

    public static (TSeries Results, Pacf Indicator) Calculate(TSeries source, int period, int lag = 1)
    {
        var indicator = new Pacf(period, lag);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period, int lag)
    {
        int len = source.Length;

        const int StackAllocThreshold = 256;
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];
        Span<double> acf = lag + 1 <= StackAllocThreshold
            ? stackalloc double[lag + 1]
            : new double[lag + 1];

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

            // Calculate PACF for current window
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

            // Calculate ACF for lags 0 to lag
            acf[0] = 1.0;
            int effectiveStart = bufferCount < period ? 0 : bufferIndex;

            for (int k = 1; k <= lag; k++)
            {
                double autocovariance = 0;
                for (int t = k; t < bufferCount; t++)
                {
                    int currentIdx = (effectiveStart + t) % period;
                    int laggedIdx = (effectiveStart + t - k) % period;
                    autocovariance += (buffer[currentIdx] - mean) * (buffer[laggedIdx] - mean);
                }
                autocovariance /= bufferCount;
                acf[k] = autocovariance / variance;
            }

            // Apply Durbin-Levinson
            double pacfValue = DurbinLevinson(acf, lag);
            output[i] = Math.Clamp(pacfValue, -1.0, 1.0);
        }
    }
}