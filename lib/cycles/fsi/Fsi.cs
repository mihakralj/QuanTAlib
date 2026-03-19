using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// FSI: Ehlers Fourier Series Indicator
/// </summary>
/// <remarks>
/// <para>
/// Decomposes price cycles into three harmonic bandpass components and reconstructs
/// a waveshape using amplitude-weighted Fourier synthesis. The output represents the
/// dominant cycle content of price, useful for identifying turning points.
/// </para>
/// <para>
/// Algorithm (TASC June 2019, John F. Ehlers "Fourier Series Model of the Market"):
/// <list type="number">
///   <item>Apply Ehlers bandpass filters at the fundamental period and its 2nd/3rd harmonics</item>
///   <item>Compute quadrature components Q = (period/2π) · d(BP)/dt for each harmonic</item>
///   <item>Estimate power P_k = Sum(BP_k² + Q_k², period) for each harmonic</item>
///   <item>Reconstruct: FSI = BP1 + sqrt(P2/P1)·BP2 + sqrt(P3/P1)·BP3</item>
/// </list>
/// </para>
/// <para>
/// <b>Complexity:</b> O(1) per bar — three IIR bandpass evaluations + rolling power sums.
/// </para>
/// </remarks>
[SkipLocalsInit]
public sealed class Fsi : AbstractBase
{
    // ── Bandpass coefficients per harmonic ──
    // Fundamental (period = x)
    private readonly double _a0_1, _a1_1, _a2_1;
    // 2nd harmonic (period = x/2)
    private readonly double _a0_2, _a1_2, _a2_2;
    // 3rd harmonic (period = x/3)
    private readonly double _a0_3, _a1_3, _a2_3;

    // Quadrature scale factor: period / (2π)
    private readonly double _qScale;

    // Rolling power sum buffers
    private readonly RingBuffer _p1Buf;
    private readonly RingBuffer _p2Buf;
    private readonly RingBuffer _p3Buf;

    private const int DefaultPeriod = 20;
    private const double DefaultBandwidth = 0.1;
    private const int MinPeriod = 6;
    private const double MinBandwidth = 0.001;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Bp1, double Bp1_1,   // fundamental bandpass
        double Bp2, double Bp2_1,   // 2nd harmonic bandpass
        double Bp3, double Bp3_1,   // 3rd harmonic bandpass
        double Src1, double Src2,   // previous close values
        double LastValid,
        int Count);

    private State _state;
    private State _p_state;

    /// <summary>Fundamental cycle period.</summary>
    public int Period { get; }

    /// <summary>Bandpass filter bandwidth.</summary>
    public double Bandwidth { get; }

    /// <summary>
    /// Creates an FSI indicator.
    /// </summary>
    /// <param name="period">Fundamental cycle length (default 20, minimum 6).</param>
    /// <param name="bandwidth">Bandpass filter bandwidth (default 0.1).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Fsi(int period = DefaultPeriod, double bandwidth = DefaultBandwidth)
    {
        if (period < MinPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                $"Period must be at least {MinPeriod}.");
        }
        if (bandwidth < MinBandwidth)
        {
            throw new ArgumentOutOfRangeException(nameof(bandwidth),
                $"Bandwidth must be at least {MinBandwidth}.");
        }

        Period = period;
        Bandwidth = bandwidth;

        // Precompute coefficients for three harmonics
        ComputeBpCoefficients(period, bandwidth, out _a0_1, out _a1_1, out _a2_1);
        ComputeBpCoefficients(period / 2.0, bandwidth, out _a0_2, out _a1_2, out _a2_2);
        ComputeBpCoefficients(period / 3.0, bandwidth, out _a0_3, out _a1_3, out _a2_3);

        _qScale = period / (2.0 * Math.PI);

        _p1Buf = new RingBuffer(period);
        _p2Buf = new RingBuffer(period);
        _p3Buf = new RingBuffer(period);

        Name = $"FSI({period},{bandwidth:F2})";
        WarmupPeriod = period;
        _state = new State { LastValid = double.NaN };
        _p_state = _state;
    }

    /// <summary>
    /// Creates an FSI indicator subscribed to a publisher source.
    /// </summary>
    public Fsi(ITValuePublisher source, int period = DefaultPeriod, double bandwidth = DefaultBandwidth)
        : this(period, bandwidth)
    {
        source.Pub += (object? _, in TValueEventArgs args) => Update(args.Value, args.IsNew);
    }

    /// <summary>
    /// Computes Ehlers bandpass filter coefficients for a given period.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeBpCoefficients(double period, double bandwidth,
        out double a0, out double a1, out double a2)
    {
        double twoPiOverP = 2.0 * Math.PI / period;
        double L = Math.Cos(twoPiOverP);
        double G = Math.Cos(bandwidth * twoPiOverP);
        double S = (1.0 / G) - Math.Sqrt((1.0 / (G * G)) - 1.0);

        a0 = 0.5 * (1.0 - S);
        a1 = L * (1.0 + S);
        a2 = -S;
    }

    public override bool IsHot => _state.Count >= WarmupPeriod;

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
            _p1Buf.Snapshot();
            _p2Buf.Snapshot();
            _p3Buf.Snapshot();
        }
        else
        {
            _state = _p_state;
            _p1Buf.Restore();
            _p2Buf.Restore();
            _p3Buf.Restore();
        }

        double src = input.Value;

        // Sanitize NaN/Inf
        if (!double.IsFinite(src))
        {
            src = double.IsFinite(_state.LastValid) ? _state.LastValid : 0.0;
        }
        else
        {
            _state.LastValid = src;
        }

        ref State s = ref _state;
        double result;

        if (s.Count < 2)
        {
            // Bootstrap: not enough bars for 2nd-order difference
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
            double diff = src - s.Src2; // Close[i] - Close[i-2]

            // ── Bandpass filters ──
            double bp1 = Math.FusedMultiplyAdd(_a0_1, diff,
                          Math.FusedMultiplyAdd(_a1_1, s.Bp1, _a2_1 * s.Bp1_1));
            double bp2 = Math.FusedMultiplyAdd(_a0_2, diff,
                          Math.FusedMultiplyAdd(_a1_2, s.Bp2, _a2_2 * s.Bp2_1));
            double bp3 = Math.FusedMultiplyAdd(_a0_3, diff,
                          Math.FusedMultiplyAdd(_a1_3, s.Bp3, _a2_3 * s.Bp3_1));

            // ── Quadrature components (differentiation-based 90° phase shift) ──
            double q1 = _qScale * (bp1 - s.Bp1);
            double q2 = _qScale * (bp2 - s.Bp2);
            double q3 = _qScale * (bp3 - s.Bp3);

            // ── Power estimation (rolling sum over period bars) ──
            double pw1 = (bp1 * bp1) + (q1 * q1);
            double pw2 = (bp2 * bp2) + (q2 * q2);
            double pw3 = (bp3 * bp3) + (q3 * q3);

            _p1Buf.Add(pw1);
            _p2Buf.Add(pw2);
            _p3Buf.Add(pw3);

            double p1Sum = _p1Buf.Sum;
            double p2Sum = _p2Buf.Sum;
            double p3Sum = _p3Buf.Sum;

            // ── Amplitude-weighted reconstruction ──
            if (p1Sum > 1e-20)
            {
                double w2 = Math.Sqrt(p2Sum / p1Sum);
                double w3 = Math.Sqrt(p3Sum / p1Sum);
                result = bp1 + (w2 * bp2) + (w3 * bp3);
            }
            else
            {
                result = bp1;
            }

            // Update bandpass state
            s.Bp1_1 = s.Bp1; s.Bp1 = bp1;
            s.Bp2_1 = s.Bp2; s.Bp2 = bp2;
            s.Bp3_1 = s.Bp3; s.Bp3 = bp3;

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
        Batch(source.Values, resultValues, Period, Bandwidth);

        var result = new TSeries();
        var times = source.Times;
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(new TValue(times[i], resultValues[i]));
        }

        // Sync internal state by replaying
        int len = source.Count;
        if (len >= 2)
        {
            var replay = new Fsi(Period, Bandwidth);
            for (int i = 0; i < len; i++)
            {
                replay.Update(new TValue(times[i], source.Values[i]));
            }
            _state = replay._state;
            _p1Buf.CopyFrom(replay._p1Buf);
            _p2Buf.CopyFrom(replay._p2Buf);
            _p3Buf.CopyFrom(replay._p3Buf);
        }
        _p_state = _state;
        return result;
    }

    /// <summary>Static batch on TSeries.</summary>
    public static TSeries Batch(TSeries source, int period = DefaultPeriod, double bandwidth = DefaultBandwidth)
    {
        var indicator = new Fsi(period, bandwidth);
        return indicator.Update(source);
    }

    /// <summary>
    /// Static batch calculation on spans. Zero allocation on the hot path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
                             int period = DefaultPeriod, double bandwidth = DefaultBandwidth)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output spans must be of equal length.", nameof(output));
        }
        if (source.Length == 0)
        {
            return;
        }
        if (period < MinPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                $"Period must be at least {MinPeriod}.");
        }
        if (bandwidth < MinBandwidth)
        {
            throw new ArgumentOutOfRangeException(nameof(bandwidth),
                $"Bandwidth must be at least {MinBandwidth}.");
        }

        // Precompute coefficients
        ComputeBpCoefficients(period, bandwidth, out double a0_1, out double a1_1, out double a2_1);
        ComputeBpCoefficients(period / 2.0, bandwidth, out double a0_2, out double a1_2, out double a2_2);
        ComputeBpCoefficients(period / 3.0, bandwidth, out double a0_3, out double a1_3, out double a2_3);
        double qScale = period / (2.0 * Math.PI);

        // Bandpass state
        double bp1 = 0, bp1_1 = 0;
        double bp2 = 0, bp2_1 = 0;
        double bp3 = 0, bp3_1 = 0;

        // Power ring buffers
        Span<double> pw1Buf = stackalloc double[period];
        pw1Buf.Clear();
        Span<double> pw2Buf = stackalloc double[period];
        pw2Buf.Clear();
        Span<double> pw3Buf = stackalloc double[period];
        pw3Buf.Clear();

        double p1Sum = 0, p2Sum = 0, p3Sum = 0;
        int pwIdx = 0;

        // First two bars: no 2nd-order difference available
        output[0] = 0.0;
        if (source.Length < 2)
        {
            return;
        }
        output[1] = 0.0;

        for (int i = 2; i < source.Length; i++)
        {
            double diff = source[i] - source[i - 2];

            // Bandpass filters
            double newBp1 = Math.FusedMultiplyAdd(a0_1, diff,
                             Math.FusedMultiplyAdd(a1_1, bp1, a2_1 * bp1_1));
            double newBp2 = Math.FusedMultiplyAdd(a0_2, diff,
                             Math.FusedMultiplyAdd(a1_2, bp2, a2_2 * bp2_1));
            double newBp3 = Math.FusedMultiplyAdd(a0_3, diff,
                             Math.FusedMultiplyAdd(a1_3, bp3, a2_3 * bp3_1));

            // Quadrature
            double q1 = qScale * (newBp1 - bp1);
            double q2 = qScale * (newBp2 - bp2);
            double q3 = qScale * (newBp3 - bp3);

            // Power
            double pw1 = (newBp1 * newBp1) + (q1 * q1);
            double pw2 = (newBp2 * newBp2) + (q2 * q2);
            double pw3 = (newBp3 * newBp3) + (q3 * q3);

            // Rolling sum update
            p1Sum = p1Sum - pw1Buf[pwIdx] + pw1;
            p2Sum = p2Sum - pw2Buf[pwIdx] + pw2;
            p3Sum = p3Sum - pw3Buf[pwIdx] + pw3;
            pw1Buf[pwIdx] = pw1;
            pw2Buf[pwIdx] = pw2;
            pw3Buf[pwIdx] = pw3;
            pwIdx = (pwIdx + 1) % period;

            // Amplitude-weighted reconstruction
            if (p1Sum > 1e-20)
            {
                double w2 = Math.Sqrt(p2Sum / p1Sum);
                double w3 = Math.Sqrt(p3Sum / p1Sum);
                output[i] = newBp1 + (w2 * newBp2) + (w3 * newBp3);
            }
            else
            {
                output[i] = newBp1;
            }

            // Advance state
            bp1_1 = bp1; bp1 = newBp1;
            bp2_1 = bp2; bp2 = newBp2;
            bp3_1 = bp3; bp3 = newBp3;
        }
    }

    /// <summary>Calculate factory returning results and indicator.</summary>
    public static (TSeries Results, Fsi Indicator) Calculate(TSeries source,
        int period = DefaultPeriod, double bandwidth = DefaultBandwidth)
    {
        var indicator = new Fsi(period, bandwidth);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _state = new State { LastValid = double.NaN };
        _p_state = _state;
        _p1Buf.Clear();
        _p2Buf.Clear();
        _p3Buf.Clear();
    }
}
