using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// HARMEAN: Harmonic Mean over a rolling window
/// </summary>
/// <remarks>
/// Harmonic Mean is the reciprocal of the arithmetic mean of reciprocals:
/// HM = n / Σ(1/xᵢ). It penalizes extreme values more strongly than the
/// arithmetic or geometric mean, making it useful for averaging rates,
/// ratios, and price/earnings multiples.
///
/// The running sum of reciprocals enables O(1) updates: add 1/new, subtract 1/old.
/// Kahan-Babuška compensated summation prevents floating-point drift in the reciprocal accumulator,
/// eliminating the need for periodic resynchronization.
///
/// Non-positive values are replaced with the last valid positive value, since
/// 1/x is undefined for x = 0 and negative reciprocals break the mean.
/// For price series (always positive), this substitution is rarely triggered.
///
/// Key Features:
/// - O(1) time complexity per update via running sum of reciprocals
/// - Kahan-Babuška compensated summation for numerical stability
/// - NaN/Infinity/non-positive substitution with last valid value
///
/// IsHot:
/// Becomes true when the buffer is full (period samples processed).
/// </remarks>
[SkipLocalsInit]
public sealed class Harmean : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private readonly TValuePublishedHandler _handler;
    private readonly ITValuePublisher? _source;
    private bool _disposed;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double SumReciprocal;
        public double C;              // Kahan primary compensation
        public double Cc;             // Kahan secondary compensation (Babuška)
        public double LastValidValue;
    }

    private State _s;
    private State _ps;

    public Harmean(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Harmean({period})";
        WarmupPeriod = period;
        _handler = Handle;
    }

    public Harmean(ITValuePublisher source, int period) : this(period)
    {
        _source = source;
        source.Pub += _handler;
    }

    public Harmean(TSeries source, int period) : this(period)
    {
        _source = source;
        source.Pub += _handler;
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        _ps = _s;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Mode B: Streaming (Stateful)
    /////////////////////////////////////////////////////////////////////////////////////////////////

    public override bool IsHot => _buffer.IsFull;

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Kahan-Babuška Core Operations (reciprocal domain)
    /////////////////////////////////////////////////////////////////////////////////////////////////

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void KahanAdd(double x)
    {
        double y = x - _s.C;
        double t = _s.SumReciprocal + y;
        _s.C = (t - _s.SumReciprocal) - y;
        _s.SumReciprocal = t;

        double z = _s.C - _s.Cc;
        double tt = _s.SumReciprocal + z;
        _s.Cc = (tt - _s.SumReciprocal) - z;
        _s.SumReciprocal = tt;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void KahanSubtract(double x)
    {
        KahanAdd(-x);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecalculateSumReciprocal()
    {
        _s.SumReciprocal = 0;
        _s.C = 0;
        _s.Cc = 0;

        var span = _buffer.GetSpan();
        for (int i = 0; i < span.Length; i++)
        {
            KahanAdd(1.0 / span[i]);
        }
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Mode C: Priming (The Bridge)
    /////////////////////////////////////////////////////////////////////////////////////////////////

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _buffer.Clear();
        _s = default;
        _ps = default;

        int warmupLength = Math.Min(source.Length, WarmupPeriod);
        int startIndex = source.Length - warmupLength;

        // Seed LastValidValue from prior context
        _s.LastValidValue = double.NaN;
        for (int i = startIndex - 1; i >= 0; i--)
        {
            if (double.IsFinite(source[i]) && source[i] > 0)
            {
                _s.LastValidValue = source[i];
                break;
            }
        }

        if (double.IsNaN(_s.LastValidValue))
        {
            for (int i = startIndex; i < source.Length; i++)
            {
                if (double.IsFinite(source[i]) && source[i] > 0)
                {
                    _s.LastValidValue = source[i];
                    break;
                }
            }
        }

        for (int i = startIndex; i < source.Length; i++)
        {
            double val = GetValidValue(source[i]);
            _buffer.Add(val);
            KahanAdd(1.0 / val);
        }

        double result = (_buffer.Count > 0 && _s.SumReciprocal > 1e-300)
            ? _buffer.Count / _s.SumReciprocal
            : double.NaN;
        Last = new TValue(DateTime.MinValue, result);
        _ps = _s;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input) && input > 0)
        {
            _s.LastValidValue = input;
            return input;
        }
        return _s.LastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;

            double val = GetValidValue(input.Value);
            double reciprocal = 1.0 / val;

            if (_buffer.Count == _buffer.Capacity)
            {
                KahanSubtract(1.0 / _buffer.Oldest);
            }

            _buffer.Add(val);
            KahanAdd(reciprocal);
        }
        else
        {
            _s = _ps;
            _buffer.Snapshot();
            _buffer.Restore();

            double val = GetValidValue(input.Value);

            if (_buffer.Count > 0)
            {
                _buffer.UpdateNewest(val);
                RecalculateSumReciprocal();
            }
            else
            {
                _buffer.Add(val);
                KahanAdd(1.0 / val);
            }
        }

        double result = (_buffer.Count > 0 && _s.SumReciprocal > 1e-300)
            ? _buffer.Count / _s.SumReciprocal
            : double.NaN;
        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
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

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        Prime(source.Values);

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Mode A: Batch (Stateless)
    /////////////////////////////////////////////////////////////////////////////////////////////////

    public static TSeries Batch(TSeries source, int period)
    {
        var h = new Harmean(period);
        return h.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // Use Kahan compensated sliding-window reciprocal sum for batch
        double sumReciprocal = 0;
        double sumReciprocalComp = 0;  // Kahan compensation
        double lastValid = double.NaN;
        int count = 0;

        // Seed lastValid
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(source[k]) && source[k] > 0)
            {
                lastValid = source[k];
                break;
            }
        }

        const int StackallocThreshold = 256;
        double[]? rented = null;
        scoped Span<double> ring;
        if (period <= StackallocThreshold)
        {
            ring = stackalloc double[period];
        }
        else
        {
            rented = ArrayPool<double>.Shared.Rent(period);
            ring = rented.AsSpan(0, period);
        }

        try
        {
            int head = 0;
            ring.Fill(0);

            for (int i = 0; i < len; i++)
            {
                double val = source[i];
                if (double.IsFinite(val) && val > 0)
                {
                    lastValid = val;
                }
                else
                {
                    val = lastValid;
                }

                double reciprocal = 1.0 / val;

                if (count == period)
                {
                    // Kahan subtract old reciprocal
                    double ys = -ring[head] - sumReciprocalComp;
                    double ts = sumReciprocal + ys;
                    sumReciprocalComp = (ts - sumReciprocal) - ys;
                    sumReciprocal = ts;
                }
                else
                {
                    count++;
                }

                ring[head] = reciprocal;

                // Kahan add new reciprocal
                {
                    double ys = reciprocal - sumReciprocalComp;
                    double ts = sumReciprocal + ys;
                    sumReciprocalComp = (ts - sumReciprocal) - ys;
                    sumReciprocal = ts;
                }

                head = (head + 1) % period;

                output[i] = (sumReciprocal > 1e-300) ? count / sumReciprocal : double.NaN;
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

    public static (TSeries Results, Harmean Indicator) Calculate(TSeries source, int period)
    {
        var h = new Harmean(period);
        TSeries results = h.Update(source);
        return (results, h);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _s = default;
        _ps = default;
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
}
