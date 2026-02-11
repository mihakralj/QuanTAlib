using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Computes the Percentage Price Oscillator (PPO), which measures the percentage difference
/// between a fast and slow exponential moving average.
/// </summary>
/// <remarks>
/// PPO Formula:
/// <c>PPO = 100 × (FastEMA - SlowEMA) / SlowEMA</c>.
///
/// PPO is similar to MACD but normalized as a percentage, enabling comparison across
/// different price levels. Positive values indicate the fast EMA is above the slow EMA.
/// This implementation uses compensated EMAs for warmup accuracy and FMA for performance.
/// Non-finite inputs (NaN/±Inf) are sanitized by substituting the last finite value observed.
///
/// For the authoritative algorithm reference, full rationale, and behavioral contracts, see the
/// companion files in the same directory.
/// </remarks>
/// <seealso href="ppo.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Ppo : AbstractBase
{
    private const int DefaultFastPeriod = 12;
    private const int DefaultSlowPeriod = 26;
    private const int DefaultSignalPeriod = 9;

    private readonly Ema _fastEma;
    private readonly Ema _slowEma;
    private readonly Ema _signalEma;
    private record struct State(double LastValid);
    private State _state, _p_state;

    private ITValuePublisher? _source;
    private bool _disposed;

    /// <summary>
    /// Gets the most recent signal line value (EMA of PPO line).
    /// </summary>
    public TValue Signal { get; private set; }

    /// <summary>
    /// Gets the most recent histogram value (PPO - Signal).
    /// </summary>
    public TValue Histogram { get; private set; }

    /// <summary>
    /// True when both fast and slow EMAs have warmed up.
    /// </summary>
    public override bool IsHot => _fastEma.IsHot && _slowEma.IsHot;

    /// <summary>
    /// Initializes a new PPO indicator.
    /// </summary>
    /// <param name="fastPeriod">Fast EMA period (must be >= 1)</param>
    /// <param name="slowPeriod">Slow EMA period (must be >= 1 and > fastPeriod)</param>
    /// <param name="signalPeriod">Signal line EMA period (must be >= 1)</param>
    public Ppo(int fastPeriod = DefaultFastPeriod, int slowPeriod = DefaultSlowPeriod, int signalPeriod = DefaultSignalPeriod)
    {
        if (fastPeriod < 1)
        {
            throw new ArgumentException("Fast period must be >= 1", nameof(fastPeriod));
        }

        if (slowPeriod < 1)
        {
            throw new ArgumentException("Slow period must be >= 1", nameof(slowPeriod));
        }

        if (signalPeriod < 1)
        {
            throw new ArgumentException("Signal period must be >= 1", nameof(signalPeriod));
        }

        if (fastPeriod >= slowPeriod)
        {
            throw new ArgumentException("Fast period must be less than slow period", nameof(fastPeriod));
        }

        _fastEma = new Ema(fastPeriod);
        _slowEma = new Ema(slowPeriod);
        _signalEma = new Ema(signalPeriod);

        Name = $"Ppo({fastPeriod},{slowPeriod},{signalPeriod})";
        WarmupPeriod = slowPeriod + signalPeriod;
    }

    /// <summary>
    /// Initializes a new PPO indicator with source for event-based chaining.
    /// </summary>
    public Ppo(ITValuePublisher source, int fastPeriod = DefaultFastPeriod, int slowPeriod = DefaultSlowPeriod, int signalPeriod = DefaultSignalPeriod)
        : this(fastPeriod, slowPeriod, signalPeriod)
    {
        _source = source;
        _source.Pub += HandleUpdate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdate(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

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

        double value = double.IsFinite(input.Value) ? input.Value : _state.LastValid;
        _state = new State(value);

        var safeInput = new TValue(input.Time, value);

        var fast = _fastEma.Update(safeInput, isNew);
        var slow = _slowEma.Update(safeInput, isNew);

        // PPO = 100 * (FastEMA - SlowEMA) / SlowEMA
        double ppoValue = slow.Value != 0.0
            ? 100.0 * (fast.Value - slow.Value) / slow.Value
            : 0.0;

        var ppoTValue = new TValue(input.Time, ppoValue);
        var signal = _signalEma.Update(ppoTValue, isNew);

        double histValue = ppoValue - signal.Value;

        Last = ppoTValue;
        Signal = signal;
        Histogram = new TValue(input.Time, histValue);

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

        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(new DateTime(source.Times[i], DateTimeKind.Utc), source.Values[i]), true);
            tSpan[i] = source.Times[i];
            vSpan[i] = Last.Value;
        }

        _p_state = _state;

        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        TimeSpan interval = step ?? TimeSpan.FromSeconds(1);
        DateTime time = DateTime.UtcNow - (interval * source.Length);

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(time, source[i]), true);
            time += interval;
        }
    }

    public static TSeries Batch(TSeries source, int fastPeriod = DefaultFastPeriod, int slowPeriod = DefaultSlowPeriod, int signalPeriod = DefaultSignalPeriod)
    {
        var indicator = new Ppo(fastPeriod, slowPeriod, signalPeriod);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates PPO line over a span of values.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> destination, int fastPeriod = DefaultFastPeriod, int slowPeriod = DefaultSlowPeriod)
    {
        if (source.Length != destination.Length)
        {
            throw new ArgumentException("Source and destination must be same length", nameof(destination));
        }

        if (fastPeriod < 1)
        {
            throw new ArgumentException("Fast period must be >= 1", nameof(fastPeriod));
        }

        if (slowPeriod < 1)
        {
            throw new ArgumentException("Slow period must be >= 1", nameof(slowPeriod));
        }

        if (fastPeriod >= slowPeriod)
        {
            throw new ArgumentException("Fast period must be less than slow period", nameof(fastPeriod));
        }

        int len = source.Length;
        double[] fastBuffer = ArrayPool<double>.Shared.Rent(len);
        double[] slowBuffer = ArrayPool<double>.Shared.Rent(len);

        try
        {
            Span<double> fastSpan = fastBuffer.AsSpan(0, len);
            Span<double> slowSpan = slowBuffer.AsSpan(0, len);

            Ema.Batch(source, fastSpan, fastPeriod);
            Ema.Batch(source, slowSpan, slowPeriod);

            for (int i = 0; i < len; i++)
            {
                destination[i] = slowSpan[i] != 0.0
                    ? 100.0 * (fastSpan[i] - slowSpan[i]) / slowSpan[i]
                    : 0.0;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(fastBuffer);
            ArrayPool<double>.Shared.Return(slowBuffer);
        }
    }

    public static (TSeries Results, Ppo Indicator) Calculate(TSeries source, int fastPeriod = DefaultFastPeriod, int slowPeriod = DefaultSlowPeriod, int signalPeriod = DefaultSignalPeriod)
    {
        var indicator = new Ppo(fastPeriod, slowPeriod, signalPeriod);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _fastEma.Reset();
        _slowEma.Reset();
        _signalEma.Reset();
        _state = default;
        _p_state = default;
        Last = default;
        Signal = default;
        Histogram = default;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (_source != null)
                {
                    _source.Pub -= HandleUpdate;
                    _source = null;
                }
                _fastEma.Dispose();
                _slowEma.Dispose();
                _signalEma.Dispose();
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
