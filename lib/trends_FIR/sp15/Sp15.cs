using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// SP15: Spencer 15-Point Moving Average
/// </summary>
/// <remarks>
/// Fixed-coefficient symmetric FIR filter designed by John Spencer (1904) for
/// seasonal adjustment. The 15 weights [-3,-6,-5,3,21,46,67,74,67,46,21,3,-5,-6,-3]/320
/// zero out periodicities at 4 and 5 bars, preserving polynomial trends up to degree 3.
/// Negative edge weights give bandpass-like characteristics.
///
/// Calculation: Compile-time constant weights applied as FIR convolution over
/// a 15-bar sliding window. No configurable parameters.
/// </remarks>
/// <seealso href="Sp15.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Sp15 : AbstractBase
{
    private const int Period = 15;
    private const double Divisor = 320.0;

    // Normalized weights: w[i] / 320.0, oldest to newest
    private static readonly double[] Weights =
    [
        -3.0 / Divisor, -6.0 / Divisor, -5.0 / Divisor,  3.0 / Divisor,
        21.0 / Divisor, 46.0 / Divisor, 67.0 / Divisor, 74.0 / Divisor,
        67.0 / Divisor, 46.0 / Divisor, 21.0 / Divisor,  3.0 / Divisor,
        -5.0 / Divisor, -6.0 / Divisor, -3.0 / Divisor
    ];

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
    /// Creates SP15 (Spencer 15-Point Moving Average). No parameters required.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sp15()
    {
        Name = "Sp15";
        WarmupPeriod = Period;
        _buffer = new RingBuffer(Period);
    }

    /// <summary>
    /// Creates SP15 connected to a data source for event-based updates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sp15(ITValuePublisher source) : this()
    {
        _source = source;
        _pubHandler = Handle;
        _source.Pub += _pubHandler;
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

            if (count < Period)
            {
                result = val;
            }
            else
            {
                result = ConvolveFull(_buffer);
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

            if (count < Period)
            {
                result = val;
            }
            else
            {
                result = ConvolveFull(_buffer);
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

        Batch(source.Values, vSpan);
        source.Times.CopyTo(tSpan);

        // Restore state by replaying last Period bars
        Reset();
        int startIndex = Math.Max(0, len - Period);
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
    /// Weight[0] corresponds to oldest bar, Weight[Period-1] to newest.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ConvolveFull(RingBuffer buffer)
    {
        ReadOnlySpan<double> internalBuf = buffer.InternalBuffer;
        int head = buffer.StartIndex;
        int capacity = buffer.Capacity;

        int part1Len = capacity - head;
        double sum1 = internalBuf.Slice(head, part1Len).DotProduct(Weights.AsSpan(0, part1Len));
        double sum2 = internalBuf[..head].DotProduct(Weights.AsSpan(part1Len));

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
    /// Calculates SP15 from a TSeries using streaming updates.
    /// </summary>
    public static TSeries Batch(TSeries source)
    {
        var sp15 = new Sp15();
        return sp15.Update(source);
    }

    /// <summary>
    /// Calculates Spencer 15-Point Moving Average over a span of values.
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="output">Output buffer (must be same length as source)</param>
    /// <param name="nanValue">Value to use for NaN substitution (default: NaN)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, double nanValue = double.NaN)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (source.Length == 0)
        {
            return;
        }

        int len = source.Length;

        const int StackallocThreshold = 256;

        // Allocate ring buffer
        double[]? ringRented = Period > StackallocThreshold ? ArrayPool<double>.Shared.Rent(Period) : null;
        Span<double> ring = Period <= StackallocThreshold
            ? stackalloc double[Period]
            : ringRented!.AsSpan(0, Period);

        // Allocate NaN-corrected values array
        double[]? cleanRented = len > StackallocThreshold ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> clean = len <= StackallocThreshold
            ? stackalloc double[len]
            : cleanRented!.AsSpan(0, len);

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

            // Apply Spencer FIR convolution
            int ringIdx = 0;
            int count = 0;

            for (int i = 0; i < len; i++)
            {
                double val = clean[i];

                ring[ringIdx] = val;
                ringIdx++;
                if (ringIdx >= Period)
                {
                    ringIdx = 0;
                }

                if (count < Period)
                {
                    count++;
                }

                if (count < Period)
                {
                    // Warmup: return raw value
                    output[i] = val;
                    continue;
                }

                // Full window: DotProduct convolution over circular buffer
                // ringIdx points to next-write = oldest entry
                int part1Len = Period - ringIdx;

                ReadOnlySpan<double> ringRo = ring;
                double sum = ringRo.Slice(ringIdx, part1Len).DotProduct(Weights.AsSpan(0, part1Len))
                           + ringRo[..ringIdx].DotProduct(Weights.AsSpan(part1Len));

                output[i] = sum;
            }
        }
        finally
        {
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
    /// Creates an SP15 indicator and calculates results from source.
    /// </summary>
    public static (TSeries Results, Sp15 Indicator) Calculate(TSeries source)
    {
        var indicator = new Sp15();
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
