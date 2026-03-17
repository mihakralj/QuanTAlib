using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// DSO: Ehlers Deviation-Scaled Oscillator
/// </summary>
/// <remarks>
/// A Fisher-transformed, RMS-normalized Super Smoother oscillator.
/// Applies input whitening (Close - Close[2]), a 2-pole Super Smoother filter,
/// rolling RMS normalization, and Fisher Transform with ±0.99 clamping.
///
/// Calculation:
/// <c>Zeros = Close - Close[2]</c>
/// <c>Filt = c1/2 * (Zeros + Zeros[1]) + c2*Filt[1] + c3*Filt[2]</c>
/// <c>RMS = √(Σ(Filt²) / period)</c>
/// <c>ScaledFilt = Filt / RMS</c>
/// <c>DSO = 0.5 * ln((1 + clamp(ScaledFilt)) / (1 - clamp(ScaledFilt)))</c>
/// </remarks>
/// <seealso href="Dso.md">Detailed documentation</seealso>
/// <seealso href="dso.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Dso : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Filt, double Filt1,
        double Zeros1, double Src1, double Src2,
        double SumSquared,
        int Count, double LastValid)
    {
        public static State New() => new()
        {
            Filt = 0, Filt1 = 0,
            Zeros1 = 0, Src1 = 0, Src2 = 0,
            SumSquared = 0,
            Count = 0, LastValid = 0
        };
    }

    private readonly int _period;
    private readonly double _c1Half;
    private readonly double _c2;
    private readonly double _c3;
    private readonly double _periodRecip;

    private State _s = State.New();
    private State _ps = State.New();

    // RingBuffer for filt² values — enables O(1) rolling RMS
    private readonly RingBuffer _filtSqBuf;

    private const double FisherClamp = 0.99;
    private const double MinRms = 1e-10;

    /// <summary>
    /// Creates DSO with specified period.
    /// </summary>
    /// <param name="period">Lookback period for RMS calculation (must be ≥ 2)</param>
    public Dso(int period)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), period, "Period must be at least 2.");
        }

        _period = period;
        _periodRecip = 1.0 / period;

        // Super Smoother (2-pole Butterworth) at half-period cutoff
        double halfPeriod = period * 0.5;
        double a1 = Math.Exp(-1.414 * Math.PI / halfPeriod);
        double b1 = 2.0 * a1 * Math.Cos(1.414 * Math.PI / halfPeriod);
        _c2 = b1;
        _c3 = -(a1 * a1);
        double c1 = 1.0 - _c2 - _c3;
        _c1Half = c1 * 0.5;

        _filtSqBuf = new RingBuffer(period);

        Name = $"Dso({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Creates DSO with specified source and period.
    /// Subscribes to source.Pub event.
    /// </summary>
    public Dso(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates DSO with a TSeries source, primes from history, then subscribes.
    /// </summary>
    public Dso(TSeries source, int period) : this(period)
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
        _filtSqBuf.Clear();

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
        _filtSqBuf.Snapshot();
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
            _filtSqBuf.Snapshot();
        }
        else
        {
            _s = _ps;
            _filtSqBuf.Restore();
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
        _filtSqBuf.Snapshot();
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Core streaming step: whitening → SSF → RMS buffer update.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Step(double input)
    {
        _s.Count++;

        // Input whitening: Zeros = Close - Close[2]
        double zeros = input - _s.Src2;

        // Super Smoother filter
        double filt;
        if (_s.Count <= 2)
        {
            filt = 0.0;
        }
        else
        {
            filt = Math.FusedMultiplyAdd(_c1Half, zeros + _s.Zeros1,
                Math.FusedMultiplyAdd(_c2, _s.Filt, _c3 * _s.Filt1));
        }

        // Update RMS buffer with filt²
        double filtSq = filt * filt;
        double removed = _filtSqBuf.Add(filtSq);
        _s.SumSquared = Math.FusedMultiplyAdd(-1.0, removed, _s.SumSquared + filtSq);

        // Update state
        _s.Zeros1 = zeros;
        _s.Filt1 = _s.Filt;
        _s.Filt = filt;
        _s.Src2 = _s.Src1;
        _s.Src1 = input;
    }

    /// <summary>
    /// Computes the final DSO value: RMS normalization → Fisher Transform.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeResult()
    {
        // RMS from running sum
        double rms = Math.Sqrt(Math.Max(_s.SumSquared * _periodRecip, MinRms));

        // Scale by RMS
        double scaledFilt = rms > MinRms ? _s.Filt / rms : 0.0;

        // Fisher Transform with clamping
        double clamped = Math.Max(-FisherClamp, Math.Min(FisherClamp, scaledFilt));
        return 0.5 * Math.Log((1.0 + clamped) / (1.0 - clamped));
    }

    /// <summary>
    /// Batch calculation returning a TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, int period)
    {
        var indicator = new Dso(period);
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

        var indicator = new Dso(period);
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
    public static (TSeries Results, Dso Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Dso(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _s = State.New();
        _ps = _s;
        _filtSqBuf.Clear();
        Last = default;
    }
}
