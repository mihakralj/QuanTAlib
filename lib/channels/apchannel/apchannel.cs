using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// APCHANNEL: Adaptive Price Channel
/// An adaptive channel that uses exponential moving averages of highs and lows
/// with a configurable smoothing factor (alpha).
/// </summary>
/// <remarks>
/// The APCHANNEL creates dynamic support and resistance levels by applying
/// exponential smoothing to price highs and lows. The alpha parameter controls
/// the sensitivity: higher alpha (closer to 1) makes the channel more responsive,
/// while lower alpha creates smoother, slower-moving bands.
/// 
/// Key characteristics:
/// - Exponential weighting for recent price action
/// - Adaptive to volatility through alpha parameter
/// - Zero-allocation O(1) updates via FMA optimization
/// - Provides dynamic support/resistance zones
/// </remarks>
[SkipLocalsInit]
public sealed class Apchannel : AbstractBase
{
    private readonly double _alpha;
    private readonly double _decay;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double HighEma,
        double LowEma,
        double LastValidHigh,
        double LastValidLow,
        int Count
    );

    private State _state;
    private State _p_state;

    /// <summary>
    /// True if the indicator has enough data to produce valid results.
    /// </summary>
    public override bool IsHot => _state.Count >= WarmupPeriod;

    /// <summary>
    /// Gets the current value of the upper band (exponential moving average of highs).
    /// </summary>
    public double UpperBand => _state.HighEma;

    /// <summary>
    /// Gets the current value of the lower band (exponential moving average of lows).
    /// </summary>
    public double LowerBand => _state.LowEma;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Apchannel(double alpha = 0.2)
    {
        if (alpha <= 0 || alpha > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(alpha),
                "Alpha must be greater than 0 and less than or equal to 1.");
        }

        _alpha = alpha;
        _decay = 1.0 - alpha;
        WarmupPeriod = (int)Math.Ceiling(3.0 / alpha); // ~95% convergence
        Name = $"Apchannel({alpha:F2})";
        Init();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Apchannel(TBarSeries source, double alpha = 0.2) : this(alpha)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Init()
    {
        _state = new State(0, 0, 0, 0, 0);
        _p_state = _state;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? source, in TBarEventArgs args) =>
        _ = Add(args.Value, args.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ManageState(bool isNew)
    {
        if (isNew)
        {
            _p_state = _state;
            _state = _state with { Count = _state.Count + 1 };
        }
        else
        {
            _state = _p_state;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double UpdateCore(double high, double low, long time, bool isNew)
    {
        ManageState(isNew);

        double validHigh = double.IsFinite(high) ? high : _state.LastValidHigh;
        double validLow = double.IsFinite(low) ? low : _state.LastValidLow;

        double highEma, lowEma;

        if (_state.Count == 1)
        {
            highEma = validHigh;
            lowEma = validLow;
        }
        else
        {
            highEma = Math.FusedMultiplyAdd(_decay, _state.HighEma, _alpha * validHigh);
            lowEma = Math.FusedMultiplyAdd(_decay, _state.LowEma, _alpha * validLow);
        }

        _state = _state with
        {
            HighEma = highEma,
            LowEma = lowEma,
            LastValidHigh = validHigh,
            LastValidLow = validLow,
        };

        double mid = (highEma + lowEma) * 0.5;
        Last = new TValue(time, mid);
        PubEvent(Last, isNew);
        return mid;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        Init();
        Last = new TValue(0, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Add(TBar bar, bool isNew = true) => Update(bar, isNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        UpdateCore(bar.High, bar.Low, bar.Time, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);

        Reset();

        for (int i = 0; i < len; i++)
        {
            var val = Update(source[i], isNew: true);
            t.Add(val.Time);
            v.Add(val.Value);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        UpdateCore(input.Value, input.Value, input.Time, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);

        Reset();

        for (int i = 0; i < len; i++)
        {
            var val = Update(source[i], isNew: true);
            t.Add(val.Time);
            v.Add(val.Value);
        }

        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        Init();
        if (source.Length == 0)
            return;

        long time = DateTime.UtcNow.Ticks;
        long dt = step?.Ticks ?? TimeSpan.TicksPerMinute;

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(time, source[i]), isNew: true);
            time += dt;
        }
    }

    /// <summary>
    /// Calculates the Adaptive Price Channel for the entire series and returns both
    /// the result series and a primed indicator instance for continued streaming.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (TBarSeries Results, Apchannel Indicator) Calculate(
        TBarSeries source, double alpha = 0.2)
    {
        var indicator = new Apchannel(alpha);
        var results = new TBarSeries();

        foreach (var bar in source)
        {
            _ = indicator.Add(bar);
            results.Add(bar.Time, indicator.UpperBand, indicator.UpperBand,
                       indicator.LowerBand, indicator.LowerBand, 0);
        }

        return (results, indicator);
    }

    /// <summary>
    /// Calculates the Adaptive Price Channel using span-based batch processing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(
        ReadOnlySpan<double> sourceHigh,
        ReadOnlySpan<double> sourceLow,
        Span<double> upperBand,
        Span<double> lowerBand,
        double alpha = 0.2)
    {
        int length = sourceHigh.Length;

        if (sourceLow.Length != length)
            throw new ArgumentException("Source arrays must have the same length.", nameof(sourceLow));
        if (upperBand.Length != length)
            throw new ArgumentException("Upper band array must match source length.", nameof(upperBand));
        if (lowerBand.Length != length)
        {
            throw new ArgumentException("Lower band array must match source length.", nameof(lowerBand));
        }
        if (alpha <= 0 || alpha > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(alpha),
                "Alpha must be greater than 0 and less than or equal to 1.");
        }

        if (length == 0)
            return;

        double decay = 1.0 - alpha;

        CalculateScalar(sourceHigh, sourceLow, upperBand, lowerBand, alpha, decay);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalar(
        ReadOnlySpan<double> sourceHigh,
        ReadOnlySpan<double> sourceLow,
        Span<double> upperBand,
        Span<double> lowerBand,
        double alpha,
        double decay)
    {
        int length = sourceHigh.Length;

        // Handle NaN tracking
        double lastValidHigh = sourceHigh[0];
        double lastValidLow = sourceLow[0];

        // Initialize first values
        double highEma = double.IsFinite(sourceHigh[0]) ? sourceHigh[0] : 0;
        double lowEma = double.IsFinite(sourceLow[0]) ? sourceLow[0] : 0;

        upperBand[0] = highEma;
        lowerBand[0] = lowEma;

        if (double.IsFinite(sourceHigh[0])) lastValidHigh = sourceHigh[0];
        if (double.IsFinite(sourceLow[0])) lastValidLow = sourceLow[0];

        for (int i = 1; i < length; i++)
        {
            double high = sourceHigh[i];
            double low = sourceLow[i];

            // Handle NaN/Infinity
            if (!double.IsFinite(high)) high = lastValidHigh;
            if (!double.IsFinite(low)) low = lastValidLow;

            // Use FMA for optimal performance and precision
            highEma = Math.FusedMultiplyAdd(decay, highEma, alpha * high);
            lowEma = Math.FusedMultiplyAdd(decay, lowEma, alpha * low);

            upperBand[i] = highEma;
            lowerBand[i] = lowEma;

            lastValidHigh = high;
            lastValidLow = low;
        }
    }
}
