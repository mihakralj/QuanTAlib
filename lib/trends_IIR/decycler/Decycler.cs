using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// DECYCLER: Ehlers Decycler
/// </summary>
/// <remarks>
/// Removes cyclic components from price by subtracting a 2-pole Butterworth
/// high-pass filter, leaving only the trend component.
/// Algorithm based on: https://github.com/mihakralj/pinescript/blob/main/trends_IIR/decycler/decycler.pine
/// Complexity: O(1)
/// </remarks>
[SkipLocalsInit]
public sealed class Decycler : AbstractBase
{
    private readonly double _a1, _b1, _c1;
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;
    private State _state;
    private State _p_state;
    private double _lastValidValue;
    private double _p_lastValidValue;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double Hp;
        public double Hp1;
        public double Src1;
        public double Src2;
        public bool IsInitialized;
    }

    /// <summary>
    /// Cutoff period for the high-pass filter.
    /// </summary>
    public int Period { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Decycler"/> class.
    /// </summary>
    /// <param name="period">Cutoff period for the high-pass filter. Default is 60.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    public Decycler(int period = 60)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 2);
        Period = period;

        // Butterworth 2-pole HP coefficient: alpha = (cos(x) + sin(x) - 1) / cos(x)
        // where x = 0.707 * 2pi / period
        double arg = 0.707 * 2.0 * Math.PI / period;
        double cosArg = Math.Cos(arg);
        double alpha = (cosArg + Math.Sin(arg) - 1.0) / cosArg;
        double halfAlpha = 1.0 - alpha * 0.5;
        _a1 = halfAlpha * halfAlpha;
        double oneMinusAlpha = 1.0 - alpha;
        _b1 = 2.0 * oneMinusAlpha;
        _c1 = -(oneMinusAlpha * oneMinusAlpha);

        Name = $"Decycler({period})";
        WarmupPeriod = period;
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Decycler"/> class with a publisher source.
    /// </summary>
    /// <param name="source">The source publisher.</param>
    /// <param name="period">Cutoff period for the high-pass filter.</param>
    public Decycler(ITValuePublisher source, int period = 60) : this(period)
    {
        _publisher = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Init()
    {
        _state = new State();
        _p_state = _state;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? source, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        Init();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.MinValue, value), isNew: true);
        }
    }

    public override bool IsHot => _state.IsInitialized;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _state = _p_state;
            _lastValidValue = _p_lastValidValue;
        }

        double src = input.Value;
        if (!double.IsFinite(src))
        {
            src = _lastValidValue;
        }
        else
        {
            _lastValidValue = src;
        }

        if (!_state.IsInitialized)
        {
            // First bar — no HP history yet, output = source
            _state.Hp = 0;
            _state.Hp1 = 0;
            _state.Src1 = src;
            _state.Src2 = src;
            _state.IsInitialized = true;

            Last = new TValue(input.Time, src);
            PubEvent(Last, isNew);
            return Last;
        }

        // HP recurrence: hp = a1*(src - 2*src1 + src2) + b1*hp + c1*hp1
        double hp = Math.FusedMultiplyAdd(_a1, src - 2.0 * _state.Src1 + _state.Src2,
                     Math.FusedMultiplyAdd(_b1, _state.Hp, _c1 * _state.Hp1));

        // Decycler = source - high-pass
        double result = src - hp;

        // Update state (same logic for isNew and correction — state was already
        // snapshotted/restored at method entry, so unconditional write is correct)
        _state.Hp1 = _state.Hp;
        _state.Hp = hp;
        _state.Src2 = _state.Src1;
        _state.Src1 = src;

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var resultValues = new double[source.Count];
        Batch(source.Values, resultValues, Period);

        var result = new TSeries();
        var times = source.Times;
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(new TValue(times[i], resultValues[i]));
        }

        // Sync internal state from batch results
        int len = source.Count;
        if (len >= 2)
        {
            // Replay from scratch to get exact HP state
            var replay = new Decycler(Period);
            for (int i = 0; i < len; i++)
            {
                replay.Update(new TValue(times[i], source.Values[i]));
            }
            _state = replay._state;
            _lastValidValue = replay._lastValidValue;
        }
        else
        {
            _state.Hp = 0;
            _state.Hp1 = 0;
            _state.Src1 = source.Values[^1];
            _state.Src2 = source.Values[^1];
            _state.IsInitialized = true;
            _lastValidValue = source.Values[^1];
        }

        _p_state = _state;
        _p_lastValidValue = _lastValidValue;

        return result;
    }

    public static TSeries Batch(TSeries source, int period = 60)
    {
        var indicator = new Decycler(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Static calculation of Decycler on a span.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output spans must be of equal length.", nameof(output));
        }

        if (source.Length == 0)
        {
            return;
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(period, 2, nameof(period));

        // Precompute coefficients
        double arg = 0.707 * 2.0 * Math.PI / period;
        double cosArg = Math.Cos(arg);
        double alpha = (cosArg + Math.Sin(arg) - 1.0) / cosArg;
        double halfAlpha = 1.0 - alpha * 0.5;
        double a1 = halfAlpha * halfAlpha;
        double oneMinusAlpha = 1.0 - alpha;
        double b1 = 2.0 * oneMinusAlpha;
        double c1 = -(oneMinusAlpha * oneMinusAlpha);

        // First bar: output = source (no HP yet)
        output[0] = source[0];
        if (source.Length < 2)
        {
            return;
        }
        output[1] = source[1];

        // Main loop from bar 2 onward
        double hp = 0;
        double hp1 = 0;

        for (int i = 2; i < source.Length; i++)
        {
            double newHp = Math.FusedMultiplyAdd(a1, source[i] - 2.0 * source[i - 1] + source[i - 2],
                            Math.FusedMultiplyAdd(b1, hp, c1 * hp1));
            output[i] = source[i] - newHp;
            hp1 = hp;
            hp = newHp;
        }
    }

    public static (TSeries Results, Decycler Indicator) Calculate(TSeries source, int period = 60)
    {
        var indicator = new Decycler(period);
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
        }
        base.Dispose(disposing);
    }
}
