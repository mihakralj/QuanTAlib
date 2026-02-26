// DWT: Discrete Wavelet Transform (À trous / Stationary Haar)
// Decomposes a signal into multi-resolution approximation and detail coefficients
// using the stationary (non-decimated) Haar wavelet. No downsampling: every output
// sample aligns precisely with its input bar. Lookback at level L = 2^L bars.
//
// Algorithm (à trous unrolled cascade, mirrors dwt.pine):
//   c[0] = input
//   c[j] = 0.5 * (c[j-1] + c[j-1][2^(j-1)])   — approximation at level j
//   d[j] = c[j-1] - c[j]                         — detail at level j
//
// output=0 → deepest approximation (trend)
// output=1..levels → detail at that level (noise/cycles)
//
// State stores all 8 level-approximation values and their delayed counterparts
// via a RingBuffer sized 2^levels. O(levels) per Update.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// DWT: Discrete Wavelet Transform (À trous Stationary Haar)
/// Decomposes a price series into multi-resolution approximation and detail
/// coefficients without downsampling, preserving exact bar alignment.
/// </summary>
/// <remarks>
/// Key properties:
/// - À trous (stationary) variant: no downsampling, every bar produces output
/// - Level j effective window: 2^j bars; max lookback = 2^levels bars
/// - output=0: deepest approximation (trend signal, lowest frequency)
/// - output=1..levels: detail at that level (cycles/noise at 2^j-bar scale)
/// - WarmupPeriod = 2^levels (buffer must be full for all lags to resolve)
/// - Perfect reconstruction: input = approx[L] + sum(detail[1..L])
/// - O(levels) per Update — levels ∈ [1,8]
/// </remarks>
[SkipLocalsInit]
public sealed class Dwt : AbstractBase
{
    private readonly int _levels;
    private readonly int _output;
    private readonly int _bufferSize; // 2^levels
    private readonly RingBuffer _buffer;

    // State: all 8 level-approximation values (c1..c8) in current cascade.
    // Only levels 1.._levels are meaningful; higher levels are carried as-is.
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValid);

    private State _state, _p_state;

    public override bool IsHot => _buffer.Count >= _bufferSize;

    /// <summary>
    /// Initializes a new Dwt indicator.
    /// </summary>
    /// <param name="levels">Decomposition levels 1-8. Level j captures structure at 2^j bars.
    /// WarmupPeriod = 2^levels (e.g., levels=4 → 16 bars). Default 4.</param>
    /// <param name="output">Output component: 0 = approximation (trend), 1..levels = detail at that level.
    /// Default 0.</param>
    public Dwt(int levels = 4, int output = 0)
    {
        if (levels < 1 || levels > 8)
        {
            throw new ArgumentException("Levels must be between 1 and 8", nameof(levels));
        }

        if (output < 0 || output > levels)
        {
            throw new ArgumentException("Output must be 0 (approximation) or 1..levels (detail)", nameof(output));
        }

        _levels = levels;
        _output = output;
        _bufferSize = 1 << levels; // 2^levels
        _buffer = new RingBuffer(_bufferSize);
        Name = $"Dwt({levels},{output})";
        WarmupPeriod = _bufferSize;
        _state = new State(0.0);
        _p_state = _state;
    }

    /// <summary>
    /// Initializes a new Dwt indicator with source for event-based chaining.
    /// </summary>
    /// <param name="source">Source indicator for chaining</param>
    /// <param name="levels">Decomposition levels 1-8. Default 4.</param>
    /// <param name="output">Output component: 0=approximation, 1..levels=detail. Default 0.</param>
    public Dwt(ITValuePublisher source, int levels = 4, int output = 0)
        : this(levels, output)
    {
        source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// Performs the à trous Haar DWT cascade over the ring buffer.
    /// Only the levels actually requested are computed; the rest short-circuit.
    /// Returns the selected output component (approximation or detail at chosen level).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeDwt()
    {
        // c0 = newest sample in buffer (index Count-1 = oldest at 0, newest at bufferSize-1)
        double c0 = _buffer[_buffer.Count - 1];

        // Level 1: lag = 2^0 = 1
        double prev1 = _buffer.Count >= 2 ? _buffer[_buffer.Count - 2] : c0;
        double c1 = Math.FusedMultiplyAdd(c0 + prev1, 0.5, 0.0);
        double d1 = c0 - c1;

        if (_levels == 1)
        {
            return _output == 0 ? c1 : d1;
        }

        // Level 2: lag = 2^1 = 2 (in c1 history, equivalent to lag 2 in c1 series)
        // In RingBuffer terms: we need c1 from 2 bars ago.
        // To get c1[2], we recompute c1 at position (bufferSize-3..bufferSize-2).
        // Rather than storing all level history, recompute cascade at required offsets.
        double c1_lag2 = ComputeC1AtLag(2);
        double c2 = Math.FusedMultiplyAdd(c1 + c1_lag2, 0.5, 0.0);
        double d2 = c1 - c2;

        if (_levels == 2)
        {
            return _output switch { 0 => c2, 1 => d1, 2 => d2, _ => d2 };
        }

        // Level 3: lag = 2^2 = 4
        double c2_lag4 = ComputeC2AtLag(4);
        double c3 = Math.FusedMultiplyAdd(c2 + c2_lag4, 0.5, 0.0);
        double d3 = c2 - c3;

        if (_levels == 3)
        {
            return _output switch { 0 => c3, 1 => d1, 2 => d2, 3 => d3, _ => d3 };
        }

        // Level 4: lag = 2^3 = 8
        double c3_lag8 = ComputeC3AtLag(8);
        double c4 = Math.FusedMultiplyAdd(c3 + c3_lag8, 0.5, 0.0);
        double d4 = c3 - c4;

        if (_levels == 4)
        {
            return _output switch { 0 => c4, 1 => d1, 2 => d2, 3 => d3, 4 => d4, _ => d4 };
        }

        // Level 5: lag = 2^4 = 16
        double c4_lag16 = ComputeC4AtLag(16);
        double c5 = Math.FusedMultiplyAdd(c4 + c4_lag16, 0.5, 0.0);
        double d5 = c4 - c5;

        if (_levels == 5)
        {
            return _output switch { 0 => c5, 1 => d1, 2 => d2, 3 => d3, 4 => d4, 5 => d5, _ => d5 };
        }

        // Level 6: lag = 2^5 = 32
        double c5_lag32 = ComputeC5AtLag(32);
        double c6 = Math.FusedMultiplyAdd(c5 + c5_lag32, 0.5, 0.0);
        double d6 = c5 - c6;

        if (_levels == 6)
        {
            return _output switch { 0 => c6, 1 => d1, 2 => d2, 3 => d3, 4 => d4, 5 => d5, 6 => d6, _ => d6 };
        }

        // Level 7: lag = 2^6 = 64
        double c6_lag64 = ComputeC6AtLag(64);
        double c7 = Math.FusedMultiplyAdd(c6 + c6_lag64, 0.5, 0.0);
        double d7 = c6 - c7;

        if (_levels == 7)
        {
            return _output switch { 0 => c7, 1 => d1, 2 => d2, 3 => d3, 4 => d4, 5 => d5, 6 => d6, 7 => d7, _ => d7 };
        }

        // Level 8: lag = 2^7 = 128
        double c7_lag128 = ComputeC7AtLag(128);
        double c8 = Math.FusedMultiplyAdd(c7 + c7_lag128, 0.5, 0.0);
        double d8 = c7 - c8;

        return _output switch { 0 => c8, 1 => d1, 2 => d2, 3 => d3, 4 => d4, 5 => d5, 6 => d6, 7 => d7, _ => d8 };
    }

    // Helpers: compute c1..c7 at a buffer offset (lag from newest).
    // These are inlined by the JIT since they're small.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetBuf(int lag)
    {
        int idx = _buffer.Count - 1 - lag;
        return idx >= 0 ? _buffer[idx] : _buffer[0]; // boundary: use oldest
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeC1At(int lag)
    {
        double a = GetBuf(lag);
        double b = GetBuf(lag + 1);
        return Math.FusedMultiplyAdd(a + b, 0.5, 0.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeC1AtLag(int lag) => ComputeC1At(lag);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeC2AtLag(int lag)
    {
        double a = ComputeC1At(lag);
        double b = ComputeC1At(lag + 2);
        return Math.FusedMultiplyAdd(a + b, 0.5, 0.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeC3AtLag(int lag)
    {
        double a = ComputeC2AtLag(lag);
        double b = ComputeC2AtLag(lag + 4);
        return Math.FusedMultiplyAdd(a + b, 0.5, 0.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeC4AtLag(int lag)
    {
        double a = ComputeC3AtLag(lag);
        double b = ComputeC3AtLag(lag + 8);
        return Math.FusedMultiplyAdd(a + b, 0.5, 0.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeC5AtLag(int lag)
    {
        double a = ComputeC4AtLag(lag);
        double b = ComputeC4AtLag(lag + 16);
        return Math.FusedMultiplyAdd(a + b, 0.5, 0.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeC6AtLag(int lag)
    {
        double a = ComputeC5AtLag(lag);
        double b = ComputeC5AtLag(lag + 32);
        return Math.FusedMultiplyAdd(a + b, 0.5, 0.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeC7AtLag(int lag)
    {
        double a = ComputeC6AtLag(lag);
        double b = ComputeC6AtLag(lag + 64);
        return Math.FusedMultiplyAdd(a + b, 0.5, 0.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        var s = _state;
        double value = input.Value;
        double result;

        if (double.IsFinite(value))
        {
            _buffer.Add(value, isNew);

            if (_buffer.Count >= _bufferSize)
            {
                result = ComputeDwt();
                s = s with { LastValid = result };
            }
            else
            {
                result = s.LastValid;
            }
        }
        else
        {
            result = s.LastValid;
        }

        _state = s;
        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        var result = new TSeries(source.Count);
        ReadOnlySpan<double> values = source.Values;
        ReadOnlySpan<long> times = source.Times;

        for (int i = 0; i < source.Count; i++)
        {
            var tv = Update(new TValue(new DateTime(times[i], DateTimeKind.Utc), values[i]), true);
            result.Add(tv, true);
        }

        return result;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        TimeSpan interval = step ?? TimeSpan.FromSeconds(1);
        DateTime time = DateTime.UtcNow - (interval * source.Length);

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(time, source[i]), true);
            time += interval;
        }
    }

    public static TSeries Batch(TSeries source, int levels = 4, int output = 0)
    {
        var indicator = new Dwt(levels, output);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates DWT over a span of values using the à trous Haar cascade.
    /// Uses stackalloc for small buffers (≤ 256 doubles), ArrayPool above that.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> source, Span<double> output,
        int levels = 4, int outputComponent = 0)
    {
        if (source.Length == 0)
        {
            throw new ArgumentException("Source cannot be empty", nameof(source));
        }

        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output length must be >= source length", nameof(output));
        }

        if (levels < 1 || levels > 8)
        {
            throw new ArgumentException("Levels must be between 1 and 8", nameof(levels));
        }

        if (outputComponent < 0 || outputComponent > levels)
        {
            throw new ArgumentException("Output must be 0 (approximation) or 1..levels (detail)", nameof(outputComponent));
        }

        int bufferSize = 1 << levels; // 2^levels
        double lastValid = 0.0;

        const int StackallocThreshold = 256;
        double[]? rented = null;
        scoped Span<double> buf;

        if (bufferSize <= StackallocThreshold)
        {
            buf = stackalloc double[bufferSize];
        }
        else
        {
            rented = ArrayPool<double>.Shared.Rent(bufferSize);
            buf = rented.AsSpan(0, bufferSize);
        }

        try
        {
            buf.Clear();
            int head = 0;
            int count = 0;

            for (int i = 0; i < source.Length; i++)
            {
                double val = source[i];
                if (!double.IsFinite(val))
                {
                    output[i] = lastValid;
                    continue;
                }

                // Write into circular buffer
                buf[head] = val;
                head = (head + 1) % bufferSize;
                if (count < bufferSize)
                {
                    count++;
                }

                if (count < bufferSize)
                {
                    output[i] = lastValid;
                    continue;
                }

                // Compute the full cascade from circular buffer.
                // newest = head-1 (mod bufferSize), oldest = head (mod bufferSize)
                double result = ComputeDwtFromSpan(buf, head, bufferSize, levels, outputComponent);
                lastValid = result;
                output[i] = result;
            }
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Performs the à trous cascade over a span-based circular buffer.
    /// head is one past the newest element (next write position).
    /// Index mapping: newest = (head-1+cap)%cap, lag k → (head-1-k+cap)%cap.
    /// </summary>
    private static double ComputeDwtFromSpan(
        Span<double> buf, int head, int cap, int levels, int outputComponent)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double Get(Span<double> b, int h, int c, int lag)
        {
            int idx = ((h - 1 - lag) % c + c) % c;
            int maxLag = c - 1;
            if (lag > maxLag) { idx = ((h - 1 - maxLag) % c + c) % c; }
            return b[idx];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double C1(Span<double> b, int h, int c, int lag)
        {
            double a = Get(b, h, c, lag);
            double bv = Get(b, h, c, lag + 1);
            return Math.FusedMultiplyAdd(a + bv, 0.5, 0.0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double C2(Span<double> b, int h, int c, int lag)
        {
            double a = C1(b, h, c, lag);
            double bv = C1(b, h, c, lag + 2);
            return Math.FusedMultiplyAdd(a + bv, 0.5, 0.0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double C3(Span<double> b, int h, int c, int lag)
        {
            double a = C2(b, h, c, lag);
            double bv = C2(b, h, c, lag + 4);
            return Math.FusedMultiplyAdd(a + bv, 0.5, 0.0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double C4(Span<double> b, int h, int c, int lag)
        {
            double a = C3(b, h, c, lag);
            double bv = C3(b, h, c, lag + 8);
            return Math.FusedMultiplyAdd(a + bv, 0.5, 0.0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double C5(Span<double> b, int h, int c, int lag)
        {
            double a = C4(b, h, c, lag);
            double bv = C4(b, h, c, lag + 16);
            return Math.FusedMultiplyAdd(a + bv, 0.5, 0.0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double C6(Span<double> b, int h, int c, int lag)
        {
            double a = C5(b, h, c, lag);
            double bv = C5(b, h, c, lag + 32);
            return Math.FusedMultiplyAdd(a + bv, 0.5, 0.0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double C7(Span<double> b, int h, int c, int lag)
        {
            double a = C6(b, h, c, lag);
            double bv = C6(b, h, c, lag + 64);
            return Math.FusedMultiplyAdd(a + bv, 0.5, 0.0);
        }

        double c0 = Get(buf, head, cap, 0);

        double c1v = C1(buf, head, cap, 0);
        double d1 = c0 - c1v;
        if (levels == 1) { return outputComponent == 0 ? c1v : d1; }

        double c2v = C2(buf, head, cap, 0);
        double d2 = c1v - c2v;
        if (levels == 2) { return outputComponent switch { 0 => c2v, 1 => d1, _ => d2 }; }

        double c3v = C3(buf, head, cap, 0);
        double d3 = c2v - c3v;
        if (levels == 3) { return outputComponent switch { 0 => c3v, 1 => d1, 2 => d2, _ => d3 }; }

        double c4v = C4(buf, head, cap, 0);
        double d4 = c3v - c4v;
        if (levels == 4) { return outputComponent switch { 0 => c4v, 1 => d1, 2 => d2, 3 => d3, _ => d4 }; }

        double c5v = C5(buf, head, cap, 0);
        double d5 = c4v - c5v;
        if (levels == 5) { return outputComponent switch { 0 => c5v, 1 => d1, 2 => d2, 3 => d3, 4 => d4, _ => d5 }; }

        double c6v = C6(buf, head, cap, 0);
        double d6 = c5v - c6v;
        if (levels == 6) { return outputComponent switch { 0 => c6v, 1 => d1, 2 => d2, 3 => d3, 4 => d4, 5 => d5, _ => d6 }; }

        double c7v = C7(buf, head, cap, 0);
        double d7 = c6v - c7v;
        if (levels == 7) { return outputComponent switch { 0 => c7v, 1 => d1, 2 => d2, 3 => d3, 4 => d4, 5 => d5, 6 => d6, _ => d7 }; }

        double c7lag = C7(buf, head, cap, 128);
        double c8v = Math.FusedMultiplyAdd(c7v + c7lag, 0.5, 0.0);
        double d8 = c7v - c8v;
        return outputComponent switch { 0 => c8v, 1 => d1, 2 => d2, 3 => d3, 4 => d4, 5 => d5, 6 => d6, 7 => d7, _ => d8 };
    }

    public static (TSeries Results, Dwt Indicator) Calculate(
        TSeries source, int levels = 4, int output = 0)
    {
        var indicator = new Dwt(levels, output);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = new State(0.0);
        _p_state = _state;
        Last = default;
    }
}
