using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// APZ: Adaptive Price Zone
/// </summary>
/// <remarks>
/// The Adaptive Price Zone (APZ) is a volatility-based technical indicator developed by
/// Lee Leibfarth. It uses a double-smoothed exponential moving average (EMA) with a
/// modified smoothing factor based on sqrt(period) to create adaptive bands around price.
///
/// Calculation:
/// smoothing_period = sqrt(period)
/// alpha = 2 / (smoothing_period + 1)
/// EMA1_price = alpha × price + (1 - alpha) × EMA1_price[1]
/// EMA2_price = alpha × EMA1_price + (1 - alpha) × EMA2_price[1]  (middle line)
/// EMA1_range = alpha × (high - low) + (1 - alpha) × EMA1_range[1]
/// EMA2_range = alpha × EMA1_range + (1 - alpha) × EMA2_range[1]  (adaptive range)
/// upper = middle + (multiplier × adaptive_range)
/// lower = middle - (multiplier × adaptive_range)
///
/// Key characteristics:
/// - Uses compound warmup compensation for nested EMAs: compensator = 1/(1-beta²)
/// - Faster response than standard EMAs due to sqrt(period) smoothing
/// - Bands adapt to volatility via the high-low range
/// - O(1) complexity per update
///
/// Sources:
/// Leibfarth, Lee (2006). "Trading With An Adaptive Price Zone," Technical Analysis of
/// Stocks &amp; Commodities, Volume 24:9.
/// </remarks>
[SkipLocalsInit]
public sealed class Apz : ITValuePublisher
{
    private readonly int _period;
    private readonly double _multiplier;
    private readonly double _alpha;
    private readonly double _beta;
    private readonly double _betaSquared;
    private readonly TBarPublishedHandler _barHandler;

    private const double ConvergenceThreshold = 1e-10;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Ema1Price,
        double Ema2Price,
        double Ema1Range,
        double Ema2Range,
        double E,              // Warmup decay factor
        double LastValidPrice,
        double LastValidHigh,
        double LastValidLow,
        bool IsHot
    )
    {
        public static State New() => new()
        {
            Ema1Price = 0,
            Ema2Price = 0,
            Ema1Range = 0,
            Ema2Range = 0,
            E = 1.0,
            LastValidPrice = double.NaN,
            LastValidHigh = double.NaN,
            LastValidLow = double.NaN,
            IsHot = false,
        };
    }

    private State _state;
    private State _p_state;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Number of periods before the indicator is considered "hot" (valid).
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Current middle band value (double-smoothed EMA of price).
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// Current upper band value.
    /// </summary>
    public TValue Upper { get; private set; }

    /// <summary>
    /// Current lower band value.
    /// </summary>
    public TValue Lower { get; private set; }

    /// <summary>
    /// True if the indicator has converged (warmup decay below threshold).
    /// </summary>
    public bool IsHot => _state.IsHot;

    /// <summary>
    /// Event triggered when a new TValue is available.
    /// </summary>
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates APZ with specified period and multiplier.
    /// </summary>
    /// <param name="period">Lookback period (sqrt applied internally for smoothing, must be > 0)</param>
    /// <param name="multiplier">Multiplier for band width (must be > 0, default: 2.0)</param>
    public Apz(int period, double multiplier = 2.0)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (multiplier <= 0)
            throw new ArgumentException("Multiplier must be greater than 0", nameof(multiplier));

        _period = period;
        _multiplier = multiplier;

        double smoothPeriod = Math.Sqrt(period);
        _alpha = 2.0 / (smoothPeriod + 1.0);
        _beta = 1.0 - _alpha;
        _betaSquared = _beta * _beta;

        Name = $"Apz({period},{multiplier:F2})";
        // Warmup is based on EMA convergence - use period as approximation
        WarmupPeriod = period;
        _state = State.New();
        _p_state = _state;
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates APZ with TBarSeries source.
    /// </summary>
    public Apz(TBarSeries source, int period, double multiplier = 2.0) : this(period, multiplier)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// Helper to invoke the Pub event.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true)
    {
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });
    }

    /// <summary>
    /// Gets valid input values, using last-value substitution for non-finite inputs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double price, double high, double low) GetValidValues(double price, double high, double low)
    {
        if (double.IsFinite(price))
            _state.LastValidPrice = price;
        else
            price = _state.LastValidPrice;

        if (double.IsFinite(high))
            _state.LastValidHigh = high;
        else
            high = _state.LastValidHigh;

        if (double.IsFinite(low))
            _state.LastValidLow = low;
        else
            low = _state.LastValidLow;

        return (price, high, low);
    }

    /// <summary>
    /// Core calculation with warmup compensation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double middle, double upper, double lower) Compute(double price, double range)
    {
        // Double-smoothed EMA for price
        _state.Ema1Price = Math.FusedMultiplyAdd(_state.Ema1Price, _beta, _alpha * price);
        _state.Ema2Price = Math.FusedMultiplyAdd(_state.Ema2Price, _beta, _alpha * _state.Ema1Price);

        // Double-smoothed EMA for range
        _state.Ema1Range = Math.FusedMultiplyAdd(_state.Ema1Range, _beta, _alpha * range);
        _state.Ema2Range = Math.FusedMultiplyAdd(_state.Ema2Range, _beta, _alpha * _state.Ema1Range);

        double middle = _state.Ema2Price;
        double adaptiveRange = _state.Ema2Range;

        // Apply compound warmup compensation
        if (!_state.IsHot)
        {
            _state.E *= _betaSquared;
            double compensator = 1.0 / (1.0 - _state.E);
            middle *= compensator;
            adaptiveRange *= compensator;

            if (_state.E <= ConvergenceThreshold)
                _state.IsHot = true;
        }

        double bandWidth = _multiplier * adaptiveRange;
        return (middle, middle + bandWidth, middle - bandWidth);
    }

    /// <summary>
    /// Updates the indicator with a TBar input.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
            _p_state = _state;
        else
            _state = _p_state;

        var (price, high, low) = GetValidValues(input.Close, input.High, input.Low);

        // Handle first value initialization
        if (double.IsNaN(_state.LastValidPrice))
        {
            Last = new TValue(input.Time, double.NaN);
            Upper = new TValue(input.Time, double.NaN);
            Lower = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        double range = high - low;
        if (range < 0) range = 0; // Safety check

        var (middle, upper, lower) = Compute(price, range);

        Last = new TValue(input.Time, middle);
        Upper = new TValue(input.Time, upper);
        Lower = new TValue(input.Time, lower);

        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates the indicator with a TBarSeries.
    /// </summary>
    public (TSeries Middle, TSeries Upper, TSeries Lower) Update(TBarSeries source)
    {
        if (source.Count == 0)
            return (new TSeries([], []), new TSeries([], []), new TSeries([], []));

        int len = source.Count;
        var tMiddle = new List<long>(len);
        var vMiddle = new List<double>(len);
        var tUpper = new List<long>(len);
        var vUpper = new List<double>(len);
        var tLower = new List<long>(len);
        var vLower = new List<double>(len);

        CollectionsMarshal.SetCount(tMiddle, len);
        CollectionsMarshal.SetCount(vMiddle, len);
        CollectionsMarshal.SetCount(tUpper, len);
        CollectionsMarshal.SetCount(vUpper, len);
        CollectionsMarshal.SetCount(tLower, len);
        CollectionsMarshal.SetCount(vLower, len);

        var tSpan = CollectionsMarshal.AsSpan(tMiddle);
        var vMiddleSpan = CollectionsMarshal.AsSpan(vMiddle);
        var vUpperSpan = CollectionsMarshal.AsSpan(vUpper);
        var vLowerSpan = CollectionsMarshal.AsSpan(vLower);

        // Use batch calculation
        Batch(source.High.Values, source.Low.Values, source.Close.Values, new BatchOutputs(vMiddleSpan, vUpperSpan, vLowerSpan), _period, _multiplier);

        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tUpper));
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tLower));

        // Prime the state for continued streaming
        Prime(source);

        return (new TSeries(tMiddle, vMiddle), new TSeries(tUpper, vUpper), new TSeries(tLower, vLower));
    }

    /// <summary>
    /// Initializes the indicator state using the provided TBarSeries history.
    /// </summary>
    public void Prime(TBarSeries source)
    {
        if (source.Count == 0) return;

        // Reset state
        _state = State.New();
        _p_state = _state;

        // Use all available data for priming to ensure proper convergence
        const int startIndex = 0;

        // Find first valid values in the data
        if (double.IsNaN(_state.LastValidPrice))
        {
            for (int i = startIndex; i < source.Count; i++)
            {
                var bar = source[i];
                if (double.IsFinite(bar.Close))
                {
                    _state.LastValidPrice = bar.Close;
                    _state.LastValidHigh = bar.High;
                    _state.LastValidLow = bar.Low;
                    break;
                }
            }
        }

        // Feed the warmup data
        for (int i = startIndex; i < source.Count; i++)
        {
            var bar = source[i];
            var (price, high, low) = GetValidValues(bar.Close, bar.High, bar.Low);

            if (double.IsFinite(price))
            {
                double range = Math.Max(0, high - low);
                var (middle, upper, lower) = Compute(price, range);
                Last = new TValue(bar.Time, middle);
                Upper = new TValue(bar.Time, upper);
                Lower = new TValue(bar.Time, lower);
            }
        }

        _p_state = _state;
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    public void Reset()
    {
        _state = State.New();
        _p_state = _state;
        Last = default;
        Upper = default;
        Lower = default;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Static Batch Methods
    /////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Output buffers for batch APZ calculation.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
#pragma warning disable S1104 // Fields should not have public accessibility
    public ref struct BatchOutputs
    {
        /// <summary>Output middle band (double-smoothed EMA of price)</summary>
        public Span<double> Middle;
        /// <summary>Output upper band</summary>
        public Span<double> Upper;
        /// <summary>Output lower band</summary>
        public Span<double> Lower;
#pragma warning restore S1104

        /// <summary>
        /// Creates a new BatchOutputs instance.
        /// </summary>
        public BatchOutputs(Span<double> middle, Span<double> upper, Span<double> lower)
        {
            Middle = middle;
            Upper = upper;
            Lower = lower;
        }
    }

    /// <summary>
    /// Internal state for scalar calculation.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private ref struct ScalarState
    {
        public double Ema1Price;
        public double Ema2Price;
        public double Ema1Range;
        public double Ema2Range;
        public double E;
        public double LastValidPrice;
        public double LastValidHigh;
        public double LastValidLow;
        public bool IsHot;
    }

    /// <summary>
    /// Calculates APZ for the entire TBarSeries using a new instance.
    /// </summary>
    public static (TSeries Middle, TSeries Upper, TSeries Lower) Batch(TBarSeries source, int period, double multiplier = 2.0)
    {
        var apz = new Apz(period, multiplier);
        return apz.Update(source);
    }

    /// <summary>
    /// Calculates APZ in-place using spans for maximum performance.
    /// Zero-allocation method.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        BatchOutputs outputs,
        int period,
        double multiplier = 2.0)
    {
        int len = close.Length;
        if (high.Length != len || low.Length != len)
            throw new ArgumentException("Input spans must have the same length", nameof(high));
        if (outputs.Middle.Length < len || outputs.Upper.Length < len || outputs.Lower.Length < len)
            throw new ArgumentException("Output buffers must be at least as long as input", nameof(outputs));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (multiplier <= 0)
            throw new ArgumentException("Multiplier must be greater than 0", nameof(multiplier));

        if (len == 0) return;

        CalculateScalarCore(high, low, close, outputs, period, multiplier);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        BatchOutputs outputs,
        int period,
        double multiplier)
    {
        int len = close.Length;

        double smoothPeriod = Math.Sqrt(period);
        double alpha = 2.0 / (smoothPeriod + 1.0);
        double beta = 1.0 - alpha;
        double betaSquared = beta * beta;

        Span<double> middle = outputs.Middle;
        Span<double> upper = outputs.Upper;
        Span<double> lower = outputs.Lower;

        var state = new ScalarState
        {
            Ema1Price = 0,
            Ema2Price = 0,
            Ema1Range = 0,
            Ema2Range = 0,
            E = 1.0,
            LastValidPrice = double.NaN,
            LastValidHigh = double.NaN,
            LastValidLow = double.NaN,
            IsHot = false,
        };

        // Seed first valid values
        SeedFirstValidValues(high, low, close, ref state);

        for (int i = 0; i < len; i++)
        {
            double price = close[i];
            double h = high[i];
            double l = low[i];

            // Get valid values
            if (double.IsFinite(price))
                state.LastValidPrice = price;
            else
                price = state.LastValidPrice;

            if (double.IsFinite(h))
                state.LastValidHigh = h;
            else
                h = state.LastValidHigh;

            if (double.IsFinite(l))
                state.LastValidLow = l;
            else
                l = state.LastValidLow;

            // Handle first valid value
            if (double.IsNaN(price))
            {
                middle[i] = double.NaN;
                upper[i] = double.NaN;
                lower[i] = double.NaN;
                continue;
            }

            double range = Math.Max(0, h - l);

            // Double-smoothed EMA for price
            state.Ema1Price = Math.FusedMultiplyAdd(state.Ema1Price, beta, alpha * price);
            state.Ema2Price = Math.FusedMultiplyAdd(state.Ema2Price, beta, alpha * state.Ema1Price);

            // Double-smoothed EMA for range
            state.Ema1Range = Math.FusedMultiplyAdd(state.Ema1Range, beta, alpha * range);
            state.Ema2Range = Math.FusedMultiplyAdd(state.Ema2Range, beta, alpha * state.Ema1Range);

            double mid = state.Ema2Price;
            double adaptiveRange = state.Ema2Range;

            // Apply compound warmup compensation
            if (!state.IsHot)
            {
                state.E *= betaSquared;
                double compensator = 1.0 / (1.0 - state.E);
                mid *= compensator;
                adaptiveRange *= compensator;

                if (state.E <= ConvergenceThreshold)
                    state.IsHot = true;
            }

            double bandWidth = multiplier * adaptiveRange;
            middle[i] = mid;
            upper[i] = mid + bandWidth;
            lower[i] = mid - bandWidth;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SeedFirstValidValues(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        ref ScalarState state)
    {
        int len = close.Length;
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(close[k]))
            {
                state.LastValidPrice = close[k];
                state.LastValidHigh = high[k];
                state.LastValidLow = low[k];
                break;
            }
        }
    }

    /// <summary>
    /// Runs a high-performance batch calculation and returns a "Hot" APZ instance.
    /// </summary>
    public static ((TSeries Middle, TSeries Upper, TSeries Lower) Results, Apz Indicator) Calculate(TBarSeries source, int period, double multiplier = 2.0)
    {
        var apz = new Apz(period, multiplier);
        var results = apz.Update(source);
        return (results, apz);
    }
}