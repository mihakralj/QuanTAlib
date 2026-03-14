using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TUKEY_W: Tukey (Tapered Cosine) Window Moving Average
/// </summary>
/// <remarks>
/// FIR filter using the Tukey (tapered cosine) window as weights.
/// Parameter alpha controls the taper fraction:
///   alpha=0 → rectangular window (SMA)
///   alpha=1 → Hann window (full cosine taper)
///   alpha=0.5 → half tapered, half flat (default)
///
/// Default period=20, alpha=0.5, min period=2.
/// </remarks>
/// <seealso href="Tukey_w.md">Detailed documentation</seealso>
[SkipLocalsInit]
#pragma warning disable S101 // S101 - Indicator name 'Tukey_w' matches file/PineScript convention with underscore variant suffix
public sealed class Tukey_w : AbstractBase
#pragma warning restore S101
{
    private readonly int _period;
    private readonly double _alpha;
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
    /// Creates TUKEY_W with specified period and alpha.
    /// </summary>
    /// <param name="period">Lookback period (must be >= 2)</param>
    /// <param name="alpha">Taper fraction: 0=SMA, 1=Hann (must be in [0,1])</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Tukey_w(int period = 20, double alpha = 0.5)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be at least 2", nameof(period));
        }
        if (alpha < 0.0 || alpha > 1.0)
        {
            throw new ArgumentException("Alpha must be between 0.0 and 1.0", nameof(alpha));
        }

        _period = period;
        _alpha = alpha;
        Name = $"Tukey_w({_period},{_alpha:F2})";
        WarmupPeriod = _period;

        _buffer = new RingBuffer(_period);
        _weights = new double[_period];

        ComputeTukeyWeights(_weights, _period, _alpha);
    }

    /// <summary>
    /// Creates TUKEY_W connected to a data source for event-based updates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Tukey_w(ITValuePublisher source, int period = 20, double alpha = 0.5) : this(period, alpha)
    {
        _source = source;
        _pubHandler = Handle;
        _source.Pub += _pubHandler;
    }

    /// <summary>
    /// Computes Tukey (tapered cosine) window weights.
    /// Left taper: w(n) = 0.5*(1 - cos(2π*n / (alpha*(N-1))))
    /// Flat center: w(n) = 1.0
    /// Right taper: w(n) = 0.5*(1 - cos(2π*(N-1-n) / (alpha*(N-1))))
    /// Normalized to sum=1.0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeTukeyWeights(Span<double> weights, int period, double alpha)
    {
        int N = period - 1;
        double aN = alpha * N;
        double wsum = 0.0;

        for (int i = 0; i < period; i++)
        {
            double w = 1.0;
            if (aN > 0.0)
            {
                if (i < aN * 0.5)
                {
                    w = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / aN));
                }
                else if (i > N - (aN * 0.5))
                {
                    w = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * (N - i) / aN));
                }
            }
            weights[i] = w;
            wsum += w;
        }

        // Normalize to sum=1.0
        if (wsum > double.Epsilon)
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

        Batch(source.Values, vSpan, _period, _alpha);
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
    /// Calculates TUKEY_W from a TSeries using streaming updates.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 20, double alpha = 0.5)
    {
        var tukey = new Tukey_w(period, alpha);
        return tukey.Update(source);
    }

    /// <summary>
    /// Calculates Tukey Window Moving Average over a span of values.
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="output">Output buffer (must be same length as source)</param>
    /// <param name="period">Period for weight calculation (must be >= 2)</param>
    /// <param name="alpha">Taper fraction: 0=SMA, 1=Hann (must be in [0,1])</param>
    /// <param name="nanValue">Value to use for NaN substitution (default: NaN)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 20, double alpha = 0.5, double nanValue = double.NaN)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be at least 2", nameof(period));
        }
        if (alpha < 0.0 || alpha > 1.0)
        {
            throw new ArgumentException("Alpha must be between 0.0 and 1.0", nameof(alpha));
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

        // Allocate weights
        double[]? weightsRented = period > StackallocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> weights = period <= StackallocThreshold
            ? stackalloc double[period]
            : weightsRented!.AsSpan(0, period);

        // Allocate ring buffer
        double[]? ringRented = period > StackallocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> ring = period <= StackallocThreshold
            ? stackalloc double[period]
            : ringRented!.AsSpan(0, period);

        // Allocate NaN-corrected values array
        double[]? cleanRented = len > StackallocThreshold ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> clean = len <= StackallocThreshold
            ? stackalloc double[len]
            : cleanRented!.AsSpan(0, len);

        ComputeTukeyWeights(weights, period, alpha);

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

            // Apply Tukey FIR convolution
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
                    // Warmup: return raw value
                    output[i] = val;
                    continue;
                }

                // Full window: DotProduct convolution over circular buffer
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

    /// <summary>
    /// Creates a TUKEY_W indicator and calculates results from source.
    /// </summary>
    public static (TSeries Results, Tukey_w Indicator) Calculate(TSeries source, int period = 20, double alpha = 0.5)
    {
        var indicator = new Tukey_w(period, alpha);
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
