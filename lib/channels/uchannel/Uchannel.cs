using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// UCHANNEL: Ehlers Ultimate Channel
/// A volatility channel using the Ehlers Ultrasmooth Filter (USF) for both
/// centerline smoothing and True Range smoothing (STR). The channel width is
/// determined by the smoothed True Range multiplied by a factor.
/// </summary>
/// <remarks>
/// The Ultimate Channel provides smooth, low-lag channel boundaries by applying
/// the USF to both the price centerline and the True Range. This creates channels
/// that adapt to volatility while maintaining minimal lag.
///
/// Key characteristics:
/// - Middle band: USF of close prices
/// - Band width: Smoothed True Range (USF of TR) × multiplier
/// - Both smoothers use the same 2-pole IIR Ultrasmooth Filter
///
/// Sources:
///     John F. Ehlers - "Ultimate Channel" (2024)
///
/// Formula:
///     TR = max(high, prev_close) - min(low, prev_close)
///     STR = USF(TR, strPeriod)
///     Middle = USF(close, centerPeriod)
///     Upper = Middle + (multiplier × STR)
///     Lower = Middle - (multiplier × STR)
///
/// USF coefficients (same as Ehlers Ultrasmooth Filter):
///     arg = sqrt(2) × π / period
///     c2 = 2 × exp(-arg) × cos(arg)
///     c3 = -exp(-arg)²
///     c1 = (1 + c2 - c3) / 4
///
/// USF formula:
///     usf = (1 - c1) × val + (2×c1 - c2) × val₋₁ - (c1 + c3) × val₋₂ + c2 × usf₋₁ + c3 × usf₋₂
/// </remarks>
[SkipLocalsInit]
public sealed class Uchannel : AbstractBase
{
    private readonly double _multiplier;
    private const int DefaultStrPeriod = 20;
    private const int DefaultCenterPeriod = 20;
    private const double DefaultMultiplier = 1.0;
    private const double MinMultiplier = 0.001;
    private const int MinPeriod = 1;

    // USF coefficients for STR
    private readonly double _c1_str, _c2_str, _c3_str;

    // USF coefficients for centerline
    private readonly double _c1_cen, _c2_cen, _c3_cen;

    // State for streaming with bar correction
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PrevClose,
        double UsStr1, double UsStr2,
        double Str1, double Str2,
        double UsCen1, double UsCen2,
        double Cen1, double Cen2,
        double LastValidClose, double LastValidHigh, double LastValidLow,
        int BarCount,
        bool IsInitialized);

    private State _state;
    private State _p_state;
    private int _index;

    public override bool IsHot => _index >= WarmupPeriod;

    /// <summary>Gets the upper band value.</summary>
    public TValue Upper { get; private set; }

    /// <summary>Gets the middle band (USF smoothed centerline) value.</summary>
    public TValue Middle { get; private set; }

    /// <summary>Gets the lower band value.</summary>
    public TValue Lower { get; private set; }

    /// <summary>Gets the current Smoothed True Range value.</summary>
    public TValue STR { get; private set; }

    /// <summary>Gets the channel width (Upper - Lower).</summary>
    public TValue Width => new(Upper.Time, Upper.Value - Lower.Value);

    /// <summary>
    /// Initializes a new instance of Uchannel with specified parameters.
    /// </summary>
    /// <param name="strPeriod">Period for smoothing True Range. Must be >= 1.</param>
    /// <param name="centerPeriod">Period for smoothing centerline. Must be >= 1.</param>
    /// <param name="multiplier">Band multiplier for STR. Must be > 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when strPeriod &lt; 1, centerPeriod &lt; 1, or multiplier &lt;= 0.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Uchannel(int strPeriod = DefaultStrPeriod, int centerPeriod = DefaultCenterPeriod, double multiplier = DefaultMultiplier)
    {
        if (strPeriod < MinPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(strPeriod),
                $"STR period must be at least {MinPeriod}.");
        }
        if (centerPeriod < MinPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(centerPeriod),
                $"Center period must be at least {MinPeriod}.");
        }
        if (multiplier < MinMultiplier)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier),
                $"Multiplier must be at least {MinMultiplier}.");
        }

        _multiplier = multiplier;

        WarmupPeriod = Math.Max(strPeriod, centerPeriod);
        Name = $"Uchannel({strPeriod},{centerPeriod},{multiplier:F1})";

        // Compute USF coefficients for STR
        double arg_str = Math.Sqrt(2) * Math.PI / strPeriod;
        double exp_str = Math.Exp(-arg_str);
        _c2_str = 2 * exp_str * Math.Cos(arg_str);
        _c3_str = -exp_str * exp_str;
        _c1_str = (1 + _c2_str - _c3_str) / 4.0;

        // Compute USF coefficients for centerline
        double arg_cen = Math.Sqrt(2) * Math.PI / centerPeriod;
        double exp_cen = Math.Exp(-arg_cen);
        _c2_cen = 2 * exp_cen * Math.Cos(arg_cen);
        _c3_cen = -exp_cen * exp_cen;
        _c1_cen = (1 + _c2_cen - _c3_cen) / 4.0;

        Init();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Init()
    {
        _index = 0;
        _state = new State(
            PrevClose: double.NaN,
            UsStr1: 0, UsStr2: 0,
            Str1: 0, Str2: 0,
            UsCen1: 0, UsCen2: 0,
            Cen1: 0, Cen2: 0,
            LastValidClose: 0, LastValidHigh: 0, LastValidLow: 0,
            BarCount: 0,
            IsInitialized: false);
        _p_state = _state;
        Upper = new TValue(DateTime.UtcNow, 0);
        Middle = new TValue(DateTime.UtcNow, 0);
        Lower = new TValue(DateTime.UtcNow, 0);
        STR = new TValue(DateTime.UtcNow, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double GetFiniteValue(double value, double fallback) =>
        double.IsFinite(value) ? value : fallback;

    /// <summary>
    /// Processes a single bar and updates the indicator.
    /// </summary>
    /// <param name="bar">The input bar containing OHLC data.</param>
    /// <param name="isNew">True if this is a new bar, false for correction.</param>
    /// <returns>The middle band value as TValue.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        return Update(bar.Time, bar.High, bar.Low, bar.Close, isNew);
    }

    /// <summary>
    /// Processes OHLC values and updates the indicator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(long time, double high, double low, double close, bool isNew = true)
    {
        // State management for bar correction
        if (isNew)
        {
            _p_state = _state;
            _index++;
        }
        else
        {
            _state = _p_state;
        }

        // Handle non-finite values
        double validClose = GetFiniteValue(close, _state.LastValidClose);
        double validHigh = GetFiniteValue(high, _state.LastValidHigh);
        double validLow = GetFiniteValue(low, _state.LastValidLow);

        // Compute True Range
        double prevClose = double.IsNaN(_state.PrevClose) ? validClose : _state.PrevClose;
        double trueHigh = Math.Max(validHigh, prevClose);
        double trueLow = Math.Min(validLow, prevClose);
        double tr = trueHigh - trueLow;

        // USF for STR
        double str_s0 = tr;
        double str_s1 = _state.BarCount >= 1 ? _state.Str1 : str_s0;
        double str_s2 = _state.BarCount >= 2 ? _state.Str2 : str_s1;
        double usStr1 = _state.UsStr1;
        double usStr2 = _state.UsStr2;

        double strValue;
        if (_state.BarCount < 2)
        {
            strValue = str_s0;
        }
        else
        {
            // USF: (1-c1)*s0 + (2*c1-c2)*s1 - (c1+c3)*s2 + c2*usf1 + c3*usf2
            strValue = Math.FusedMultiplyAdd(1 - _c1_str, str_s0,
                Math.FusedMultiplyAdd(2 * _c1_str - _c2_str, str_s1,
                Math.FusedMultiplyAdd(-(_c1_str + _c3_str), str_s2,
                Math.FusedMultiplyAdd(_c2_str, usStr1, _c3_str * usStr2))));
        }

        // USF for centerline
        double cen_s0 = validClose;
        double cen_s1 = _state.BarCount >= 1 ? _state.Cen1 : cen_s0;
        double cen_s2 = _state.BarCount >= 2 ? _state.Cen2 : cen_s1;
        double usCen1 = _state.UsCen1;
        double usCen2 = _state.UsCen2;

        double cenValue;
        if (_state.BarCount < 2)
        {
            cenValue = cen_s0;
        }
        else
        {
            cenValue = Math.FusedMultiplyAdd(1 - _c1_cen, cen_s0,
                Math.FusedMultiplyAdd(2 * _c1_cen - _c2_cen, cen_s1,
                Math.FusedMultiplyAdd(-(_c1_cen + _c3_cen), cen_s2,
                Math.FusedMultiplyAdd(_c2_cen, usCen1, _c3_cen * usCen2))));
        }

        // Compute bands
        double bandwidth = _multiplier * strValue;
        double upper = cenValue + bandwidth;
        double lower = cenValue - bandwidth;

        // Update state
        _state = new State(
            PrevClose: validClose,
            UsStr1: strValue, UsStr2: usStr1,
            Str1: str_s0, Str2: str_s1,
            UsCen1: cenValue, UsCen2: usCen1,
            Cen1: cen_s0, Cen2: cen_s1,
            LastValidClose: validClose, LastValidHigh: validHigh, LastValidLow: validLow,
            BarCount: _state.BarCount + (isNew ? 1 : 0),
            IsInitialized: true);

        Upper = new TValue(time, upper);
        Middle = new TValue(time, cenValue);
        Lower = new TValue(time, lower);
        STR = new TValue(time, strValue);

        Last = Middle;
        return Last;
    }

    /// <summary>
    /// Updates with TValue - requires High, Low, Close data so this uses the value as Close
    /// with High = Low = Close (not recommended, use TBar overload instead)
    /// </summary>
    public override TValue Update(TValue input, bool isNew = true)
    {
        // Convert to TBar with O=H=L=C=value, V=0
        TBar bar = new(input.Time, input.Value, input.Value, input.Value, input.Value, 0);
        return Update(bar, isNew);
    }

    /// <summary>
    /// Processes a series of bars.
    /// </summary>
    public TSeries Update(TBarSeries series)
    {
        if (series == null)
        {
            throw new ArgumentNullException(nameof(series));
        }

        int len = series.Count;
        TSeries result = new(capacity: len);

        for (int i = 0; i < len; i++)
        {
            var bar = series[i];
            Update(bar, isNew: true);
            result.Add(Last.Time, Last.Value, isNew: true);
        }

        return result;
    }

    /// <summary>
    /// Updates the indicator with a new time series and returns the result series.
    /// </summary>
    public override TSeries Update(TSeries source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        int len = source.Count;
        TSeries result = new(capacity: len);

        for (int i = 0; i < len; i++)
        {
            var item = source[i];
            Update(item, isNew: true);
            result.Add(Last.Time, Last.Value, isNew: true);
        }

        return result;
    }

    public override void Reset()
    {
        Init();
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        step ??= TimeSpan.FromSeconds(1);
        DateTime startTime = DateTime.UtcNow;

        for (int i = 0; i < source.Length; i++)
        {
            // Treat as close price only
            Update(new TValue(startTime + i * step.Value, source[i]), isNew: true);
        }
    }

    /// <summary>
    /// Static method to calculate Ultimate Channel for a bar series.
    /// </summary>
    public static (TSeries Upper, TSeries Middle, TSeries Lower, TSeries STR) Calculate(
        TBarSeries source, int strPeriod = DefaultStrPeriod, int centerPeriod = DefaultCenterPeriod, double multiplier = DefaultMultiplier)
    {
        var indicator = new Uchannel(strPeriod, centerPeriod, multiplier);
        var upper = new TSeries(source.Count);
        var middle = new TSeries(source.Count);
        var lower = new TSeries(source.Count);
        var str = new TSeries(source.Count);

        foreach (var bar in source)
        {
            indicator.Update(bar);
            upper.Add(indicator.Upper);
            middle.Add(indicator.Middle);
            lower.Add(indicator.Lower);
            str.Add(indicator.STR);
        }

        return (upper, middle, lower, str);
    }

    /// <summary>
    /// Static span-based calculation for maximum performance.
    /// </summary>
    /// <param name="high">Source high prices.</param>
    /// <param name="low">Source low prices.</param>
    /// <param name="close">Source close prices.</param>
    /// <param name="upper">Output upper band.</param>
    /// <param name="middle">Output middle band.</param>
    /// <param name="lower">Output lower band.</param>
    /// <param name="strPeriod">Period for STR smoothing.</param>
    /// <param name="centerPeriod">Period for centerline smoothing.</param>
    /// <param name="multiplier">Band multiplier.</param>
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> upper,
        Span<double> middle,
        Span<double> lower,
        int strPeriod = DefaultStrPeriod,
        int centerPeriod = DefaultCenterPeriod,
        double multiplier = DefaultMultiplier)
    {
        if (strPeriod < MinPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(strPeriod),
                $"STR period must be at least {MinPeriod}.");
        }
        if (centerPeriod < MinPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(centerPeriod),
                $"Center period must be at least {MinPeriod}.");
        }
        if (multiplier < MinMultiplier)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier),
                $"Multiplier must be at least {MinMultiplier}.");
        }

        int length = close.Length;
        if (high.Length != length || low.Length != length)
        {
            throw new ArgumentException("All input arrays must have the same length", nameof(high));
        }

        if (upper.Length != length || middle.Length != length || lower.Length != length)
        {
            throw new ArgumentException("Output arrays must match input length", nameof(upper));
        }

        if (length == 0)
        {
            return;
        }

        // Compute USF coefficients
        double arg_str = Math.Sqrt(2) * Math.PI / strPeriod;
        double exp_str = Math.Exp(-arg_str);
        double c2_str = 2 * exp_str * Math.Cos(arg_str);
        double c3_str = -exp_str * exp_str;
        double c1_str = (1 + c2_str - c3_str) / 4.0;

        double arg_cen = Math.Sqrt(2) * Math.PI / centerPeriod;
        double exp_cen = Math.Exp(-arg_cen);
        double c2_cen = 2 * exp_cen * Math.Cos(arg_cen);
        double c3_cen = -exp_cen * exp_cen;
        double c1_cen = (1 + c2_cen - c3_cen) / 4.0;

        double prevClose = close[0];
        double lastValidClose = close[0];
        double lastValidHigh = high[0];
        double lastValidLow = low[0];

        double usStr1 = 0, usStr2 = 0;
        double str1 = 0, str2 = 0;
        double usCen1 = 0, usCen2 = 0;
        double cen1 = 0, cen2 = 0;

        for (int i = 0; i < length; i++)
        {
            double h = double.IsFinite(high[i]) ? high[i] : lastValidHigh;
            double l = double.IsFinite(low[i]) ? low[i] : lastValidLow;
            double c = double.IsFinite(close[i]) ? close[i] : lastValidClose;

            lastValidHigh = h;
            lastValidLow = l;
            lastValidClose = c;

            // True Range
            double trueHigh = Math.Max(h, prevClose);
            double trueLow = Math.Min(l, prevClose);
            double tr = trueHigh - trueLow;

            // USF for STR
            double str_s0 = tr;
            double str_s1 = i >= 1 ? str1 : str_s0;
            double str_s2 = i >= 2 ? str2 : str_s1;

            double strValue;
            if (i < 2)
            {
                strValue = str_s0;
            }
            else
            {
                strValue = Math.FusedMultiplyAdd(1 - c1_str, str_s0,
                    Math.FusedMultiplyAdd(2 * c1_str - c2_str, str_s1,
                    Math.FusedMultiplyAdd(-(c1_str + c3_str), str_s2,
                    Math.FusedMultiplyAdd(c2_str, usStr1, c3_str * usStr2))));
            }

            // USF for centerline
            double cen_s0 = c;
            double cen_s1 = i >= 1 ? cen1 : cen_s0;
            double cen_s2 = i >= 2 ? cen2 : cen_s1;

            double cenValue;
            if (i < 2)
            {
                cenValue = cen_s0;
            }
            else
            {
                cenValue = Math.FusedMultiplyAdd(1 - c1_cen, cen_s0,
                    Math.FusedMultiplyAdd(2 * c1_cen - c2_cen, cen_s1,
                    Math.FusedMultiplyAdd(-(c1_cen + c3_cen), cen_s2,
                    Math.FusedMultiplyAdd(c2_cen, usCen1, c3_cen * usCen2))));
            }

            // Bands
            double bandwidth = multiplier * strValue;
            upper[i] = cenValue + bandwidth;
            middle[i] = cenValue;
            lower[i] = cenValue - bandwidth;

            // Update state
            str2 = str1;
            str1 = str_s0;
            usStr2 = usStr1;
            usStr1 = strValue;

            cen2 = cen1;
            cen1 = cen_s0;
            usCen2 = usCen1;
            usCen1 = cenValue;

            prevClose = c;
        }
    }
}
