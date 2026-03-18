using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// LPF: Ehlers Linear Predictive Filter
/// Estimates the dominant cycle period using a Griffiths linear predictive filter
/// combined with spectral analysis of the adapted filter coefficients.
/// </summary>
/// <remarks>
/// The algorithm is based on John F. Ehlers' "Linear Predictive Filters And
/// Instantaneous Frequency" from Technical Analysis of Stocks &amp; Commodities,
/// January 2025.
///
/// Algorithm stages:
///   1. Roofing filter (HP + SuperSmoother) band-limits the input
///   2. AGC normalizes the band-limited signal to ±1
///   3. Griffiths LMS predictor adapts coefficients to minimize prediction error
///   4. DFT of coefficients yields power spectrum
///   5. Center of gravity of spectrum identifies dominant cycle
///
/// Key properties:
///   - Self-calibrating via adaptive coefficient updates
///   - Dominant cycle constrained to change by at most 2 bars per update
///   - Also provides predictive signal and trigger outputs
///   - Based on Lloyd Griffiths' "Rapid Measurement of Digital Instantaneous
///     Frequency" (IEEE Trans. ASSP-23, 1975)
///
/// Pine Script reference:
/// https://github.com/mihakralj/pinescript/blob/main/indicators/cycles/lpf.md
///
/// Complexity: O(Length² + Length × (UpperBound − LowerBound)) per bar
/// </remarks>
[SkipLocalsInit]
public sealed class Lpf : AbstractBase
{
    private readonly int _lowerBound;
    private readonly int _upperBound;
    private readonly int _dataLength;

    // Roofing filter coefficients (precomputed)
    private readonly double _hpAlpha;
    private readonly double _ssC1, _ssC2, _ssC3;

    // Griffiths coefficient arrays (need snapshot/restore)
    private readonly double[] _coef;
    private readonly double[] _p_coef;

    // Signal history buffer
    private readonly double[] _xx;
    private readonly double[] _p_xx;

    // Power spectrum buffer (reused each bar)
    private readonly double[] _pwr;

    // Filter state
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Src1, double Src2,
        double Hp1, double Hp2,
        double Lp1, double Lp2,
        double Peak,
        double Signal,
        double Dom, double PrevDom,
        double Predict,
        double DomPower,
        int BarCount,
        double LastValid
    )
    {
        public static State New() => new()
        {
            Peak = 0.1,
            Dom = 0.0,
            PrevDom = 0.0,
            LastValid = double.NaN
        };
    }

    private State _state;
    private State _p_state;

    private ITValuePublisher? _publisher;
    private TValuePublishedHandler? _handler;
    private bool _isNew;

    /// <summary>Gets the current dominant cycle period.</summary>
    public double DominantCycle => _state.Dom;

    /// <summary>Gets the AGC-normalized band-limited signal (±1 range).</summary>
    public double Signal => _state.Signal;

    /// <summary>Gets the predicted value (2-bar ahead prediction of the normalized signal).</summary>
    public double Predict => _state.Predict;

    /// <summary>Lower bandpass boundary (minimum period).</summary>
    public int LowerBound => _lowerBound;

    /// <summary>Upper bandpass boundary (maximum period).</summary>
    public int UpperBound => _upperBound;

    /// <summary>Data length for Griffiths predictor.</summary>
    public int DataLength => _dataLength;

    public bool IsNew => _isNew;
    public override bool IsHot => _state.BarCount >= WarmupPeriod;

    /// <summary>
    /// Creates a new Ehlers Linear Predictive Filter indicator.
    /// </summary>
    /// <param name="lowerBound">Lower bandpass boundary - minimum period to detect (≥ 8).</param>
    /// <param name="upperBound">Upper bandpass boundary - maximum period to detect (> lowerBound).</param>
    /// <param name="dataLength">Data length for adaptive predictor (≥ 4). Ehlers recommends matching upperBound.</param>
    public Lpf(int lowerBound = 18, int upperBound = 40, int dataLength = 40)
    {
        if (lowerBound < 8)
        {
            throw new ArgumentOutOfRangeException(nameof(lowerBound), "Lower bound must be at least 8.");
        }
        if (upperBound <= lowerBound)
        {
            throw new ArgumentOutOfRangeException(nameof(upperBound), "Upper bound must be greater than lower bound.");
        }
        if (dataLength < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(dataLength), "Data length must be at least 4.");
        }

        _lowerBound = lowerBound;
        _upperBound = upperBound;
        _dataLength = dataLength;

        Name = $"LPF({lowerBound},{upperBound},{dataLength})";
        WarmupPeriod = upperBound * 2;

        // Precompute HP Butterworth coefficient
        double angle707 = 0.707 * 2.0 * Math.PI / upperBound;
        _hpAlpha = (Math.Cos(angle707) + Math.Sin(angle707) - 1.0) / Math.Cos(angle707);

        // Precompute SuperSmoother (LP) coefficients
        double sqrt2Pi = Math.Sqrt(2.0) * Math.PI / lowerBound;
        double a1 = Math.Exp(-sqrt2Pi);
        double b1 = 2.0 * a1 * Math.Cos(sqrt2Pi);
        _ssC2 = b1;
        _ssC3 = -(a1 * a1);
        _ssC1 = 1.0 - _ssC2 - _ssC3;

        // Allocate arrays
        _coef = new double[dataLength + 1];
        _p_coef = new double[dataLength + 1];
        _xx = new double[dataLength + 1];
        _p_xx = new double[dataLength + 1];
        _pwr = new double[upperBound + 2];

        // Initialize state
        _state = State.New();
        _state = _state with { Dom = (lowerBound + upperBound) * 0.5, PrevDom = (lowerBound + upperBound) * 0.5 };
        _p_state = _state;
    }

    /// <summary>
    /// Creates a chained LPF indicator subscribed to a source publisher.
    /// </summary>
    public Lpf(ITValuePublisher source, int lowerBound = 18, int upperBound = 40, int dataLength = 40)
        : this(lowerBound, upperBound, dataLength)
    {
        ArgumentNullException.ThrowIfNull(source);
        _publisher = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        _isNew = isNew;
        if (isNew)
        {
            _p_state = _state;
            Array.Copy(_coef, _p_coef, _coef.Length);
            Array.Copy(_xx, _p_xx, _xx.Length);
        }
        else
        {
            _state = _p_state;
            Array.Copy(_p_coef, _coef, _coef.Length);
            Array.Copy(_p_xx, _xx, _xx.Length);
        }

        // Handle non-finite input
        double val = input.Value;
        if (!double.IsFinite(val))
        {
            val = double.IsFinite(_state.LastValid) ? _state.LastValid : 0.0;
        }
        else
        {
            _state = _state with { LastValid = val };
        }

        int barCount = isNew ? _state.BarCount + 1 : _state.BarCount;

        // ── Stage 1: Roofing Filter ──────────────────────────────────────
        // Highpass: 2nd-order Butterworth
        double hpCoef = (1.0 - _hpAlpha * 0.5);
        double hpInput = hpCoef * hpCoef * (val - 2.0 * _state.Src1 + _state.Src2);
        double oneMinusAlpha = 1.0 - _hpAlpha;
        double hp = hpInput + 2.0 * oneMinusAlpha * _state.Hp1 - oneMinusAlpha * oneMinusAlpha * _state.Hp2;

        // SuperSmoother lowpass
        double lp = Math.FusedMultiplyAdd(_ssC1, (hp + _state.Hp1) * 0.5,
                        Math.FusedMultiplyAdd(_ssC2, _state.Lp1, _ssC3 * _state.Lp2));

        // ── Stage 2: AGC Normalization ───────────────────────────────────
        double peak = 0.991 * _state.Peak;
        double absLp = Math.Abs(lp);
        if (absLp > peak)
        {
            peak = absLp;
        }
        double signal = peak > 0.0 ? lp / peak : 0.0;

        // ── Stage 3: Griffiths Adaptive Predictor ────────────────────────
        // Shift data buffer (shift right by 1)
        for (int i = _dataLength; i >= 1; i--)
        {
            _xx[i] = _xx[i - 1];
        }
        _xx[0] = signal;

        // Compute signal power for normalization
        double sigPower = 0.0;
        for (int i = 0; i < _dataLength; i++)
        {
            sigPower += _xx[i] * _xx[i];
        }
        sigPower /= _dataLength;

        // Convergence factor (μ)
        double mu = sigPower > 0.0 ? 0.25 / (sigPower * _dataLength) : 0.0;

        // Predict current value from past
        double xBar = 0.0;
        for (int i = 1; i <= _dataLength; i++)
        {
            xBar += _coef[i] * _xx[i];
        }

        // Error = actual - predicted
        double error = _xx[0] - xBar;

        // Update coefficients via LMS rule
        for (int i = 1; i <= _dataLength; i++)
        {
            _coef[i] += mu * error * _xx[i];
        }

        // ── Stage 4: Spectrum from Coefficients (DFT) ────────────────────
        double maxPwrLocal = 0.0;
        for (int period = _lowerBound; period <= _upperBound; period++)
        {
            double realPart = 0.0;
            double imagPart = 0.0;
            double angleStep = 2.0 * Math.PI / period;
            for (int i = 1; i <= _dataLength; i++)
            {
                double angle = angleStep * i;
                realPart += _coef[i] * Math.Cos(angle);
                imagPart += _coef[i] * Math.Sin(angle);
            }
            double p = realPart * realPart + imagPart * imagPart;
            _pwr[period] = p;
            if (p > maxPwrLocal)
            {
                maxPwrLocal = p;
            }
        }

        // ── Stage 5: Dominant Cycle via Center of Gravity ────────────────
        double spx = 0.0;
        double sp = 0.0;
        for (int period = _lowerBound; period <= _upperBound; period++)
        {
            double normPwr = maxPwrLocal > 0.0 ? _pwr[period] / maxPwrLocal : 0.0;
            if (normPwr >= 0.5)
            {
                spx += period * normPwr;
                sp += normPwr;
            }
        }

        double rawDom = sp > 0.0 ? spx / sp : _state.Dom;

        // Constrain change to ±2 bars per update (prevents bouncing)
        double prevDom = _state.PrevDom;
        if (rawDom - prevDom > 2.0)
        {
            rawDom = prevDom + 2.0;
        }
        if (prevDom - rawDom > 2.0)
        {
            rawDom = prevDom - 2.0;
        }

        double dom = Math.Clamp(rawDom, _lowerBound, _upperBound);

        // Two-bar prediction for trigger line
        double xPred = 0.0;
        if (_dataLength > 2)
        {
            for (int i = 1; i <= _dataLength - 2; i++)
            {
                xPred += _coef[i] * _xx[i];
            }
        }

        // Compute power at dominant cycle
        int domIdx = Math.Clamp((int)Math.Round(dom), _lowerBound, _upperBound);
        double domPower = maxPwrLocal > 0.0 ? Math.Clamp(_pwr[domIdx] / maxPwrLocal, 0.0, 1.0) : 0.0;

        // Update state
        _state = new State(
            Src1: val, Src2: _state.Src1,
            Hp1: hp, Hp2: _state.Hp1,
            Lp1: lp, Lp2: _state.Lp1,
            Peak: peak,
            Signal: signal,
            Dom: dom, PrevDom: dom,
            Predict: xPred,
            DomPower: domPower,
            BarCount: barCount,
            LastValid: _state.LastValid
        );

        Last = new TValue(input.Time, dom);
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

        for (int i = 0; i < len; i++)
        {
            var result = Update(source[i]);
            vSpan[i] = result.Value;
        }
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    /// <summary>Computes LPF for a given time series.</summary>
    public static TSeries Batch(TSeries source, int lowerBound = 18, int upperBound = 40, int dataLength = 40)
    {
        var indicator = new Lpf(lowerBound, upperBound, dataLength);
        return indicator.Update(source);
    }

    /// <summary>Computes LPF in-place using pre-allocated output span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
                             int lowerBound = 18, int upperBound = 40, int dataLength = 40)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }

        var indicator = new Lpf(lowerBound, upperBound, dataLength);
        for (int i = 0; i < source.Length; i++)
        {
            var result = indicator.Update(new TValue(DateTime.MinValue, source[i]));
            output[i] = result.Value;
        }
    }

    /// <summary>Calculates LPF for a time series and returns both results and indicator state.</summary>
    public static (TSeries Results, Lpf Indicator) Calculate(TSeries source,
        int lowerBound = 18, int upperBound = 40, int dataLength = 40)
    {
        var indicator = new Lpf(lowerBound, upperBound, dataLength);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _state = State.New();
        _state = _state with { Dom = (_lowerBound + _upperBound) * 0.5, PrevDom = (_lowerBound + _upperBound) * 0.5 };
        _p_state = _state;
        Array.Clear(_coef);
        Array.Clear(_p_coef);
        Array.Clear(_xx);
        Array.Clear(_p_xx);
        Array.Clear(_pwr);
        Last = default;
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
