using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// SPBF: Ehlers Super Passband Filter
/// A wide-band bandpass filter formed by differencing two z-transformed EMAs.
/// Rejects both DC trend and high-frequency noise, passing only the cyclic energy
/// between two EMA-defined cutoff periods. Output oscillates around zero.
/// </summary>
/// <remarks>
/// The algorithm is based on a Pine Script implementation:
/// https://github.com/mihakralj/pinescript/blob/main/indicators/filters/spbf.md
///
/// Key properties:
///   - Passband via differenced EMAs: PB = EMA_short - EMA_long (in z-domain)
///   - Second-order IIR recurrence with O(1) streaming update
///   - RMS trigger envelope for signal/noise discrimination
///   - Zero DC gain by construction (bandpass behavior)
///   - Ehlers smoothing: alpha = 5/period (more reactive than standard 2/(N+1))
///
/// Complexity: O(1) for passband, O(rmsPeriod) for RMS envelope
/// </remarks>
[SkipLocalsInit]
public sealed class Spbf : AbstractBase
{
    private readonly double _pbCoeffSrc;   // (a1 - a2)
    private readonly double _pbCoeffSrc1;  // a2*(1-a1) - a1*(1-a2)
    private readonly double _pbCoeffPb1;   // (1-a1) + (1-a2)
    private readonly double _pbCoeffPb2;   // -(1-a1)*(1-a2)
    private readonly int _rmsPeriod;
    private readonly RingBuffer _pbBuffer;
    private ITValuePublisher? _publisher;
    private TValuePublishedHandler? _handler;
    private bool _isNew;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double Src1;       // src[1] — previous input
        public double Pb1;        // PB[1] — previous passband output
        public double Pb2;        // PB[2] — two bars ago passband output
        public double LastValid;  // Last finite input for NaN substitution
        public int Count;         // Bar count for warmup
    }

    private State _state;
    private State _p_state;

    /// <summary>Short EMA period (alpha1 = 5/shortPeriod).</summary>
    public int ShortPeriod { get; }

    /// <summary>Long EMA period (alpha2 = 5/longPeriod).</summary>
    public int LongPeriod { get; }

    /// <summary>RMS averaging period for trigger envelope.</summary>
    public int RmsPeriod => _rmsPeriod;

    /// <summary>Last computed RMS trigger level.</summary>
    public double Rms { get; private set; }

    public bool IsNew => _isNew;
    public override bool IsHot => _state.Count >= 2;

    public Spbf(int shortPeriod = 40, int longPeriod = 60, int rmsPeriod = 50)
    {
        if (shortPeriod < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(shortPeriod), "Short period must be >= 1.");
        }

        if (longPeriod < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(longPeriod), "Long period must be >= 1.");
        }

        if (rmsPeriod < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rmsPeriod), "RMS period must be >= 1.");
        }

        ShortPeriod = shortPeriod;
        LongPeriod = longPeriod;
        _rmsPeriod = rmsPeriod;
        Name = $"SPBF({shortPeriod},{longPeriod},{rmsPeriod})";
        WarmupPeriod = Math.Max(longPeriod, rmsPeriod);

        // Precompute passband recurrence coefficients
        // Ehlers smoothing convention: alpha = 5/N
        double a1 = 5.0 / shortPeriod;
        double a2 = 5.0 / longPeriod;
        // PB = (a1-a2)*src + (a2*(1-a1) - a1*(1-a2))*src[1]
        //    + ((1-a1)+(1-a2))*PB[1] - (1-a1)*(1-a2)*PB[2]
        double d1 = 1.0 - a1;
        double d2 = 1.0 - a2;
        _pbCoeffSrc = a1 - a2;
        _pbCoeffSrc1 = Math.FusedMultiplyAdd(a2, d1, -a1 * d2);  // a2*(1-a1) - a1*(1-a2)
        _pbCoeffPb1 = d1 + d2;                                      // (1-a1) + (1-a2)
        _pbCoeffPb2 = -(d1 * d2);                                   // -(1-a1)*(1-a2)

        _pbBuffer = new RingBuffer(rmsPeriod);
        _state.LastValid = double.NaN;
    }

    public Spbf(ITValuePublisher source, int shortPeriod = 40, int longPeriod = 60, int rmsPeriod = 50)
        : this(shortPeriod, longPeriod, rmsPeriod)
    {
        _publisher = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        double[] values = source.Values.ToArray();
        double[] results = new double[values.Length];

        Batch(values, results, ShortPeriod, LongPeriod, _rmsPeriod);

        TSeries output = [];
        for (int i = 0; i < values.Length; i++)
        {
            output.Add(source[i].Time, results[i]);
        }

        // Resync internal state by replaying
        Reset();
        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i]);
        }

        return output;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        _isNew = isNew;
        if (isNew)
        {
            _p_state = _state;
            _pbBuffer.Snapshot();
        }
        else
        {
            _state = _p_state;
            _pbBuffer.Restore();
        }

        var s = _state;

        // Handle bad data — last-valid substitution
        double val = input.Value;
        if (!double.IsFinite(val))
        {
            val = double.IsFinite(s.LastValid) ? s.LastValid : 0.0;
        }
        else
        {
            s.LastValid = val;
        }

        // Passband filter (z-transformed differenced EMAs)
        // PB = pbCoeffSrc*src + pbCoeffSrc1*src[1] + pbCoeffPb1*PB[1] + pbCoeffPb2*PB[2]
        double pb = Math.FusedMultiplyAdd(
            _pbCoeffSrc, val,
            Math.FusedMultiplyAdd(
                _pbCoeffSrc1, s.Src1,
                Math.FusedMultiplyAdd(_pbCoeffPb1, s.Pb1, _pbCoeffPb2 * s.Pb2)));

        // RMS trigger envelope — buffer stores pb² values, Sum gives total
        _pbBuffer.Add(pb * pb, isNew);
        double rms = Math.Sqrt(_pbBuffer.Sum / _pbBuffer.Count);

        if (isNew)
        {
            s.Src1 = val;
            s.Pb2 = s.Pb1;
            s.Pb1 = pb;
            s.Count++;
        }

        _state = s;
        Rms = rms;

        Last = new TValue(input.Time, pb);
        PubEvent(Last, isNew);
        return Last;
    }

    public static TSeries Batch(TSeries source, int shortPeriod = 40, int longPeriod = 60, int rmsPeriod = 50)
    {
        var indicator = new Spbf(shortPeriod, longPeriod, rmsPeriod);
        return indicator.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
        int shortPeriod = 40, int longPeriod = 60, int rmsPeriod = 50)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output spans must be of the same length.", nameof(output));
        }

        // Precompute coefficients
        double a1 = 5.0 / shortPeriod;
        double a2 = 5.0 / longPeriod;
        double d1 = 1.0 - a1;
        double d2 = 1.0 - a2;
        double cSrc = a1 - a2;
        double cSrc1 = Math.FusedMultiplyAdd(a2, d1, -a1 * d2);
        double cPb1 = d1 + d2;
        double cPb2 = -(d1 * d2);

        double src1 = 0, pb1 = 0, pb2 = 0;
        double lastValid = 0;

        if (source.Length > 0)
        {
            lastValid = source[0];
            if (!double.IsFinite(lastValid))
            {
                lastValid = 0;
            }
        }

        for (int i = 0; i < source.Length; i++)
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

            // Passband recurrence
            double pb = Math.FusedMultiplyAdd(
                cSrc, val,
                Math.FusedMultiplyAdd(
                    cSrc1, src1,
                    Math.FusedMultiplyAdd(cPb1, pb1, cPb2 * pb2)));

            output[i] = pb;

            // Shift state
            src1 = val;
            pb2 = pb1;
            pb1 = pb;
        }
    }

    /// <summary>
    /// Batch computation returning both passband and RMS arrays.
    /// </summary>
    public static void BatchWithRms(ReadOnlySpan<double> source, Span<double> passband, Span<double> rms,
        int shortPeriod = 40, int longPeriod = 60, int rmsPeriod = 50)
    {
        if (source.Length != passband.Length || source.Length != rms.Length)
        {
            throw new ArgumentException("Source, passband, and RMS spans must be of the same length.", nameof(passband));
        }

        // Compute passband first
        Batch(source, passband, shortPeriod, longPeriod, rmsPeriod);

        // Compute RMS envelope
        var ring = new RingBuffer(rmsPeriod);
        for (int i = 0; i < passband.Length; i++)
        {
            double pb = passband[i];
            ring.Add(pb * pb, true);
            rms[i] = Math.Sqrt(ring.Sum / ring.Count);
        }
    }

    public override void Reset()
    {
        _state = default;
        _state.LastValid = double.NaN;
        _p_state = default;
        _pbBuffer.Clear();
        Rms = 0;
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double val in source)
        {
            Update(new TValue(DateTime.UtcNow, val), isNew: true);
        }
    }

    public static (TSeries Results, Spbf Indicator) Calculate(TSeries source,
        int shortPeriod = 40, int longPeriod = 60, int rmsPeriod = 50)
    {
        var indicator = new Spbf(shortPeriod, longPeriod, rmsPeriod);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
            _publisher = null;
            _handler = null;
        }
        base.Dispose(disposing);
    }
}
