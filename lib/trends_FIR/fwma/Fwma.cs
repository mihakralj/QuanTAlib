using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// FWMA: Fibonacci Weighted Moving Average
/// </summary>
/// <remarks>
/// FIR filter with Fibonacci sequence weights F(N)..F(1) giving golden-ratio decay.
/// O(N) per bar via DotProduct convolution over circular buffer. No O(1) shortcut exists.
///
/// Calculation: <c>FWMA = Σ(F(N-i) × P_{t-i}) / Σ(F(i))</c>
/// </remarks>
/// <seealso href="Fwma.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Fwma : AbstractBase
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
    /// Creates FWMA with specified period.
    /// </summary>
    /// <param name="period">Lookback period (number of Fibonacci weights, must be >= 1)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Fwma(int period = 10)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        Name = $"Fwma({_period})";
        WarmupPeriod = _period;

        _buffer = new RingBuffer(_period);
        _weights = new double[_period];

        ComputeFibonacciWeights(_weights, _period);
    }

    /// <summary>
    /// Creates FWMA connected to a data source for event-based updates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Fwma(ITValuePublisher source, int period = 10) : this(period)
    {
        _source = source;
        _pubHandler = Handle;
        _source.Pub += _pubHandler;
    }

    /// <summary>
    /// Computes normalized Fibonacci weights.
    /// weights[0] = F(period) (newest bar), weights[period-1] = F(1) (oldest bar).
    /// Normalized to sum = 1.0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeFibonacciWeights(Span<double> weights, int period)
    {
        // Generate Fibonacci sequence F(1)..F(period)
        // Then reverse so index 0 = newest = F(period), index period-1 = oldest = F(1)
        double prev2 = 0.0;
        double prev1 = 1.0;
        double wsum = 0.0;

        for (int i = 0; i < period; i++)
        {
            double fib = i <= 1 ? 1.0 : prev1 + prev2;
            // Store reversed: index 0 gets F(period), last index gets F(1)
            weights[period - 1 - i] = fib;
            wsum += fib;
            prev2 = prev1;
            prev1 = fib;
        }

        // Normalize to sum = 1.0
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
                // Warmup: use partial Fibonacci weights for available bars
                result = ConvolvePartial(_buffer, _weights, _period);
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
                result = ConvolvePartial(_buffer, _weights, _period);
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
    /// FIR convolution using SIMD DotProduct over full circular buffer.
    /// weights[0] corresponds to newest bar, weights[period-1] to oldest.
    /// RingBuffer iteration: StartIndex = oldest entry.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ConvolveFull(RingBuffer buffer, double[] weights)
    {
        ReadOnlySpan<double> internalBuf = buffer.InternalBuffer;
        int head = buffer.StartIndex;
        int period = buffer.Capacity;

        // Iterate oldest-to-newest and pair with weights[period-1-i]
        // weights[0]=newest, weights[N-1]=oldest
        double sum = 0.0;
        int wi = period - 1; // Start with weight for oldest bar
        for (int i = head; i < period; i++)
        {
            sum = Math.FusedMultiplyAdd(internalBuf[i], weights[wi], sum);
            wi--;
        }
        for (int i = 0; i < head; i++)
        {
            sum = Math.FusedMultiplyAdd(internalBuf[i], weights[wi], sum);
            wi--;
        }

        return sum;
    }

    /// <summary>
    /// Partial convolution during warmup using only available bars.
    /// Recomputes partial Fibonacci weights normalized for the partial window.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ConvolvePartial(RingBuffer buffer, double[] fullWeights, int fullPeriod)
    {
        int count = buffer.Count;
        if (count == 0)
        {
            return double.NaN;
        }
        if (count == 1)
        {
            // Single bar: return the value itself (F(1)/F(1) = 1.0)
            return buffer.Oldest;
        }

        // Generate partial Fibonacci weights for 'count' bars, normalized
        // F(1), F(2), ..., F(count) reversed and normalized
        // We can compute inline with stackalloc for small counts
        const int StackallocThreshold = 256;
        double[]? rented = count > StackallocThreshold ? ArrayPool<double>.Shared.Rent(count) : null;
        Span<double> partialWeights = count <= StackallocThreshold
            ? stackalloc double[count]
            : rented!.AsSpan(0, count);

        try
        {
            double prev2 = 0.0;
            double prev1 = 1.0;
            double wsum = 0.0;

            for (int i = 0; i < count; i++)
            {
                double fib = i <= 1 ? 1.0 : prev1 + prev2;
                partialWeights[count - 1 - i] = fib;
                wsum += fib;
                prev2 = prev1;
                prev1 = fib;
            }

            double inv = 1.0 / wsum;
            for (int i = 0; i < count; i++)
            {
                partialWeights[i] *= inv;
            }

            // Convolve: iterate oldest-to-newest
            ReadOnlySpan<double> internalBuf = buffer.InternalBuffer;
            int head = buffer.StartIndex;
            int capacity = buffer.Capacity;
            double sum = 0.0;
            int wi = count - 1;

            // Buffer may not be full, so we iterate only 'count' elements oldest-first
            for (int k = 0; k < count; k++)
            {
                int idx = (head + k) % capacity;
                sum = Math.FusedMultiplyAdd(internalBuf[idx], partialWeights[wi], sum);
                wi--;
            }

            return sum;
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    /// <summary>
    /// Calculates FWMA from a TSeries using streaming updates.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 10)
    {
        var fwma = new Fwma(period);
        return fwma.Update(source);
    }

    /// <summary>
    /// Calculates Fibonacci Weighted Moving Average over a span of values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 10)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (source.Length == 0)
        {
            return;
        }

        CalculateScalarCore(source, output, period);
    }

    /// <summary>
    /// Creates a FWMA indicator and calculates results from source.
    /// </summary>
    public static (TSeries Results, Fwma Indicator) Calculate(TSeries source, int period = 10)
    {
        var indicator = new Fwma(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;

        const int StackallocThreshold = 256;

        // Allocate full weights
        double[]? weightsRented = period > StackallocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> weights = period <= StackallocThreshold
            ? stackalloc double[period]
            : weightsRented!.AsSpan(0, period);

        // Allocate ring buffer
        double[]? ringRented = period > StackallocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> ring = period <= StackallocThreshold
            ? stackalloc double[period]
            : ringRented!.AsSpan(0, period);

        // Allocate NaN-corrected array
        double[]? cleanRented = len > StackallocThreshold ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> clean = len <= StackallocThreshold
            ? stackalloc double[len]
            : cleanRented!.AsSpan(0, len);

        ComputeFibonacciWeights(weights, period);

        try
        {
            // Build NaN-corrected values
            double lastValid = double.NaN;
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

            // FIR convolution with growing-then-sliding window
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
                    // Warmup: compute partial Fibonacci-weighted average
                    output[i] = ComputePartialFwma(ring, ringIdx, count);
                    continue;
                }

                // Full window: convolve ring with weights
                // ringIdx now points to next-write = oldest entry
                double sum = 0.0;
                int wi = period - 1; // oldest weight index
                for (int k = 0; k < period; k++)
                {
                    int idx = (ringIdx + k) % period;
                    sum = Math.FusedMultiplyAdd(ring[idx], weights[wi], sum);
                    wi--;
                }

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
    /// Computes partial Fibonacci-weighted average for warmup bars.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputePartialFwma(ReadOnlySpan<double> ring, int ringIdx, int count)
    {
        if (count == 1)
        {
            // Single value: F(1)/F(1) = value itself
            int idx = (ringIdx - 1 + ring.Length) % ring.Length;
            return ring[idx];
        }

        // Generate partial Fibonacci weights for count bars
        double prev2 = 0.0;
        double prev1 = 1.0;
        double wsum = 0.0;

        // We need F(1)..F(count), then compute weighted sum with newest getting F(count)
        // Oldest-first iteration from ring
        const int StackallocThreshold = 256;
        double[]? rented = count > StackallocThreshold ? ArrayPool<double>.Shared.Rent(count) : null;
        Span<double> pw = count <= StackallocThreshold
            ? stackalloc double[count]
            : rented!.AsSpan(0, count);

        try
        {
            for (int i = 0; i < count; i++)
            {
                double fib = i <= 1 ? 1.0 : prev1 + prev2;
                pw[count - 1 - i] = fib; // newest gets largest
                wsum += fib;
                prev2 = prev1;
                prev1 = fib;
            }

            // Convolve: oldest first from ring
            // The oldest bar in the ring: (ringIdx - count + ring.Length) % ring.Length
            int oldestIdx = (ringIdx - count + ring.Length) % ring.Length;
            double sum = 0.0;
            int wi = count - 1; // weight for oldest bar
            for (int k = 0; k < count; k++)
            {
                int idx = (oldestIdx + k) % ring.Length;
                sum = Math.FusedMultiplyAdd(ring[idx], pw[wi], sum);
                wi--;
            }

            return sum / wsum;
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }
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
