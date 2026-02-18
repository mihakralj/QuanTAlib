using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ROOFING: Ehlers Roofing Filter
/// A bandpass filter combining a 2nd-order Butterworth Highpass (removes trend) with
/// a 2nd-order Super Smoother Lowpass (removes noise). Passes frequencies between
/// the two cutoff periods, oscillating around zero.
/// </summary>
/// <remarks>
/// The algorithm is based on a Pine Script implementation:
/// https://github.com/mihakralj/pinescript/blob/main/indicators/filters/roofing.md
///
/// Key properties:
///   - HP stage removes cycles longer than hpLength (detrends)
///   - SS stage removes cycles shorter than ssLength (smooths)
///   - Output oscillates around zero (bandpass behavior)
///   - Zero crossings serve as trading signals
///
/// Complexity: O(1)
/// Computation: 7 multiplications, 6 additions per cycle
/// </remarks>
[SkipLocalsInit]
public sealed class Roofing : AbstractBase
{
    private readonly double _hpC1, _hpC2, _hpC3;
    private readonly double _ssC1, _ssC2, _ssC3;
    private ITValuePublisher? _publisher;
    private TValuePublishedHandler? _handler;
    private bool _isNew;

    // State buffer: [src1, src2, hp1, hp2, roof1, roof2]
    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double Src1, Src2;
        public double Hp1, Hp2;
        public double Roof1, Roof2;
        public double LastValid;
    }

    private State _state;
    private State _p_state; // Previous state for rollback

    /// <summary>
    /// Highpass cutoff period. Removes cycles longer than this period (detrending).
    /// </summary>
    public int HpLength { get; }

    /// <summary>
    /// Super Smoother cutoff period. Removes cycles shorter than this period (noise removal).
    /// </summary>
    public int SsLength { get; }

    public bool IsNew => _isNew;
    public override bool IsHot => double.IsFinite(_state.Roof2); // Sufficiently warm when we have history

    public Roofing(int hpLength = 48, int ssLength = 10)
    {
        if (hpLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(hpLength), "HP length must be >= 1");
        }

        if (ssLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ssLength), "SS length must be >= 1");
        }

        HpLength = hpLength;
        SsLength = ssLength;
        Name = $"ROOFING({hpLength},{ssLength})";
        WarmupPeriod = hpLength; // HP stage dominates warmup

        // Precompute Highpass Butterworth coefficients
        double sqrt2Pi = Math.Sqrt(2.0) * Math.PI;
        double hpArg = sqrt2Pi / hpLength;
        double hpExpArg = Math.Exp(-hpArg);
        _hpC2 = 2.0 * hpExpArg * Math.Cos(hpArg);
        _hpC3 = -hpExpArg * hpExpArg;
        _hpC1 = (1.0 + _hpC2 - _hpC3) * 0.25;

        // Precompute Super Smoother (Lowpass) Butterworth coefficients
        double ssArg = sqrt2Pi / ssLength;
        double ssExpArg = Math.Exp(-ssArg);
        _ssC2 = 2.0 * ssExpArg * Math.Cos(ssArg);
        _ssC3 = -ssExpArg * ssExpArg;
        _ssC1 = 1.0 - _ssC2 - _ssC3;

        _state.LastValid = double.NaN;
    }

    public Roofing(ITValuePublisher source, int hpLength = 48, int ssLength = 10) : this(hpLength, ssLength)
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

        Batch(values, results, HpLength, SsLength);

        TSeries output = [];
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
        _isNew = isNew;
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
            val = double.IsFinite(_state.LastValid) ? _state.LastValid : 0.0;
        }
        else
        {
            _state.LastValid = val;
        }

        // Stage 1: Highpass Filter (removes trend)
        // hp = hpC1 * (val - 2*src1 + src2) + hpC2 * hp1 + hpC3 * hp2
        double hpInput = _hpC1 * (val - 2.0 * _state.Src1 + _state.Src2);
        double hp = Math.FusedMultiplyAdd(_hpC2, _state.Hp1, Math.FusedMultiplyAdd(_hpC3, _state.Hp2, hpInput));

        // Stage 2: Super Smoother (removes noise from HP output)
        // roof = ssC1 * hp + ssC2 * roof1 + ssC3 * roof2
        double roof = Math.FusedMultiplyAdd(_ssC1, hp, Math.FusedMultiplyAdd(_ssC2, _state.Roof1, _ssC3 * _state.Roof2));

        if (isNew)
        {
            _state.Src2 = _state.Src1;
            _state.Src1 = val;
            _state.Hp2 = _state.Hp1;
            _state.Hp1 = hp;
            _state.Roof2 = _state.Roof1;
            _state.Roof1 = roof;
        }

        Last = new TValue(input.Time, roof);
        PubEvent(Last, isNew);
        return Last;
    }

    public static TSeries Batch(TSeries source, int hpLength = 48, int ssLength = 10)
    {
        var indicator = new Roofing(hpLength, ssLength);
        return indicator.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int hpLength = 48, int ssLength = 10)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output spans must be of the same length.", nameof(output));
        }

        // Precompute coefficients
        double sqrt2Pi = Math.Sqrt(2.0) * Math.PI;

        double hpArg = sqrt2Pi / hpLength;
        double hpExpArg = Math.Exp(-hpArg);
        double hpC2 = 2.0 * hpExpArg * Math.Cos(hpArg);
        double hpC3 = -hpExpArg * hpExpArg;
        double hpC1 = (1.0 + hpC2 - hpC3) * 0.25;

        double ssArg = sqrt2Pi / ssLength;
        double ssExpArg = Math.Exp(-ssArg);
        double ssC2 = 2.0 * ssExpArg * Math.Cos(ssArg);
        double ssC3 = -ssExpArg * ssExpArg;
        double ssC1 = 1.0 - ssC2 - ssC3;

        // State variables
        double src1 = 0, src2 = 0;
        double hp1 = 0, hp2 = 0;
        double roof1 = 0, roof2 = 0;
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

            // Highpass
            double hpInput = hpC1 * (val - 2.0 * src1 + src2);
            double hp = Math.FusedMultiplyAdd(hpC2, hp1, Math.FusedMultiplyAdd(hpC3, hp2, hpInput));

            // Super Smoother
            double roof = Math.FusedMultiplyAdd(ssC1, hp, Math.FusedMultiplyAdd(ssC2, roof1, ssC3 * roof2));

            output[i] = roof;

            // Shift state
            src2 = src1;
            src1 = val;
            hp2 = hp1;
            hp1 = hp;
            roof2 = roof1;
            roof1 = roof;
        }
    }

    public override void Reset()
    {
        _state = default;
        _state.LastValid = double.NaN;
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

    public static (TSeries Results, Roofing Indicator) Calculate(TSeries source, int hpLength = 48, int ssLength = 10)
    {
        var indicator = new Roofing(hpLength, ssLength);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    /// <summary>
    /// Unsubscribes from the source publisher if one was provided during construction.
    /// </summary>
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
