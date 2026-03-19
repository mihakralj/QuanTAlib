using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// PTA: Ehlers Precision Trend Analysis
/// </summary>
/// <remarks>
/// <para>
/// Dual highpass filter bandpass approach for near-zero-lag trend extraction.
/// Applies two 2-pole Butterworth highpass filters with different cutoff periods
/// to the same input, then subtracts: Trend = HP(longPeriod) - HP(shortPeriod).
/// This preserves cyclic components between shortPeriod and longPeriod bars,
/// producing a zero-centered trend indicator with near-zero lag.
/// </para>
/// <para>
/// Algorithm based on: John F. Ehlers, "Precision Trend Analysis," TASC September 2024.
/// </para>
/// <para>
/// <b>Complexity:</b> O(1) per bar — two IIR filter evaluations + subtraction.
/// </para>
/// </remarks>
[SkipLocalsInit]
public sealed class Pta : AbstractBase
{
    // ── HP1 (long-period) coefficients ──
    private readonly double _c1L, _c2L, _c3L;
    // ── HP2 (short-period) coefficients ──
    private readonly double _c1S, _c2S, _c3S;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        // HP1 (long-period highpass)
        public double Hp1;
        public double Hp1_1;
        // HP2 (short-period highpass)
        public double Hp2;
        public double Hp2_1;
        // Source history (shared by both filters)
        public double Src1;
        public double Src2;
        public int Count;

        public static State New() => new()
        {
            Hp1 = 0, Hp1_1 = 0,
            Hp2 = 0, Hp2_1 = 0,
            Src1 = 0, Src2 = 0,
            Count = 0
        };
    }

    private State _state;
    private State _p_state;

    /// <summary>Long-period cutoff for the first highpass filter.</summary>
    public int LongPeriod { get; }

    /// <summary>Short-period cutoff for the second highpass filter.</summary>
    public int ShortPeriod { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Pta"/> class.
    /// </summary>
    /// <param name="longPeriod">Long-period HP cutoff. Default is 250 (~1 year daily).</param>
    /// <param name="shortPeriod">Short-period HP cutoff. Default is 40 (~2 months daily).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when longPeriod &lt; 3, shortPeriod &lt; 2, or longPeriod &lt;= shortPeriod.
    /// </exception>
    public Pta(int longPeriod = 250, int shortPeriod = 40)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(longPeriod, 3, nameof(longPeriod));
        ArgumentOutOfRangeException.ThrowIfLessThan(shortPeriod, 2, nameof(shortPeriod));
        if (longPeriod <= shortPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(longPeriod),
                $"longPeriod ({longPeriod}) must be greater than shortPeriod ({shortPeriod}).");
        }

        LongPeriod = longPeriod;
        ShortPeriod = shortPeriod;

        // Precompute HP coefficients for long period
        ComputeHpCoefficients(longPeriod, out _c1L, out _c2L, out _c3L);
        // Precompute HP coefficients for short period
        ComputeHpCoefficients(shortPeriod, out _c1S, out _c2S, out _c3S);

        Name = $"PTA({longPeriod},{shortPeriod})";
        WarmupPeriod = longPeriod;
        _state = State.New();
        _p_state = _state;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Pta"/> class with a publisher source.
    /// </summary>
    public Pta(ITValuePublisher source, int longPeriod = 250, int shortPeriod = 40)
        : this(longPeriod, shortPeriod)
    {
        source.Pub += (object? _, in TValueEventArgs args) => Update(args.Value, args.IsNew);
    }

    /// <summary>
    /// Computes 2-pole Butterworth highpass filter coefficients.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeHpCoefficients(int period, out double c1, out double c2, out double c3)
    {
        double f = 1.414 * Math.PI / period;
        double a1 = Math.Exp(-f);
        double b1 = 2.0 * a1 * Math.Cos(f);
        c2 = b1;
        c3 = -(a1 * a1);
        c1 = (1.0 + c2 - c3) * 0.25;
    }

    public override bool IsHot => _state.Count >= 2;

    /// <summary>Primes the indicator with historical data.</summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double v in source)
        {
            Update(new TValue(DateTime.MinValue, v), isNew: true);
        }
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

        double src = input.Value;
        ref State s = ref _state;

        double result;
        if (s.Count < 2)
        {
            // Bootstrap: not enough bars for HP differentiation
            if (s.Count == 0)
            {
                s.Src1 = src;
                s.Src2 = src;
            }
            else
            {
                s.Src2 = s.Src1;
                s.Src1 = src;
            }
            s.Count++;
            result = 0.0;
        }
        else
        {
            // 2nd-order difference: src - 2*src1 + src2
            double diff = src - 2.0 * s.Src1 + s.Src2;

            // HP1 (long period): hp1 = c1L*diff + c2L*hp1 + c3L*hp1_1
            double hp1 = Math.FusedMultiplyAdd(_c1L, diff,
                          Math.FusedMultiplyAdd(_c2L, s.Hp1, _c3L * s.Hp1_1));

            // HP2 (short period): hp2 = c1S*diff + c2S*hp2 + c3S*hp2_1
            double hp2 = Math.FusedMultiplyAdd(_c1S, diff,
                          Math.FusedMultiplyAdd(_c2S, s.Hp2, _c3S * s.Hp2_1));

            // Trend = HP1 - HP2 (bandpass between shortPeriod and longPeriod)
            result = hp1 - hp2;

            // Update HP state
            s.Hp1_1 = s.Hp1;
            s.Hp1 = hp1;
            s.Hp2_1 = s.Hp2;
            s.Hp2 = hp2;

            // Update source history
            s.Src2 = s.Src1;
            s.Src1 = src;
            s.Count++;
        }

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>Updates with a full TSeries and returns results.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var resultValues = new double[source.Count];
        Batch(source.Values, resultValues, LongPeriod, ShortPeriod);

        var result = new TSeries();
        var times = source.Times;
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(new TValue(times[i], resultValues[i]));
        }

        // Sync internal state
        int len = source.Count;
        if (len >= 2)
        {
            var replay = new Pta(LongPeriod, ShortPeriod);
            for (int i = 0; i < len; i++)
            {
                replay.Update(new TValue(times[i], source.Values[i]));
            }
            _state = replay._state;
        }
        _p_state = _state;
        return result;
    }

    /// <summary>Static batch on TSeries.</summary>
    public static TSeries Batch(TSeries source, int longPeriod = 250, int shortPeriod = 40)
    {
        var indicator = new Pta(longPeriod, shortPeriod);
        return indicator.Update(source);
    }

    /// <summary>
    /// Static batch calculation on spans. Zero allocation on the hot path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
                             int longPeriod = 250, int shortPeriod = 40)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output spans must be of equal length.", nameof(output));
        }
        if (source.Length == 0)
        {
            return;
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(longPeriod, 3, nameof(longPeriod));
        ArgumentOutOfRangeException.ThrowIfLessThan(shortPeriod, 2, nameof(shortPeriod));
        if (longPeriod <= shortPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(longPeriod),
                $"longPeriod ({longPeriod}) must be greater than shortPeriod ({shortPeriod}).");
        }

        // Precompute coefficients
        ComputeHpCoefficients(longPeriod, out double c1L, out double c2L, out double c3L);
        ComputeHpCoefficients(shortPeriod, out double c1S, out double c2S, out double c3S);

        // Bar 0 and 1: output = 0 (not enough history for 2nd-order diff)
        output[0] = 0.0;
        if (source.Length < 2)
        {
            return;
        }
        output[1] = 0.0;

        double hp1 = 0, hp1_1 = 0;
        double hp2 = 0, hp2_1 = 0;

        for (int i = 2; i < source.Length; i++)
        {
            double diff = source[i] - 2.0 * source[i - 1] + source[i - 2];

            double newHp1 = Math.FusedMultiplyAdd(c1L, diff,
                             Math.FusedMultiplyAdd(c2L, hp1, c3L * hp1_1));
            double newHp2 = Math.FusedMultiplyAdd(c1S, diff,
                             Math.FusedMultiplyAdd(c2S, hp2, c3S * hp2_1));

            output[i] = newHp1 - newHp2;

            hp1_1 = hp1; hp1 = newHp1;
            hp2_1 = hp2; hp2 = newHp2;
        }
    }

    /// <summary>Calculate factory returning results and indicator.</summary>
    public static (TSeries Results, Pta Indicator) Calculate(TSeries source,
        int longPeriod = 250, int shortPeriod = 40)
    {
        var indicator = new Pta(longPeriod, shortPeriod);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _state = State.New();
        _p_state = _state;
    }
}
