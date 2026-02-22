using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// LANCZOS: Lanczos (Sinc) Window Moving Average
/// </summary>
/// <remarks>
/// Symmetric FIR filter using the normalized sinc function as the window shape.
/// The sinc function is the impulse response of the ideal brick-wall low-pass filter;
/// windowing it to finite length trades sharp cutoff for practical realizability.
///
/// Calculation: Precomputed weights via sinc(2k/(N-1) - 1), applied as FIR
/// convolution over sliding window. Negative sidelobes are preserved for
/// frequency-domain fidelity.
/// </remarks>
/// <seealso href="Lanczos.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Lanczos : AbstractBase
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
    /// Creates LANCZOS with specified period.
    /// </summary>
    /// <param name="period">Lookback period (>= 2)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Lanczos(int period = 14)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be at least 2", nameof(period));
        }

        _period = period;
        Name = $"Lanczos({_period.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
        WarmupPeriod = _period;

        _buffer = new RingBuffer(_period);
        _weights = new double[_period];

        ComputeLanczosWeights(_weights, _period);
    }

    /// <summary>
    /// Creates LANCZOS connected to a data source for event-based updates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Lanczos(ITValuePublisher source, int period = 14) : this(period)
    {
        _source = source;
        _pubHandler = Handle;
        _source.Pub += _pubHandler;
    }

    /// <summary>
    /// Computes Lanczos (sinc) window weights and normalizes to sum=1.
    /// w(k) = sinc(2k/(N-1) - 1), where sinc(x) = sin(pi*x)/(pi*x), sinc(0) = 1.
    /// Negative sidelobes are preserved for frequency-domain fidelity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeLanczosWeights(Span<double> weights, int period)
    {
        double nm1 = period - 1;

        double wsum = 0.0;
        for (int k = 0; k < period; k++)
        {
            double x = nm1 > 0 ? (2.0 * k / nm1) - 1.0 : 0.0;
            double w;
            if (Math.Abs(x) < 1e-10)
            {
                w = 1.0;
            }
            else
            {
                double piX = Math.PI * x;
                w = Math.Sin(piX) / piX;
            }
            weights[k] = w;
            wsum += w;
        }

        if (Math.Abs(wsum) > double.Epsilon)
        {
            double inv = 1.0 / wsum;
            for (int k = 0; k < period; k++)
            {
                weights[k] *= inv;
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
                result = val;
            }
            else
            {
                result = ConvolveFull(_buffer, _weights);
            }

            Last = new TValue(input.Time, result);
            if (publish) { PubEvent(Last, isNew); }
            return Last;
        }
        else
        {
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

    public static TSeries Batch(TSeries source, int period = 14)
    {
        var lanczos = new Lanczos(period);
        return lanczos.Update(source);
    }

    /// <summary>
    /// Calculates Lanczos Window MA over a span of values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 14, double nanValue = double.NaN)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be at least 2", nameof(period));
        }
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

        double[]? weightsRented = period > StackallocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> weights = period <= StackallocThreshold
            ? stackalloc double[period]
            : weightsRented!.AsSpan(0, period);

        double[]? ringRented = period > StackallocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> ring = period <= StackallocThreshold
            ? stackalloc double[period]
            : ringRented!.AsSpan(0, period);

        double[]? cleanRented = len > StackallocThreshold ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> clean = len <= StackallocThreshold
            ? stackalloc double[len]
            : cleanRented!.AsSpan(0, len);

        ComputeLanczosWeights(weights, period);

        try
        {
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

            int ringIdx = 0;
            int count = 0;

            for (int i = 0; i < len; i++)
            {
                double val = clean[i];

                ring[ringIdx] = val;
                ringIdx++;
                if (ringIdx >= period)
                {
                    ringIdx = 0;
                }

                if (count < period)
                {
                    count++;
                }

                if (count < period)
                {
                    output[i] = val;
                    continue;
                }

                int part1Len = period - ringIdx;

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

    public static (TSeries Results, Lanczos Indicator) Calculate(TSeries source, int period = 14)
    {
        var indicator = new Lanczos(period);
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
