// TSI: True Strength Index by William Blau
// Momentum oscillator measuring overbought/oversold conditions.
// Uses double-smoothed EMA of price momentum vs absolute momentum.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TSI: True Strength Index
/// </summary>
/// <remarks>
/// Momentum oscillator that uses double-smoothed exponential moving averages
/// of price momentum to reduce noise and identify trend strength.
/// Ranges from -100 to +100, with higher values indicating bullish momentum.
///
/// Calculation:
/// <code>
/// Momentum = Price - Price[1]
/// TSI = 100 × EMA(EMA(Momentum, longPeriod), shortPeriod) / EMA(EMA(|Momentum|, longPeriod), shortPeriod)
/// Signal = EMA(TSI, signalPeriod)
/// </code>
///
/// Key characteristics:
/// - Double smoothing reduces noise and false signals
/// - Bounded oscillator: -100 to +100
/// - Signal line crossovers generate trade signals
/// - Zero line crossovers indicate trend changes
/// </remarks>
/// <seealso href="Tsi.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Tsi : AbstractBase
{
    private const int DefaultLongPeriod = 25;
    private const int DefaultShortPeriod = 13;
    private const int DefaultSignalPeriod = 13;

    /// <summary>
    /// Gets the long period for first EMA smoothing.
    /// </summary>
    public int LongPeriod { get; }

    /// <summary>
    /// Gets the short period for second EMA smoothing.
    /// </summary>
    public int ShortPeriod { get; }

    /// <summary>
    /// Gets the signal line period.
    /// </summary>
    public int SignalPeriod { get; }
    private readonly TValuePublishedHandler _handler;

    // Four EMAs for double smoothing
    private readonly Ema _emaMomLong;      // First smoothing of momentum
    private readonly Ema _emaMomShort;     // Second smoothing of momentum
    private readonly Ema _emaAbsMomLong;   // First smoothing of |momentum|
    private readonly Ema _emaAbsMomShort;  // Second smoothing of |momentum|
    private readonly Ema _emaSignal;       // Signal line EMA

    private double _prevValue;
    private double _p_prevValue;
    private double _lastSignal;
    private double _p_lastSignal;

    /// <summary>
    /// Gets the signal line value.
    /// </summary>
    public double Signal => _lastSignal;

    public override bool IsHot => _emaMomShort.IsHot && _emaAbsMomShort.IsHot && _emaSignal.IsHot;

    /// <summary>
    /// Initializes a new instance of the TSI indicator.
    /// </summary>
    /// <param name="longPeriod">The long period for first EMA smoothing (default: 25).</param>
    /// <param name="shortPeriod">The short period for second EMA smoothing (default: 13).</param>
    /// <param name="signalPeriod">The period for signal line EMA (default: 13).</param>
    /// <exception cref="ArgumentException">Thrown when any period is less than 1.</exception>
    public Tsi(int longPeriod = DefaultLongPeriod, int shortPeriod = DefaultShortPeriod, int signalPeriod = DefaultSignalPeriod)
    {
        if (longPeriod < 1)
        {
            throw new ArgumentException("Long period must be at least 1", nameof(longPeriod));
        }
        if (shortPeriod < 1)
        {
            throw new ArgumentException("Short period must be at least 1", nameof(shortPeriod));
        }
        if (signalPeriod < 1)
        {
            throw new ArgumentException("Signal period must be at least 1", nameof(signalPeriod));
        }

        LongPeriod = longPeriod;
        ShortPeriod = shortPeriod;
        SignalPeriod = signalPeriod;
        _handler = Handle;

        // Initialize EMAs - use period directly for warmup
        _emaMomLong = new Ema(longPeriod);
        _emaMomShort = new Ema(shortPeriod);
        _emaAbsMomLong = new Ema(longPeriod);
        _emaAbsMomShort = new Ema(shortPeriod);
        _emaSignal = new Ema(signalPeriod);

        _prevValue = double.NaN;
        _p_prevValue = double.NaN;
        _lastSignal = 0;
        _p_lastSignal = 0;

        Name = $"Tsi({longPeriod},{shortPeriod},{signalPeriod})";
        WarmupPeriod = longPeriod + shortPeriod + signalPeriod;
    }

    public Tsi(ITValuePublisher source, int longPeriod = DefaultLongPeriod, int shortPeriod = DefaultShortPeriod, int signalPeriod = DefaultSignalPeriod)
        : this(longPeriod, shortPeriod, signalPeriod)
    {
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_prevValue = _prevValue;
            _p_lastSignal = _lastSignal;
        }
        else
        {
            _prevValue = _p_prevValue;
            _lastSignal = _p_lastSignal;
        }

        double val = input.Value;
        double mom = 0;
        double absMom = 0;

        if (!double.IsNaN(_prevValue))
        {
            mom = val - _prevValue;
            absMom = Math.Abs(mom);
        }

        if (isNew)
        {
            _prevValue = val;
        }

        // Double smooth the momentum: EMA(EMA(mom, longPeriod), shortPeriod)
        double smoothedMomLong = _emaMomLong.Update(new TValue(input.Time, mom), isNew).Value;
        double doubleSmoothedMom = _emaMomShort.Update(new TValue(input.Time, smoothedMomLong), isNew).Value;

        // Double smooth the absolute momentum: EMA(EMA(|mom|, longPeriod), shortPeriod)
        double smoothedAbsMomLong = _emaAbsMomLong.Update(new TValue(input.Time, absMom), isNew).Value;
        double doubleSmoothedAbsMom = _emaAbsMomShort.Update(new TValue(input.Time, smoothedAbsMomLong), isNew).Value;

        // Calculate TSI: 100 × doubleSmoothedMom / doubleSmoothedAbsMom
        double tsi;
        const double epsilon = 1e-10;
        if (Math.Abs(doubleSmoothedAbsMom) < epsilon)
        {
            tsi = 0;  // Avoid division by zero
        }
        else
        {
            tsi = 100.0 * doubleSmoothedMom / doubleSmoothedAbsMom;
        }

        // Calculate signal line: EMA(TSI, signalPeriod)
        _lastSignal = _emaSignal.Update(new TValue(input.Time, tsi), isNew).Value;

        Last = new TValue(input.Time, tsi);
        PubEvent(Last, isNew);
        return Last;
    }

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

        // Batch calculate
        Calculate(source.Values, vSpan, LongPeriod, ShortPeriod);
        source.Times.CopyTo(tSpan);

        // Restore state for streaming by replaying
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]));
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    public static TSeries Batch(TSeries source, int longPeriod = DefaultLongPeriod, int shortPeriod = DefaultShortPeriod, int signalPeriod = DefaultSignalPeriod)
    {
        var tsi = new Tsi(longPeriod, shortPeriod, signalPeriod);
        return tsi.Update(source);
    }

    /// <summary>
    /// Batch calculates TSI values (without signal line).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int longPeriod = DefaultLongPeriod, int shortPeriod = DefaultShortPeriod)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (longPeriod < 1)
        {
            throw new ArgumentException("Long period must be at least 1", nameof(longPeriod));
        }
        if (shortPeriod < 1)
        {
            throw new ArgumentException("Short period must be at least 1", nameof(shortPeriod));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // Calculate momentum: source[i] - source[i-1]
        double[] mom = System.Buffers.ArrayPool<double>.Shared.Rent(len);
        double[] absMom = System.Buffers.ArrayPool<double>.Shared.Rent(len);
        double[] smoothedMom = System.Buffers.ArrayPool<double>.Shared.Rent(len);
        double[] smoothedAbsMom = System.Buffers.ArrayPool<double>.Shared.Rent(len);

        Span<double> momSpan = mom.AsSpan(0, len);
        Span<double> absMomSpan = absMom.AsSpan(0, len);
        Span<double> smoothedMomSpan = smoothedMom.AsSpan(0, len);
        Span<double> smoothedAbsMomSpan = smoothedAbsMom.AsSpan(0, len);

        momSpan[0] = 0;
        absMomSpan[0] = 0;
        for (int i = 1; i < len; i++)
        {
            momSpan[i] = source[i] - source[i - 1];
            absMomSpan[i] = Math.Abs(momSpan[i]);
        }

        // Double smooth momentum: EMA(EMA(mom, longPeriod), shortPeriod)
        Ema.Batch(momSpan, smoothedMomSpan, longPeriod);
        Ema.Batch(smoothedMomSpan, smoothedMomSpan, shortPeriod);  // In-place

        // Double smooth absolute momentum: EMA(EMA(|mom|, longPeriod), shortPeriod)
        Ema.Batch(absMomSpan, smoothedAbsMomSpan, longPeriod);
        Ema.Batch(smoothedAbsMomSpan, smoothedAbsMomSpan, shortPeriod);  // In-place

        // Calculate TSI: 100 × smoothedMom / smoothedAbsMom
        const double epsilon = 1e-10;
        for (int i = 0; i < len; i++)
        {
            if (Math.Abs(smoothedAbsMomSpan[i]) < epsilon)
            {
                output[i] = 0;
            }
            else
            {
                output[i] = 100.0 * smoothedMomSpan[i] / smoothedAbsMomSpan[i];
            }
        }

        System.Buffers.ArrayPool<double>.Shared.Return(mom);
        System.Buffers.ArrayPool<double>.Shared.Return(absMom);
        System.Buffers.ArrayPool<double>.Shared.Return(smoothedMom);
        System.Buffers.ArrayPool<double>.Shared.Return(smoothedAbsMom);
    }

    public override void Reset()
    {
        _emaMomLong.Reset();
        _emaMomShort.Reset();
        _emaAbsMomLong.Reset();
        _emaAbsMomShort.Reset();
        _emaSignal.Reset();
        _prevValue = double.NaN;
        _p_prevValue = double.NaN;
        _lastSignal = 0;
        _p_lastSignal = 0;
        Last = default;
    }
}
