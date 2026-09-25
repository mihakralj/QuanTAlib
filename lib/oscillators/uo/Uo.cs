using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// UO: Ehlers Universal Oscillator
/// </summary>
/// <remarks>
/// A white-noise extraction followed by Super Smoother filtering and AGC peak
/// normalization. Output bounded [-1, +1].
///
/// Calculation:
/// <c>WhiteNoise = (Close - Close[2]) / 2</c>
/// <c>Filt = c1/2 * (WN + WN[1]) + c2*Filt[1] + c3*Filt[2]</c>
/// <c>Peak = max(0.991 * Peak, |Filt|)</c>
/// <c>Universal = Peak > 0 ? Filt / Peak : 0</c>
///
/// Reference: Ehlers, J.F. (2015). "Relax, It's Just Noise — Whiter Is Brighter."
///            Technical Analysis of Stocks &amp; Commodities, January 2015.
/// </remarks>
/// <seealso href="uo.md">Detailed documentation</seealso>
/// <seealso href="uo.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Uo : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Src1, double Src2,
        double WN1,
        double Filt, double Filt1,
        double Peak,
        int Count, double LastValid)
    {
        public static State New() => new()
        {
            Src1 = 0, Src2 = 0,
            WN1 = 0,
            Filt = 0, Filt1 = 0,
            Peak = 0,
            Count = 0, LastValid = 0
        };
    }

    private const double AgcDecay = 0.991;
    private const double MinPeak = 1e-10;

    private readonly int _bandEdge;
    private readonly double _c1Half;
    private readonly double _c2;
    private readonly double _c3;

    private State _s = State.New();
    private State _ps = State.New();

    /// <summary>
    /// Creates UO with specified band edge period.
    /// </summary>
    /// <param name="bandEdge">Super Smoother cutoff period (must be ≥ 2)</param>
    public Uo(int bandEdge = 20)
    {
        if (bandEdge < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(bandEdge), bandEdge, "BandEdge must be at least 2.");
        }

        _bandEdge = bandEdge;

        // Super Smoother (2-pole Butterworth) at BandEdge cutoff
        double a1 = Math.Exp(-1.414 * Math.PI / bandEdge);
        double b1 = 2.0 * a1 * Math.Cos(1.414 * Math.PI / bandEdge);
        _c2 = b1;
        _c3 = -(a1 * a1);
        double c1 = 1.0 - _c2 - _c3;
        _c1Half = c1 * 0.5;

        Name = $"Uo({bandEdge})";
        WarmupPeriod = bandEdge;
    }

    /// <summary>
    /// Creates UO with specified source and band edge.
    /// Subscribes to source.Pub event.
    /// </summary>
    public Uo(ITValuePublisher source, int bandEdge = 20) : this(bandEdge)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates UO with a TSeries source, primes from history, then subscribes.
    /// </summary>
    public Uo(TSeries source, int bandEdge = 20) : this(bandEdge)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += Handle;
    }

    public override bool IsHot => _s.Count >= _bandEdge;

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _s = State.New();
        _ps = State.New();

        int len = source.Length;
        for (int i = 0; i < len; i++)
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

        Last = new TValue(DateTime.MinValue, ComputeResult());
        _ps = _s;
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
        }
        else
        {
            _s = _ps;
        }

        double val = GetValidValue(input.Value, ref _s);
        Step(val);
        double result = ComputeResult();

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
            vSpan[i] = ComputeResult();
        }

        _ps = _s;
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Core streaming step: white noise → Super Smoother → AGC.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Step(double input)
    {
        _s.Count++;

        // White noise extraction: (Close - Close[2]) / 2
        double wn = (input - _s.Src2) * 0.5;

        // Super Smoother filter
        double filt;
        if (_s.Count <= 2)
        {
            filt = 0.0;
        }
        else
        {
            filt = Math.FusedMultiplyAdd(_c1Half, wn + _s.WN1,
                Math.FusedMultiplyAdd(_c2, _s.Filt, _c3 * _s.Filt1));
        }

        // AGC peak tracking
        double peak = AgcDecay * _s.Peak;
        double absFilt = Math.Abs(filt);
        if (absFilt > peak)
        {
            peak = absFilt;
        }

        // Update state
        _s.WN1 = wn;
        _s.Filt1 = _s.Filt;
        _s.Filt = filt;
        _s.Peak = peak;
        _s.Src2 = _s.Src1;
        _s.Src1 = input;
    }

    /// <summary>
    /// Returns the AGC-normalized Super Smoother output.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeResult() => _s.Peak > MinPeak ? _s.Filt / _s.Peak : 0.0;

    /// <summary>
    /// Batch calculation returning a TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, int bandEdge = 20)
    {
        var indicator = new Uo(bandEdge);
        return indicator.Update(source);
    }

    /// <summary>
    /// Batch calculation writing to a pre-allocated output span. Zero-allocation hot path.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int bandEdge = 20)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (bandEdge < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(bandEdge), bandEdge, "BandEdge must be at least 2.");
        }

        if (source.Length == 0)
        {
            return;
        }

        var indicator = new Uo(bandEdge);
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
            output[i] = indicator.ComputeResult();
        }
    }

    /// <summary>
    /// Creates a hot indicator from historical data, ready for streaming.
    /// </summary>
    public static (TSeries Results, Uo Indicator) Calculate(TSeries source, int bandEdge = 20)
    {
        var indicator = new Uo(bandEdge);
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
