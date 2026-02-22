using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// HEND: Henderson Moving Average
/// </summary>
/// <remarks>
/// Symmetric FIR filter from the X-11 seasonal adjustment framework that
/// preserves cubic polynomial trends without distortion. Weights are derived
/// from the closed-form Henderson formula and can be negative at edges.
///
/// Calculation: Precomputed weights via Henderson (1916) closed-form formula,
/// applied as FIR convolution over sliding window. Period must be odd >= 5.
/// </remarks>
/// <seealso href="Hend.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Hend : AbstractBase
{
    private readonly int _period;
    private readonly double[] _weights;
    private readonly RingBuffer _buffer;
    private readonly ITValuePublisher? _source;
    private readonly TValuePublishedHandler? _pubHandler;
    private bool _isNew = true;
    private bool _disposed;
    private double _lastValidValue = double.NaN;
    private double _p_lastValidValue = double.NaN;

    public bool IsNew => _isNew;
    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Creates HEND with specified period.
    /// </summary>
    /// <param name="period">Lookback period (must be odd, >= 5)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hend(int period = 7)
    {
        if (period < 5)
        {
            throw new ArgumentException("Period must be at least 5", nameof(period));
        }

        // Ensure period is odd
        _period = period % 2 == 0 ? period + 1 : period;
        Name = $"Hend({_period})";
        WarmupPeriod = _period;

        _buffer = new RingBuffer(_period);
        _weights = new double[_period];

        ComputeHendersonWeights(_weights, _period);
    }

    /// <summary>
    /// Creates HEND connected to a data source for event-based updates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hend(ITValuePublisher source, int period = 7) : this(period)
    {
        _source = source;
        _pubHandler = Handle;
        _source.Pub += _pubHandler;
    }

    /// <summary>
    /// Computes Henderson filter weights using the closed-form formula.
    /// w(k) = 315 * [(n-1)²-k²][(n²-k²)][(n+1)²-k²][3n²-16-11k²]
    ///        / {8n(n²-1)(4n²-1)(4n²-9)(4n²-25)}
    /// where n = (period+3)/2, k ranges from -(period-1)/2 to (period-1)/2.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeHendersonWeights(Span<double> weights, int period)
    {
        int half = (period - 1) / 2;
        double n = (period + 3) * 0.5;
        double n2 = n * n;
        double nm1_2 = (n - 1) * (n - 1);
        double np1_2 = (n + 1) * (n + 1);
        double denom = 8.0 * n * (n2 - 1) * (4 * n2 - 1) * (4 * n2 - 9) * (4 * n2 - 25);

        double wsum = 0.0;
        for (int i = 0; i < period; i++)
        {
            int k = i - half;
            double k2 = (double)(k * k);
            double w = 315.0 * (nm1_2 - k2) * (n2 - k2) * (np1_2 - k2) * (3 * n2 - 16 - 11 * k2) / denom;
            weights[i] = w;
            wsum += w;
        }

        // Normalize to sum=1.0 (handles floating-point drift)
        if (Math.Abs(wsum) > double.Epsilon)
        {
            double inv = 1.0 / wsum;
            for (int i = 0; i < period; i++)
            {
                weights[i] *= inv;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        _isNew = isNew;
        return Update(input, isNew, publish: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue Update(TValue input, bool isNew, bool publish)
    {
        if (isNew)
        {
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _lastValidValue = _p_lastValidValue;
        }

        double val = GetValidValue(input.Value);

        if (!double.IsFinite(val))
        {
            Last = new TValue(input.Time, double.NaN);
            if (publish) { PubEvent(Last, isNew); }
            return Last;
        }

        if (isNew)
        {
            _lastValidValue = val;
            _buffer.Add(val);

            int count = _buffer.Count;
            double result;

            if (count < _period)
            {
                // During warmup, return raw value (matching Pine behavior)
                result = val;
            }
            else
            {
                // Full window: apply Henderson FIR convolution via DotProduct
                result = ConvolveFull(_buffer, _weights);
            }

            Last = new TValue(input.Time, result);
            if (publish) { PubEvent(Last, isNew); }
            return Last;
        }
        else
        {
            // Bar correction: snapshot, compute, restore
            _buffer.Snapshot();
            double prevLast = _lastValidValue;
            double prevPLast = _p_lastValidValue;

            _lastValidValue = val;
            _buffer.UpdateNewest(val);

            int count = _buffer.Count;
            double result;

            if (count < _period)
            {
                result = val;
            }
            else
            {
                result = ConvolveFull(_buffer, _weights);
            }

            Last = new TValue(input.Time, result);

            // Restore buffer and state
            _buffer.Restore();
            _lastValidValue = prevLast;
            _p_lastValidValue = prevPLast;

            if (publish) { PubEvent(Last, isNew); }
            return Last;
        }
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
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

        // Restore state by replaying last period bars
        Reset();
        int startIndex = Math.Max(0, len - _period);
        for (int i = startIndex; i < len; i++)
        {
            Update(source[i], isNew: true, publish: false);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            return input;
        }
        return double.IsFinite(_lastValidValue) ? _lastValidValue : double.NaN;
    }

    /// <summary>
    /// FIR convolution using SIMD DotProduct over circular buffer.
    /// Weight[0] corresponds to oldest bar, Weight[period-1] to newest.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ConvolveFull(RingBuffer buffer, double[] weights)
    {
        ReadOnlySpan<double> internalBuf = buffer.InternalBuffer;
        int head = buffer.StartIndex;
        int period = buffer.Capacity;

        int part1Len = period - head;
        double sum1 = internalBuf.Slice(head, part1Len).DotProduct(weights.AsSpan(0, part1Len));
        double sum2 = internalBuf[..head].DotProduct(weights.AsSpan(part1Len));

        return sum1 + sum2;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    /// <summary>
    /// Calculates HEND from a TSeries using streaming updates.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 7)
    {
        var hend = new Hend(period);
        return hend.Update(source);
    }

    /// <summary>
    /// Calculates Henderson Moving Average over a span of values.
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="output">Output buffer (must be same length as source)</param>
    /// <param name="period">Period for weight calculation (must be odd, >= 5)</param>
    /// <param name="nanValue">Value to use for NaN substitution (default: NaN)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 7, double nanValue = double.NaN)
    {
        if (period < 5)
        {
            throw new ArgumentException("Period must be at least 5", nameof(period));
        }

        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (source.Length == 0)
        {
            return;
        }

        int usePeriod = period % 2 == 0 ? period + 1 : period;
        int len = source.Length;

        const int StackallocThreshold = 256;

        // Allocate weights
        double[]? weightsRented = usePeriod > StackallocThreshold ? ArrayPool<double>.Shared.Rent(usePeriod) : null;
        Span<double> weights = usePeriod <= StackallocThreshold
            ? stackalloc double[usePeriod]
            : weightsRented!.AsSpan(0, usePeriod);

        // Allocate ring buffer
        double[]? ringRented = usePeriod > StackallocThreshold ? ArrayPool<double>.Shared.Rent(usePeriod) : null;
        Span<double> ring = usePeriod <= StackallocThreshold
            ? stackalloc double[usePeriod]
            : ringRented!.AsSpan(0, usePeriod);

        // Allocate NaN-corrected values array
        double[]? cleanRented = len > StackallocThreshold ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> clean = len <= StackallocThreshold
            ? stackalloc double[len]
            : cleanRented!.AsSpan(0, len);

        ComputeHendersonWeights(weights, usePeriod);

        try
        {
            // Build NaN-corrected values array
            double lastValid = nanValue;
            for (int i = 0; i < len; i++)
            {
                double val = source[i];
                if (double.IsFinite(val))
                {
                    lastValid = val;
                    clean[i] = val;
                }
                else if (double.IsFinite(lastValid))
                {
                    clean[i] = lastValid;
                }
                else
                {
                    clean[i] = double.NaN;
                }
            }

            // Apply Henderson FIR convolution
            int ringIdx = 0;
            int count = 0;

            for (int i = 0; i < len; i++)
            {
                double val = clean[i];

                ring[ringIdx] = val;
                ringIdx++;
                if (ringIdx >= usePeriod)
                {
                    ringIdx = 0;
                }

                if (count < usePeriod)
                {
                    count++;
                }

                if (count < usePeriod)
                {
                    // Warmup: return raw value
                    output[i] = val;
                    continue;
                }

                // Full window: DotProduct convolution over circular buffer
                // ringIdx points to next-write = oldest entry
                int part1Len = usePeriod - ringIdx;

                ReadOnlySpan<double> ringRo = ring;
                double sum = ringRo.Slice(ringIdx, part1Len).DotProduct(weights.Slice(0, part1Len))
                           + ringRo[..ringIdx].DotProduct(weights.Slice(part1Len));

                output[i] = sum;
            }
        }
        finally
        {
            if (weightsRented != null)
            {
                ArrayPool<double>.Shared.Return(weightsRented);
            }
            if (ringRented != null)
            {
                ArrayPool<double>.Shared.Return(ringRented);
            }
            if (cleanRented != null)
            {
                ArrayPool<double>.Shared.Return(cleanRented);
            }
        }
    }

    /// <summary>
    /// Creates a HEND indicator and calculates results from source.
    /// </summary>
    public static (TSeries Results, Hend Indicator) Calculate(TSeries source, int period = 7)
    {
        var indicator = new Hend(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _lastValidValue = double.NaN;
        _p_lastValidValue = double.NaN;
        Last = default;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null && _pubHandler != null)
            {
                _source.Pub -= _pubHandler;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
