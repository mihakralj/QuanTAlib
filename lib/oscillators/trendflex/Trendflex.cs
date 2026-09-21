using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TRENDFLEX: Ehlers Trendflex Indicator
/// </summary>
/// <remarks>
/// Measures the slope of the Super Smoother output over a lookback window,
/// normalized by its own RMS for a zero-centered, unit-scale oscillator.
/// John F. Ehlers (2013) — combines a 2-pole Butterworth low-pass (Super Smoother)
/// with O(1) cumulative slope via circular buffer and exponential RMS normalization.
///
/// Calculation:
/// <c>SSF[n] = c1 * (src + src[1]) * 0.5 + c2 * SSF[1] + c3 * SSF[2]</c>
/// <c>Slope = (n * SSF - Σ SSF[i]) / period</c>
/// <c>MS = 0.04 * Slope² + 0.96 * MS[1]</c>
/// <c>Trendflex = Slope / √MS</c>
/// </remarks>
/// <seealso href="Trendflex.md">Detailed documentation</seealso>
/// <seealso href="trendflex.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Trendflex : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Filt, double Filt1,
        double Src1, double Ms,
        int Count, double LastValid)
    {
        public static State New() => new()
        {
            Filt = 0,
            Filt1 = 0,
            Src1 = 0,
            Ms = 0,
            Count = 0,
            LastValid = 0
        };
    }

    private readonly int _period;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;

    private State _s = State.New();
    private State _ps = State.New();
    private readonly RingBuffer _buf;

    private const double RMS_ALPHA = 0.04;
    private const double RMS_DECAY = 0.96;
    private const int StackallocThreshold = 1024;

    /// <summary>
    /// Creates Trendflex with specified period.
    /// </summary>
    /// <param name="period">Lookback period for trend measurement (must be &gt; 0)</param>
    public Trendflex(int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        _period = period;

        // Super Smoother (2-pole Butterworth) coefficients
        double halfPeriod = period * 0.5;
        double a1 = Math.Exp(-1.414 * Math.PI / halfPeriod);
        double b1 = 2.0 * a1 * Math.Cos(1.414 * Math.PI / halfPeriod);
        _c2 = b1;
        _c3 = -(a1 * a1);
        _c1 = 1.0 - _c2 - _c3;

        _buf = new RingBuffer(period);

        Name = $"Trendflex({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Creates Trendflex with specified source and period.
    /// Subscribes to source.Pub event.
    /// </summary>
    public Trendflex(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates Trendflex with a TSeries source, primes from history, then subscribes.
    /// </summary>
    public Trendflex(TSeries source, int period) : this(period)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += Handle;
    }
    public override bool IsHot => _s.Count >= _period;
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _s = State.New();
        _ps = State.New();
        _buf.Clear();

        int len = source.Length;
        double[]? rented = len > StackallocThreshold ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> temp = rented != null ? rented.AsSpan(0, len) : stackalloc double[len];

        try
        {
            CalculateCore(source, temp, _period, _c1, _c2, _c3, ref _s, _buf);

            Last = new TValue(DateTime.MinValue, temp[len - 1]);
            _ps = _s;
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetValidValue(double input, ref State s)
    {
        if (double.IsFinite(input))
        {
            s.LastValid = input;
            return input;
        }
        return s.LastValid;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _buf.Snapshot();
        }
        else
        {
            _s = _ps;
            _buf.Restore();
        }

        double val = GetValidValue(input.Value, ref _s);
        double result = Compute(val, _period, _c1, _c2, _c3, ref _s, _buf);

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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

        CalculateCore(source.Values, vSpan, _period, _c1, _c2, _c3, ref _s, _buf);

        source.Times.CopyTo(tSpan);

        _ps = _s;
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Core streaming computation: SSF + slope via RingBuffer + RMS normalization.
    /// O(1) per bar.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double Compute(double input, int period, double c1, double c2, double c3,
        ref State s, RingBuffer buf)
    {
        s.Count++;

        // --- Super Smoother filter ---
        double filt;
        if (s.Count <= 2)
        {
            filt = input;
        }
        else
        {
            filt = Math.FusedMultiplyAdd(c1, (input + s.Src1) * 0.5,
                Math.FusedMultiplyAdd(c2, s.Filt, c3 * s.Filt1));
        }

        s.Filt1 = s.Filt;
        s.Filt = filt;
        s.Src1 = input;

        // --- O(1) cumulative slope ---
        // Always use Add (not UpdateNewest) because Snapshot/Restore already handles rollback
        buf.Add(filt);
        int n = Math.Min(s.Count, period);
        double slopeSum = n > 0 ? ((n * filt) - buf.Sum) / period : 0.0;

        // --- RMS normalization ---
        s.Ms = Math.FusedMultiplyAdd(RMS_ALPHA, slopeSum * slopeSum, RMS_DECAY * s.Ms);

        return s.Ms > 0 ? slopeSum / Math.Sqrt(s.Ms) : 0.0;
    }

    /// <summary>
    /// Core batch calculation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateCore(ReadOnlySpan<double> source, Span<double> output,
        int period, double c1, double c2, double c3, ref State s, RingBuffer buf)
    {
        int len = source.Length;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                s.LastValid = val;
            }
            else
            {
                val = s.LastValid;
            }

            s.Count++;

            // Super Smoother
            double filt;
            if (s.Count <= 2)
            {
                filt = val;
            }
            else
            {
                filt = Math.FusedMultiplyAdd(c1, (val + s.Src1) * 0.5,
                    Math.FusedMultiplyAdd(c2, s.Filt, c3 * s.Filt1));
            }

            s.Filt1 = s.Filt;
            s.Filt = filt;
            s.Src1 = val;

            // Slope
            buf.Add(filt);
            int n = Math.Min(s.Count, period);
            double slopeSum = n > 0 ? ((n * filt) - buf.Sum) / period : 0.0;

            // RMS
            s.Ms = Math.FusedMultiplyAdd(RMS_ALPHA, slopeSum * slopeSum, RMS_DECAY * s.Ms);

            output[i] = s.Ms > 0 ? slopeSum / Math.Sqrt(s.Ms) : 0.0;
        }
    }

    /// <summary>
    /// Batch calculation returning a TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, int period)
    {
        var indicator = new Trendflex(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Batch calculation writing to a pre-allocated output span. Zero-allocation hot path.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        if (source.Length == 0)
        {
            return;
        }

        // Compute SSF coefficients
        double halfPeriod = period * 0.5;
        double a1 = Math.Exp(-1.414 * Math.PI / halfPeriod);
        double b1 = 2.0 * a1 * Math.Cos(1.414 * Math.PI / halfPeriod);
        double c2 = b1;
        double c3 = -(a1 * a1);
        double c1 = 1.0 - c2 - c3;

        var state = State.New();
        var buf = new RingBuffer(period);

        CalculateCore(source, output, period, c1, c2, c3, ref state, buf);
    }

    /// <summary>
    /// Creates a hot indicator from historical data, ready for streaming.
    /// </summary>
    public static (TSeries Results, Trendflex Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Trendflex(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
    public override void Reset()
    {
        _s = State.New();
        _ps = _s;
        _buf.Clear();
        Last = default;
    }
}
