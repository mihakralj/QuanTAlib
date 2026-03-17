using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// EEO: Ehlers Elegant Oscillator
/// </summary>
/// <remarks>
/// An Inverse Fisher Transform (IFT) applied to RMS-normalized 2-bar momentum,
/// smoothed by a 2-pole Super Smoother filter. Output bounded approximately [-1, +1].
///
/// Calculation:
/// <c>Deriv = Close - Close[2]</c>
/// <c>RMS = √(Σ(Deriv²) / 50)</c>     (fixed 50-bar window)
/// <c>NDeriv = Deriv / RMS</c>
/// <c>IFish = tanh(NDeriv) = (e^(2·NDeriv) - 1) / (e^(2·NDeriv) + 1)</c>
/// <c>SS = c1/2 * (IFish + IFish[1]) + c2*SS[1] + c3*SS[2]</c>
/// </remarks>
/// <seealso href="Eeo.md">Detailed documentation</seealso>
/// <seealso href="eeo.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Eeo : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Deriv, double Src1, double Src2,
        double SumSquared,
        double IFish1,
        double SS, double SS1,
        int Count, double LastValid)
    {
        public static State New() => new()
        {
            Deriv = 0, Src1 = 0, Src2 = 0,
            SumSquared = 0,
            IFish1 = 0,
            SS = 0, SS1 = 0,
            Count = 0, LastValid = 0
        };
    }

    private const int RmsWindow = 50;
    private const double MinRms = 1e-10;

    private readonly int _bandEdge;
    private readonly double _c1Half;
    private readonly double _c2;
    private readonly double _c3;
    private readonly double _rmsRecip;

    private State _s = State.New();
    private State _ps = State.New();

    // RingBuffer for deriv² values — enables O(1) rolling RMS
    private readonly RingBuffer _derivSqBuf;

    /// <summary>
    /// Creates EEO with specified band edge period.
    /// </summary>
    /// <param name="bandEdge">Super Smoother cutoff period (must be ≥ 2)</param>
    public Eeo(int bandEdge = 20)
    {
        if (bandEdge < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(bandEdge), bandEdge, "BandEdge must be at least 2.");
        }

        _bandEdge = bandEdge;
        _rmsRecip = 1.0 / RmsWindow;

        // Super Smoother (2-pole Butterworth) at BandEdge cutoff
        double a1 = Math.Exp(-1.414 * Math.PI / bandEdge);
        double b1 = 2.0 * a1 * Math.Cos(1.414 * Math.PI / bandEdge);
        _c2 = b1;
        _c3 = -(a1 * a1);
        double c1 = 1.0 - _c2 - _c3;
        _c1Half = c1 * 0.5;

        _derivSqBuf = new RingBuffer(RmsWindow);

        Name = $"Eeo({bandEdge})";
        WarmupPeriod = RmsWindow + bandEdge;
    }

    /// <summary>
    /// Creates EEO with specified source and band edge.
    /// Subscribes to source.Pub event.
    /// </summary>
    public Eeo(ITValuePublisher source, int bandEdge = 20) : this(bandEdge)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates EEO with a TSeries source, primes from history, then subscribes.
    /// </summary>
    public Eeo(TSeries source, int bandEdge = 20) : this(bandEdge)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += Handle;
    }

    public override bool IsHot => _s.Count >= RmsWindow + _bandEdge;

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _s = State.New();
        _ps = State.New();
        _derivSqBuf.Clear();

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
        _derivSqBuf.Snapshot();
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
            _derivSqBuf.Snapshot();
        }
        else
        {
            _s = _ps;
            _derivSqBuf.Restore();
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
        _derivSqBuf.Snapshot();
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Core streaming step: derivative → RMS buffer update → IFT → SSF.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Step(double input)
    {
        _s.Count++;

        // 2-bar momentum (derivative)
        double deriv = input - _s.Src2;

        // Update RMS buffer with deriv²
        double derivSq = deriv * deriv;
        double removed = _derivSqBuf.Add(derivSq);
        _s.SumSquared = Math.FusedMultiplyAdd(-1.0, removed, _s.SumSquared + derivSq);

        // RMS normalization
        double rms = Math.Sqrt(Math.Max(_s.SumSquared * _rmsRecip, MinRms));
        double nDeriv = rms > MinRms ? deriv / rms : 0.0;

        // Inverse Fisher Transform: tanh(nDeriv)
        double iFish = Math.Tanh(nDeriv);

        // Super Smoother filter
        double ss;
        if (_s.Count <= 2)
        {
            ss = 0.0;
        }
        else
        {
            ss = Math.FusedMultiplyAdd(_c1Half, iFish + _s.IFish1,
                Math.FusedMultiplyAdd(_c2, _s.SS, _c3 * _s.SS1));
        }

        // Update state
        _s.IFish1 = iFish;
        _s.SS1 = _s.SS;
        _s.SS = ss;
        _s.Deriv = deriv;
        _s.Src2 = _s.Src1;
        _s.Src1 = input;
    }

    /// <summary>
    /// Returns the current Super Smoother output.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeResult() => _s.SS;

    /// <summary>
    /// Batch calculation returning a TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, int bandEdge = 20)
    {
        var indicator = new Eeo(bandEdge);
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

        var indicator = new Eeo(bandEdge);
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
    public static (TSeries Results, Eeo Indicator) Calculate(TSeries source, int bandEdge = 20)
    {
        var indicator = new Eeo(bandEdge);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _s = State.New();
        _ps = _s;
        _derivSqBuf.Clear();
        Last = default;
    }
}
