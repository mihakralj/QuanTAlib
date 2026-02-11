using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// AMAT: Archer Moving Averages Trends
/// </summary>
/// <remarks>
/// Trend system requiring fast/slow EMA alignment in same direction for signals.
/// Returns +1 (bullish), -1 (bearish), or 0 (neutral) with strength percentage.
///
/// Signal: <c>+1</c> when FastEMA > SlowEMA and both rising; <c>-1</c> when FastEMA &lt; SlowEMA and both falling.
/// </remarks>
/// <seealso href="Amat.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Amat : ITValuePublisher, IDisposable
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double FastEma,
        double SlowEma,
        double FastE,
        double SlowE,
        double PrevFastEma,
        double PrevSlowEma,
        bool FastIsHot,
        bool SlowIsHot,
        bool FastIsCompensated,
        bool SlowIsCompensated,
        int TickCount)
    {
        public static State New() => new()
        {
            FastEma = 0,
            SlowEma = 0,
            FastE = 1.0,
            SlowE = 1.0,
            PrevFastEma = 0,
            PrevSlowEma = 0,
            FastIsHot = false,
            SlowIsHot = false,
            FastIsCompensated = false,
            SlowIsCompensated = false,
            TickCount = 0,
        };
    }

    private readonly double _fastAlpha;
    private readonly double _slowAlpha;
    private readonly double _fastDecay;
    private readonly double _slowDecay;

    private State _state = State.New();
    private State _p_state = State.New();
    private double _lastValidValue;
    private double _p_lastValidValue;
    private ITValuePublisher? _source;
    private bool _disposed;

    private const double COVERAGE_THRESHOLD = 0.05;
    private const double COMPENSATOR_THRESHOLD = 1e-10;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Event triggered when a new TValue is available.
    /// </summary>
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current trend direction: +1 (bullish), -1 (bearish), 0 (neutral).
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// Current trend strength as percentage: |Fast - Slow| / Slow * 100.
    /// </summary>
    public TValue Strength { get; private set; }

    /// <summary>
    /// Current Fast EMA value.
    /// </summary>
    public TValue FastEma { get; private set; }

    /// <summary>
    /// Current Slow EMA value.
    /// </summary>
    public TValue SlowEma { get; private set; }

    /// <summary>
    /// True if both EMAs have warmed up and are providing valid results.
    /// </summary>
    public bool IsHot => _state.FastIsHot && _state.SlowIsHot;

    /// <summary>
    /// The number of bars required for the indicator to warm up.
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Creates AMAT with specified fast and slow periods.
    /// </summary>
    /// <param name="fastPeriod">Fast EMA period (must be > 0)</param>
    /// <param name="slowPeriod">Slow EMA period (must be > fast period)</param>
    public Amat(int fastPeriod = 10, int slowPeriod = 50)
    {
        if (fastPeriod <= 0)
        {
            throw new ArgumentException("Fast period must be greater than 0", nameof(fastPeriod));
        }

        if (slowPeriod <= 0)
        {
            throw new ArgumentException("Slow period must be greater than 0", nameof(slowPeriod));
        }

        if (fastPeriod >= slowPeriod)
        {
            throw new ArgumentException("Fast period must be less than slow period", nameof(fastPeriod));
        }

        _fastAlpha = 2.0 / (fastPeriod + 1);
        _slowAlpha = 2.0 / (slowPeriod + 1);
        _fastDecay = 1.0 - _fastAlpha;
        _slowDecay = 1.0 - _slowAlpha;

        Name = $"Amat({fastPeriod},{slowPeriod})";
        WarmupPeriod = slowPeriod;
    }

    /// <summary>
    /// Creates AMAT with specified source and periods.
    /// Subscribes to source.Pub event.
    /// </summary>
    /// <param name="source">Source to subscribe to</param>
    /// <param name="fastPeriod">Fast EMA period</param>
    /// <param name="slowPeriod">Slow EMA period</param>
    public Amat(ITValuePublisher source, int fastPeriod = 10, int slowPeriod = 50)
        : this(fastPeriod, slowPeriod)
    {
        _source = source;
        source.Pub += Handle;
    }

    /// <summary>
    /// Releases resources and unsubscribes from the source publisher.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            if (_source != null)
            {
                _source.Pub -= Handle;
                _source = null;
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Resets the AMAT state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _state = State.New();
        _p_state = State.New();
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Last = default;
        Strength = default;
        FastEma = default;
        SlowEma = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _lastValidValue = input;
            return input;
        }
        return _lastValidValue;
    }

    /// <summary>
    /// Updates the indicator with a single value.
    /// </summary>
    /// <param name="input">Input value</param>
    /// <param name="isNew">True if this is a new bar, False if it's an update to the last bar</param>
    /// <returns>Updated trend value (+1, -1, or 0)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
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

        double val = GetValidValue(input.Value);

        // Store previous EMA values before update
        double prevFast = _state.FastEma;
        double prevSlow = _state.SlowEma;

        // Extract state fields to local variables (record struct properties cannot be passed by ref)
        double fastEmaState = _state.FastEma;
        double fastE = _state.FastE;
        bool fastIsHot = _state.FastIsHot;
        bool fastIsCompensated = _state.FastIsCompensated;

        double slowEmaState = _state.SlowEma;
        double slowE = _state.SlowE;
        bool slowIsHot = _state.SlowIsHot;
        bool slowIsCompensated = _state.SlowIsCompensated;

        int tickCount = _state.TickCount;

        // Compute Fast EMA with compensation
        double fastEma = ComputeEma(val, _fastAlpha, _fastDecay,
            ref fastEmaState, ref fastE, ref fastIsHot, ref fastIsCompensated);

        // Compute Slow EMA with compensation
        double slowEma = ComputeEma(val, _slowAlpha, _slowDecay,
            ref slowEmaState, ref slowE, ref slowIsHot, ref slowIsCompensated);

        // Update state with new values
        _state = new State(
            FastEma: fastEmaState,
            SlowEma: slowEmaState,
            FastE: fastE,
            SlowE: slowE,
            PrevFastEma: tickCount > 0 ? prevFast : 0,
            PrevSlowEma: tickCount > 0 ? prevSlow : 0,
            FastIsHot: fastIsHot,
            SlowIsHot: slowIsHot,
            FastIsCompensated: fastIsCompensated,
            SlowIsCompensated: slowIsCompensated,
            TickCount: tickCount + 1
        );

        // Determine trend direction
        double trend = 0;
        double strength = 0;

        if (_state.TickCount >= 2) // Need at least 2 ticks to compare previous values
        {
            double prevFastCompensated = GetCompensatedValue(_state.PrevFastEma, _state.FastE * (1.0 / _fastDecay), _state.FastIsCompensated);
            double prevSlowCompensated = GetCompensatedValue(_state.PrevSlowEma, _state.SlowE * (1.0 / _slowDecay), _state.SlowIsCompensated);

            bool fastAboveSlow = fastEma > slowEma;
            bool fastBelowSlow = fastEma < slowEma;
            bool fastRising = fastEma > prevFastCompensated;
            bool slowRising = slowEma > prevSlowCompensated;
            bool fastFalling = fastEma < prevFastCompensated;
            bool slowFalling = slowEma < prevSlowCompensated;

            // Bullish: Fast > Slow AND both rising
            if (fastAboveSlow && fastRising && slowRising)
            {
                trend = 1.0;
            }
            // Bearish: Fast < Slow AND both falling
            else if (fastBelowSlow && fastFalling && slowFalling)
            {
                trend = -1.0;
            }
            // Neutral: mixed conditions
            else
            {
                trend = 0;
            }

            // Calculate strength
            if (slowEma > 0)
            {
                strength = Math.Abs(fastEma - slowEma) / slowEma * 100.0;
            }
        }

        Last = new TValue(input.Time, trend);
        Strength = new TValue(input.Time, strength);
        FastEma = new TValue(input.Time, fastEma);
        SlowEma = new TValue(input.Time, slowEma);

        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates the indicator with a series of values.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <returns>Series of trend values</returns>
    public TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);

        // Pre-size lists to avoid reallocations
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
            tSpan[i] = source[i].Time;
            vSpan[i] = Last.Value;
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetCompensatedValue(double ema, double e, bool isCompensated)
    {
        if (isCompensated || e <= COMPENSATOR_THRESHOLD)
        {
            return ema;
        }

        return ema / (1.0 - e);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeEma(double input, double alpha, double decay,
        ref double ema, ref double e, ref bool isHot, ref bool isCompensated)
    {
        ema = Math.FusedMultiplyAdd(ema, decay, alpha * input);

        double result;
        if (!isCompensated)
        {
            e *= decay;

            if (!isHot && e <= COVERAGE_THRESHOLD)
            {
                isHot = true;
            }

            if (e <= COMPENSATOR_THRESHOLD)
            {
                isCompensated = true;
                result = ema;
            }
            else
            {
                result = ema / (1.0 - e);
            }
        }
        else
        {
            result = ema;
        }

        return result;
    }

    /// <summary>
    /// Calculates AMAT trend values for a span of input values.
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="trend">Output trend values (+1, -1, 0)</param>
    /// <param name="strength">Output strength values (percentage)</param>
    /// <param name="fastPeriod">Fast EMA period</param>
    /// <param name="slowPeriod">Slow EMA period</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> trend, Span<double> strength,
        int fastPeriod = 10, int slowPeriod = 50)
    {
        if (source.Length != trend.Length)
        {
            throw new ArgumentException("Source and trend must have the same length", nameof(trend));
        }

        if (source.Length != strength.Length)
        {
            throw new ArgumentException("Source and strength must have the same length", nameof(strength));
        }

        if (fastPeriod <= 0)
        {
            throw new ArgumentException("Fast period must be greater than 0", nameof(fastPeriod));
        }

        if (slowPeriod <= 0)
        {
            throw new ArgumentException("Slow period must be greater than 0", nameof(slowPeriod));
        }

        if (fastPeriod >= slowPeriod)
        {
            throw new ArgumentException("Fast period must be less than slow period", nameof(fastPeriod));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double fastAlpha = 2.0 / (fastPeriod + 1);
        double slowAlpha = 2.0 / (slowPeriod + 1);

        // Use ArrayPool for EMA buffers
        double[] fastBuffer = ArrayPool<double>.Shared.Rent(len);
        double[] slowBuffer = ArrayPool<double>.Shared.Rent(len);

        try
        {
            Span<double> fastSpan = fastBuffer.AsSpan(0, len);
            Span<double> slowSpan = slowBuffer.AsSpan(0, len);

            // Calculate Fast and Slow EMAs
            Ema.Batch(source, fastSpan, fastAlpha);
            Ema.Batch(source, slowSpan, slowAlpha);

            // Calculate trend and strength
            trend[0] = 0;
            strength[0] = 0;

            for (int i = 1; i < len; i++)
            {
                double fastEma = fastSpan[i];
                double slowEma = slowSpan[i];
                double prevFastEma = fastSpan[i - 1];
                double prevSlowEma = slowSpan[i - 1];

                bool fastAboveSlow = fastEma > slowEma;
                bool fastBelowSlow = fastEma < slowEma;
                bool fastRising = fastEma > prevFastEma;
                bool slowRising = slowEma > prevSlowEma;
                bool fastFalling = fastEma < prevFastEma;
                bool slowFalling = slowEma < prevSlowEma;

                // Bullish: Fast > Slow AND both rising
                if (fastAboveSlow && fastRising && slowRising)
                {
                    trend[i] = 1.0;
                }
                // Bearish: Fast < Slow AND both falling
                else if (fastBelowSlow && fastFalling && slowFalling)
                {
                    trend[i] = -1.0;
                }
                // Neutral
                else
                {
                    trend[i] = 0;
                }

                // Strength
                if (slowEma > 0)
                {
                    strength[i] = Math.Abs(fastEma - slowEma) / slowEma * 100.0;
                }
                else
                {
                    strength[i] = 0;
                }
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(fastBuffer);
            ArrayPool<double>.Shared.Return(slowBuffer);
        }
    }

    /// <summary>
    /// Calculates AMAT trend values for a span (trend only, no strength).
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="trend">Output trend values (+1, -1, 0)</param>
    /// <param name="fastPeriod">Fast EMA period</param>
    /// <param name="slowPeriod">Slow EMA period</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> trend,
        int fastPeriod = 10, int slowPeriod = 50)
    {
        if (source.Length != trend.Length)
        {
            throw new ArgumentException("Source and trend must have the same length", nameof(trend));
        }

        if (fastPeriod <= 0)
        {
            throw new ArgumentException("Fast period must be greater than 0", nameof(fastPeriod));
        }

        if (slowPeriod <= 0)
        {
            throw new ArgumentException("Slow period must be greater than 0", nameof(slowPeriod));
        }

        if (fastPeriod >= slowPeriod)
        {
            throw new ArgumentException("Fast period must be less than slow period", nameof(fastPeriod));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double fastAlpha = 2.0 / (fastPeriod + 1);
        double slowAlpha = 2.0 / (slowPeriod + 1);

        // Use single ArrayPool rent with slicing for both EMA buffers
        double[]? rented = ArrayPool<double>.Shared.Rent(len * 2);
        try
        {
            Span<double> buffer = rented.AsSpan(0, len * 2);
            Span<double> fastSpan = buffer.Slice(0, len);
            Span<double> slowSpan = buffer.Slice(len, len);

            // Calculate Fast and Slow EMAs
            Ema.Batch(source, fastSpan, fastAlpha);
            Ema.Batch(source, slowSpan, slowAlpha);

            // Calculate trend only (no strength computation needed)
            trend[0] = 0;

            for (int i = 1; i < len; i++)
            {
                double fastEma = fastSpan[i];
                double slowEma = slowSpan[i];
                double prevFastEma = fastSpan[i - 1];
                double prevSlowEma = slowSpan[i - 1];

                bool fastAboveSlow = fastEma > slowEma;
                bool fastBelowSlow = fastEma < slowEma;
                bool fastRising = fastEma > prevFastEma;
                bool slowRising = slowEma > prevSlowEma;
                bool fastFalling = fastEma < prevFastEma;
                bool slowFalling = slowEma < prevSlowEma;

                // Bullish: Fast > Slow AND both rising
                if (fastAboveSlow && fastRising && slowRising)
                {
                    trend[i] = 1.0;
                }
                // Bearish: Fast < Slow AND both falling
                else if (fastBelowSlow && fastFalling && slowFalling)
                {
                    trend[i] = -1.0;
                }
                // Neutral
                else
                {
                    trend[i] = 0;
                }
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Calculates AMAT for the entire series using a new instance.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="fastPeriod">Fast EMA period</param>
    /// <param name="slowPeriod">Slow EMA period</param>
    /// <returns>AMAT trend series</returns>
    public static TSeries Batch(TSeries source, int fastPeriod = 10, int slowPeriod = 50)
    {
        var amat = new Amat(fastPeriod, slowPeriod);
        return amat.Update(source);
    }

    /// <summary>
    /// Runs a high-performance batch calculation on history and returns
    /// a "Hot" Amat instance ready to process the next tick immediately.
    /// </summary>
    /// <param name="source">Historical time series</param>
    /// <param name="fastPeriod">Fast EMA period</param>
    /// <param name="slowPeriod">Slow EMA period</param>
    /// <returns>A tuple containing the full calculation results and the hot indicator instance</returns>
    public static (TSeries Results, Amat Indicator) Calculate(TSeries source, int fastPeriod = 10, int slowPeriod = 50)
    {
        var amat = new Amat(fastPeriod, slowPeriod);
        TSeries results = amat.Update(source);
        return (results, amat);
    }

}
