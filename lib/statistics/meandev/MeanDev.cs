using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MeanDev: Mean Absolute Deviation (Average Absolute Deviation)
/// </summary>
/// <remarks>
/// Measures the average of the absolute differences between each value and the
/// arithmetic mean over a rolling window. Unlike Standard Deviation, deviations
/// are not squared, making MeanDev more robust to outliers.
///
/// Formula:
///   MD = (1/N) * Σ|xᵢ - x̄|
///   where x̄ = (1/N) * Σxᵢ
///
/// Key property (normal distribution):
///   MD ≈ sqrt(2/π) * σ ≈ 0.7979 * σ
///
/// Core component of CCI (Commodity Channel Index).
///
/// O(N) per update — the window mean changes every bar so absolute deviations
/// must be re-accumulated across the full window.
///
/// IsHot: Becomes true when the buffer reaches full period length.
/// </remarks>
[SkipLocalsInit]
public sealed class MeanDev : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private readonly TValuePublishedHandler _handler;
#pragma warning disable S2933 // _source is mutated in Dispose to release event subscription; cannot be readonly
    private ITValuePublisher? _source;
#pragma warning restore S2933
    private bool _disposed;

    // Running sum for O(1) mean computation; re-accumulated in Resync
    private double _sum;
    private double _p_sum;
    private double _lastValidValue;
    private double _p_lastValidValue;
    private int _updateCount;
    private const int ResyncInterval = 1000;

    public override bool IsHot => _buffer.IsFull;

    /// <summary>Creates a new MeanDev indicator with the specified period.</summary>
    /// <param name="period">Lookback window length. Must be >= 1.</param>
    public MeanDev(int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"MeanDev({period})";
        WarmupPeriod = period;
        _handler = Handle;
    }

    /// <summary>Creates a chaining constructor that subscribes to an upstream publisher.</summary>
    public MeanDev(ITValuePublisher source, int period) : this(period)
    {
        _source = source;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    // S4136 suppressed: Update(TSeries) overload follows immediately below — all Update overloads are adjacent
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double value = input.Value;

        // NaN/Infinity guard — substitute last valid
        if (!double.IsFinite(value))
        {
            value = _lastValidValue;
        }
        else
        {
            if (isNew)
            {
                _p_lastValidValue = _lastValidValue;
            }
            _lastValidValue = value;
        }

        if (isNew)
        {
            // Save state snapshot for rollback
            _p_sum = _sum;

            if (_buffer.IsFull)
            {
                _sum -= _buffer.Oldest;
            }

            _buffer.Add(value);
            _sum += value;

            _updateCount++;
            if (_updateCount % ResyncInterval == 0)
            {
                ResyncSum();
            }
        }
        else
        {
            // Rollback to previous state
            _lastValidValue = _p_lastValidValue;
            _sum = _p_sum;

            if (_buffer.Count > 0)
            {
                _buffer.UpdateNewest(value);
                ResyncSum();
            }
            else
            {
                _buffer.Add(value);
                _sum = value;
            }

            if (double.IsFinite(input.Value))
            {
                _lastValidValue = input.Value;
            }
        }

        double result = CalculateMeanDev();

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
        // MA0016 - List<T> required for CollectionsMarshal
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Reset and prime the streaming state from tail of source
        _buffer.Clear();
        _sum = 0;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        _updateCount = 0;

        int primeStart = Math.Max(0, len - _period);
        for (int i = primeStart; i < len; i++)
        {
            Update(source[i]);
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateMeanDev()
    {
        int n = _buffer.Count;
        if (n == 0)
        {
            return 0;
        }

        double mean = _sum / n;
        double devSum = 0;
        var span = _buffer.GetSpan();

        // O(N): must re-walk window because mean changes with every new bar
        for (int i = 0; i < span.Length; i++)
        {
            devSum += Math.Abs(span[i] - mean);
        }

        return devSum / n;
    }

    private void ResyncSum()
    {
        double sum = 0;
        var span = _buffer.GetSpan();
        for (int i = 0; i < span.Length; i++)
        {
            sum += span[i];
        }
        _sum = sum;
    }

    /// <summary>Creates a MeanDev from a TSeries source and returns result series.</summary>
    public static TSeries Batch(TSeries source, int period)
    {
        var md = new MeanDev(period);
        return md.Update(source);
    }

    /// <summary>Span-based batch calculation. Output length must equal source length.</summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }

        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        CalculateScalarCore(source, output, period);
    }

    public static (TSeries Results, MeanDev Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new MeanDev(period);
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
        _sum = 0;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        _updateCount = 0;

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
        _sum = 0;
        _p_sum = 0;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        _updateCount = 0;
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

        // Sanitize NaN/Infinity using last-valid substitution so sliding-window
        // removal uses exactly the same value that was originally accumulated
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

            double sum = 0;
            int i = 0;

            // Warmup phase: growing window
            int warmupEnd = Math.Min(period, len);
            for (; i < warmupEnd; i++)
            {
                sum += sanitized[i];
                double n = i + 1;
                double mean = sum / n;
                double devSum = 0;
                for (int k = 0; k <= i; k++)
                {
                    devSum += Math.Abs(sanitized[k] - mean);
                }
                output[i] = devSum / n;
            }

            // Sliding window phase: full period
            for (; i < len; i++)
            {
                sum = sum - sanitized[i - period] + sanitized[i];
                double mean = sum / period;
                double devSum = 0;
                int start = i - period + 1;
                for (int k = start; k <= i; k++)
                {
                    devSum += Math.Abs(sanitized[k] - mean);
                }
                output[i] = devSum / period;
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
}
