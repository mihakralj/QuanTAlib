using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// BPF: Bandpass Filter
/// A combined highpass and lowpass filter that passes frequencies within a specific range.
/// The filter is implemented as a cascade of a 2nd-order Highpass filter and a 2nd-order Lowpass filter.
/// </summary>
/// <remarks>
/// The algorithm is based on a Pine Script implementation:
/// https://github.com/mihakralj/pinescript/blob/main/indicators/filters/bpf.md
/// 
/// Complexity: O(1)
/// Computation: 7 multiplications, 6 additions per cycle
/// </remarks>
[SkipLocalsInit]
public sealed class Bpf : AbstractBase
{
    private readonly double _hpC1, _hpC2, _hpC3;
    private readonly double _lpC1, _lpC2, _lpC3;
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;

    // State buffer: [src1, src2, hp1, hp2, bp1, bp2]
    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double Src1, Src2;
        public double Hp1, Hp2;
        public double Bp1, Bp2;
        public double LastValid;
    }

    private State _state;
    private State _p_state; // Previous state for rollback

    /// <summary>
    /// Lower cutoff period (High-pass filter cutoff).
    /// </summary>
    public int LowerPeriod { get; }

    /// <summary>
    /// Upper cutoff period (Low-pass filter cutoff).
    /// </summary>
    public int UpperPeriod { get; }

    public override bool IsHot => double.IsFinite(_state.Bp2); // Sufficiently warm when we have history

    public Bpf(int lowerPeriod, int upperPeriod)
    {
        if (lowerPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(lowerPeriod), "Lower period must be >= 1");
        if (upperPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(upperPeriod), "Upper period must be >= 1");

        LowerPeriod = lowerPeriod;
        UpperPeriod = upperPeriod;
        Name = $"BPF({lowerPeriod},{upperPeriod})";
        WarmupPeriod = Math.Max(lowerPeriod, upperPeriod); // Approximate warmup

        // Precompute Highpass coefficients
        double sqrt2Pi = Math.Sqrt(2.0) * Math.PI;
        double hpArg = sqrt2Pi / lowerPeriod;
        double hpExpArg = Math.Exp(-hpArg);
        _hpC2 = 2.0 * hpExpArg * Math.Cos(hpArg);
        _hpC3 = -hpExpArg * hpExpArg;
        _hpC1 = (1.0 + _hpC2 - _hpC3) * 0.25;

        // Precompute Lowpass coefficients
        double lpArg = sqrt2Pi / upperPeriod;
        double lpExpArg = Math.Exp(-lpArg);
        _lpC2 = 2.0 * lpExpArg * Math.Cos(lpArg);
        _lpC3 = -lpExpArg * lpExpArg;
        _lpC1 = 1.0 - _lpC2 - _lpC3;
    }

    public Bpf(ITValuePublisher source, int lowerPeriod, int upperPeriod) : this(lowerPeriod, upperPeriod)
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
        // Use the Span-based calculation for performance
        if (source.Count == 0) return new TSeries();

        double[] values = source.Values.ToArray();
        double[] results = new double[values.Length];
        
        Calculate(values, results, LowerPeriod, UpperPeriod);

        // Create TSeries from results
        TSeries output = new TSeries();
        for (int i = 0; i < values.Length; i++)
        {
            output.Add(source[i].Time, results[i]);
        }
        
        // Update internal state to match the end of the batch
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
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        // Handle bad data
        double val = input.Value;
        if (!double.IsFinite(val))
        {
            val = _state.LastValid;
        }
        else
        {
            _state.LastValid = val;
        }
        
        // Highpass Filter Step
        // hp = hp_c1 * (val - 2*src1 + src2) + hp_c2 * hp1 + hp_c3 * hp2
        double term1 = _hpC1 * (val - 2.0 * _state.Src1 + _state.Src2);
        double hp = Math.FusedMultiplyAdd(_hpC2, _state.Hp1, Math.FusedMultiplyAdd(_hpC3, _state.Hp2, term1));

        // Lowpass Filter Step (Bandpass output)
        // bpf = lp_c1 * hp + lp_c2 * bp1 + lp_c3 * bp2
        double bpf = Math.FusedMultiplyAdd(_lpC1, hp, Math.FusedMultiplyAdd(_lpC2, _state.Bp1, _lpC3 * _state.Bp2));

        if (isNew)
        {
            _state.Src2 = _state.Src1;
            _state.Src1 = val;
            _state.Hp2 = _state.Hp1;
            _state.Hp1 = hp;
            _state.Bp2 = _state.Bp1;
            _state.Bp1 = bpf;
        }

        Last = new TValue(input.Time, bpf);
        PubEvent(Last, isNew);
        return Last;
    }

    public override void Reset()
    {
        _state = default;
        _p_state = default;
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double val in source)
        {
            Update(new TValue(DateTime.UtcNow, val), isNew: true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int lowerPeriod, int upperPeriod)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output spans must be of the same length.", nameof(output));

        // Coefficients
        double sqrt2Pi = Math.Sqrt(2.0) * Math.PI;

        double hpArg = sqrt2Pi / lowerPeriod;
        double hpExpArg = Math.Exp(-hpArg);
        double hpC2 = 2.0 * hpExpArg * Math.Cos(hpArg);
        double hpC3 = -hpExpArg * hpExpArg;
        double hpC1 = (1.0 + hpC2 - hpC3) * 0.25;

        double lpArg = sqrt2Pi / upperPeriod;
        double lpExpArg = Math.Exp(-lpArg);
        double lpC2 = 2.0 * lpExpArg * Math.Cos(lpArg);
        double lpC3 = -lpExpArg * lpExpArg;
        double lpC1 = 1.0 - lpC2 - lpC3;

        // State variables
        double src1 = 0, src2 = 0;
        double hp1 = 0, hp2 = 0;
        double bp1 = 0, bp2 = 0;
        double lastValid = 0;

        // Handle first value if needed or assume 0
        if (source.Length > 0)
        {
            lastValid = source[0];
            if (!double.IsFinite(lastValid)) lastValid = 0;
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

            // Highpass
            double term1 = hpC1 * (val - 2.0 * src1 + src2);
            double hp = Math.FusedMultiplyAdd(hpC2, hp1, Math.FusedMultiplyAdd(hpC3, hp2, term1));

            // Lowpass
            double bpf = Math.FusedMultiplyAdd(lpC1, hp, Math.FusedMultiplyAdd(lpC2, bp1, lpC3 * bp2));

            output[i] = bpf;

            // Shift state
            src2 = src1;
            src1 = val;
            hp2 = hp1;
            hp1 = hp;
            bp2 = bp1;
            bp1 = bpf;
        }
    }

    /// <summary>
    /// Unsubscribes from the source publisher if one was provided during construction.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
        }
        base.Dispose(disposing);
    }
}
