using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// EBSW: Ehlers Even Better Sinewave - A normalized oscillator that extracts the
/// dominant cycle from price data using high-pass and super-smoother filters with
/// automatic gain control (AGC).
/// </summary>
/// <remarks>
/// The Even Better Sinewave indicator, developed by John Ehlers, improves upon
/// earlier sinewave indicators by combining detrending, smoothing, and normalization
/// in a single robust algorithm.
///
/// Algorithm:
/// 1. High-pass filter (HPF) removes low-frequency trend component
/// 2. Super-smoother filter (SSF) removes high-frequency noise
/// 3. Three-bar average creates the wave component
/// 4. Automatic gain control normalizes output to [-1, +1]
///
/// Formula:
/// alpha1 = (1 - sin(2π/hpLength)) / cos(2π/hpLength)
/// hp = 0.5 * (1 + alpha1) * (src - src[1]) + alpha1 * hp[1]
///
/// alpha2 = exp(-√2 * π / ssfLength)
/// beta = 2 * alpha2 * cos(√2 * π / ssfLength)
/// c1 = 1 - beta + alpha2², c2 = beta, c3 = -alpha2²
/// filt = c1 * (hp + hp[1]) / 2 + c2 * filt[1] + c3 * filt[2]
///
/// wave = (filt + filt[1] + filt[2]) / 3
/// pwr = (filt² + filt[1]² + filt[2]²) / 3
/// sinewave = wave / sqrt(pwr)  [clamped to -1..+1]
///
/// Properties:
/// - Oscillates between -1 and +1
/// - Zero crossings identify potential turning points
/// - High-pass filter removes trend bias
/// - Super-smoother reduces whipsaws
/// - AGC adapts to volatility automatically
/// </remarks>
[SkipLocalsInit]
public sealed class Ebsw : AbstractBase
{
    private readonly int _hpLength;
    private readonly int _ssfLength;

    // High-pass filter coefficient
    private readonly double _alpha1;

    // Super-smoother filter coefficients
    private readonly double _c1, _c2, _c3;

    // State record for snapshot/restore
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Src0, double Src1,
        double Hp0, double Hp1,
        double Filt0, double Filt1, double Filt2,
        double LastValidValue
    );

    private State _s;
    private State _ps;

    private int _barCount;
    private int _p_barCount;

    public override bool IsHot => _barCount >= WarmupPeriod;

    /// <summary>
    /// Creates a new Ehlers Even Better Sinewave indicator.
    /// </summary>
    /// <param name="hpLength">Period for the high-pass filter (detrending). Must be >= 1 and != 4.</param>
    /// <param name="ssfLength">Period for the super-smoother filter. Must be >= 1.</param>
    public Ebsw(int hpLength = 40, int ssfLength = 10)
    {
        if (hpLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(hpLength), "HP length must be at least 1.");
        }
        if (ssfLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ssfLength), "SSF length must be at least 1.");
        }

        // Validate that cos(2π/hpLength) is not zero (hpLength == 4 causes division by zero)
        double angleHp = 2.0 * Math.PI / hpLength;
        double cosAngleHp = Math.Cos(angleHp);
        if (Math.Abs(cosAngleHp) < 1e-10)
        {
            throw new ArgumentOutOfRangeException(nameof(hpLength), "hpLength cannot be 4 because cos(2π/hpLength) is zero, causing division by zero.");
        }

        _hpLength = hpLength;
        _ssfLength = ssfLength;

        // High-pass filter coefficient: alpha1 = (1 - sin(angle)) / cos(angle)
        _alpha1 = (1.0 - Math.Sin(angleHp)) / cosAngleHp;

        // Super-smoother filter coefficients
        double angleSsf = Math.Sqrt(2.0) * Math.PI / ssfLength;
        double alpha2 = Math.Exp(-angleSsf);
        double beta = 2.0 * alpha2 * Math.Cos(angleSsf);
        _c2 = beta;
        _c3 = -(alpha2 * alpha2);
        _c1 = 1.0 - _c2 - _c3;

        Name = $"Ebsw({hpLength},{ssfLength})";
        WarmupPeriod = Math.Max(hpLength, ssfLength) + 3; // Need 3 bars for wave calculation

        // Initialize state
        _s = new State(0, 0, 0, 0, 0, 0, 0, 0);
        _ps = _s;
        _barCount = 0;
        _p_barCount = 0;
    }

    /// <summary>
    /// Creates a chained Ehlers Even Better Sinewave indicator.
    /// </summary>
    /// <param name="source">The source indicator to chain from.</param>
    /// <param name="hpLength">Period for the high-pass filter.</param>
    /// <param name="ssfLength">Period for the super-smoother filter.</param>
    public Ebsw(ITValuePublisher source, int hpLength = 40, int ssfLength = 10) : this(hpLength, ssfLength)
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
            _p_barCount = _barCount;
        }
        else
        {
            _s = _ps;
            _barCount = _p_barCount;
        }

        var s = _s;

        // Handle non-finite values
        double src = input.Value;
        if (!double.IsFinite(src))
        {
            src = s.LastValidValue;
        }
        else
        {
            s = s with { LastValidValue = src };
        }

        // Increment bar count
        if (isNew)
        {
            _barCount++;
        }

        // Shift source history
        double src1 = s.Src0;
        double src0 = src;

        // High-pass filter: hp = 0.5 * (1 + alpha1) * (src - src[1]) + alpha1 * hp[1]
        double hp1 = s.Hp0;
        double hp0 = Math.FusedMultiplyAdd(0.5 * (1.0 + _alpha1), src0 - src1, _alpha1 * hp1);

        // Super-smoother filter: filt = c1 * (hp + hp[1]) / 2 + c2 * filt[1] + c3 * filt[2]
        double filt2 = s.Filt1;
        double filt1 = s.Filt0;
        double filt0 = Math.FusedMultiplyAdd(_c1, (hp0 + hp1) * 0.5,
                           Math.FusedMultiplyAdd(_c2, filt1, _c3 * filt2));

        // Wave component: 3-bar average of filtered values
        double wave = (filt0 + filt1 + filt2) / 3.0;

        // Power: 3-bar average of squared filtered values
        double pwr = ((filt0 * filt0) + (filt1 * filt1) + (filt2 * filt2)) / 3.0;

        // Automatic gain control: normalize by RMS, clamp to [-1, +1]
        double sineWave = pwr > 0 ? wave / Math.Sqrt(pwr) : 0;
        sineWave = Math.Clamp(sineWave, -1.0, 1.0);

        // Update state
        _s = new State(src0, src1, hp0, hp1, filt0, filt1, filt2, s.LastValidValue);

        Last = new TValue(input.Time, sineWave);
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

        Batch(source.Values, vSpan, _hpLength, _ssfLength);
        source.Times.CopyTo(tSpan);

        // Prime state with all values (IIR-based indicator)
        foreach (var tv in source)
        {
            Update(tv);
        }

        return new TSeries(t, v);
    }

    public override void Reset()
    {
        _s = new State(0, 0, 0, 0, 0, 0, 0, 0);
        _ps = _s;
        _barCount = 0;
        _p_barCount = 0;
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
    /// Calculates EBSW for a time series.
    /// </summary>
    public static TSeries Batch(TSeries source, int hpLength = 40, int ssfLength = 10)
    {
        var ebsw = new Ebsw(hpLength, ssfLength);
        return ebsw.Update(source);
    }

    /// <summary>
    /// Calculates EBSW in-place using a pre-allocated output span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int hpLength = 40, int ssfLength = 10)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (hpLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(hpLength), "HP length must be at least 1.");
        }
        if (ssfLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ssfLength), "SSF length must be at least 1.");
        }

        // Validate that cos(2π/hpLength) is not zero (hpLength == 4 causes division by zero)
        double angleHp = 2.0 * Math.PI / hpLength;
        double cosAngleHp = Math.Cos(angleHp);
        if (Math.Abs(cosAngleHp) < 1e-10)
        {
            throw new ArgumentOutOfRangeException(nameof(hpLength), "hpLength cannot be 4 because cos(2π/hpLength) is zero, causing division by zero.");
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // High-pass filter coefficient
        double alpha1 = (1.0 - Math.Sin(angleHp)) / cosAngleHp;
        double hpCoef = 0.5 * (1.0 + alpha1);

        // Super-smoother filter coefficients
        double angleSsf = Math.Sqrt(2.0) * Math.PI / ssfLength;
        double alpha2 = Math.Exp(-angleSsf);
        double beta = 2.0 * alpha2 * Math.Cos(angleSsf);
        double c2 = beta;
        double c3 = -(alpha2 * alpha2);
        double c1 = 1.0 - c2 - c3;

        double src1 = 0, hp0 = 0, hp1 = 0;
        double filt0 = 0, filt1 = 0, filt2 = 0;
        double lastValid = 0;

        for (int i = 0; i < len; i++)
        {
            double src0 = source[i];
            if (!double.IsFinite(src0))
            {
                src0 = lastValid;
            }
            else
            {
                lastValid = src0;
            }

            // High-pass filter
            hp0 = Math.FusedMultiplyAdd(hpCoef, src0 - src1, alpha1 * hp1);

            // Super-smoother filter
            filt0 = Math.FusedMultiplyAdd(c1, (hp0 + hp1) * 0.5,
                        Math.FusedMultiplyAdd(c2, filt1, c3 * filt2));

            // Wave component
            double wave = (filt0 + filt1 + filt2) / 3.0;

            // Power
            double pwr = ((filt0 * filt0) + (filt1 * filt1) + (filt2 * filt2)) / 3.0;

            // AGC normalization
            double sineWave = pwr > 0 ? wave / Math.Sqrt(pwr) : 0;
            output[i] = Math.Clamp(sineWave, -1.0, 1.0);

            // Shift history
            src1 = src0;
            hp1 = hp0;
            filt2 = filt1;
            filt1 = filt0;
        }
    }

    public static (TSeries Results, Ebsw Indicator) Calculate(TSeries source, int hpLength = 40, int ssfLength = 10)
    {
        var indicator = new Ebsw(hpLength, ssfLength);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}