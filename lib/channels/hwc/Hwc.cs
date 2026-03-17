using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// HWC: Holt-Winter Channel
/// </summary>
/// <remarks>
/// Volatility channel built around the Holt-Winters Moving Average (HWMA).
/// Bands are placed at ±multiplier × √(filt) where filt is an EMA-smoothed
/// squared forecast error, giving adaptive-width bands that widen with
/// prediction error and contract when the HWMA tracks price well.
///
/// Calculation:
/// <c>Middle = HWMA(source)</c>,
/// <c>filt = α×(source − forecast)² + (1−α)×prev_filt</c>,
/// <c>Upper = Middle + mult×√filt</c>,
/// <c>Lower = Middle − mult×√filt</c>.
/// </remarks>
[SkipLocalsInit]
public sealed class Hwc : AbstractBase
{
    private readonly double _alpha;
    private readonly double _beta;
    private readonly double _gamma;
    private readonly double _decayAlpha;
    private readonly double _decayBeta;
    private readonly double _decayGamma;
    private readonly double _multiplier;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double F, double V, double A,
        double Filt, double LastValidValue,
        bool IsInitialized
    );
    private State _state;
    private State _p_state;

    public override bool IsHot => _state.IsInitialized;

    /// <summary>Upper band = HWMA + mult × √filt</summary>
    public TValue Upper { get; private set; }

    /// <summary>Middle band = HWMA output</summary>
    public TValue Middle { get; private set; }

    /// <summary>Lower band = HWMA − mult × √filt</summary>
    public TValue Lower { get; private set; }

    // ────────────────────────── constructors ──────────────────────────

    /// <summary>
    /// Creates HWC with auto-derived α/β/γ from period.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hwc(int period = 20, double multiplier = 1.0)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be > 0", nameof(period));
        }
        if (multiplier <= 0)
        {
            throw new ArgumentException("Multiplier must be > 0", nameof(multiplier));
        }

        _alpha = 2.0 / (period + 1.0);
        _beta = 1.0 / period;
        _gamma = 1.0 / period;
        _decayAlpha = 1.0 - _alpha;
        _decayBeta = 1.0 - _beta;
        _decayGamma = 1.0 - _gamma;
        _multiplier = multiplier;

        WarmupPeriod = period;
        Name = $"Hwc({period},{multiplier:F1})";
        _state = new State(double.NaN, 0, 0, 0, double.NaN, false);
    }

    /// <summary>
    /// Creates HWC with explicit smoothing factors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hwc(double alpha, double beta, double gamma, double multiplier = 1.0)
    {
        if (alpha is <= 0 or > 1)
        {
            throw new ArgumentException("Alpha must be (0,1]", nameof(alpha));
        }
        if (beta is < 0 or > 1)
        {
            throw new ArgumentException("Beta must be [0,1]", nameof(beta));
        }
        if (gamma is < 0 or > 1)
        {
            throw new ArgumentException("Gamma must be [0,1]", nameof(gamma));
        }
        if (multiplier <= 0)
        {
            throw new ArgumentException("Multiplier must be > 0", nameof(multiplier));
        }

        _alpha = alpha;
        _beta = beta;
        _gamma = gamma;
        _decayAlpha = 1.0 - alpha;
        _decayBeta = 1.0 - beta;
        _decayGamma = 1.0 - gamma;
        _multiplier = multiplier;

        int effectivePeriod = Math.Max((int)(2.0 / alpha - 1.0), 1);
        WarmupPeriod = effectivePeriod;
        Name = $"Hwc({alpha:F3},{beta:F3},{gamma:F3},{multiplier:F1})";
        _state = new State(double.NaN, 0, 0, 0, double.NaN, false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hwc(ITValuePublisher source, int period = 20, double multiplier = 1.0)
        : this(period, multiplier)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            return input;
        }
        return _state.IsInitialized ? _state.LastValidValue : double.NaN;
    }

    // ────────────────────────── core Update ──────────────────────────

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

        double val = GetValidValue(input.Value);

        if (!double.IsFinite(val))
        {
            Last = new TValue(input.Time, double.NaN);
            Upper = Middle = Lower = Last;
            PubEvent(Last);
            return Last;
        }

        _state = _state with { LastValidValue = val };

        double result;
        double filtVal;

        if (!_state.IsInitialized)
        {
            _state = _state with { F = val, V = 0, A = 0, Filt = 0, IsInitialized = true };
            result = val;
            filtVal = 0;
        }
        else
        {
            double prevF = _state.F;
            double prevV = _state.V;
            double prevA = _state.A;

            // HWMA: F = α×src + (1−α)×(prevF + prevV + 0.5×prevA)
            double forecast = prevF + prevV + 0.5 * prevA;
            double newF = Math.FusedMultiplyAdd(forecast, _decayAlpha, _alpha * val);

            // V = β×(F − prevF) + (1−β)×(prevV + prevA)
            double newV = Math.FusedMultiplyAdd(prevV + prevA, _decayBeta, _beta * (newF - prevF));

            // A = γ×(V − prevV) + (1−γ)×prevA
            double newA = Math.FusedMultiplyAdd(prevA, _decayGamma, _gamma * (newV - prevV));

            result = newF + newV + 0.5 * newA;

            // Adaptive volatility filter: filt = α×(src − forecast)² + (1−α)×prevFilt
            double err = val - forecast;
            filtVal = Math.FusedMultiplyAdd(err * err, _alpha, _state.Filt * _decayAlpha);

            _state = _state with { F = newF, V = newV, A = newA, Filt = filtVal };
        }

        double band = _multiplier * Math.Sqrt(filtVal);

        Last = new TValue(input.Time, result);
        Middle = Last;
        Upper = new TValue(input.Time, result + band);
        Lower = new TValue(input.Time, result - band);

        PubEvent(Last);
        return Last;
    }

    // ────────────────────────── Update(TSeries) ──────────────────────────

    public override TSeries Update(TSeries source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        int len = source.Count;
        TSeries middleSeries = new(capacity: len);

        Reset();
        for (int i = 0; i < len; i++)
        {
            TValue input = source[i];
            Update(input, isNew: true);
            middleSeries.Add(input.Time, Middle.Value, isNew: true);
        }

        return middleSeries;
    }

    // ────────────────────────── Prime ──────────────────────────

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        step ??= TimeSpan.FromSeconds(1);
        DateTime startTime = DateTime.UtcNow;

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(startTime + i * step.Value, source[i]), isNew: true);
        }
    }

    // ────────────────────────── Reset ──────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _state = new State(double.NaN, 0, 0, 0, double.NaN, false);
        _p_state = _state;
        Upper = Middle = Lower = default;
    }

    // ────────────────────────── static Batch (Span) ──────────────────────────

    public static void Batch(
        ReadOnlySpan<double> source,
        Span<double> upper, Span<double> middle, Span<double> lower,
        int period = 20, double multiplier = 1.0)
    {
        int n = source.Length;
        if (n != upper.Length || n != middle.Length || n != lower.Length)
        {
            throw new ArgumentException("All spans must have the same length", nameof(source));
        }

        double alpha = 2.0 / (period + 1.0);
        double beta = 1.0 / period;
        double gamma = 1.0 / period;
        double dA = 1.0 - alpha;
        double dB = 1.0 - beta;
        double dG = 1.0 - gamma;

        double f = double.NaN, v = 0, a = 0, filt = 0;
        bool init = false;
        double lastValid = double.NaN;

        for (int i = 0; i < n; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val))
            {
                val = double.IsFinite(lastValid) ? lastValid : 0;
            }
            else
            {
                lastValid = val;
            }

            double result;
            if (!init)
            {
                f = val; v = 0; a = 0; filt = 0;
                init = true;
                result = val;
            }
            else
            {
                double forecast = f + v + 0.5 * a;
                double newF = Math.FusedMultiplyAdd(forecast, dA, alpha * val);
                double newV = Math.FusedMultiplyAdd(v + a, dB, beta * (newF - f));
                double newA = Math.FusedMultiplyAdd(a, dG, gamma * (newV - v));
                result = newF + newV + 0.5 * newA;

                double err = val - forecast;
                filt = Math.FusedMultiplyAdd(err * err, alpha, filt * dA);

                f = newF; v = newV; a = newA;
            }

            double band = multiplier * Math.Sqrt(filt);
            middle[i] = result;
            upper[i] = result + band;
            lower[i] = result - band;
        }
    }

    // ────────────────────────── static Batch (TSeries) ──────────────────────────

    public static (TSeries Upper, TSeries Middle, TSeries Lower) Batch(
        TSeries source, int period = 20, double multiplier = 1.0)
    {
        var ind = new Hwc(period, multiplier);
        int len = source.Count;
        if (len == 0)
        {
            return ([], [], []);
        }

        var tU = new List<long>(len); var vU = new List<double>(len);
        var tM = new List<long>(len); var vM = new List<double>(len);
        var tL = new List<long>(len); var vL = new List<double>(len);
        CollectionsMarshal.SetCount(tU, len); CollectionsMarshal.SetCount(vU, len);
        CollectionsMarshal.SetCount(tM, len); CollectionsMarshal.SetCount(vM, len);
        CollectionsMarshal.SetCount(tL, len); CollectionsMarshal.SetCount(vL, len);

        var tuSpan = CollectionsMarshal.AsSpan(tU); var vuSpan = CollectionsMarshal.AsSpan(vU);
        var tmSpan = CollectionsMarshal.AsSpan(tM); var vmSpan = CollectionsMarshal.AsSpan(vM);
        var tlSpan = CollectionsMarshal.AsSpan(tL); var vlSpan = CollectionsMarshal.AsSpan(vL);

        for (int i = 0; i < len; i++)
        {
            ind.Update(source[i], isNew: true);
            long time = source[i].Time;
            tuSpan[i] = time; vuSpan[i] = ind.Upper.Value;
            tmSpan[i] = time; vmSpan[i] = ind.Middle.Value;
            tlSpan[i] = time; vlSpan[i] = ind.Lower.Value;
        }

        return (new TSeries(tU, vU), new TSeries(tM, vM), new TSeries(tL, vL));
    }

    public static ((TSeries Upper, TSeries Middle, TSeries Lower) Results, Hwc Indicator) Calculate(
        TSeries source, int period = 20, double multiplier = 1.0)
    {
        var ind = new Hwc(period, multiplier);
        int len = source.Count;
        if (len == 0)
        {
            return (([], [], []), ind);
        }

        var tU = new List<long>(len); var vU = new List<double>(len);
        var tM = new List<long>(len); var vM = new List<double>(len);
        var tL = new List<long>(len); var vL = new List<double>(len);
        CollectionsMarshal.SetCount(tU, len); CollectionsMarshal.SetCount(vU, len);
        CollectionsMarshal.SetCount(tM, len); CollectionsMarshal.SetCount(vM, len);
        CollectionsMarshal.SetCount(tL, len); CollectionsMarshal.SetCount(vL, len);

        var tuSpan = CollectionsMarshal.AsSpan(tU); var vuSpan = CollectionsMarshal.AsSpan(vU);
        var tmSpan = CollectionsMarshal.AsSpan(tM); var vmSpan = CollectionsMarshal.AsSpan(vM);
        var tlSpan = CollectionsMarshal.AsSpan(tL); var vlSpan = CollectionsMarshal.AsSpan(vL);

        for (int i = 0; i < len; i++)
        {
            ind.Update(source[i], isNew: true);
            long time = source[i].Time;
            tuSpan[i] = time; vuSpan[i] = ind.Upper.Value;
            tmSpan[i] = time; vmSpan[i] = ind.Middle.Value;
            tlSpan[i] = time; vlSpan[i] = ind.Lower.Value;
        }

        return ((new TSeries(tU, vU), new TSeries(tM, vM), new TSeries(tL, vL)), ind);
    }
}
