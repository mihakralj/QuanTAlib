using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// UBANDS: Ehlers Ultimate Bands
/// A volatility channel indicator using the Ehlers Ultrasmooth Filter (USF) as the middle band
/// with bands defined by the RMS (Root Mean Square) of residuals from the smooth.
/// </summary>
/// <remarks>
/// The UBANDS calculation process:
/// 1. Calculate the Ehlers Ultrasmooth Filter (USF) of the source
/// 2. Calculate residuals: source - USF
/// 3. Calculate RMS of residuals over the lookback period
/// 4. Upper band = USF + (multiplier × RMS)
/// 5. Lower band = USF - (multiplier × RMS)
///
/// Key characteristics:
/// - USF provides zero-lag smoothing for the center line
/// - RMS-based bands adapt to actual deviation from the smooth
/// - Multiplier controls band width sensitivity
///
/// Sources:
///     John F. Ehlers - Ultimate Bands (2024)
///     https://www.mesasoftware.com/
/// </remarks>
[SkipLocalsInit]
public sealed class Ubands : AbstractBase
{
    private readonly double _multiplier;
    private readonly double _c2, _c3;
    private readonly double _k0, _k1, _k2;
    private readonly RingBuffer _residualBuffer;
    private const int DefaultPeriod = 20;
    private const double DefaultMultiplier = 1.0;
    private const double MinMultiplier = 0.001;
    private const int MinPeriod = 1;

    // State for streaming with bar correction
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Usf1,
        double Usf2,
        double PrevInput1,
        double PrevInput2,
        double LastPrice,
        int Bars);

    private State _state;
    private State _p_state;

    public override bool IsHot => _state.Bars >= WarmupPeriod;

    /// <summary>
    /// Upper band (middle + mult × RMS)
    /// </summary>
    public TValue Upper { get; private set; }

    /// <summary>
    /// Middle band (Ehlers Ultrasmooth Filter)
    /// </summary>
    public TValue Middle { get; private set; }

    /// <summary>
    /// Lower band (middle - mult × RMS)
    /// </summary>
    public TValue Lower { get; private set; }

    /// <summary>
    /// Band width (Upper - Lower = 2 × mult × RMS)
    /// </summary>
    public TValue Width { get; private set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ubands(int period = DefaultPeriod, double multiplier = DefaultMultiplier)
    {
        if (period < MinPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                $"Period must be at least {MinPeriod}.");
        }
        if (multiplier < MinMultiplier)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier),
                $"Multiplier must be at least {MinMultiplier}.");
        }

        _multiplier = multiplier;
        _residualBuffer = new RingBuffer(period);

        // Calculate USF coefficients (same as Usf.cs)
        double sqrt2_pi = Math.Sqrt(2) * Math.PI;
        double arg = sqrt2_pi / period;
        double exp_arg = Math.Exp(-arg);

        _c2 = 2.0 * exp_arg * Math.Cos(arg);
        _c3 = -exp_arg * exp_arg;
        double c1 = (1.0 + _c2 - _c3) / 4.0;

        // Precompute coefficients for FMA optimization
        _k0 = 1.0 - c1;           // coefficient for val
        _k1 = 2.0 * c1 - _c2;     // coefficient for PrevInput1
        _k2 = -(c1 + _c3);        // coefficient for PrevInput2

        WarmupPeriod = period;
        Name = $"Ubands({period},{multiplier:F1})";
        Init();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Init()
    {
        _state = default;
        _p_state = default;
        _residualBuffer.Clear();
        Upper = default;
        Middle = default;
        Lower = default;
        Width = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleStateSnapshot(bool isNew)
    {
        if (isNew)
        {
            _p_state = _state;
            _residualBuffer.Snapshot();
        }
        else
        {
            _state = _p_state;
            _residualBuffer.Restore();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double usf, double upper, double lower) Step(double value, bool isNew)
    {
        HandleStateSnapshot(isNew);

        // Handle NaN/Infinity input
        if (!double.IsFinite(value))
        {
            if (_state.Bars == 0)
            {
                return (double.NaN, double.NaN, double.NaN);
            }
            value = _state.LastPrice;
        }
        else
        {
            _state.LastPrice = value;
        }

        _state.Bars++;

        // Initialize on first bar
        if (_state.Bars == 1)
        {
            _state.Usf1 = value;
            _state.Usf2 = value;
            _state.PrevInput1 = value;
            _state.PrevInput2 = value;
            return (value, value, value);
        }

        // Calculate USF (Ehlers Ultrasmooth Filter)
        double usf;
        if (_state.Bars < 4)
        {
            usf = value;
        }
        else
        {
            usf = Math.FusedMultiplyAdd(_c3, _state.Usf2,
                Math.FusedMultiplyAdd(_c2, _state.Usf1,
                    Math.FusedMultiplyAdd(_k2, _state.PrevInput2,
                        Math.FusedMultiplyAdd(_k1, _state.PrevInput1, _k0 * value))));
            // Guard against NaN propagation from state
            if (!double.IsFinite(usf))
            {
                usf = value;
            }
        }

        // Update USF state
        _state.Usf2 = _state.Usf1;
        _state.Usf1 = usf;
        _state.PrevInput2 = _state.PrevInput1;
        _state.PrevInput1 = value;

        // Calculate residual and add to buffer
        double residual = value - usf;
        _residualBuffer.Add(residual * residual); // Store squared residual

        // Calculate RMS from squared residuals
        // Use Max(0, Sum) to protect against floating-point drift making Sum slightly negative
        double sumSq = _residualBuffer.Sum;
        double rms = (_residualBuffer.Count > 0 && sumSq > 0)
            ? Math.Sqrt(sumSq / _residualBuffer.Count)
            : 0;

        // Calculate bands
        double bandOffset = _multiplier * rms;
        double upper = usf + bandOffset;
        double lower = usf - bandOffset;

        return (usf, upper, lower);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        var (usf, upper, lower) = Step(input.Value, isNew);

        // Update output values
        Upper = new TValue(input.Time, upper);
        Middle = new TValue(input.Time, usf);
        Lower = new TValue(input.Time, lower);
        Width = new TValue(input.Time, upper - lower);

        Last = Middle;
        return Last;
    }

    /// <summary>
    /// Updates the indicator with a time series and returns the middle band series.
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
        _residualBuffer.Clear();
        Init();
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        step ??= TimeSpan.FromSeconds(1);
        DateTime startTime = DateTime.UtcNow;

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(startTime + i * step.Value, source[i]), isNew: true);
        }
    }

    /// <summary>
    /// Calculates Ultimate Bands for the entire series.
    /// </summary>
    public static TSeries Calculate(TSeries source, int period = DefaultPeriod, double multiplier = DefaultMultiplier)
    {
        Ubands ubands = new(period, multiplier);
        return ubands.Update(source);
    }

    /// <summary>
    /// Calculates Ultimate Bands across data using spans.
    /// </summary>
    public static void Calculate(
        ReadOnlySpan<double> source,
        Span<double> upper,
        Span<double> middle,
        Span<double> lower,
        int period = DefaultPeriod,
        double multiplier = DefaultMultiplier)
    {
        int len = source.Length;
        if (len != upper.Length || len != middle.Length || len != lower.Length)
        {
            throw new ArgumentException("All spans must have the same length.", nameof(source));
        }
        if (period < MinPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                $"Period must be at least {MinPeriod}.");
        }
        if (multiplier < MinMultiplier)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier),
                $"Multiplier must be at least {MinMultiplier}.");
        }

        if (len == 0)
        {
            return;
        }

        // Calculate USF coefficients
        double sqrt2_pi = Math.Sqrt(2) * Math.PI;
        double arg = sqrt2_pi / period;
        double exp_arg = Math.Exp(-arg);

        double c2 = 2.0 * exp_arg * Math.Cos(arg);
        double c3 = -exp_arg * exp_arg;
        double c1 = (1.0 + c2 - c3) / 4.0;

        double k0 = 1.0 - c1;
        double k1 = 2.0 * c1 - c2;
        double k2 = -(c1 + c3);

        // Use stackalloc for residual buffer if small enough
        Span<double> residualSqBuffer = period <= 256 ? stackalloc double[period] : new double[period];
        int head = 0;
        int count = 0;
        double sumSq = 0;

        double usf1 = 0, usf2 = 0;
        double prevInput1 = 0, prevInput2 = 0;
        double lastValidValue = double.NaN;
        int usfCount = 0;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                lastValidValue = val;
            }
            else
            {
                val = double.IsFinite(lastValidValue) ? lastValidValue : 0;
            }

            // Initialize on first value
            if (usfCount == 0)
            {
                usf1 = val;
                usf2 = val;
                prevInput1 = val;
                prevInput2 = val;
                usfCount = 1;
            }

            // Calculate USF
            double usf;
            if (usfCount < 4)
            {
                usf = val;
            }
            else
            {
                usf = Math.FusedMultiplyAdd(c3, usf2,
                    Math.FusedMultiplyAdd(c2, usf1,
                        Math.FusedMultiplyAdd(k2, prevInput2,
                            Math.FusedMultiplyAdd(k1, prevInput1, k0 * val))));
            }

            usf2 = usf1;
            usf1 = usf;
            prevInput2 = prevInput1;
            prevInput1 = val;
            usfCount++;

            // Calculate residual squared
            double residual = val - usf;
            double residualSq = residual * residual;

            // Update running sum with ring buffer
            if (count == period)
            {
                sumSq -= residualSqBuffer[head];
                count--;
            }
            sumSq += residualSq;
            count++;
            residualSqBuffer[head] = residualSq;
            head = (head + 1) % period;

            // Calculate RMS
            double rms = count > 0 ? Math.Sqrt(sumSq / count) : 0;

            // Calculate bands
            double bandOffset = multiplier * rms;
            upper[i] = usf + bandOffset;
            middle[i] = usf;
            lower[i] = usf - bandOffset;
        }
    }
}