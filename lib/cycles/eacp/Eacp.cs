using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// EACP: Ehlers Autocorrelation Periodogram - Dominant cycle estimator using
/// autocorrelation and spectral analysis via the Wiener-Khinchin theorem.
/// </summary>
/// <remarks>
/// The Autocorrelation Periodogram indicator, developed by John Ehlers, estimates
/// the dominant cycle period in price data by computing autocorrelation coefficients
/// and transforming them to the frequency domain using DFT principles.
///
/// Algorithm:
/// 1. High-pass filter removes DC offset and low-frequency trend
/// 2. Super-smoother filter reduces high-frequency noise
/// 3. Pearson correlation coefficients computed for each lag
/// 4. DFT converts correlation to power spectrum
/// 5. Smoothed power spectrum identifies dominant frequency
/// 6. Weighted average of high-power periods yields dominant cycle
///
/// Properties:
/// - Returns estimated dominant cycle period
/// - Also provides normalized power at dominant period
/// - Enhance mode applies cubic emphasis to highlight peaks
/// - Self-calibrating via adaptive maximum power tracking
///
/// Key Insight:
/// Autocorrelation naturally detects periodicity as a signal correlates with
/// its own lagged values. The Wiener-Khinchin theorem relates autocorrelation
/// to spectral density, enabling frequency domain analysis.
/// </remarks>
[SkipLocalsInit]
public sealed class Eacp : AbstractBase
{
    private readonly int _minPeriod;
    private readonly int _maxPeriod;
    private readonly int _avgLength;
    private readonly bool _enhance;

    // Filter coefficients
    private readonly double _alphaHP;
    private readonly double _c1, _c2, _c3;
    private readonly double _k; // Power decay factor

    // Buffers for autocorrelation and power spectrum
    private readonly double[] _corr;
    private readonly double[] _power;
    private readonly double[] _smooth;

    // State for filters and output
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Price0, double Price1, double Price2,
        double Hp0, double Hp1, double Hp2,
        double Filt0, double Filt1, double Filt2,
        double Dom, double DomPower, double MaxPwr,
        int BarCount, double LastValidValue
    );

    private State _s;
    private State _ps;

    // History buffer for correlation calculation
    private readonly RingBuffer _filtHistory;

    /// <summary>Gets the current dominant cycle period.</summary>
    public double DominantCycle => _s.Dom;

    /// <summary>Gets the normalized power at the dominant cycle period (0-1).</summary>
    public double NormalizedPower => _s.DomPower;

    public override bool IsHot => _s.BarCount >= WarmupPeriod;

    /// <summary>
    /// Creates a new Ehlers Autocorrelation Periodogram indicator.
    /// </summary>
    /// <param name="minPeriod">Minimum period to evaluate (must be >= 3).</param>
    /// <param name="maxPeriod">Maximum period to evaluate (must be > minPeriod).</param>
    /// <param name="avgLength">Averaging length for Pearson correlation (0 uses lag length).</param>
    /// <param name="enhance">Apply cubic emphasis to highlight dominant peaks.</param>
    public Eacp(int minPeriod = 8, int maxPeriod = 48, int avgLength = 3, bool enhance = true)
    {
        if (minPeriod < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(minPeriod), "Min period must be at least 3.");
        }
        if (maxPeriod <= minPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPeriod), "Max period must be greater than min period.");
        }
        if (avgLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(avgLength), "Average length must be non-negative.");
        }

        _minPeriod = minPeriod;
        _maxPeriod = maxPeriod;
        _avgLength = avgLength;
        _enhance = enhance;

        int size = maxPeriod + 1;

        // High-pass filter coefficient (tuned to maxPeriod)
        double angle = Math.Sqrt(2.0) * Math.PI / maxPeriod;
        _alphaHP = (Math.Cos(angle) + Math.Sin(angle) - 1.0) / Math.Cos(angle);

        // Super-smoother filter coefficients (tuned to minPeriod)
        double a1 = Math.Exp(-Math.Sqrt(2.0) * Math.PI / minPeriod);
        double b1 = 2.0 * a1 * Math.Cos(Math.Sqrt(2.0) * Math.PI / minPeriod);
        _c2 = b1;
        _c3 = -(a1 * a1);
        _c1 = 1.0 - _c2 - _c3;

        // Power decay factor
        double diff = maxPeriod - minPeriod;
        _k = diff > 0 ? Math.Pow(10.0, -0.15 / diff) : 1.0;

        // Allocate buffers
        _corr = new double[size];
        _power = new double[size];
        _smooth = new double[size];
        _filtHistory = new RingBuffer(size + maxPeriod);

        Name = $"Eacp({minPeriod},{maxPeriod})";
        WarmupPeriod = maxPeriod * 2;

        // Initialize state
        double initialDom = (minPeriod + maxPeriod) * 0.5;
        _s = new State(0, 0, 0, 0, 0, 0, 0, 0, 0, initialDom, 0, 0, 0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Creates a chained Ehlers Autocorrelation Periodogram indicator.
    /// </summary>
    public Eacp(ITValuePublisher source, int minPeriod = 8, int maxPeriod = 48, int avgLength = 3, bool enhance = true)
        : this(minPeriod, maxPeriod, avgLength, enhance)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Pub += HandleInput;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleInput(object? sender, in TValueEventArgs e)
    {
        Update(e.Value, e.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _filtHistory.Snapshot();
        }
        else
        {
            _s = _ps;
            _filtHistory.Restore();
        }

        var s = _s;

        // Handle non-finite values
        double price = input.Value;
        if (!double.IsFinite(price))
        {
            price = s.LastValidValue;
        }
        else
        {
            s = s with { LastValidValue = price };
        }

        // Increment bar count
        int barCount = isNew ? s.BarCount + 1 : s.BarCount;

        // Shift price history
        double price2 = s.Price1;
        double price1 = s.Price0;
        double price0 = price;

        // High-pass filter: removes DC and low-frequency trend
        double hp2 = s.Hp1;
        double hp1 = s.Hp0;
        double coef = (1.0 - _alphaHP / 2.0);
        double hp0 = coef * coef * (price0 - 2.0 * price1 + price2)
                   + 2.0 * (1.0 - _alphaHP) * hp1
                   - (1.0 - _alphaHP) * (1.0 - _alphaHP) * hp2;

        // Super-smoother filter: removes high-frequency noise
        double filt2 = s.Filt1;
        double filt1 = s.Filt0;
        double filt0 = _c1 * (hp0 + hp1) * 0.5 + _c2 * filt1 + _c3 * filt2;

        // Add filtered value to history buffer
        _filtHistory.Add(filt0);

        // Compute autocorrelation for each lag
        ComputeAutocorrelation();

        // Compute power spectrum via DFT
        ComputePowerSpectrum();

        // Find dominant cycle
        var (dom, domPower, maxPwr) = FindDominantCycle(s.Dom, s.MaxPwr);

        // Update state
        _s = new State(price0, price1, price2, hp0, hp1, hp2, filt0, filt1, filt2,
                       dom, domPower, maxPwr, barCount, s.LastValidValue);

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

        // Process each value
        for (int i = 0; i < len; i++)
        {
            var result = Update(source[i]);
            vSpan[i] = result.Value;
        }
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ComputeAutocorrelation()
    {
        int histCount = _filtHistory.Count;

        for (int lag = 0; lag <= _maxPeriod; lag++)
        {
            if (lag < 2)
            {
                _corr[lag] = 0;
                continue;
            }

            int window = _avgLength == 0 ? lag : _avgLength;
            if (window < 2)
            {
                window = 2;
            }

            // Compute Pearson correlation coefficient
            double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
            int valid = 0;

            for (int k = 0; k < window && (lag + k) < histCount; k++)
            {
                double x = _filtHistory[histCount - 1 - k];
                double y = (lag + k) < histCount ? _filtHistory[histCount - 1 - lag - k] : 0;
                sx += x;
                sy += y;
                sxx += x * x;
                syy += y * y;
                sxy += x * y;
                valid++;
            }

            double corrVal = 0;
            if (valid > 1)
            {
                double denomX = valid * sxx - sx * sx;
                double denomY = valid * syy - sy * sy;
                double denom = denomX * denomY;
                if (denom > 0)
                {
                    corrVal = (valid * sxy - sx * sy) / Math.Sqrt(denom);
                }
            }

            _corr[lag] = corrVal;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ComputePowerSpectrum()
    {
        // DFT to convert correlation to power spectrum
        for (int period = _minPeriod; period <= _maxPeriod; period++)
        {
            double cosAcc = 0, sinAcc = 0;

            for (int n = 2; n <= _maxPeriod; n++)
            {
                double angle = 2.0 * Math.PI * n / period;
                cosAcc += _corr[n] * Math.Cos(angle);
                sinAcc += _corr[n] * Math.Sin(angle);
            }

            // Power = amplitude squared
            double sq = cosAcc * cosAcc + sinAcc * sinAcc;

            // Smooth the power spectrum (EMA-like smoothing)
            _smooth[period] = 0.2 * sq + 0.8 * _smooth[period];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double dom, double domPower, double maxPwr) FindDominantCycle(double prevDom, double prevMaxPwr)
    {
        // Find local maximum power
        double localMaxPwr = 0;
        for (int period = _minPeriod; period <= _maxPeriod; period++)
        {
            if (_smooth[period] > localMaxPwr)
            {
                localMaxPwr = _smooth[period];
            }
        }

        // Adaptive maximum power tracking
        double maxPwr;
        if (localMaxPwr > prevMaxPwr)
        {
            maxPwr = localMaxPwr;
        }
        else
        {
            maxPwr = _k * prevMaxPwr;
        }

        // Normalize power and apply enhancement
        double weighted = 0, sumWeight = 0, peakPwr = 0;
        for (int period = _minPeriod; period <= _maxPeriod; period++)
        {
            double pwr = maxPwr > 0 ? _smooth[period] / maxPwr : 0;
            if (_enhance)
            {
                pwr = pwr * pwr * pwr; // Cubic emphasis
            }
            _power[period] = pwr;

            if (pwr > peakPwr)
            {
                peakPwr = pwr;
            }

            if (pwr >= 0.5)
            {
                weighted += period * pwr;
                sumWeight += pwr;
            }
        }

        // Calculate dominant cycle - use prevDom as fallback
        double baseDom = sumWeight >= 0.25 ? weighted / sumWeight : prevDom;

        // Apply EMA smoothing (alpha = 0.2) - this is the PineScript formula
        // dom := alpha*(base-dom)+dom which equals dom + alpha*(base-dom)
        double dom = prevDom + 0.2 * (baseDom - prevDom);

        // Ensure dom stays within bounds
        dom = Math.Clamp(dom, _minPeriod, _maxPeriod);

        // Get power at dominant cycle - clamp to [0,1] for floating-point safety
        int domIdx = Math.Clamp((int)Math.Round(dom), _minPeriod, _maxPeriod);
        double domPower = Math.Clamp(_power[domIdx], 0.0, 1.0);

        return (dom, domPower, maxPwr);
    }

    public override void Reset()
    {
        double initialDom = (_minPeriod + _maxPeriod) * 0.5;
        _s = new State(0, 0, 0, 0, 0, 0, 0, 0, 0, initialDom, 0, 0, 0, 0);
        _ps = _s;
        _filtHistory.Clear();
        Array.Clear(_corr);
        Array.Clear(_power);
        Array.Clear(_smooth);
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.UtcNow, value));
        }
    }

    /// <summary>
    /// Calculates EACP for a time series.
    /// </summary>
    public static TSeries Batch(TSeries source, int minPeriod = 8, int maxPeriod = 48,
                                     int avgLength = 3, bool enhance = true)
    {
        var eacp = new Eacp(minPeriod, maxPeriod, avgLength, enhance);
        return eacp.Update(source);
    }

    /// <summary>
    /// Calculates EACP in-place using a pre-allocated output span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
                             int minPeriod = 8, int maxPeriod = 48, int avgLength = 3, bool enhance = true)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (minPeriod < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(minPeriod), "Min period must be at least 3.");
        }
        if (maxPeriod <= minPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPeriod), "Max period must be greater than min period.");
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // Use streaming implementation for batch (complex state management)
        var eacp = new Eacp(minPeriod, maxPeriod, avgLength, enhance);
        for (int i = 0; i < len; i++)
        {
            var result = eacp.Update(new TValue(DateTime.UtcNow, source[i]));
            output[i] = result.Value;
        }
    }

    public static (TSeries Results, Eacp Indicator) Calculate(TSeries source, int minPeriod = 8, int maxPeriod = 48, int avgLength = 3, bool enhance = true)
    {
        var indicator = new Eacp(minPeriod, maxPeriod, avgLength, enhance);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}