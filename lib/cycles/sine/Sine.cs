// Ehlers Sine Wave (SINE) - Cycle extraction using Hilbert Transform
// Uses High-Pass filter + Super-Smoother + Hilbert Transform to extract sine wave
// Based on John Ehlers' "Cybernetic Analysis for Stocks and Futures"

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Ehlers Sine Wave indicator extracts the dominant cycle from price data.
/// Uses a High-Pass filter for detrending, Super-Smoother for noise reduction,
/// and Hilbert Transform FIR for quadrature component extraction.
/// Output ranges from -1.0 to +1.0 representing the normalized sine wave.
/// </summary>
[SkipLocalsInit]
public sealed class Sine : AbstractBase
{
    private readonly int _hpPeriod;
    private readonly int _ssfPeriod;
    private readonly RingBuffer _srcBuffer;
    private readonly RingBuffer _hpBuffer;
    private readonly RingBuffer _filtBuffer;

    // High-Pass filter coefficient
    private readonly double _alphaHP;

    // Super-Smoother coefficients
    private readonly double _c1, _c2, _c3;

    // Hilbert FIR coefficients
    private const double H1 = 0.0962;
    private const double H2 = 0.5769;

    // State tracking
    private int _count;

    public int HpPeriod => _hpPeriod;
    public int SsfPeriod => _ssfPeriod;
    public override bool IsHot => _count >= WarmupPeriod;

    /// <summary>
    /// Creates a new Ehlers Sine Wave indicator.
    /// </summary>
    /// <param name="hpPeriod">High-Pass filter period for detrending (default: 40)</param>
    /// <param name="ssfPeriod">Super-Smoother filter period for smoothing (default: 10)</param>
    public Sine(int hpPeriod = 40, int ssfPeriod = 10)
    {
        if (hpPeriod < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(hpPeriod), "High-Pass period must be >= 1");
        }
        if (ssfPeriod < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ssfPeriod), "Super-Smoother period must be >= 1");
        }

        _hpPeriod = hpPeriod;
        _ssfPeriod = ssfPeriod;
        Name = "SINE";
        WarmupPeriod = Math.Max(hpPeriod, ssfPeriod) + 8; // +8 for Hilbert lookback

        // High-Pass filter coefficient
        double angHP = 2.0 * Math.PI / hpPeriod;
        _alphaHP = (1.0 - Math.Sin(angHP)) / Math.Cos(angHP);

        // Super-Smoother coefficients (2-pole Butterworth)
        double angSSF = Math.Sqrt(2.0) * Math.PI / ssfPeriod;
        double aSSF = Math.Exp(-angSSF);
        double bSSF = 2.0 * aSSF * Math.Cos(angSSF);
        _c2 = bSSF;
        _c3 = -aSSF * aSSF;
        _c1 = 1.0 - _c2 - _c3;

        // Buffers for historical values
        _srcBuffer = new RingBuffer(2);   // src[0], src[1]
        _hpBuffer = new RingBuffer(2);    // hp[0], hp[1]
        _filtBuffer = new RingBuffer(8);  // filt[0..7] for Hilbert

        _count = 0;
        Last = new TValue(DateTime.UtcNow, 0);
    }

    /// <summary>
    /// Creates a chained Sine indicator.
    /// </summary>
    public Sine(ITValuePublisher source, int hpPeriod = 40, int ssfPeriod = 10) : this(hpPeriod, ssfPeriod)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Pub += HandleInput;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleInput(object? sender, in TValueEventArgs e)
    {
        Update(e.Value, e.IsNew);
    }

    // Last valid value for NaN substitution
    private double _lastValidValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double src = input.Value;

        // Handle NaN/Infinity: substitute with last valid value
        if (!double.IsFinite(src))
        {
            src = _lastValidValue;
        }
        else
        {
            _lastValidValue = src;
        }

        if (isNew)
        {
            _srcBuffer.Add(src);
            _count++;
        }
        else
        {
            _srcBuffer.UpdateNewest(src);
        }

        // High-Pass filter: hp = 0.5 * (1 + α) * (src - src[1]) + α * hp[1]
        double src1 = _srcBuffer.Count > 1 ? _srcBuffer[0] : 0;
        double hp1 = _hpBuffer.Count > 0 ? _hpBuffer[^1] : 0;
        double hp = Math.FusedMultiplyAdd(0.5 * (1.0 + _alphaHP), src - src1, _alphaHP * hp1);

        if (isNew)
        {
            _hpBuffer.Add(hp);
        }
        else
        {
            _hpBuffer.UpdateNewest(hp);
        }

        // Super-Smoother: filt = c1 * (hp + hp[1]) / 2 + c2 * filt[1] + c3 * filt[2]
        double hp1b = _hpBuffer.Count > 1 ? _hpBuffer[0] : hp;
        double filt1 = _filtBuffer.Count > 0 ? _filtBuffer[^1] : 0;
        double filt2 = _filtBuffer.Count > 1 ? _filtBuffer[^2] : 0;
        double filt = Math.FusedMultiplyAdd(_c1, (hp + hp1b) / 2.0,
                      Math.FusedMultiplyAdd(_c2, filt1, _c3 * filt2));

        if (isNew)
        {
            _filtBuffer.Add(filt);
        }
        else
        {
            _filtBuffer.UpdateNewest(filt);
        }

        // Hilbert Transform for quadrature component Q
        // Q = 0.0962 * filt[3] + 0.5769 * filt[1] - 0.5769 * filt[5] - 0.0962 * filt[7]
        // Using ^N for from-end indexing: ^1 = newest, ^2 = second newest, etc.
        double filt1q = _filtBuffer.Count > 1 ? _filtBuffer[^2] : 0;
        double filt3 = _filtBuffer.Count > 3 ? _filtBuffer[^4] : 0;
        double filt5 = _filtBuffer.Count > 5 ? _filtBuffer[^6] : 0;
        double filt7 = _filtBuffer.Count > 7 ? _filtBuffer[^8] : 0;

        double Q = Math.FusedMultiplyAdd(H1, filt3,
                   Math.FusedMultiplyAdd(H2, filt1q,
                   Math.FusedMultiplyAdd(-H2, filt5, -H1 * filt7)));

        // In-phase component I = filt (current smoothed value)
        double I = filt;

        // Power and normalization
        double pwr = (I * I) + (Q * Q);
        double sineWave = pwr < double.Epsilon ? 0.0 : I / Math.Sqrt(pwr);

        // Clamp to [-1, 1]
        sineWave = Math.Clamp(sineWave, -1.0, 1.0);

        Last = new TValue(input.Time, sineWave);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Calculates Sine for an entire TSeries.
    /// </summary>
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

        // Reset and process each value
        Reset();
        for (int i = 0; i < len; i++)
        {
            var result = Update(source[i], true);
            tSpan[i] = source.Times[i];
            vSpan[i] = result.Value;
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Creates a new Sine indicator and calculates for the source series.
    /// </summary>
    public static TSeries Batch(TSeries source, int hpPeriod = 40, int ssfPeriod = 10)
    {
        var sine = new Sine(hpPeriod, ssfPeriod);
        return sine.Update(source);
    }

    public static (TSeries Results, Sine Indicator) Calculate(TSeries source, int hpPeriod = 40, int ssfPeriod = 10)
    {
        var indicator = new Sine(hpPeriod, ssfPeriod);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _srcBuffer.Clear();
        _hpBuffer.Clear();
        _filtBuffer.Clear();
        _count = 0;
        Last = new TValue(DateTime.UtcNow, 0);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        TimeSpan interval = step ?? TimeSpan.FromDays(1);
        DateTime baseTime = DateTime.UtcNow;

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(baseTime + (interval * i), source[i]), true);
        }
    }
}
