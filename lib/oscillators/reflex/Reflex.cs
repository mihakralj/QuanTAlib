using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// REFLEX: Ehlers Reflex Indicator
/// </summary>
/// <remarks>
/// Measures the reversal tendency of price by comparing a Super-Smoother-filtered
/// price against a linear extrapolation from N bars ago. John F. Ehlers (2020).
///
/// Calculation:
/// <c>SSF[n] = c1 * (src + src[1]) * 0.5 + c2 * SSF[1] + c3 * SSF[2]</c>
/// <c>slope = (Filt[N] - Filt) / N</c>
/// <c>Sum = Σ(i=1..N)[(Filt + i*slope) - Filt[i]] / N</c>
/// <c>MS = 0.04 * Sum² + 0.96 * MS[1]</c>
/// <c>Reflex = Sum / √MS</c>
/// </remarks>
/// <seealso href="Reflex.md">Detailed documentation</seealso>
/// <seealso href="reflex.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Reflex : AbstractBase
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

    // Circular buffer of size period+1 to store filt history for lookback access
    private readonly double[] _buf;
    private int _head;
    private int _snapHead;

    private const double RMS_ALPHA = 0.04;
    private const double RMS_DECAY = 0.96;
    private const int StackallocThreshold = 256;

    /// <summary>
    /// Creates Reflex with specified period.
    /// </summary>
    /// <param name="period">Lookback period for reflex measurement (must be &gt; 1)</param>
    public Reflex(int period)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), period, "Period must be at least 2.");
        }

        _period = period;

        // Super Smoother (2-pole Butterworth) at half-period cutoff
        double halfPeriod = period * 0.5;
        double a1 = Math.Exp(-1.414 * Math.PI / halfPeriod);
        double b1 = 2.0 * a1 * Math.Cos(1.414 * Math.PI / halfPeriod);
        _c2 = b1;
        _c3 = -(a1 * a1);
        _c1 = 1.0 - _c2 - _c3;

        // Circular buffer of size period+1; index 0..period
        _buf = new double[period + 1];
        _head = 0;
        _snapHead = 0;

        Name = $"Reflex({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Creates Reflex with specified source and period.
    /// Subscribes to source.Pub event.
    /// </summary>
    public Reflex(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates Reflex with a TSeries source, primes from history, then subscribes.
    /// </summary>
    public Reflex(TSeries source, int period) : this(period)
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
        Array.Clear(_buf);
        _head = 0;
        _snapHead = 0;

        int len = source.Length;
        double[]? rented = len > StackallocThreshold ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> temp = rented != null ? rented.AsSpan(0, len) : stackalloc double[len];

        try
        {
            CalculateCore(source, temp, _period, _c1, _c2, _c3, ref _s, _buf, ref _head);

            Last = new TValue(DateTime.MinValue, temp[len - 1]);
            _ps = _s;
            _snapHead = _head;
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
            _snapHead = _head;
        }
        else
        {
            _s = _ps;
            _head = _snapHead;
        }

        double val = GetValidValue(input.Value, ref _s);
        double result = Compute(val, _period, _c1, _c2, _c3, ref _s, _buf, ref _head);

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

        CalculateCore(source.Values, vSpan, _period, _c1, _c2, _c3, ref _s, _buf, ref _head);

        source.Times.CopyTo(tSpan);

        _ps = _s;
        _snapHead = _head;
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Core streaming computation: SSF → circular buffer → slope + deviation sum → RMS normalization.
    /// O(period) per bar for the deviation summation loop.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double Compute(double input, int period, double c1, double c2, double c3,
        ref State s, double[] buf, ref int head)
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

        // --- Store current filt in circular buffer ---
        // buf has size period+1; head points to the slot to write current value
        buf[head] = filt;

        int count = Math.Min(s.Count, period);

        double result = 0.0;
        if (count >= period)
        {
            // filt[period] is the oldest entry: (head - period + period+1) % (period+1)
            int bufSize = period + 1;
            int lagIdx = (head - period + bufSize) % bufSize;
            double filtLag = buf[lagIdx];

            // slope = (filtLag - filt) / period  [Pine: (Filt[N] - Filt) / N]
            double slope = (filtLag - filt) / period;

            // Sum deviations from linear extrapolation
            double sum = 0.0;
            for (int i = 1; i <= period; i++)
            {
                int idx = (head - i + bufSize) % bufSize;
                // (filt + i*slope) - filt[i]
                sum += Math.FusedMultiplyAdd((double)i, slope, filt) - buf[idx];
            }
            sum /= period;

            // RMS normalization
            s.Ms = Math.FusedMultiplyAdd(RMS_ALPHA, sum * sum, RMS_DECAY * s.Ms);
            result = s.Ms > 0.0 ? sum / Math.Sqrt(s.Ms) : 0.0;
        }

        // Advance head after storing current value and computing (so filt[1] is buf[prev_head])
        head = (head + 1) % (period + 1);

        return result;
    }

    /// <summary>
    /// Core batch calculation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateCore(ReadOnlySpan<double> source, Span<double> output,
        int period, double c1, double c2, double c3, ref State s, double[] buf, ref int head)
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

            buf[head] = filt;

            int count = Math.Min(s.Count, period);

            double result = 0.0;
            if (count >= period)
            {
                int bufSize = period + 1;
                int lagIdx = (head - period + bufSize) % bufSize;
                double filtLag = buf[lagIdx];
                double slope = (filtLag - filt) / period;

                double sum = 0.0;
                for (int j = 1; j <= period; j++)
                {
                    int idx = (head - j + bufSize) % bufSize;
                    sum += Math.FusedMultiplyAdd((double)j, slope, filt) - buf[idx];
                }
                sum /= period;

                s.Ms = Math.FusedMultiplyAdd(RMS_ALPHA, sum * sum, RMS_DECAY * s.Ms);
                result = s.Ms > 0.0 ? sum / Math.Sqrt(s.Ms) : 0.0;
            }

            head = (head + 1) % (period + 1);
            output[i] = result;
        }
    }

    /// <summary>
    /// Batch calculation returning a TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, int period)
    {
        var indicator = new Reflex(period);
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
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), period, "Period must be at least 2.");
        }

        if (source.Length == 0)
        {
            return;
        }

        double halfPeriod = period * 0.5;
        double a1 = Math.Exp(-1.414 * Math.PI / halfPeriod);
        double b1 = 2.0 * a1 * Math.Cos(1.414 * Math.PI / halfPeriod);
        double c2 = b1;
        double c3 = -(a1 * a1);
        double c1 = 1.0 - c2 - c3;

        var state = State.New();
        var buf = new double[period + 1];
        int head = 0;

        CalculateCore(source, output, period, c1, c2, c3, ref state, buf, ref head);
    }

    /// <summary>
    /// Creates a hot indicator from historical data, ready for streaming.
    /// </summary>
    public static (TSeries Results, Reflex Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Reflex(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
    public override void Reset()
    {
        _s = State.New();
        _ps = _s;
        Array.Clear(_buf);
        _head = 0;
        _snapHead = 0;
        Last = default;
    }
}
