using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// USI: Ehlers Ultimate Strength Index
/// </summary>
/// <remarks>
/// A symmetric RSI replacement that uses the UltimateSmoother filter instead of
/// Wilder's exponential smoothing. Output ranges from -1 to +1 with significantly
/// reduced lag compared to traditional RSI.
///
/// Pipeline:
/// <list type="number">
///   <item>SU = max(0, Close - Close[1]), SD = max(0, Close[1] - Close)</item>
///   <item>avgSU = SMA(SU, 4), avgSD = SMA(SD, 4)</item>
///   <item>USU = UltimateSmoother(avgSU, period), USD = UltimateSmoother(avgSD, period)</item>
///   <item>USI = (USU - USD) / (USU + USD) when denom > 0</item>
/// </list>
///
/// Reference: John F. Ehlers, "Ultimate Strength Index (USI)",
///            Technical Analysis of Stocks &amp; Commodities, November 2024.
/// </remarks>
/// <seealso href="Usi.md">Detailed documentation</seealso>
/// <seealso href="usi.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Usi : AbstractBase
{
    private const int SmaLen = 4;
    private const double MinSmoothed = 0.01;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PrevClose,
        // SMA(4) circular buffers
        double Su0, double Su1, double Su2, double Su3,
        double Sd0, double Sd1, double Sd2, double Sd3,
        int BufIdx,
        // UltimateSmoother IIR state for SU path
        double Usu1, double Usu2,
        double AvgSu1, double AvgSu2,
        // UltimateSmoother IIR state for SD path
        double Usd1, double Usd2,
        double AvgSd1, double AvgSd2,
        // Output
        double Usi,
        int Count, double LastValid)
    {
        public static State New() => new()
        {
            PrevClose = double.NaN,
            Su0 = 0, Su1 = 0, Su2 = 0, Su3 = 0,
            Sd0 = 0, Sd1 = 0, Sd2 = 0, Sd3 = 0,
            BufIdx = 0,
            Usu1 = 0, Usu2 = 0, AvgSu1 = 0, AvgSu2 = 0,
            Usd1 = 0, Usd2 = 0, AvgSd1 = 0, AvgSd2 = 0,
            Usi = 0,
            Count = 0, LastValid = 0
        };
    }

    // USF precomputed coefficients
    private readonly double _k0;  // (1 - c1)
    private readonly double _k1;  // (2*c1 - c2)
    private readonly double _k2;  // -(c1 + c3)
    private readonly double _c2;
    private readonly double _c3;

    private State _s = State.New();
    private State _ps = State.New();

    /// <summary>
    /// Creates USI with specified period.
    /// </summary>
    /// <param name="period">UltimateSmoother filter period (must be &gt; 0, default 28)</param>
    public Usi(int period = 28)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        // UltimateSmoother coefficients (same as USF)
        double arg = Math.Sqrt(2) * Math.PI / period;
        double expArg = Math.Exp(-arg);
        _c2 = 2.0 * expArg * Math.Cos(arg);
        _c3 = -(expArg * expArg);
        double c1 = (1.0 + _c2 - _c3) / 4.0;

        _k0 = 1.0 - c1;
        _k1 = 2.0 * c1 - _c2;
        _k2 = -(c1 + _c3);

        Name = $"Usi({period})";
        WarmupPeriod = period + SmaLen;
    }

    /// <summary>
    /// Creates USI with specified source and period.
    /// </summary>
    public Usi(ITValuePublisher source, int period = 28) : this(period)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates USI with a TSeries source, primes from history, then subscribes.
    /// </summary>
    public Usi(TSeries source, int period = 28) : this(period)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += Handle;
    }

    public override bool IsHot => _s.Count >= WarmupPeriod;

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _s = State.New();
        _ps = State.New();

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                _s.LastValid = val;
            }
            else
            {
                val = _s.LastValid;
            }

            Step(val);
        }

        Last = new TValue(DateTime.MinValue, _s.Usi);
        _ps = _s;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        double val = input.Value;
        if (double.IsFinite(val))
        {
            _s.LastValid = val;
        }
        else
        {
            val = _s.LastValid;
        }

        Step(val);

        Last = new TValue(input.Time, _s.Usi);
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

        source.Times.CopyTo(tSpan);

        Reset();
        for (int i = 0; i < len; i++)
        {
            double val = source.Values[i];
            if (double.IsFinite(val))
            {
                _s.LastValid = val;
            }
            else
            {
                val = _s.LastValid;
            }

            Step(val);
            vSpan[i] = _s.Usi;
        }

        _ps = _s;
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Core streaming step: SU/SD → SMA(4) → UltimateSmoother → normalize.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Step(double close)
    {
        _s.Count++;

        // First bar: seed PrevClose, no SU/SD yet
        if (double.IsNaN(_s.PrevClose))
        {
            _s.PrevClose = close;
            return;
        }

        // --- Strength Up / Strength Down ---
        double diff = close - _s.PrevClose;
        double su = diff > 0 ? diff : 0.0;
        double sd = diff < 0 ? -diff : 0.0;
        _s.PrevClose = close;

        // --- SMA(4) circular buffer for SU ---
        int idx = _s.BufIdx & 3; // idx mod 4
        switch (idx)
        {
            case 0: _s.Su0 = su; _s.Sd0 = sd; break;
            case 1: _s.Su1 = su; _s.Sd1 = sd; break;
            case 2: _s.Su2 = su; _s.Sd2 = sd; break;
            case 3: _s.Su3 = su; _s.Sd3 = sd; break;
        }
        _s.BufIdx++;

        double avgSu = (_s.Su0 + _s.Su1 + _s.Su2 + _s.Su3) * 0.25;
        double avgSd = (_s.Sd0 + _s.Sd1 + _s.Sd2 + _s.Sd3) * 0.25;

        // --- UltimateSmoother for USU and USD ---
        double usu, usd;
        if (_s.Count < 8)
        {
            // Bootstrap: pass-through before IIR has enough history
            usu = avgSu;
            usd = avgSd;
        }
        else
        {
            // USF IIR: k0*avg + k1*avg[1] + k2*avg[2] + c2*USF[1] + c3*USF[2]
            usu = Math.FusedMultiplyAdd(_c3, _s.Usu2,
                Math.FusedMultiplyAdd(_c2, _s.Usu1,
                    Math.FusedMultiplyAdd(_k2, _s.AvgSu2,
                        Math.FusedMultiplyAdd(_k1, _s.AvgSu1, _k0 * avgSu))));

            usd = Math.FusedMultiplyAdd(_c3, _s.Usd2,
                Math.FusedMultiplyAdd(_c2, _s.Usd1,
                    Math.FusedMultiplyAdd(_k2, _s.AvgSd2,
                        Math.FusedMultiplyAdd(_k1, _s.AvgSd1, _k0 * avgSd))));
        }

        // Shift IIR state
        _s.Usu2 = _s.Usu1;
        _s.Usu1 = usu;
        _s.AvgSu2 = _s.AvgSu1;
        _s.AvgSu1 = avgSu;

        _s.Usd2 = _s.Usd1;
        _s.Usd1 = usd;
        _s.AvgSd2 = _s.AvgSd1;
        _s.AvgSd1 = avgSd;

        // --- Normalization ---
        double denom = usu + usd;
        if (denom > MinSmoothed)
        {
            _s.Usi = Math.Clamp((usu - usd) / denom, -1.0, 1.0);
        }
        // else: keep previous _s.Usi value (denominator near zero = flat market)
    }

    /// <summary>
    /// Batch calculation returning a TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 28)
    {
        var indicator = new Usi(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Batch calculation writing to a pre-allocated output span. Zero-allocation hot path.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 28)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (source.Length == 0)
        {
            return;
        }

        var indicator = new Usi(period);
        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                indicator._s.LastValid = val;
            }
            else
            {
                val = indicator._s.LastValid;
            }

            indicator.Step(val);
            output[i] = indicator._s.Usi;
        }
    }

    /// <summary>
    /// Creates a hot indicator from historical data, ready for streaming.
    /// </summary>
    public static (TSeries Results, Usi Indicator) Calculate(TSeries source, int period = 28)
    {
        var indicator = new Usi(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _s = State.New();
        _ps = _s;
        Last = default;
    }
}
