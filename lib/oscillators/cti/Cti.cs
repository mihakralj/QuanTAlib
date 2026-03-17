using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CTI: Ehlers Correlation Trend Indicator
/// </summary>
/// <remarks>
/// Measures the Pearson correlation coefficient between the price series and a
/// perfect linear time index over a rolling window. Output is bounded [-1, +1]:
/// +1 = perfect uptrend, -1 = perfect downtrend, 0 = no linear trend.
///
/// Uses O(1) incremental running sums: ΣY, ΣY², ΣXY. The X-side sums (ΣX, ΣX²)
/// are analytical closed-form functions of n and never need maintenance.
///
/// Incremental ΣXY trick (same as CFO):
///   When the window slides forward one bar:
///   ΣXY -= ΣY_before_removal   (shifts all position indices down by 1)
///   ΣXY += (n-1) × y_new       (new value enters at highest position)
///
/// References:
///   Ehlers, J.F. (2001). Rocket Science for Traders. Wiley
///   PineScript reference: cti.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Cti : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;

    // Precomputed X-side constants (full-window)
    private readonly double _sx;    // period*(period-1)/2
    private readonly double _sxx;   // period*(period-1)*(2*period-1)/6
    private readonly double _denomX; // period*sxx - sx*sx  (constant, never changes)

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double SumY,
        double SumY2,
        double SumXY,
        double SumYComp,
        double SumY2Comp,
        double SumXYComp,
        int Count,
        double LastValid);
    private State _s, _ps;

    /// <summary>
    /// Creates CTI with the specified lookback period.
    /// </summary>
    /// <param name="period">Rolling window length (must be ≥ 2)</param>
    public Cti(int period = 20)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be greater than or equal to 2", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Cti({period})";
        WarmupPeriod = period;

        _sx = period * (period - 1) / 2.0;
        _sxx = period * (period - 1.0) * (2 * period - 1) / 6.0;
        _denomX = Math.FusedMultiplyAdd(period, _sxx, -_sx * _sx);
    }

    /// <summary>
    /// Creates CTI subscribed to an upstream publisher.
    /// </summary>
    public Cti(ITValuePublisher source, int period = 20) : this(period)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);
    public override bool IsHot => _buffer.IsFull;

    /// <summary>Period of the indicator.</summary>
    public int Period => _period;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double value = input.Value;

        // Sanitize input — substitute last-valid on NaN/Infinity
        if (!double.IsFinite(value))
        {
            value = double.IsFinite(_s.LastValid) ? _s.LastValid : 0.0;
        }
        else
        {
            _s.LastValid = value;
        }

        if (isNew)
        {
            _ps = _s;

            if (_buffer.Count == _buffer.Capacity)
            {
                // Full window: Kahan compensated O(1) update
                double oldest = _buffer.Oldest;
                // Kahan delta for SumY
                {
                    double delta = value - oldest;
                    double y = delta - _s.SumYComp;
                    double t = _s.SumY + y;
                    _s.SumYComp = (t - _s.SumY) - y;
                    _s.SumY = t;
                }
                // Kahan delta for SumY2
                {
                    double deltaSq = (value * value) - (oldest * oldest);
                    double y = deltaSq - _s.SumY2Comp;
                    double t = _s.SumY2 + y;
                    _s.SumY2Comp = (t - _s.SumY2) - y;
                    _s.SumY2 = t;
                }
                // SumXY net delta = -SumY_new + period*value
                {
                    double netDelta = -_s.SumY + (_period * value);
                    double y = netDelta - _s.SumXYComp;
                    double t = _s.SumXY + y;
                    _s.SumXYComp = (t - _s.SumXY) - y;
                    _s.SumXY = t;
                }
            }
            else
            {
                // Growing window: Kahan additions
                {
                    double y = value - _s.SumYComp;
                    double t = _s.SumY + y;
                    _s.SumYComp = (t - _s.SumY) - y;
                    _s.SumY = t;
                }
                {
                    double sq = value * value;
                    double y = sq - _s.SumY2Comp;
                    double t = _s.SumY2 + y;
                    _s.SumY2Comp = (t - _s.SumY2) - y;
                    _s.SumY2 = t;
                }
                {
                    double addXY = _s.Count * value;
                    double y = addXY - _s.SumXYComp;
                    double t = _s.SumXY + y;
                    _s.SumXYComp = (t - _s.SumXY) - y;
                    _s.SumXY = t;
                }
                _s.Count++;
            }

            _buffer.Add(value);
        }
        else
        {
            _s = _ps;
            _buffer.UpdateNewest(value);
            Resync();
        }

        if (!_buffer.IsFull)
        {
            Last = new TValue(input.Time, 0.0);
            PubEvent(Last, isNew);
            return Last;
        }

        double cti = ComputePearson(_s.SumY, _s.SumY2, _s.SumXY, _period, _sx, _sxx, _denomX);

        Last = new TValue(input.Time, cti);
        PubEvent(Last, isNew);
        return Last;
    }
    public override TSeries Update(TSeries source)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Replay to sync internal state
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputePearson(
        double sumY, double sumY2, double sumXY,
        double n, double sx, double sxx, double denomX)
    {
        double denomY = Math.FusedMultiplyAdd(n, sumY2, -sumY * sumY);
        double denom = denomX * denomY;
        if (denom <= 0.0)
        {
            return 0.0;
        }

        double numer = Math.FusedMultiplyAdd(n, sumXY, -sx * sumY);
        return Math.Clamp(numer / Math.Sqrt(denom), -1.0, 1.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Resync()
    {
        _s.SumY = 0.0;
        _s.SumY2 = 0.0;
        _s.SumXY = 0.0;
        _s.Count = _buffer.Count;
        for (int i = 0; i < _buffer.Count; i++)
        {
            double v = _buffer[i];
            _s.SumY += v;
            _s.SumY2 = Math.FusedMultiplyAdd(v, v, _s.SumY2);
            _s.SumXY = Math.FusedMultiplyAdd(i, v, _s.SumXY);
        }
    }
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);
        }
    }
    public override void Reset()
    {
        _buffer.Clear();
        _s = default;
        _ps = default;
        Last = default;
    }

    /// <summary>Calculates CTI for an entire TSeries.</summary>
    public static TSeries Batch(TSeries source, int period = 20)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, period);
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch CTI calculation using O(1) incremental Pearson correlation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 20)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period < 2)
        {
            throw new ArgumentException("Period must be greater than or equal to 2", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double sx = period * (period - 1) / 2.0;
        double sxx = period * (period - 1.0) * (2 * period - 1) / 6.0;
        double denomX = Math.FusedMultiplyAdd(period, sxx, -sx * sx);

        double sumY = 0.0;
        double sumY2 = 0.0;
        double sumXY = 0.0;
        int count = 0;
        double lastValid = 0.0;

        var buf = new RingBuffer(period);

        for (int i = 0; i < len; i++)
        {
            double val = source[i];

            if (!double.IsFinite(val))
            {
                val = lastValid;
            }
            else
            {
                lastValid = val;
            }

            if (buf.Count == buf.Capacity)
            {
                double oldest = buf.Oldest;
                sumY -= oldest;
                sumY2 -= oldest * oldest;
                sumXY -= sumY;
                sumXY += (period - 1) * val;
            }
            else
            {
                sumXY += count * val;
                count++;
            }

            sumY += val;
            sumY2 = Math.FusedMultiplyAdd(val, val, sumY2);
            buf.Add(val);

            if (count < period)
            {
                output[i] = 0.0;
                continue;
            }

            double denomY = Math.FusedMultiplyAdd(period, sumY2, -sumY * sumY);
            double denom = denomX * denomY;

            if (denom <= 0.0)
            {
                output[i] = 0.0;
                continue;
            }

            double numer = Math.FusedMultiplyAdd(period, sumXY, -sx * sumY);
            output[i] = Math.Clamp(numer / Math.Sqrt(denom), -1.0, 1.0);
        }
    }

    /// <summary>Calculates CTI and returns both the series and the live indicator.</summary>
    public static (TSeries Results, Cti Indicator) Calculate(TSeries source, int period = 20)
    {
        var indicator = new Cti(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
