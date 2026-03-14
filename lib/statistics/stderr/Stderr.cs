using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Stderr: Standard Error of Regression (Standard Error of the Estimate)
/// </summary>
/// <remarks>
/// Measures the typical distance that observed values fall from the OLS
/// regression line fitted to the rolling window. Equivalent to the root mean
/// square of the residuals, scaled by N-2 degrees of freedom (one per
/// regression coefficient: slope and intercept).
/// Uses Kahan compensated summation for numerical stability of running regression sums,
/// eliminating the need for periodic resynchronization.
///
/// Formula:
///   SE = sqrt( SSR / (N - 2) )
///   SSR = Σ(yᵢ - ŷᵢ)²  where  ŷᵢ = slope * xᵢ + intercept
///   slope     = (N·Σxy - Σx·Σy) / (N·Σx² - (Σx)²)
///   intercept = (Σy - slope·Σx) / N
///   x values: 0, 1, …, N-1  (oldest=0, newest=N-1)
///
/// Period minimum is 3 to allow N-2 > 0.
///
/// The regression sums Σy and Σxy use O(1) updates identical to LinReg:
///   ΔΣxy = Σy_prev - N * oldest   (when window is full)
///
/// The residual sum SSR requires an O(N) walk; there is no known O(1) update
/// that remains numerically stable for arbitrary inputs.
///
/// IsHot: Becomes true when the buffer reaches full period length.
/// </remarks>
[SkipLocalsInit]
public sealed class Stderr : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private readonly TValuePublishedHandler _handler;
#pragma warning disable S2933 // _source is mutated in Dispose to release event subscription; cannot be readonly
    private ITValuePublisher? _source;
#pragma warning restore S2933
    private bool _disposed;

    // O(1) running regression sums with Kahan compensation
    private double _sumY;
    private double _sumXY;
    private double _p_sumY;
    private double _p_sumXY;
    private double _sumYComp;      // Kahan compensation for _sumY
    private double _sumXYComp;     // Kahan compensation for _sumXY
    private double _p_sumYComp;
    private double _p_sumXYComp;
    private double _lastVal;
    private double _p_lastVal;
    private double _lastValidValue;
    private double _p_lastValidValue;

    // Precomputed constants (depend only on period)
    private readonly double _sumX;   // 0+1+…+(N-1) = N(N-1)/2
    private readonly double _sumX2;  // 0²+…+(N-1)² = (N-1)N(2N-1)/6
    private readonly double _denom;  // N·Σx² - (Σx)²

    public override bool IsHot => _buffer.IsFull;

    /// <summary>Creates a new Stderr indicator with the specified period.</summary>
    /// <param name="period">Lookback window length. Must be >= 3.</param>
    public Stderr(int period)
    {
        if (period < 3)
        {
            throw new ArgumentException("Period must be at least 3.", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Stderr({period})";
        WarmupPeriod = period;
        _handler = Handle;

        // Precompute fixed regression constants
        _sumX = 0.5 * period * (period - 1);
        _sumX2 = (period - 1.0) * period * (2.0 * period - 1.0) / 6.0;
        _denom = period * _sumX2 - _sumX * _sumX;
    }

    /// <summary>Creates a chaining constructor that subscribes to an upstream publisher.</summary>
    public Stderr(ITValuePublisher source, int period) : this(period)
    {
        _source = source;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _lastValidValue = input;
            return input;
        }
        return _lastValidValue;
    }

    // S4136 suppressed: Update(TSeries) overload follows immediately — all Update overloads are adjacent
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            double val = GetValidValue(input.Value);
            UpdateStateNew(val);
            _p_sumY = _sumY;
            _p_sumXY = _sumXY;
            _p_sumYComp = _sumYComp;
            _p_sumXYComp = _sumXYComp;
            _p_lastVal = _lastVal;
            _p_lastValidValue = _lastValidValue;
            _lastVal = val;
        }
        else
        {
            _lastValidValue = _p_lastValidValue;
            double val = GetValidValue(input.Value);

            // Restore compensations
            _sumYComp = _p_sumYComp;
            _sumXYComp = _p_sumXYComp;

            // Correct running sums for newest bar change
            _sumY = _p_sumY - _p_lastVal + val;
            _sumXY = _p_sumXY - (_period - 1) * (_p_lastVal - val);
            // Re-derive sumXY correctly via recalculation to avoid drift on bar corrections
            if (_buffer.Count > 0)
            {
                _buffer.UpdateNewest(val);
                RecalculateSums();
            }
            else
            {
                _buffer.Add(val);
                _sumY = val;
                _sumYComp = 0;
                _sumXY = 0;
                _sumXYComp = 0;
            }

            _lastVal = val;
        }

        double result = CalculateStderr();

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    // Update(TSeries) placed adjacent to Update(TValue) per S4136
    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        // MA0016 — List<T> required for CollectionsMarshal
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Reset and prime streaming state from tail
        _buffer.Clear();
        _sumY = 0;
        _sumXY = 0;
        _sumYComp = 0;
        _sumXYComp = 0;
        _lastVal = 0;
        _lastValidValue = 0;
        _p_lastValidValue = 0;

        int primeStart = Math.Max(0, len - _period);
        for (int i = primeStart; i < len; i++)
        {
            Update(source[i]);
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateStateNew(double val)
    {
        if (_buffer.IsFull)
        {
            double oldest = _buffer.Oldest;
            double prevSumY = _sumY;

            // O(1) update for sumXY with Kahan compensation
            // ΣXY_new = ΣXY_old - ΣY_old + oldest + (N-1)*val
            {
                double delta = -prevSumY + oldest + (_period - 1) * val;
                double y = delta - _sumXYComp;
                double t = _sumXY + y;
                _sumXYComp = (t - _sumXY) - y;
                _sumXY = t;
            }

            // O(1) update for sumY with Kahan compensation
            {
                double delta = val - oldest;
                double y = delta - _sumYComp;
                double t = _sumY + y;
                _sumYComp = (t - _sumY) - y;
                _sumY = t;
            }
        }
        else
        {
            _buffer.Add(val);

            // Kahan add val to sumY
            {
                double y = val - _sumYComp;
                double t = _sumY + y;
                _sumYComp = (t - _sumY) - y;
                _sumY = t;
            }

            // Recalculate sumXY from scratch during warmup (buffer not yet full)
            _sumXY = 0;
            _sumXYComp = 0;
            var span = _buffer.GetSpan();
            for (int i = 0; i < span.Length; i++)
            {
                // x=0 is oldest (index 0 in ordered span), x=count-1 is newest
                _sumXY = Math.FusedMultiplyAdd(i, span[i], _sumXY);
            }
            return;
        }

        _buffer.Add(val);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateStderr()
    {
        int n = _buffer.Count;
        if (n < 3)
        {
            return 0;
        }

        double sumY = _sumY;
        double sumXY = _sumXY;
        double sumX = (n == _period) ? _sumX : 0.5 * n * (n - 1);
        double sumX2 = (n == _period) ? _sumX2 : (n - 1.0) * n * (2.0 * n - 1.0) / 6.0;
        double denom = (n == _period) ? _denom : n * sumX2 - sumX * sumX;

        if (denom == 0)
        {
            return 0;
        }

        double slope = (n * sumXY - sumX * sumY) / denom;
        double intercept = (sumY - slope * sumX) / n;

        // O(N): accumulate residual sum of squares
        double ssr = 0;
        var span = _buffer.GetSpan();
        for (int i = 0; i < span.Length; i++)
        {
            double predicted = Math.FusedMultiplyAdd(slope, i, intercept);
            double residual = span[i] - predicted;
            ssr = Math.FusedMultiplyAdd(residual, residual, ssr);
        }

        return Math.Sqrt(ssr / (n - 2.0));
    }

    private void RecalculateSums()
    {
        _sumY = 0;
        _sumYComp = 0;
        _sumXY = 0;
        _sumXYComp = 0;
        var span = _buffer.GetSpan();
        for (int i = 0; i < span.Length; i++)
        {
            // Kahan add to sumY
            double y = span[i] - _sumYComp;
            double t = _sumY + y;
            _sumYComp = (t - _sumY) - y;
            _sumY = t;

            _sumXY = Math.FusedMultiplyAdd(i, span[i], _sumXY);
        }
    }

    /// <summary>Creates a Stderr from a TSeries source and returns result series.</summary>
    public static TSeries Batch(TSeries source, int period)
    {
        var se = new Stderr(period);
        return se.Update(source);
    }

    /// <summary>Span-based batch calculation. Output length must equal source length.</summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }

        if (period < 3)
        {
            throw new ArgumentException("Period must be at least 3.", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        CalculateScalarCore(source, output, period);
    }

    public static (TSeries Results, Stderr Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Stderr(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _buffer.Clear();
        _sumY = 0;
        _sumXY = 0;
        _sumYComp = 0;
        _sumXYComp = 0;
        _lastVal = 0;
        _lastValidValue = 0;
        _p_lastValidValue = 0;

        int warmupLength = Math.Min(source.Length, WarmupPeriod);
        int startIndex = source.Length - warmupLength;

        for (int i = startIndex; i < source.Length; i++)
        {
            Update(new TValue(DateTime.MinValue, source[i]));
        }
    }

    public override void Reset()
    {
        _buffer.Clear();
        _sumY = 0;
        _sumXY = 0;
        _p_sumY = 0;
        _p_sumXY = 0;
        _sumYComp = 0;
        _sumXYComp = 0;
        _p_sumYComp = 0;
        _p_sumXYComp = 0;
        _lastVal = 0;
        _p_lastVal = 0;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Last = default;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null)
            {
                _source.Pub -= _handler;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }

    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;

        const int StackallocThreshold = 256;
        double[]? rented = null;
        scoped Span<double> sanitized;
        if (len <= StackallocThreshold)
        {
            sanitized = stackalloc double[len];
        }
        else
        {
            rented = ArrayPool<double>.Shared.Rent(len);
            sanitized = rented.AsSpan(0, len);
        }

        try
        {
            double lastValid = 0;
            for (int j = 0; j < len; j++)
            {
                double val = source[j];
                if (!double.IsFinite(val))
                {
                    val = lastValid;
                }
                else
                {
                    lastValid = val;
                }
                sanitized[j] = val;
            }

            // Precompute constants for full period window
            double sumXFull = 0.5 * period * (period - 1);
            double sumX2Full = (period - 1.0) * period * (2.0 * period - 1.0) / 6.0;
            double denomFull = period * sumX2Full - sumXFull * sumXFull;

            double sumY = 0;
            double sumXY = 0;
            double sumYComp = 0;     // Kahan compensation for sumY
            double sumXYComp = 0;    // Kahan compensation for sumXY
            int i = 0;

            // Warmup: growing window, recompute sums from scratch each bar
            int warmupEnd = Math.Min(period, len);
            for (; i < warmupEnd; i++)
            {
                // Kahan add to sumY
                {
                    double y = sanitized[i] - sumYComp;
                    double t = sumY + y;
                    sumYComp = (t - sumY) - y;
                    sumY = t;
                }

                // Recalculate sumXY with new element appended (oldest=0, newest=i)
                sumXY = 0;
                for (int k = 0; k <= i; k++)
                {
                    sumXY = Math.FusedMultiplyAdd(k, sanitized[k], sumXY);
                }

                int n = i + 1;
                output[i] = (n >= 3) ? CalcStderrFromSums(sanitized, 0, n, sumY, sumXY) : 0;
            }

            // Reset compensation at transition to sliding window
            sumXYComp = 0;

            // Sliding window: O(1) sum updates + O(N) residuals
            for (; i < len; i++)
            {
                double oldest = sanitized[i - period];
                double newest = sanitized[i];

                // O(1) Kahan compensated update for sumXY
                {
                    double delta = -sumY + oldest + (period - 1) * newest;
                    double y = delta - sumXYComp;
                    double t = sumXY + y;
                    sumXYComp = (t - sumXY) - y;
                    sumXY = t;
                }

                // O(1) Kahan compensated update for sumY
                {
                    double delta = newest - oldest;
                    double y = delta - sumYComp;
                    double t = sumY + y;
                    sumYComp = (t - sumY) - y;
                    sumY = t;
                }

                double slope = (period * sumXY - sumXFull * sumY) / denomFull;
                double intercept = (sumY - slope * sumXFull) / period;

                double ssr = 0;
                int start = i - period + 1;
                for (int k = 0; k < period; k++)
                {
                    double predicted = Math.FusedMultiplyAdd(slope, k, intercept);
                    double residual = sanitized[start + k] - predicted;
                    ssr = Math.FusedMultiplyAdd(residual, residual, ssr);
                }

                output[i] = Math.Sqrt(ssr / (period - 2.0));
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }
    }

    private static double CalcStderrFromSums(ReadOnlySpan<double> sanitized, int start, int n,
        double sumY, double sumXY)
    {
        if (n < 3)
        {
            return 0;
        }

        double sumX = 0.5 * n * (n - 1);
        double sumX2 = (n - 1.0) * n * (2.0 * n - 1.0) / 6.0;
        double denom = n * sumX2 - sumX * sumX;

        if (denom == 0)
        {
            return 0;
        }

        double slope = (n * sumXY - sumX * sumY) / denom;
        double intercept = (sumY - slope * sumX) / n;

        double ssr = 0;
        for (int k = 0; k < n; k++)
        {
            double predicted = Math.FusedMultiplyAdd(slope, k, intercept);
            double residual = sanitized[start + k] - predicted;
            ssr = Math.FusedMultiplyAdd(residual, residual, ssr);
        }

        return Math.Sqrt(ssr / (n - 2.0));
    }
}
